using System;
using Sandbox;
using System.Collections.Generic;

public sealed class GameNetworkManager : Component, Component.INetworkListener
{
	/// <summary> Fallback: first scene object with this exact <see cref="GameObject.Name"/> is the clone source, unless <see cref="PlayerTemplateRoot"/> is set. </summary>
	[Property] public string PlayerTemplateName { get; set; } = "Player";

	/// <summary> If set, used as the player prefab to clone — avoids grabbing the wrong <see cref="GameObject"/> when multiple exist. Wire it in the inspector. </summary>
	[Property] public GameObject PlayerTemplateRoot { get; set; }

	/// <summary> If set, this object&apos;s <see cref="GameObject.WorldTransform"/> is the spawn pose; otherwise the template&apos;s transform at startup is snapped. </summary>
	[Property] public GameObject SpawnPoint { get; set; }

	/// <summary> Team 0 spawn points. Host picks the first one with no living team-0 player within <see cref="SpawnPointOccupiedRadius"/>; falls back to a random entry, then to <see cref="Team0Spawn"/>, then <see cref="SpawnPoint"/>. </summary>
	[Property] public List<GameObject> Team0Spawns { get; set; } = new();

	/// <summary> Team 1 spawn points. Same rules as <see cref="Team0Spawns"/>. </summary>
	[Property] public List<GameObject> Team1Spawns { get; set; } = new();

	/// <summary> Legacy single team-0 spawn. Used only if <see cref="Team0Spawns"/> is empty. </summary>
	[Property] public GameObject Team0Spawn { get; set; }

	/// <summary> Legacy single team-1 spawn. Used only if <see cref="Team1Spawns"/> is empty. </summary>
	[Property] public GameObject Team1Spawn { get; set; }

	/// <summary> Optional; if unset, first <see cref="MapMatchConfig"/> in the scene is used. </summary>
	[Property] public MapMatchConfig MatchConfig { get; set; }

	/// <summary> A spawn point is considered occupied if a same-team player is within this many units. </summary>
	[Property] public float SpawnPointOccupiedRadius { get; set; } = 60f;

	/// <summary> Legacy line spacing — only used when falling back to a single <see cref="Team0Spawn"/> / <see cref="Team1Spawn"/> / <see cref="SpawnPoint"/>. </summary>
	[Property] public float JoinSpawnSpacing { get; set; } = 64f;

	[Property] public bool DisableTemplateOnStart { get; set; } = true;
	[Property] public bool EnableNetDebugLogs { get; set; } = false;
	/// <summary> Forces LOD0 on all citizen-like avatars in this scene (host + client). Uses late <see cref="CitizenAvatarLodSystem"/> plus this component&apos;s <c>OnPreRender</c>; avoids <c>OnUpdate</c> where proxy LOD can still change later in the frame.</summary>
	[Property] public bool LockPlayerModelLodInPreRender { get; set; } = true;
	/// <summary> Bone-merged citizen pieces (often arms / body chunks) skipped LOD lock caused pale low-detail skin; disable if clothing flashes wrong materials again.</summary>
	[Property] public bool LockBoneMergedSkinnedMeshLod { get; set; } = false;

	private readonly Dictionary<long, GameObject> spawnedPlayersBySteamId = new();
	private readonly Dictionary<long, int> preferredTeamBySteamId = new();
	private GameObject playerTemplate;
	private Transform designSpawnTransform;
	private bool designSpawnCaptured;
	private float nextEnsurePlayersAt;

	protected override void OnStart()
	{
		EnsureTemplateFound();
		if ( playerTemplate.IsValid() )
			PlayerClass.PrepareDresserBeforeSpawn( playerTemplate );

		CaptureDesignSpawnTransform();
		if ( EnableNetDebugLogs )
		{
			Log.Info( $"[NetDebug] GameNetworkManager.OnStart IsHost={Networking.IsHost} TemplateValid={playerTemplate.IsValid()} SpawnCaptured={designSpawnCaptured}" );
		}

		EnsurePlayersForActiveConnections();
		TackleComicTextHud.EnsureOnMainCamera( Scene );
		SpeedBlitzVfxResources.EnsureLoaded();
		nextEnsurePlayersAt = Time.Now + 1f;
		CitizenAvatarLod.SceneWideLockEnabled = LockPlayerModelLodInPreRender;
		CitizenAvatarLod.ApplyLodLockToBoneMergedSkinnedMeshes = LockBoneMergedSkinnedMeshLod;
	}

