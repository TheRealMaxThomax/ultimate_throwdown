using System;
using Sandbox;

/// <summary>
/// Host ball OOB watcher + sequence (hide ball, whistle, synced drop marker, sky-drop). On <c>main_ball</c>.
/// </summary>
public sealed class BallOutOfBoundsHost : Component
{
	const string DefaultWhistleSoundPath = "sounds/whistle/match_whistle.sound";

	[Property] public float ConfirmDwellSeconds { get; set; } = 1f;
	[Property] public float MaxHorizontalSpeed { get; set; } = 120f;
	[Property] public float MaxVerticalSpeed { get; set; } = 80f;
	[Property] public float MaxSupportTraceDistance { get; set; } = 96f;
	[Property] public float DropCountdownSeconds { get; set; } = 10f;
	[Property] public float SkyDropHeight { get; set; } = 200f;
	[Property] public SoundEvent WhistleSound { get; set; }
	[Property] public bool EnableOobDebugLogs { get; set; }

	private float dwellRemaining;
	private bool ballHiddenForSequence;
	private bool clientAppliedOobHidden;
	private BallLastTouchLedger ledger;

	public static BallOutOfBoundsHost EnsureOnMainBall( Scene scene )
	{
		if ( scene is null )
			return null;

		foreach ( var go in scene.GetAllObjects( true ) )
		{
			if ( !go.IsValid() || go.Name != "main_ball" )
				continue;

			return go.Components.GetOrCreate<BallOutOfBoundsHost>();
		}

		return null;
	}

	public bool IsSequenceActiveOnHost => Networking.IsHost && GetSyncedSequenceActive();

	protected override void OnStart()
	{
		ledger = BallLastTouchLedger.GetOrCreate( GameObject );
	}

	protected override void OnUpdate()
	{
		if ( !Networking.IsHost )
		{
			SyncClientHiddenFromNetworkState();
			return;
		}

		if ( IsPracticeArena() )
			return;

		if ( IsSequenceActiveOnHost )
		{
			TickActiveSequence();
			return;
		}

		if ( !IsOobWatchingAllowed() )
		{
			ResetDwell();
			return;
		}

		TickOobWatcher();
	}

	/// <summary> Host: cancel countdown/marker without sky-drop (round reset, rematch). </summary>
	public void CancelSequenceOnHost()
	{
		if ( !Networking.IsHost )
			return;

		if ( !IsSequenceActiveOnHost && !ballHiddenForSequence )
			return;

		ResetDwell();
		SetBallSimulationVisible( true );
		ClearSyncedOobState();
		PushOobStateToPlayers();

		if ( EnableOobDebugLogs )
			Log.Info( "[BallOob] Sequence cancelled." );
	}

	public static void CancelSequenceInScene( Scene scene )
	{
		if ( scene is null )
			return;

		foreach ( var host in scene.GetAllComponents<BallOutOfBoundsHost>() )
		{
			if ( host.IsValid() )
				host.CancelSequenceOnHost();
		}
	}

	void SyncClientHiddenFromNetworkState()
	{
		var shouldHide = GetSyncedSequenceActive();
		if ( shouldHide == clientAppliedOobHidden )
			return;

		clientAppliedOobHidden = shouldHide;
		SetBallSimulationVisible( !shouldHide );
	}

	void TickActiveSequence()
	{
		var team = GetAnyNetworkedPlayerTeam();
		if ( team is null )
			return;

		if ( Time.Now < team.NetBallOobDropAt )
			return;

		ExecuteSkyDropOnHost( team.NetBallOobDropAnchor );
	}

	void TickOobWatcher()
	{
		if ( IsMainBallHeldByAnyone() )
		{
			ResetDwell();
			return;
		}

		if ( !TryGetLooseBallSample( out var samplePosition, out var velocity ) )
		{
			ResetDwell();
			return;
		}

		if ( !OutOfBoundsZone.IsPointInsideAnyZone( Scene, samplePosition ) )
		{
			ResetDwell();
			return;
		}

		if ( !IsBallSettled( samplePosition, velocity ) )
		{
			ResetDwell();
			return;
		}

		dwellRemaining += Time.Delta;
		if ( dwellRemaining < ConfirmDwellSeconds )
			return;

		ConfirmOobOnHost( samplePosition );
	}

