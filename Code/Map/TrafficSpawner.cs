using System;
using System.Collections.Generic;
using Sandbox;

/// <summary>
/// Host spawns <see cref="TrafficCar"/> clones on a ordered waypoint lane (e.g. Road0).
/// Wire waypoints in travel order; keep <see cref="CarTemplate"/> disabled in-scene as the clone source.
/// </summary>
public sealed class TrafficSpawner : Component
{
	[Property] public GameObject CarTemplate { get; set; }

	/// <summary> Lane empties in drive order — first = spawn pose, last = despawn near end. </summary>
	[Property] public List<GameObject> Waypoints { get; set; } = new();

	[Property, Group( "Spawn" )] public float MinSpawnIntervalSeconds { get; set; } = 8f;
	[Property, Group( "Spawn" )] public float MaxSpawnIntervalSeconds { get; set; } = 18f;
	[Property, Group( "Spawn" )] public int MaxAliveCars { get; set; } = 2;
	[Property, Group( "Spawn" )] public bool DisableTemplateOnStart { get; set; } = true;
	[Property, Group( "Spawn" )] public bool OnlySpawnWhileMatchPlaying { get; set; } = true;
	/// <summary>Random Body mesh per spawn — sets Body <see cref="ModelRenderer"/> + <see cref="ModelCollider"/>; empty keeps template models.</summary>
	[Property, Group( "Spawn" )] public List<Model> CarModelVariants { get; set; } = new();

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
	/// <summary>Path distance ahead — car starts slowing this far before a bend.</summary>
	[Property, Group( "Car" )] public float CurveSlowLookAhead { get; set; } = 180f;
	[Property, Group( "Car" )] public float CurveMinSpeedFraction { get; set; } = 0.35f;
	/// <summary>Extra yaw on the car root after path facing — use 180 if the model nose points backward.</summary>
	[Property, Group( "Car" )] public float FacingYawOffsetDegrees { get; set; } = 0f;
	[Property, Group( "Car" )] public float PlayerHitCooldownSeconds { get; set; } = 0.75f;

	[Property] public Color LaneGizmoColor { get; set; } = new( 0.2f, 0.85f, 1f, 0.85f );

	private readonly List<GameObject> aliveCars = new();
	private float nextSpawnAt;
	private MatchDirector matchDirector;

	protected override void OnStart()
	{
		if ( !Game.IsPlaying )
			return;

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
		if ( !Game.IsPlaying || !Networking.IsHost )
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
		if ( !Game.IsPlaying )
			return false;

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

		carGo.Enabled = false;

		if ( !TryGetTrafficCar( carGo, out var trafficCar ) )
		{
			Log.Warning( $"[TrafficSpawner] {GameObject.Name}: CarTemplate has no TrafficCar — add it to the template root." );
			carGo.Destroy();
			return false;
		}

		var spawnModel = PickRandomCarModelVariant();
		if ( spawnModel is null )
			Log.Warning( $"[TrafficSpawner] {GameObject.Name}: No valid CarModelVariants — using template Body models." );

		trafficCar.ConfigureLane( this, Waypoints );
		TrafficCar.PrepareHierarchyForNetworkSpawn( carGo );

		carGo.Enabled = true;
		trafficCar.Enabled = true;
		carGo.NetworkMode = NetworkMode.Object;
		if ( !carGo.NetworkSpawn() )
		{
			Log.Warning( $"[TrafficSpawner] {GameObject.Name}: NetworkSpawn failed for traffic car." );
			carGo.Destroy();
			return false;
		}

		// Apply after NetworkSpawn — spawn resets renderer/collider to template defaults.
		if ( spawnModel is not null )
			ApplyCarBodyModel( carGo, spawnModel );

		carGo.Network.Refresh();
		aliveCars.Add( carGo );
		return true;
	}

	/// <summary>Clone works even when the template GameObject is disabled in the editor (same as player prefab).</summary>
	private GameObject CloneCarFromTemplate( Transform spawnTransform )
	{
		if ( !CarTemplate.IsValid() )
			return null;

		return CarTemplate.Clone( spawnTransform );
	}

	private static void ApplyCarBodyModel( GameObject carRoot, Model model )
	{
		if ( !carRoot.IsValid() || model is null || !model.IsValid )
			return;

		var bodyGo = FindBodyGameObject( carRoot );
		if ( !bodyGo.IsValid() )
			return;

		var renderer = bodyGo.Components.Get<ModelRenderer>();
		if ( renderer.IsValid() )
			renderer.Model = model;

		var collider = bodyGo.Components.Get<ModelCollider>();
		if ( collider.IsValid() )
			collider.Model = model;
	}

	private Model PickRandomCarModelVariant()
	{
		if ( CarModelVariants is null || CarModelVariants.Count == 0 )
			return null;

		var validModels = new List<Model>();
		foreach ( var candidate in CarModelVariants )
		{
			var resolved = ResolveCarModel( candidate );
			if ( resolved is not null )
				validModels.Add( resolved );
		}

		if ( validModels.Count == 0 )
			return null;

		return validModels[Game.Random.Int( 0, validModels.Count - 1 )];
	}

	private static Model ResolveCarModel( Model candidate )
	{
		if ( candidate is null )
			return null;

		if ( candidate.IsValid )
			return candidate;

		if ( string.IsNullOrWhiteSpace( candidate.Name ) )
			return null;

		var loaded = Model.Load( candidate.Name );
		return loaded is not null && loaded.IsValid ? loaded : null;
	}

	private static GameObject FindBodyGameObject( GameObject carRoot )
	{
		if ( !carRoot.IsValid() )
			return null;

		foreach ( var child in carRoot.Children )
		{
			if ( !child.IsValid() || !child.Name.Equals( "Body", StringComparison.OrdinalIgnoreCase ) )
				continue;

			return child;
		}

		foreach ( var renderer in carRoot.Components.GetAll<ModelRenderer>( FindMode.EverythingInSelfAndDescendants ) )
		{
			if ( !renderer.IsValid() || renderer.GameObject == carRoot )
				continue;

			return renderer.GameObject;
		}

		return null;
	}

	private static ModelRenderer FindBodyModelRenderer( GameObject carRoot )
	{
		var bodyGo = FindBodyGameObject( carRoot );
		if ( !bodyGo.IsValid() )
			return null;

		return bodyGo.Components.Get<ModelRenderer>();
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