	/// <summary> Scene-wide LOD lock so <b>this</b> machine renders citizens at highest LOD (covers proxies + NPCs without cosmetics on root).</summary>
	protected override void OnPreRender()
	{
		if ( LockPlayerModelLodInPreRender )
			CitizenAvatarLod.ApplyToWholeScene( Scene );
	}

	protected override void OnUpdate()
	{
		if ( Time.Now < nextEnsurePlayersAt )
			return;

		EnsurePlayersForActiveConnections();
		nextEnsurePlayersAt = Time.Now + 1f;
	}

	public void OnActive( Connection connection )
	{
		if ( !Networking.IsHost )
			return;

		SpawnPlayerForConnectionIfMissing( connection );
	}

	public void OnDisconnected( Connection connection )
	{
		if ( !Networking.IsHost )
			return;

		var steamId = (long)connection.SteamId;
		if ( !spawnedPlayersBySteamId.TryGetValue( steamId, out var player ) )
			return;

		if ( player.IsValid() )
		{
			player.Destroy();
			if ( EnableNetDebugLogs )
			{
				Log.Info( $"[NetDebug] Destroyed player for {connection.DisplayName} ({connection.SteamId})." );
			}
		}

		spawnedPlayersBySteamId.Remove( steamId );
	}

	private void EnsureTemplateFound()
	{
		if ( playerTemplate.IsValid() )
			return;

		if ( PlayerTemplateRoot.IsValid() )
		{
			playerTemplate = PlayerTemplateRoot;
			if ( EnableNetDebugLogs )
				Log.Info( "[NetDebug] Using PlayerTemplateRoot reference." );
			return;
		}

		foreach ( var go in Scene.GetAllObjects( true ) )
		{
			if ( go.Name != PlayerTemplateName )
				continue;

			playerTemplate = go;
			break;
		}

		if ( !playerTemplate.IsValid() )
		{
			Log.Warning( $"GameNetworkManager could not find player template '{PlayerTemplateName}'." );
		}
		else if ( EnableNetDebugLogs )
		{
			Log.Info( $"[NetDebug] Found player template '{playerTemplate.Name}' by name." );
		}
	}

	private void CaptureDesignSpawnTransform()
	{
		if ( designSpawnCaptured )
			return;

		if ( SpawnPoint.IsValid() )
		{
			designSpawnTransform = WithoutScale( SpawnPoint.WorldTransform );
			designSpawnCaptured = true;
			return;
		}

		if ( playerTemplate.IsValid() )
		{
			designSpawnTransform = playerTemplate.WorldTransform;
			designSpawnCaptured = true;
		}
	}

	/// <summary> Spawn empties are often scaled up for editor visibility — strip that so the cloned player keeps the template scale. </summary>
	private static Transform WithoutScale( Transform t )
	{
		return new Transform( t.Position, t.Rotation, 1f );
	}

	private void EnsurePlayersForActiveConnections()
	{
		if ( !Networking.IsHost )
			return;

		EnsureTemplateFound();
		if ( !playerTemplate.IsValid() )
			return;

		CaptureDesignSpawnTransform();

		if ( EnableNetDebugLogs )
		{
			Log.Info( $"[NetDebug] Ensuring players. ConnectionCount={Connection.All.Count}" );
		}

		foreach ( var connection in Connection.All )
		{
			if ( connection is null )
				continue;

			if ( EnableNetDebugLogs )
			{
				Log.Info( $"[NetDebug] Connection seen: Name={connection.DisplayName} SteamId={connection.SteamId} Active={connection.IsActive} IsHost={connection.IsHost}" );
			}

			if ( !connection.IsActive )
				continue;

			SpawnPlayerForConnectionIfMissing( connection );
		}

		var local = Connection.Local;
		if ( local is not null )
		{
			if ( EnableNetDebugLogs )
			{
				Log.Info( $"[NetDebug] Local fallback: Name={local.DisplayName} SteamId={local.SteamId} Active={local.IsActive} IsHost={local.IsHost}" );
			}

			SpawnPlayerForConnectionIfMissing( local );
		}
	}

