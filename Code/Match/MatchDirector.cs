using System;
using Sandbox;

/// <summary> Host-authoritative match phases, round wins, match timer, and goal flow. </summary>
public sealed class MatchDirector : Component
{
	public const int NoTeam = -1;

	[Property] public float MatchDurationSeconds { get; set; } = 600f;
	[Property] public float GoalCelebrationSeconds { get; set; } = 5f;
	[Property] public float IntermissionSeconds { get; set; } = 20f;
	[Property] public float MatchOverCelebrationSeconds { get; set; } = 10f;
	[Property] public int RoundWinsToWinMatch { get; set; } = 5;

	/// <summary> DEBUG: remove or gate before shipping — simulates a goal for testing without <see cref="GoalZone"/>. </summary>
	[Property] public bool EnableDebugForceGoal { get; set; } = false;

	/// <summary> DEBUG: input action from <c>Input.config</c> (default F9). Host only. </summary>
	[Property] public string DebugForceGoalAction { get; set; } = "DebugForceGoal";

	[Property] public bool EnableMatchDebugLogs { get; set; } = false;

	/// <summary> Center kickoff empty — wire in editor. Ball teleports here on round reset. </summary>
	[Property] public GameObject BallSpawn { get; set; }

	/// <summary> If unset, first <see cref="GameNetworkManager"/> in the scene is used for spawn teleports. </summary>
	[Property] public GameNetworkManager NetworkManager { get; set; }

	[Sync( SyncFlags.FromHost )] public int NetPhase { get; set; }
	[Sync( SyncFlags.FromHost )] public int NetTeam0RoundWins { get; set; }
	[Sync( SyncFlags.FromHost )] public int NetTeam1RoundWins { get; set; }
	[Sync( SyncFlags.FromHost )] public float NetMatchTimeRemaining { get; set; }
	[Sync( SyncFlags.FromHost )] public float NetPhaseTimeRemaining { get; set; }
	[Sync( SyncFlags.FromHost )] public int NetLastGoalScoringTeamId { get; set; } = NoTeam;
	[Sync( SyncFlags.FromHost )] public bool NetIsOvertime { get; set; }
	[Sync( SyncFlags.FromHost )] public int NetMatchWinnerTeamId { get; set; } = NoTeam;

	public MatchPhase CurrentPhase => (MatchPhase)NetPhase;

	/// <summary> Movement, ball, tackle, etc. Goal + match-over celebration allow movement. </summary>
	public bool IsGameplayInputAllowed =>
		CurrentPhase is MatchPhase.Playing or MatchPhase.GoalCelebration
		|| (CurrentPhase == MatchPhase.MatchOver && NetPhaseTimeRemaining > 0f);

	/// <summary> Camera look is always allowed during intermission / match over. </summary>
	public bool IsCameraInputAllowed => true;

	public bool IsScoringAllowed => Networking.IsHost && CurrentPhase == MatchPhase.Playing;

	protected override void OnStart()
	{
		if ( !Networking.IsHost )
			return;

		ResetMatchState();
	}

	protected override void OnUpdate()
	{
		if ( !Networking.IsHost )
			return;

		TickMatchTimer();
		TickPhase();
		TickDebugForceGoal();
		PushMatchHudStateToPlayers();
	}

	public static MatchDirector FindInScene( Scene scene )
	{
		if ( scene is null )
			return null;

		foreach ( var go in scene.GetAllObjects( true ) )
		{
			var director = go.Components.Get<MatchDirector>();
			if ( director.IsValid() )
				return director;
		}

		return null;
	}

	/// <summary> Called by <see cref="GoalZone"/> (slice 3) or debug input. Host only. </summary>
	public void RegisterGoal( int scoringTeamId )
	{
		if ( !Networking.IsHost )
			return;

		if ( !MatchTeamIds.IsValid( scoringTeamId ) )
			return;

		if ( CurrentPhase != MatchPhase.Playing )
			return;

		if ( NetMatchWinnerTeamId != NoTeam )
			return;

		NetLastGoalScoringTeamId = scoringTeamId;

		if ( NetIsOvertime )
		{
			EndMatch( scoringTeamId, "overtime golden goal" );
			return;
		}

		if ( scoringTeamId == MatchTeamIds.Team0 )
			NetTeam0RoundWins++;
		else
			NetTeam1RoundWins++;

		var roundWins = scoringTeamId == MatchTeamIds.Team0 ? NetTeam0RoundWins : NetTeam1RoundWins;
		if ( roundWins >= RoundWinsToWinMatch )
		{
			EndMatch( scoringTeamId, $"reached {RoundWinsToWinMatch} round wins" );
			return;
		}

		BeginGoalCelebration( scoringTeamId );
	}

