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

	/// <summary> Each joining connection spawns this many units along the spawn point&apos;s local +X so players don&apos;t stack (0 = same spot). </summary>
	[Property] public float JoinSpawnSpacing { get; set; } = 64f;

	[Property] public bool DisableTemplateOnStart { get; set; } = true;
	[Property] public bool EnableNetDebugLogs { get; set; } = false;
	/// <summary> Forces LOD0 on all citizen-like avatars in this scene (host + client). Uses late <see cref="CitizenAvatarLodSystem"/> plus this component&apos;s <c>OnPreRender</c>; avoids <c>OnUpdate</c> where proxy LOD can still change later in the frame.</summary>
	[Property] public bool LockPlayerModelLodInPreRender { get; set; } = true;
	/// <summary> Bone-merged citizen pieces (often arms / body chunks) skipped LOD lock caused pale low-detail skin; disable if clothing flashes wrong materials again.</summary>
	[Property] public bool LockBoneMergedSkinnedMeshLod { get; set; } = false;

	private readonly Dictionary<long, GameObject> spawnedPlayersBySteamId = new();
	private GameObject playerTemplate;
	private Transform designSpawnTransform;
	private bool designSpawnCaptured;
	private float nextEnsurePlayersAt;

	protected override void OnStart()
	{
		EnsureTemplateFound();
		CaptureDesignSpawnTransform();
		if ( EnableNetDebugLogs )
		{
			Log.Info( $"[NetDebug] GameNetworkManager.OnStart IsHost={Networking.IsHost} TemplateValid={playerTemplate.IsValid()} SpawnCaptured={designSpawnCaptured}" );
		}

		EnsurePlayersForActiveConnections();
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
			designSpawnTransform = SpawnPoint.WorldTransform;
			designSpawnCaptured = true;
			return;
		}

		if ( playerTemplate.IsValid() )
		{
			designSpawnTransform = playerTemplate.WorldTransform;
			designSpawnCaptured = true;
		}
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

		var slotIndex = spawnedPlayersBySteamId.Count;
		var t = designSpawnTransform;
		t.Position += t.Rotation * Vector3.Right * (slotIndex * JoinSpawnSpacing);

		var player = playerTemplate.Clone( t );
		player.Name = $"Player_{connection.DisplayName}";
		player.Enabled = true;
		player.NetworkSpawn( connection );

		spawnedPlayersBySteamId[steamId] = player;

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
			Log.Info( $"[NetDebug] Spawned player for {connection.DisplayName} ({connection.SteamId}) at {t.Position} slot={slotIndex}." );
		}
	}

}