	private void SpawnPlayerForConnectionIfMissing( Connection connection )
	{
		if ( !Networking.IsHost )
			return;

		EnsureTemplateFound();
		if ( !playerTemplate.IsValid() )
			return;

		CaptureDesignSpawnTransform();
		if ( !designSpawnCaptured )
		{
			Log.Warning( "[GameNetworkManager] No spawn transform (set SpawnPoint or fix Player template)." );
			return;
		}

		var steamId = (long)connection.SteamId;
		if ( spawnedPlayersBySteamId.TryGetValue( steamId, out var existingPlayer ) && existingPlayer.IsValid() )
			return;

		var teamId = AssignTeamForConnection( connection );
		preferredTeamBySteamId[steamId] = teamId;

		var teamSlotIndex = CountPlayersOnTeam( teamId );
		var t = GetSpawnTransformForTeam( teamId, teamSlotIndex );

		var player = playerTemplate.Clone( t );
		player.Name = $"Player_{connection.DisplayName}";
		PlayerClass.PrepareDresserBeforeSpawn( player );
		player.Enabled = true;

		var playerTeam = player.Components.GetOrCreate<PlayerTeam>();
		playerTeam.TeamId = teamId;
		player.Components.GetOrCreate<PlayerDisableCrouch>();
		player.Components.GetOrCreate<PlayerEnemyOutline>();
		player.Components.GetOrCreate<BallCompassHud>();
		player.Components.GetOrCreate<PlayerBallHoldAnim>();
		player.Components.GetOrCreate<PlayerChargeRunAnim>();
		player.Components.GetOrCreate<PlayerSpeedBlitzWindUpAnim>();
		player.Components.GetOrCreate<BlitzConnectPoseFreeze>();
		player.Components.GetOrCreate<TackleImpactFeel>();
		player.Components.GetOrCreate<CombatFeelPredictDedupe>();

		player.NetworkSpawn( connection );

		spawnedPlayersBySteamId[steamId] = player;

		SyncMatchHudStateToPlayer( player );
		ApplyMidMatchSpawnRules( player );

		if ( DisableTemplateOnStart && playerTemplate.IsValid() && playerTemplate.Enabled )
		{
			playerTemplate.Enabled = false;
			if ( EnableNetDebugLogs )
			{
				Log.Info( "[NetDebug] Disabled template player after first successful spawn." );
			}
		}

		if ( EnableNetDebugLogs )
		{
			var teamName = ResolveMapMatchConfig()?.GetTeamDisplayName( teamId ) ?? $"Team{teamId}";
			Log.Info( $"[NetDebug] Spawned player for {connection.DisplayName} ({connection.SteamId}) at {t.Position} team={teamId} ({teamName}) teamSlot={teamSlotIndex}." );
		}
	}

	private MapMatchConfig ResolveMapMatchConfig()
	{
		if ( MatchConfig.IsValid() )
			return MatchConfig;

		return MapMatchConfig.FindInScene( Scene );
	}

	private int CountPlayersOnTeam( int teamId )
	{
		var count = 0;
		foreach ( var player in spawnedPlayersBySteamId.Values )
		{
			if ( !player.IsValid() )
				continue;

			var playerTeam = player.Components.Get<PlayerTeam>();
			if ( !playerTeam.IsValid() )
				continue;

			if ( playerTeam.TeamId == teamId )
				count++;
		}

		return count;
	}

