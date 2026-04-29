using Sandbox;
using System.Collections.Generic;

public sealed class GameNetworkManager : Component, Component.INetworkListener
{
	[Property] public string PlayerTemplateName { get; set; } = "Player";
	[Property] public bool DisableTemplateOnStart { get; set; } = true;
	[Property] public bool EnableNetDebugLogs { get; set; } = true;

	private readonly Dictionary<long, GameObject> spawnedPlayersBySteamId = new();
	private GameObject playerTemplate;
	private float nextEnsurePlayersAt;

	protected override void OnStart()
	{
		EnsureTemplateFound();
		if ( EnableNetDebugLogs )
		{
			Log.Info( $"[NetDebug] GameNetworkManager.OnStart host ready. TemplateValid={playerTemplate.IsValid()}" );
		}

		EnsurePlayersForActiveConnections();
		nextEnsurePlayersAt = Time.Now + 1f;
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
		SpawnPlayerForConnectionIfMissing( connection );
	}

	public void OnDisconnected( Connection connection )
	{
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
			Log.Info( $"[NetDebug] Found player template '{playerTemplate.Name}'." );
		}
	}

	private void EnsurePlayersForActiveConnections()
	{
		EnsureTemplateFound();
		if ( !playerTemplate.IsValid() )
			return;

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

		// Fallback: in some editor-host scenarios the local connection can be missing
		// from the active list early. Try local explicitly so host always gets a pawn.
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
		EnsureTemplateFound();
		if ( !playerTemplate.IsValid() )
			return;

		var steamId = (long)connection.SteamId;
		if ( spawnedPlayersBySteamId.TryGetValue( steamId, out var existingPlayer ) && existingPlayer.IsValid() )
			return;

		var player = playerTemplate.Clone( playerTemplate.WorldTransform );
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
			Log.Info( $"[NetDebug] Spawned player for {connection.DisplayName} ({connection.SteamId})." );
		}
	}
}