	/// <summary> Slice 6 — host rematch same map (Playing + fresh score/timer + round reset). </summary>
	public void HostRequestRematch()
	{
		if ( !Networking.IsHost )
			return;

		if ( CurrentPhase != MatchPhase.MatchOver || NetPhaseTimeRemaining > 0f )
			return;

		PerformRoundReset( 0f );
		ResetMatchState();
		LogMatch( "Rematch started." );
	}

	private void ResetMatchState()
	{
		NetPhase = (int)MatchPhase.Playing;
		NetTeam0RoundWins = 0;
		NetTeam1RoundWins = 0;
		NetMatchTimeRemaining = MatchDurationSeconds;
		NetPhaseTimeRemaining = 0f;
		NetLastGoalScoringTeamId = NoTeam;
		NetIsOvertime = false;
		NetMatchWinnerTeamId = NoTeam;
		PlayerUltCharge.ResetAllPlayersInScene( Scene );
		PushMatchHudStateToPlayers();
	}

	private void TickMatchTimer()
	{
		if ( CurrentPhase != MatchPhase.Playing )
			return;

		if ( NetIsOvertime )
			return;

		if ( NetMatchTimeRemaining <= 0f )
		{
			ResolveMatchTimerExpired();
			return;
		}

		NetMatchTimeRemaining = MathF.Max( 0f, NetMatchTimeRemaining - Time.Delta );
		if ( NetMatchTimeRemaining <= 0f )
			ResolveMatchTimerExpired();
	}

	private void ResolveMatchTimerExpired()
	{
		NetMatchTimeRemaining = 0f;

		if ( NetTeam0RoundWins > NetTeam1RoundWins )
		{
			EndMatch( MatchTeamIds.Team0, "match timer (round-win lead)" );
			return;
		}

		if ( NetTeam1RoundWins > NetTeam0RoundWins )
		{
			EndMatch( MatchTeamIds.Team1, "match timer (round-win lead)" );
			return;
		}

		BeginOvertimeSetup();
	}

	/// <summary> Tied at 0:00 — flag OT, full round reset, then standard intermission before golden-goal play. </summary>
	private void BeginOvertimeSetup()
	{
		NetIsOvertime = true;
		NetMatchTimeRemaining = 0f;
		LogMatch( "Match timer expired — tied round wins. OVERTIME: reset + intermission, then next goal wins." );
		PerformRoundReset( IntermissionSeconds );
		LogMatch( "OVERTIME reset complete." );
		BeginIntermission();
	}

	private void TickPhase()
	{
		if ( NetPhaseTimeRemaining <= 0f )
			return;

		NetPhaseTimeRemaining = MathF.Max( 0f, NetPhaseTimeRemaining - Time.Delta );
		if ( NetPhaseTimeRemaining > 0f )
			return;

		switch ( CurrentPhase )
		{
			case MatchPhase.GoalCelebration:
				OnGoalCelebrationEnded();
				break;
			case MatchPhase.Intermission:
				BeginPlaying();
				break;
		}
	}

	private void BeginGoalCelebration( int scoringTeamId )
	{
		NetPhase = (int)MatchPhase.GoalCelebration;
		NetPhaseTimeRemaining = GoalCelebrationSeconds;
		PushMatchHudStateToPlayers();

		var teamName = ResolveTeamDisplayName( scoringTeamId );
		LogMatch( $"GOAL — {teamName} scored. Celebration {GoalCelebrationSeconds:0}s." );
	}

	private void OnGoalCelebrationEnded()
	{
		PerformRoundReset( IntermissionSeconds );
		LogMatch( "Celebration ended — round reset complete." );
		BeginIntermission();
	}

	/// <summary> Host only: stand ragdolls, release ball, teleport players and ball; optional pickup block (intermission). </summary>
	private void PerformRoundReset( float pickupBlockSeconds )
	{
		if ( !Networking.IsHost )
			return;

		SpeedsterSpeedBlitzUlt.CancelAllInScene( Scene );

		foreach ( var tackle in Scene.GetAllComponents<PlayerTackle>() )
		{
			if ( tackle.IsValid() )
				tackle.ForceStandUpFromHost();
		}

		foreach ( var grab in Scene.GetAllComponents<BallGrab>() )
		{
			if ( !grab.IsValid() || !grab.IsHolding )
				continue;

			grab.ReleaseHeldBall();
		}

		ResetBallToSpawn();

		var networkManager = ResolveNetworkManager();
		networkManager?.ApplyRoundResetToAllPlayers( pickupBlockSeconds );

		foreach ( var grab in Scene.GetAllComponents<BallGrab>() )
		{
			if ( grab.IsValid() )
				grab.BlockPickupForSeconds( pickupBlockSeconds );
		}
	}