	private int AssignTeamForConnection( Connection connection )
	{
		var config = ResolveMapMatchConfig();
		if ( config.IsValid() && config.PracticeArenaMode )
			return config.ResolveSpawnTeamId( MatchTeamIds.Team0 );

		var steamId = (long)connection.SteamId;
		if ( preferredTeamBySteamId.TryGetValue( steamId, out var preferredTeam ) && MatchTeamIds.IsValid( preferredTeam ) )
			return preferredTeam;

		var team0Count = CountPlayersOnTeam( MatchTeamIds.Team0 );
		var team1Count = CountPlayersOnTeam( MatchTeamIds.Team1 );

		if ( team0Count < team1Count )
			return MatchTeamIds.Team0;

		if ( team1Count < team0Count )
			return MatchTeamIds.Team1;

		return Game.Random.Int( 0, MatchTeamIds.TeamCount - 1 );
	}

	/// <summary> Host: replicate a grounded team-spawn reset to every active player (all peers apply via <see cref="PlayerTeam"/>). </summary>
	public void ApplyRoundResetToAllPlayers( float pickupBlockSeconds )
	{
		if ( !Networking.IsHost )
			return;

		foreach ( var player in spawnedPlayersBySteamId.Values )
		{
			if ( !player.IsValid() )
				continue;

			ApplyRoundResetToPlayer( player, pickupBlockSeconds );
		}
	}

	/// <summary> Host: set synced reset pose + bump sequence so owner and proxies teleport locally. </summary>
	public void ApplyRoundResetToPlayer( GameObject player, float pickupBlockSeconds )
	{
		if ( !Networking.IsHost || !player.IsValid() )
			return;

		var playerTeam = player.Components.Get<PlayerTeam>();
		if ( !playerTeam.IsValid() || !MatchTeamIds.IsValid( playerTeam.TeamId ) )
			return;

		var t = GetGroundedSpawnTransformForTeam( playerTeam.TeamId );
		playerTeam.NetRoundResetPosition = t.Position;
		playerTeam.NetRoundResetRotation = t.Rotation;
		playerTeam.NetRoundResetSequence++;
		playerTeam.ApplyRoundResetTransform();

		var ballGrab = player.Components.Get<BallGrab>();
		if ( ballGrab.IsValid() )
			ballGrab.BlockPickupForSeconds( pickupBlockSeconds );
	}

	private void SyncMatchHudStateToPlayer( GameObject player )
	{
		if ( !Networking.IsHost || !player.IsValid() )
			return;

		var director = MatchDirector.FindInScene( Scene );
		var playerTeam = player.Components.Get<PlayerTeam>();
		if ( !director.IsValid() || !playerTeam.IsValid() )
			return;

		playerTeam.NetMatchPhase = director.NetPhase;
		playerTeam.NetTeam0RoundWins = director.NetTeam0RoundWins;
		playerTeam.NetTeam1RoundWins = director.NetTeam1RoundWins;
		playerTeam.NetMatchTimeRemaining = director.NetMatchTimeRemaining;
		playerTeam.NetPhaseTimeRemaining = director.NetPhaseTimeRemaining;
		playerTeam.NetLastGoalScoringTeamId = director.NetLastGoalScoringTeamId;
		playerTeam.NetIsOvertime = director.NetIsOvertime;
		playerTeam.NetMatchWinnerTeamId = director.NetMatchWinnerTeamId;
	}

	private Transform GetGroundedSpawnTransformForTeam( int teamId ) => GetSpawnTransformForTeam( teamId, 0 );

	public static Vector3 SnapPositionToGround( Scene scene, Vector3 origin, GameObject ignore = null )
	{
		var trace = scene.Trace
			.Ray( origin + Vector3.Up * 30f, origin + Vector3.Down * 200f )
			.WithoutTags( "ragdoll" );

		if ( ignore.IsValid() )
			trace = trace.IgnoreGameObjectHierarchy( ignore );

		var tr = trace.Run();
		return tr.Hit ? tr.HitPosition : origin;
	}

	/// <summary> Ground trace + lift so a ball pivot (center) rests on the surface, not half-buried. </summary>
	public static Vector3 SnapBallToGround( Scene scene, GameObject ball, Vector3 origin )
	{
		var ground = SnapPositionToGround( scene, origin, ball );
		return ground + Vector3.Up * GetBallGroundClearance( ball );
	}

