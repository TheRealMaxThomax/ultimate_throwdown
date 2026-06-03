using System.Collections.Generic;
using Sandbox;

/// <summary>
/// Host spawns <see cref="TrafficCar"/> clones on a ordered waypoint lane (e.g. Road0).
/// Wire waypoints in travel order; keep <see cref="CarTemplate"/> disabled in-scene as the clone source.
/// </summary>
public sealed class TrafficSpawner : Component, Component.ExecuteInEditor
{
	[Property] public GameObject CarTemplate { get; set; }

	/// <summary> Lane empties in drive order — first = spawn pose, last = despawn near end. </summary>
	[Property] public List<GameObject> Waypoints { get; set; } = new();

	[Property, Group( "Spawn" )] public float MinSpawnIntervalSeconds { get; set; } = 8f;
	[Property, Group( "Spawn" )] public float MaxSpawnIntervalSeconds { get; set; } = 18f;
	[Property, Group( "Spawn" )] public int MaxAliveCars { get; set; } = 2;
	[Property, Group( "Spawn" )] public bool DisableTemplateOnStart { get; set; } = true;
	[Property, Group( "Spawn" )] public bool OnlySpawnWhileMatchPlaying { get; set; } = true;

	[Property, Group( "Car" )] public float CarSpeed { get; set; } = 420f;
	[Property, Group( "Car" )] public float CarAcceleration { get; set; } = 140f;
	[Property, Group( "Car" )] public float CarDeceleration { get; set; } = 260f;
	/// <summary>Added to every waypoint Y when spawning/moving — raise if the car sinks (model pivot above wheels).</summary>
	[Property, Group( "Car" )] public float CarHeightOffset { get; set; } = 0f;
	[Property, Group( "Car" )] public float KnockdownLaunchSpeed { get; set; } = 520f;
	[Property, Group( "Car" )] public float KnockdownLaunchArc { get; set; } = 0.85f;
	[Property, Group( "Car" )] public Vector3 HitHalfExtents { get; set; } = new( 90f, 45f, 55f );
	/// <summary>Local offset (car forward/left/up) for knockdown box center — raise Z if model pivot is low (wheels).</summary>
	[Property, Group( "Car" )] public Vector3 HitBoxCenterOffset { get; set; }
	/// <summary>Radius of rounded corners between straight segments (units).</summary>
	[Property, Group( "Car" )] public float CornerFilletRadius { get; set; } = 90f;
	[Property, Group( "Car" )] public int CornerArcSamples { get; set; } = 10;
	[Property, Group( "Car" )] public int StraightSegmentSamples { get; set; } = 4;
	/// <summary>Path distance ahead used to detect bends and lower target speed.</summary>
	[Property, Group( "Car" )] public float CurveSlowLookAhead { get; set; } = 120f;
	[Property, Group( "Car" )] public float CurveMinSpeedFraction { get; set; } = 0.4f;
	/// <summary>Extra yaw on the car root after path facing — use 180 if the model nose points backward.</summary>
	[Property, Group( "Car" )] public float FacingYawOffsetDegrees { get; set; } = 0f;
	[Property, Group( "Car" )] public float PlayerHitCooldownSeconds { get; set; } = 0.75f;

	[Property] public Color LaneGizmoColor { get; set; } = new( 0.2f, 0.85f, 1f, 0.85f );

	private readonly List<GameObject> aliveCars = new();
	private float nextSpawnAt;
	private MatchDirector matchDirector;

	protected override void OnStart()
	{
		if ( CarTemplate.IsValid() && DisableTemplateOnStart )
			CarTemplate.Enabled = false;

		if ( !Networking.IsHost )
			return;

		CleanupStrayTrafficCarsInScene();
		matchDirector = MatchDirector.FindInScene( Scene );
		ScheduleNextSpawn();
	}

	protected override void OnUpdate()
	{
		if ( !Networking.IsHost )
			return;

		PruneAliveCars();

		if ( !CanSpawnNow() )
			return;

		if ( Time.Now < nextSpawnAt )
			return;

		if ( !TrySpawnCar() )
			return;

		ScheduleNextSpawn();
	}

	internal void NotifyCarFinished( TrafficCar car )
	{
		if ( car?.GameObject is null )
			return;

		aliveCars.Remove( car.GameObject );
	}

