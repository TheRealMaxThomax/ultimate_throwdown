using Sandbox;

/// <summary>
/// Mounts a compiled Hammer map when Play starts. MapList in ultimate_throwdown.sbproj does not load maps by itself;
/// without MapInstance you only get StartupScene geometry (flat floor / primitives).
/// If load fails, change <see cref="StartupMapId"/> (see log / Asset Browser for the real id).
/// </summary>
public sealed class StartupMapBootstrap : GameObjectSystem<StartupMapBootstrap>, ISceneStartup
{
	/// Same tag as <c>PlayerTackle</c> practice dummies — keeps scene NPCs from sliding when players bump their dynamic <see cref="Rigidbody"/>.
	private const string PracticeNpcTag = "practice_npc";

	/// <summary>
	/// Base map name only — resolves to <c>maps/testing_map.*</c> inside this game package. Using
	/// <c>local.ultimate_throwdown.testing_map</c> made the loader look for <c>maps/local.ultimate_throwdown</c>.
	/// Ensure ultimate_throwdown.sbproj sets <c>"Resources": "*"</c> so compiled maps ship with the game (otherwise VPK mount fails).
	/// </summary>
	private const string StartupMapId = "testing_map";

	public StartupMapBootstrap( Scene scene )
		: base( scene )
	{
	}

	void ISceneStartup.OnHostPreInitialize( SceneFile sceneFile )
	{
	}

	void ISceneStartup.OnHostInitialize()
	{
		var mapAlreadyInScene = false;
		foreach ( var existing in Scene.GetAllComponents<MapInstance>() )
		{
			if ( existing.IsValid() )
			{
				mapAlreadyInScene = true;
				break;
			}
		}

		if ( !mapAlreadyInScene )
		{
			var root = new GameObject( true, "StartupMapInstance" );
			var map = root.AddComponent<MapInstance>();
			map.MapName = StartupMapId;
			map.EnableCollision = true;
			map.UseMapFromLaunch = false;

			Log.Info( $"[StartupMapBootstrap] Loading map '{StartupMapId}' via MapInstance." );
		}

		ApplyPracticeNpcRigidbodyLocks();
	}

	void ISceneStartup.OnClientInitialize()
	{
		ApplyPracticeNpcRigidbodyLocks();
	}

	/// <summary>
	/// Practice NPCs use the same prefab as players (<see cref="Rigidbody"/> + <see cref="PlayerController"/>).
	/// A dynamic body gets pushed by other character controllers and slides; locking translation makes them act like solid obstacles.
	/// </summary>
	private void ApplyPracticeNpcRigidbodyLocks()
	{
		foreach ( var go in Scene.GetAllObjects( true ) )
		{
			if ( !go.Tags.Has( PracticeNpcTag ) )
				continue;

			var rb = go.Components.Get<Rigidbody>();
			if ( !rb.IsValid() )
				continue;

			var lockTx = rb.Locking;
			lockTx.X = true;
			lockTx.Y = true;
			lockTx.Z = true;
			rb.Locking = lockTx;
		}
	}
}