	/// <summary> World-space distance from ball pivot to the bottom of its sphere collider(s). </summary>
	public static float GetBallGroundClearance( GameObject ball )
	{
		const float fallbackRadius = 13f;
		const float slack = 2f;

		if ( !ball.IsValid() )
			return fallbackRadius + slack;

		var maxClearance = 0f;
		var scale = ball.WorldTransform.Scale;
		var uniformScale = MathF.Max( scale.x, MathF.Max( scale.y, scale.z ) );

		foreach ( var sphere in ball.Components.GetAll<SphereCollider>( FindMode.EverythingInSelfAndDescendants ) )
		{
			if ( !sphere.IsValid() || !sphere.Enabled )
				continue;

			var worldCenter = ball.WorldTransform.PointToWorld( sphere.Center );
			var worldRadius = sphere.Radius * uniformScale;
			var bottomZ = worldCenter.z - worldRadius;
			var clearance = ball.WorldPosition.z - bottomZ;
			maxClearance = MathF.Max( maxClearance, clearance );
		}

		if ( maxClearance <= 0f )
			maxClearance = fallbackRadius;

		return maxClearance + slack;
	}

	private void ApplyMidMatchSpawnRules( GameObject player )
	{
		if ( !Networking.IsHost || !player.IsValid() )
			return;

		var director = MatchDirector.FindInScene( Scene );
		if ( !director.IsValid() )
			return;

		if ( director.CurrentPhase != MatchPhase.Intermission )
			return;

		var ballGrab = player.Components.Get<BallGrab>();
		if ( ballGrab.IsValid() && director.NetPhaseTimeRemaining > 0f )
			ballGrab.BlockPickupForSeconds( director.NetPhaseTimeRemaining );
	}

	private Transform GetSpawnTransformForTeam( int teamId, int teamSlotIndex )
	{
		var config = ResolveMapMatchConfig();
		if ( config.IsValid() && config.PracticeArenaMode )
			teamId = config.ResolveSpawnTeamId( teamId );

		var spawnList = teamId == MatchTeamIds.Team0 ? Team0Spawns : Team1Spawns;
		var freePoint = PickFreeSpawnPoint( spawnList, teamId );
		Transform t;
		if ( freePoint.IsValid() )
			t = WithoutScale( freePoint.WorldTransform );
		else
		{
			var singleSpawn = teamId == MatchTeamIds.Team0 ? Team0Spawn : Team1Spawn;
			t = singleSpawn.IsValid() ? WithoutScale( singleSpawn.WorldTransform ) : designSpawnTransform;
			t.Position += t.Rotation * Vector3.Right * (teamSlotIndex * JoinSpawnSpacing);
		}

		t.Position = SnapPositionToGround( Scene, t.Position );
		return t;
	}

	/// <summary> First spawn with no same-team player within <see cref="SpawnPointOccupiedRadius"/>. If all are occupied, returns a random entry so we still place the player somewhere. </summary>
	private GameObject PickFreeSpawnPoint( List<GameObject> spawnList, int teamId )
	{
		if ( spawnList is null || spawnList.Count == 0 )
			return null;

		foreach ( var spawn in spawnList )
		{
			if ( !spawn.IsValid() )
				continue;

			if ( !IsSpawnPointOccupied( spawn, teamId ) )
				return spawn;
		}

		for ( var attempt = 0; attempt < 4; attempt++ )
		{
			var pick = spawnList[Game.Random.Int( 0, spawnList.Count - 1 )];
			if ( pick.IsValid() )
				return pick;
		}

		return null;
	}

	private bool IsSpawnPointOccupied( GameObject spawn, int teamId )
	{
		var radiusSq = SpawnPointOccupiedRadius * SpawnPointOccupiedRadius;
		var spawnPos = spawn.WorldPosition;

		foreach ( var player in spawnedPlayersBySteamId.Values )
		{
			if ( !player.IsValid() )
				continue;

			var playerTeam = player.Components.Get<PlayerTeam>();
			if ( !playerTeam.IsValid() || playerTeam.TeamId != teamId )
				continue;

			if ( (player.WorldPosition - spawnPos).LengthSquared <= radiusSq )
				return true;
		}

		return false;
	}

}
