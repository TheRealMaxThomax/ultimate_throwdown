using Sandbox;

/// <summary>
/// Host-only pass-assist chain on <c>main_ball</c> — throw credit, assist window, void rules.
/// See GAMEPLAY_DESIGN.md → Ultimates → Assist.
/// </summary>
public sealed class BallPassAssistState : Component
{
	[Property] public float AssistWindowSeconds { get; set; } = 10f;
	[Property] public bool EnableAssistDebugLogs { get; set; }

	private GameObject pendingPasser;
	private int pendingPasserTeamId = -1;
	private GameObject assistCandidate;
	private float windowStartedAt = -1f;
	private bool assistWindowStarted;
	private bool isVoided;
	private bool trackingActive;

	public static BallPassAssistState GetOrCreate( GameObject ball )
	{
		if ( !ball.IsValid() )
			return null;

		var state = ball.Components.Get<BallPassAssistState>();
		if ( state.IsValid() )
			return state;

		return ball.Components.Create<BallPassAssistState>();
	}

	protected override void OnStart()
	{
		if ( !Networking.IsHost )
			return;

		EnsureCollisionRelays();
	}

	/// <summary> Host: clear chain (round reset, post-goal). </summary>
	public void ResetOnHost()
	{
		if ( !Networking.IsHost )
			return;

		pendingPasser = null;
		pendingPasserTeamId = -1;
		assistCandidate = null;
		windowStartedAt = -1f;
		assistWindowStarted = false;
		isVoided = false;
		trackingActive = false;
	}

	/// <summary> Host: throw released — starts / replaces assist chain (relay voids prior passer). </summary>
	public void NotifyThrowOnHost( GameObject thrower )
	{
		if ( !Networking.IsHost || !IsAssistsEnabled() )
			return;

		if ( !thrower.IsValid() )
			return;

		var throwerTeamId = GetTeamId( thrower );
		if ( !MatchTeamIds.IsValid( throwerTeamId ) )
			return;

		if ( thrower.Tags.Has( CitizenAvatarLod.PracticeNpcTag ) )
			return;

		pendingPasser = thrower;
		pendingPasserTeamId = throwerTeamId;
		assistCandidate = null;
		windowStartedAt = -1f;
		assistWindowStarted = false;
		isVoided = false;
		trackingActive = true;

		if ( EnableAssistDebugLogs )
			Log.Info( $"[BallAssist] Throw by {thrower.Name} (team {throwerTeamId}) — window pending first contact or teammate grab." );
	}

	/// <summary> Host: ball picked up — enemy voids; teammate confirms assist passer and may start window. </summary>
	public void NotifyPickupOnHost( GameObject grabber )
	{
		if ( !Networking.IsHost || !IsAssistsEnabled() || !trackingActive || isVoided )
			return;

		if ( !grabber.IsValid() || grabber.Tags.Has( CitizenAvatarLod.PracticeNpcTag ) )
			return;

		var grabberTeamId = GetTeamId( grabber );
		if ( !MatchTeamIds.IsValid( grabberTeamId ) )
			return;

		if ( !MatchTeamIds.IsValid( pendingPasserTeamId ) || !pendingPasser.IsValid() )
			return;

		if ( grabberTeamId != pendingPasserTeamId )
		{
			VoidChain( "enemy_grab" );
			return;
		}

		assistCandidate = pendingPasser;

		if ( !assistWindowStarted )
			StartAssistWindow( "teammate_grab" );

		if ( EnableAssistDebugLogs )
			Log.Info( $"[BallAssist] Teammate grab by {grabber.Name} — assist candidate {assistCandidate.Name}." );
	}

	/// <summary> Host: enemy tackled current ball carrier — void assist chain. </summary>
	public void VoidOnEnemyTackleCarrierOnHost()
	{
		if ( !Networking.IsHost || !trackingActive || isVoided )
			return;

		VoidChain( "enemy_tackle_carrier" );
	}

	internal void NotifyWorldContactOnHost( Collision collision )
	{
		if ( !Networking.IsHost || !IsAssistsEnabled() || !trackingActive || isVoided || assistWindowStarted )
			return;

		if ( IsBallHierarchy( collision.Other.GameObject ) )
			return;

		if ( IsTriggerCollision( collision ) )
			return;

		if ( IsPlayerBodyCollision( collision.Other.GameObject ) )
			return;

		StartAssistWindow( "world_contact" );
	}