	private void ResetBallToSpawn()
	{
		if ( !BallSpawn.IsValid() )
		{
			Log.Warning( "[Match] BallSpawn is not set — ball was not teleported for round reset." );
			return;
		}

		var ball = FindMainBallObject();
		if ( !ball.IsValid() )
		{
			Log.Warning( "[Match] Could not find main ball for round reset." );
			return;
		}

		var spawnTransform = WithoutScale( BallSpawn.WorldTransform );
		spawnTransform.Position = GameNetworkManager.SnapBallToGround( Scene, ball, spawnTransform.Position );
		ball.WorldPosition = spawnTransform.Position;
		ball.WorldRotation = spawnTransform.Rotation;

		foreach ( var body in ball.Components.GetAll<Rigidbody>( FindMode.EverythingInSelfAndDescendants ) )
		{
			if ( !body.IsValid() )
				continue;

			body.Velocity = Vector3.Zero;
			body.AngularVelocity = Vector3.Zero;
		}
	}

	private GameObject FindMainBallObject()
	{
		foreach ( var grab in Scene.GetAllComponents<BallGrab>() )
		{
			if ( grab.MainBall.IsValid() )
				return grab.MainBall;
		}

		foreach ( var go in Scene.GetAllObjects( true ) )
		{
			if ( go.Name == "main_ball" )
				return go;
		}

		return null;
	}

	private GameNetworkManager ResolveNetworkManager()
	{
		if ( NetworkManager.IsValid() )
			return NetworkManager;

		foreach ( var go in Scene.GetAllObjects( true ) )
		{
			var manager = go.Components.Get<GameNetworkManager>();
			if ( manager.IsValid() )
				return manager;
		}

		return null;
	}

	private static Transform WithoutScale( Transform t )
	{
		return new Transform( t.Position, t.Rotation, 1f );
	}

	private void BeginIntermission()
	{
		NetPhase = (int)MatchPhase.Intermission;
		NetPhaseTimeRemaining = IntermissionSeconds;
		PushMatchHudStateToPlayers();
		if ( NetIsOvertime && NetTeam0RoundWins == NetTeam1RoundWins )
			LogMatch( $"OVERTIME intermission {IntermissionSeconds:0}s — next goal wins." );
		else
			LogMatch( $"Intermission {IntermissionSeconds:0}s." );
	}

	private void BeginPlaying()
	{
		NetPhase = (int)MatchPhase.Playing;
		NetPhaseTimeRemaining = 0f;
		PushMatchHudStateToPlayers();
		LogMatch( "Round resumed — Playing." );
	}

	private void EndMatch( int winningTeamId, string reason )
	{
		NetMatchWinnerTeamId = winningTeamId;
		NetPhase = (int)MatchPhase.MatchOver;
		NetPhaseTimeRemaining = MatchOverCelebrationSeconds;
		NetMatchTimeRemaining = 0f;
		PushMatchHudStateToPlayers();

		var teamName = ResolveTeamDisplayName( winningTeamId );
		LogMatch( $"Match over — {teamName} wins ({reason}). Score {NetTeam0RoundWins}-{NetTeam1RoundWins}. Celebration {MatchOverCelebrationSeconds:0}s." );
	}

	/// <summary> MatchDirector lives on local scene objects — HUD + phase must be copied onto networked players for clients. </summary>
	public void PushMatchHudStateToPlayers()
	{
		if ( !Networking.IsHost )
			return;

		foreach ( var playerTeam in Scene.GetAllComponents<PlayerTeam>() )
		{
			if ( !playerTeam.GameObject.Network.Active )
				continue;

			playerTeam.NetMatchPhase = NetPhase;
			playerTeam.NetTeam0RoundWins = NetTeam0RoundWins;
			playerTeam.NetTeam1RoundWins = NetTeam1RoundWins;
			playerTeam.NetMatchTimeRemaining = NetMatchTimeRemaining;
			playerTeam.NetPhaseTimeRemaining = NetPhaseTimeRemaining;
			playerTeam.NetLastGoalScoringTeamId = NetLastGoalScoringTeamId;
			playerTeam.NetIsOvertime = NetIsOvertime;
			playerTeam.NetMatchWinnerTeamId = NetMatchWinnerTeamId;
		}
	}

	private string ResolveTeamDisplayName( int teamId )
	{
		var config = MapMatchConfig.FindInScene( Scene );
		return config.IsValid() ? config.GetTeamDisplayName( teamId ) : $"Team{teamId}";
	}

	private void TickDebugForceGoal()
	{
		if ( !EnableDebugForceGoal )
			return;

		if ( !Input.Pressed( DebugForceGoalAction ) )
			return;

		var teamId = Game.Random.Int( 0, MatchTeamIds.TeamCount - 1 );
		LogMatch( $"DEBUG force goal — team {teamId}." );
		RegisterGoal( teamId );
	}

	private void LogMatch( string message )
	{
		if ( !EnableMatchDebugLogs )
			return;

		Log.Info( $"[Match] {message}" );
	}
}