	void ConfirmOobOnHost( Vector3 ballPosition )
	{
		ResetDwell();

		if ( ledger is null || !ledger.TryGetDropAnchor( out var anchor ) )
			anchor = ResolveFallbackSpawnPosition();

		anchor = ResolveDropAnchorOnGround( anchor );

		BallPassAssistState.GetOrCreate( GameObject )?.ResetOnHost();
		SetBallSimulationVisible( false );

		var team = GetAnyNetworkedPlayerTeam();
		if ( team is null )
			return;

		team.NetBallOobActive = true;
		team.NetBallOobSequenceId++;
		team.NetBallOobSequenceStartTime = Time.Now;
		team.NetBallOobDropAnchor = anchor;
		team.NetBallOobDropAt = Time.Now + DropCountdownSeconds;
		PushOobStateToPlayers();

		PlayWhistleBroadcast();

		if ( EnableOobDebugLogs )
			Log.Info( $"[BallOob] Confirmed at {ballPosition} — drop anchor {anchor}, drop at {team.NetBallOobDropAt:F1}." );
	}

	void ExecuteSkyDropOnHost( Vector3 dropAnchor )
	{
		GameObject.WorldPosition = dropAnchor + Vector3.Up * SkyDropHeight;
		GameObject.WorldRotation = Rotation.Identity;

		foreach ( var body in GameObject.Components.GetAll<Rigidbody>( FindMode.EverythingInSelfAndDescendants ) )
		{
			if ( !body.IsValid() )
				continue;

			body.Velocity = Vector3.Zero;
			body.AngularVelocity = Vector3.Zero;
		}

		SetBallSimulationVisible( true );
		ledger?.ClearOnHost();
		ClearSyncedOobState();
		PushOobStateToPlayers();

		if ( EnableOobDebugLogs )
			Log.Info( $"[BallOob] Sky-drop at {GameObject.WorldPosition}." );
	}

	void ClearSyncedOobState()
	{
		var team = GetAnyNetworkedPlayerTeam();
		if ( team is null )
			return;

		team.NetBallOobActive = false;
		team.NetBallOobSequenceStartTime = 0f;
		team.NetBallOobDropAt = 0f;
	}

	void PushOobStateToPlayers()
	{
		var source = GetAnyNetworkedPlayerTeam();
		if ( source is null )
			return;

		foreach ( var playerTeam in Scene.GetAllComponents<PlayerTeam>() )
		{
			if ( !playerTeam.GameObject.Network.Active )
				continue;

			playerTeam.NetBallOobActive = source.NetBallOobActive;
			playerTeam.NetBallOobSequenceId = source.NetBallOobSequenceId;
			playerTeam.NetBallOobSequenceStartTime = source.NetBallOobSequenceStartTime;
			playerTeam.NetBallOobDropAnchor = source.NetBallOobDropAnchor;
			playerTeam.NetBallOobDropAt = source.NetBallOobDropAt;
		}
	}

	bool GetSyncedSequenceActive()
	{
		var team = GetAnyNetworkedPlayerTeam();
		return team is not null && team.NetBallOobActive;
	}

	static PlayerTeam GetAnyNetworkedPlayerTeam( Scene scene )
	{
		if ( scene is null )
			return null;

		foreach ( var playerTeam in scene.GetAllComponents<PlayerTeam>() )
		{
			if ( playerTeam.GameObject.Network.Active )
				return playerTeam;
		}

		return null;
	}

	PlayerTeam GetAnyNetworkedPlayerTeam() => GetAnyNetworkedPlayerTeam( Scene );

	bool IsPracticeArena()
	{
		var config = MapMatchConfig.FindInScene( Scene );
		return config.IsValid() && config.PracticeArenaMode;
	}

	bool IsOobWatchingAllowed()
	{
		var director = MatchDirector.FindInScene( Scene );
		return director.IsValid() && director.CurrentPhase == MatchPhase.Playing;
	}