	/// <summary> Host: after scorer goal bump — grant assist charge if window + candidate still valid. </summary>
	public bool TryGrantAssistChargeOnHost( GameObject scorer )
	{
		if ( !Networking.IsHost || !IsAssistsEnabled() || isVoided || !trackingActive )
			return false;

		if ( !scorer.IsValid() || scorer.Tags.Has( CitizenAvatarLod.PracticeNpcTag ) )
			return false;

		if ( !assistCandidate.IsValid() || assistCandidate == scorer )
			return false;

		if ( GetTeamId( scorer ) != GetTeamId( assistCandidate ) )
			return false;

		if ( !assistWindowStarted || windowStartedAt < 0f )
			return false;

		var elapsed = Time.Now - windowStartedAt;
		if ( elapsed > AssistWindowSeconds )
		{
			if ( EnableAssistDebugLogs )
				Log.Info( $"[BallAssist] Window expired ({elapsed:F1}s > {AssistWindowSeconds:F1}s) — no assist." );

			ResetOnHost();
			return false;
		}

		assistCandidate.Components.Get<PlayerUltCharge>()?.GrantAssistChargeOnHost();
		ResetOnHost();
		return true;
	}

	void EnsureCollisionRelays()
	{
		foreach ( var body in GameObject.Components.GetAll<Rigidbody>( FindMode.EverythingInSelfAndDescendants ) )
		{
			if ( !body.IsValid() )
				continue;

			body.CollisionEventsEnabled = true;

			var relay = body.GameObject.Components.Get<BallPassAssistCollisionRelay>();
			if ( !relay.IsValid() )
				relay = body.GameObject.Components.Create<BallPassAssistCollisionRelay>();

			relay.AssistState = this;
		}
	}

	void StartAssistWindow( string reason )
	{
		assistWindowStarted = true;
		windowStartedAt = Time.Now;

		if ( EnableAssistDebugLogs )
			Log.Info( $"[BallAssist] Window started ({reason}) — {AssistWindowSeconds:F1}s to score for assist." );
	}

	void VoidChain( string reason )
	{
		isVoided = true;
		trackingActive = false;
		assistCandidate = null;

		if ( EnableAssistDebugLogs )
			Log.Info( $"[BallAssist] Voided ({reason})." );
	}

	static bool IsAssistsEnabled( Scene scene )
	{
		var config = MapMatchConfig.FindInScene( scene );
		return config is null || !config.PracticeArenaMode;
	}

	bool IsAssistsEnabled() => IsAssistsEnabled( Scene );

	static int GetTeamId( GameObject player )
	{
		var team = player.Components.Get<PlayerTeam>();
		return team.IsValid() ? team.TeamId : -1;
	}

	static bool IsBallHierarchy( GameObject other )
	{
		var current = other;
		while ( current.IsValid() )
		{
			if ( current.Components.Get<BallPassAssistState>().IsValid() )
				return true;

			current = current.Parent;
		}

		return false;
	}

	static bool IsTriggerCollision( Collision collision )
	{
		return collision.Other.IsTrigger;
	}

	static bool IsPlayerBodyCollision( GameObject other )
	{
		var current = other;
		while ( current.IsValid() )
		{
			if ( current.Components.Get<TrafficCar>().IsValid() )
				return false;

			if ( current.Components.Get<PlayerTeam>().IsValid() )
				return true;

			if ( current.Tags.Has( CitizenAvatarLod.PracticeNpcTag ) )
				return true;

			current = current.Parent;
		}

		return false;
	}
}

/// <summary> Forwards rigidbody collision events to <see cref="BallPassAssistState"/> on the ball root. </summary>
sealed class BallPassAssistCollisionRelay : Component, Component.ICollisionListener
{
	public BallPassAssistState AssistState { get; set; }

	public void OnCollisionStart( Collision other )
	{
		AssistState?.NotifyWorldContactOnHost( other );
	}

	public void OnCollisionUpdate( Collision other )
	{
	}

	public void OnCollisionStop( CollisionStop other )
	{
	}
}