	private bool CanSpawnNow()
	{
		if ( aliveCars.Count >= MaxAliveCars )
			return false;

		if ( !HasValidLane() )
			return false;

		if ( !OnlySpawnWhileMatchPlaying )
			return true;

		if ( !matchDirector.IsValid() )
			matchDirector = MatchDirector.FindInScene( Scene );

		return matchDirector.IsValid() && matchDirector.CurrentPhase == MatchPhase.Playing;
	}

	private bool HasValidLane()
	{
		if ( !CarTemplate.IsValid() || Waypoints is null || Waypoints.Count < 2 )
			return false;

		for ( var i = 0; i < Waypoints.Count; i++ )
		{
			if ( !Waypoints[i].IsValid() )
				return false;
		}

		return true;
	}

	private bool TrySpawnCar()
	{
		var carGo = CloneCarFromTemplate( Waypoints[0].WorldTransform );
		if ( !carGo.IsValid() )
			return false;

		carGo.Enabled = true;
		if ( !TryGetTrafficCar( carGo, out var trafficCar ) )
		{
			Log.Warning( $"[TrafficSpawner] {GameObject.Name}: CarTemplate has no TrafficCar — add it to the template root." );
			carGo.Destroy();
			return false;
		}

		trafficCar.Enabled = true;
		trafficCar.ConfigureLane( this, Waypoints );
		carGo.NetworkSpawn();
		aliveCars.Add( carGo );
		return true;
	}

	/// <summary>Clone works even when the template GameObject is disabled in the editor (same as player prefab).</summary>
	private GameObject CloneCarFromTemplate( Transform spawnTransform )
	{
		if ( !CarTemplate.IsValid() )
			return null;

		var restoreTemplateEnabled = !CarTemplate.Enabled;
		if ( restoreTemplateEnabled )
			CarTemplate.Enabled = true;

		var carGo = CarTemplate.Clone( spawnTransform );

		if ( restoreTemplateEnabled && CarTemplate.IsValid() )
			CarTemplate.Enabled = false;

		return carGo;
	}

	private static bool TryGetTrafficCar( GameObject carGo, out TrafficCar trafficCar )
	{
		foreach ( var candidate in carGo.Components.GetAll<TrafficCar>( FindMode.EverythingInSelfAndDescendants ) )
		{
			if ( !candidate.IsValid() )
				continue;

			trafficCar = candidate;
			return true;
		}

		trafficCar = null;
		return false;
	}

	/// <summary>Removes saved play-mode clones; only spawner-created cars should exist at runtime.</summary>
	private void CleanupStrayTrafficCarsInScene()
	{
		foreach ( var trafficCar in Scene.GetAllComponents<TrafficCar>() )
		{
			if ( !trafficCar.IsValid() )
				continue;

			var go = trafficCar.GameObject;
			if ( IsAnySpawnerCarTemplate( go ) )
				continue;

			go.Destroy();
		}
	}

	private static bool IsAnySpawnerCarTemplate( GameObject go )
	{
		foreach ( var spawner in go.Scene.GetAllComponents<TrafficSpawner>() )
		{
			if ( spawner.CarTemplate.IsValid() && spawner.CarTemplate == go )
				return true;
		}

		return false;
	}

	private void PruneAliveCars()
	{
		for ( var i = aliveCars.Count - 1; i >= 0; i-- )
		{
			if ( !aliveCars[i].IsValid() )
				aliveCars.RemoveAt( i );
		}
	}

	private void ScheduleNextSpawn()
	{
		var min = MinSpawnIntervalSeconds.Clamp( 0.5f, 600f );
		var max = MaxSpawnIntervalSeconds.Clamp( min, 600f );
		nextSpawnAt = Time.Now + Game.Random.Float( min, max );
	}

	protected override void DrawGizmos()
	{
		if ( Waypoints is null || Waypoints.Count < 2 )
			return;

		Gizmo.Draw.Color = LaneGizmoColor;
		for ( var i = 0; i < Waypoints.Count; i++ )
		{
			var wp = Waypoints[i];
			if ( !wp.IsValid() )
				continue;

			Gizmo.Draw.SolidSphere( wp.WorldPosition, 12f );

			if ( i + 1 >= Waypoints.Count )
				continue;

			var next = Waypoints[i + 1];
			if ( !next.IsValid() )
				continue;

			Gizmo.Draw.Line( wp.WorldPosition, next.WorldPosition );
		}
	}
}