	bool IsMainBallHeldByAnyone()
	{
		foreach ( var grab in Scene.GetAllComponents<BallGrab>() )
		{
			if ( grab.IsValid() && grab.IsHolding && grab.HeldBall == GameObject )
				return true;
		}

		return false;
	}

	bool TryGetLooseBallSample( out Vector3 position, out Vector3 velocity )
	{
		position = GameObject.WorldPosition;
		velocity = Vector3.Zero;

		var body = GameObject.Components.Get<Rigidbody>( FindMode.EverythingInSelfAndDescendants );
		if ( !body.IsValid() || !body.Enabled )
			return false;

		position = GameObject.WorldPosition;
		velocity = body.Velocity;
		return true;
	}

	bool IsBallSettled( Vector3 position, Vector3 velocity )
	{
		var horizontalSpeed = velocity.WithZ( 0f ).Length;
		if ( horizontalSpeed > MaxHorizontalSpeed )
			return false;

		if ( MathF.Abs( velocity.z ) > MaxVerticalSpeed )
			return false;

		return TryTraceSupport( position, out _ );
	}

	bool TryTraceSupport( Vector3 position, out Vector3 hitPosition )
	{
		var start = position + Vector3.Up * 8f;
		var end = position - Vector3.Up * MaxSupportTraceDistance;
		var trace = Scene.Trace.Ray( start, end )
			.WithoutTags( "ragdoll" )
			.IgnoreGameObjectHierarchy( GameObject )
			.Run();

		if ( !trace.Hit )
		{
			hitPosition = default;
			return false;
		}

		hitPosition = trace.HitPosition;
		return true;
	}

	Vector3 ResolveDropAnchorOnGround( Vector3 anchor )
	{
		var probe = anchor + Vector3.Up * 64f;
		if ( TryTraceSupport( probe, out var hit ) )
			return new Vector3( anchor.x, anchor.y, hit.z );

		var snapped = GameNetworkManager.SnapPositionToGround( Scene, probe, GameObject );
		return new Vector3( anchor.x, anchor.y, snapped.z );
	}

	Vector3 ResolveFallbackSpawnPosition()
	{
		var director = MatchDirector.FindInScene( Scene );
		if ( director.IsValid() && director.BallSpawn.IsValid() )
			return director.BallSpawn.WorldPosition;

		return GameObject.WorldPosition;
	}

	void SetBallSimulationVisible( bool visible )
	{
		ballHiddenForSequence = !visible;

		foreach ( var renderer in GameObject.Components.GetAll<ModelRenderer>( FindMode.EverythingInSelfAndDescendants ) )
		{
			if ( renderer.IsValid() )
				renderer.Enabled = visible;
		}

		foreach ( var body in GameObject.Components.GetAll<Rigidbody>( FindMode.EverythingInSelfAndDescendants ) )
		{
			if ( !body.IsValid() )
				continue;

			body.Enabled = visible;
			if ( !visible )
			{
				body.Velocity = Vector3.Zero;
				body.AngularVelocity = Vector3.Zero;
			}
		}

		foreach ( var collider in GameObject.Components.GetAll<Collider>( FindMode.EverythingInSelfAndDescendants ) )
		{
			if ( collider.IsValid() )
				collider.Enabled = visible;
		}
	}

	void ResetDwell()
	{
		dwellRemaining = 0f;
	}

	void PlayWhistleBroadcast()
	{
		var sound = ResolveWhistleSound();
		if ( !sound.IsValid() )
			return;

		PlayOobWhistleRpc( sound.ResourcePath );
	}

	SoundEvent ResolveWhistleSound()
	{
		if ( WhistleSound.IsValid() )
			return WhistleSound;

		return ResourceLibrary.Get<SoundEvent>( DefaultWhistleSoundPath );
	}

	[Rpc.Broadcast]
	void PlayOobWhistleRpc( string soundResourcePath )
	{
		if ( string.IsNullOrWhiteSpace( soundResourcePath ) )
			return;

		var sound = ResourceLibrary.Get<SoundEvent>( soundResourcePath );
		if ( !sound.IsValid() )
			return;

		var handle = Sound.Play( sound );
		if ( handle is null )
			return;

		handle.SpacialBlend = 0f;
	}
}
