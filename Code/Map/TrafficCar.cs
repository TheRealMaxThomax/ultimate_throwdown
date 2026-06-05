using System;
using System.Collections.Generic;
using Sandbox;

/// <summary>
/// Host-driven lane traffic: straight segments + rounded corner fillets, gradual accel/decel.
/// </summary>
public sealed class TrafficCar : Component, Component.ExecuteInEditor
{
	[Property] public Color HitBoxGizmoColor { get; set; } = new( 1f, 0.35f, 0.1f, 0.35f );

	/// <summary>Uniform scale on the Body child (match template Body transform). Used for host sync + client fallback.</summary>
	[Property, Group( "Car visual" )] public float MeshUniformScale { get; set; } = 0.6f;

	/// <summary>How quickly client proxies chase synced pose (higher = tighter, lower = smoother).</summary>
	[Property, Group( "Network proxy" )] public float ProxyPoseFollowSharpness { get; set; } = 18f;

	[Property, Group( "Engine sound" )] public SoundEvent EngineIdleSound { get; set; }
	[Property, Group( "Engine sound" )] public SoundEvent EngineDriveSound { get; set; }
	[Property, Group( "Engine sound" )] public float EngineSoundVolume { get; set; } = 0.55f;
	[Property, Group( "Engine sound" )] public float EngineSoundMaxDistance { get; set; } = 2800f;
	[Property, Group( "Engine sound" )] public Vector3 EngineSoundLocalOffset { get; set; } = new( 0f, 0f, 24f );
	[Property, Group( "Engine sound" )] public float EngineSoundBlendSharpness { get; set; } = 6f;

	[Sync( SyncFlags.FromHost )] private Vector3 NetWorldPosition { get; set; }
	[Sync( SyncFlags.FromHost )] private Rotation NetWorldRotation { get; set; }
	/// <summary>Uniform scale of the Body child on host (template default 0.6).</summary>
	[Sync( SyncFlags.FromHost )] private float NetMeshUniformScale { get; set; } = 1f;
	[Sync( SyncFlags.FromHost )] private float NetCurrentSpeed { get; set; }
	[Sync( SyncFlags.FromHost )] private float NetDriveBlend { get; set; }

	private GameObject proxyBodyObject;
	private bool proxyPoseInitialized;
	private SoundHandle engineIdleHandle;
	private SoundHandle engineDriveHandle;
	private float engineDriveBlendSmoothed;
	private bool engineSoundsActive;

	private TrafficSpawner spawner;
	private IReadOnlyList<GameObject> waypoints;
	private Vector3 travelDir = Vector3.Forward;
	private readonly Dictionary<Guid, float> nextHitAllowedForPlayerId = new();

	private readonly List<Vector3> pathSamplePoints = new();
	private readonly List<float> pathSampleDistances = new();
	private readonly List<float> pathSampleBaseSpeedMultipliers = new();
	private readonly List<float> pathSpeedMultipliers = new();
	private float totalPathLength;
	private float pathDistance;
	private float currentSpeed;

	private float CruiseSpeed => spawner.IsValid() ? spawner.CarSpeed : 0f;
	private float CarAcceleration => spawner.IsValid() ? spawner.CarAcceleration : 140f;
	private float CarDeceleration => spawner.IsValid() ? spawner.CarDeceleration : 260f;
	private float CornerFilletRadius => spawner.IsValid() ? spawner.CornerFilletRadius : 90f;
	private int CornerArcSamples => spawner.IsValid() ? spawner.CornerArcSamples : 10;
	private int StraightSegmentSamples => spawner.IsValid() ? spawner.StraightSegmentSamples : 4;
	private float CurveSlowLookAhead => spawner.IsValid() ? spawner.CurveSlowLookAhead : 180f;
	private float CurveMinSpeedFraction => spawner.IsValid() ? spawner.CurveMinSpeedFraction : 0.35f;
	private float FacingYawOffsetDegrees => spawner.IsValid() ? spawner.FacingYawOffsetDegrees : 0f;

	private Vector3 HitHalfExtents => spawner.IsValid()
		? spawner.HitHalfExtents
		: ResolveEditorSpawner()?.HitHalfExtents ?? new Vector3( 80f, 40f, 50f );

	private Vector3 HitBoxCenterOffset => spawner.IsValid()
		? spawner.HitBoxCenterOffset
		: ResolveEditorHitBoxCenterOffset();

	private Transform GetHitBoxTransform() =>
		new( WorldPosition + WorldRotation * HitBoxCenterOffset, WorldRotation );

	private Vector3 ResolveEditorHitBoxCenterOffset() => ResolveEditorSpawner()?.HitBoxCenterOffset ?? Vector3.Zero;

	private TrafficSpawner ResolveEditorSpawner()
	{
		var root = GameObject;
		foreach ( var laneSpawner in Scene.GetAllComponents<TrafficSpawner>() )
		{
			if ( !laneSpawner.IsValid() || !laneSpawner.CarTemplate.IsValid() )
				continue;

			if ( laneSpawner.CarTemplate == root )
				return laneSpawner;
		}

		return null;
	}

	internal float CarHeightOffset => spawner.IsValid() ? spawner.CarHeightOffset : 0f;

	internal Vector3 ApplyLaneHeight( Vector3 waypointWorldPosition ) =>
		waypointWorldPosition + Vector3.Up * CarHeightOffset;

	internal void ConfigureLane( TrafficSpawner laneSpawner, IReadOnlyList<GameObject> laneWaypoints )
	{
		spawner = laneSpawner;
		waypoints = laneWaypoints;
		BuildPathSamples();
		pathDistance = 0f;
		currentSpeed = 0f;
		PlaceOnPath( 0f );
	}

	/// <summary>Call on host before <c>NetworkSpawn</c> so clients receive enabled renderers.</summary>
	internal static void PrepareHierarchyForNetworkSpawn( GameObject carRoot )
	{
		EnableHierarchyForRendering( carRoot );
	}

	protected override void OnStart()
	{
		if ( IsProxy )
		{
			proxyBodyObject = FindBodyObject();
			DisableHoistedRootRendererIfPresent();
			EnableHierarchyForRendering( GameObject );
			DisableProxyPhysicsAndColliders();
			ApplyProxyBodyScale();
		}

		StartEngineSounds();
	}

	protected override void OnUpdate()
	{
		if ( IsProxy )
		{
			ApplySyncedPoseFromHost();
			UpdateEngineSounds();
			return;
		}

		if ( !Networking.IsHost || waypoints is null || waypoints.Count < 2 || totalPathLength <= 0f )
			return;

		AdvanceAlongLane();
		TryKnockdownPlayersInHitBox();
		UpdateEngineSounds();
	}

	protected override void OnDisabled()
	{
		if ( IsSpawnerCarTemplate() )
			return;

		StopEngineSounds();
	}

	protected override void OnDestroy()
	{
		if ( IsSpawnerCarTemplate() )
			return;

		StopEngineSounds();
	}

	private void ApplySyncedPoseFromHost()
	{
		ApplyProxyBodyScale();

		if ( !proxyPoseInitialized )
		{
			WorldPosition = NetWorldPosition;
			WorldRotation = NetWorldRotation;
			proxyPoseInitialized = true;
			return;
		}

		var blend = MathF.Min( 1f, Time.Delta * ProxyPoseFollowSharpness.Clamp( 1f, 60f ) );
		WorldPosition = Vector3.Lerp( WorldPosition, NetWorldPosition, blend );
		WorldRotation = Rotation.Slerp( WorldRotation, NetWorldRotation, blend );
	}

	private void DisableProxyPhysicsAndColliders()
	{
		foreach ( var rb in Components.GetAll<Rigidbody>( FindMode.EverythingInSelfAndDescendants ) )
		{
			if ( !rb.IsValid() )
				continue;

			rb.MotionEnabled = false;
			rb.Enabled = false;
		}

		foreach ( var col in Components.GetAll<Collider>( FindMode.EverythingInSelfAndDescendants ) )
		{
			if ( col.IsValid() )
				col.Enabled = false;
		}
	}

	private void ApplyProxyBodyScale()
	{
		if ( !proxyBodyObject.IsValid() )
			proxyBodyObject = FindBodyObject();

		if ( !proxyBodyObject.IsValid() )
			return;

		var uniform = ResolveMeshUniformScale();
		proxyBodyObject.LocalScale = new Vector3( uniform, uniform, uniform );
	}

	private float ResolveMeshUniformScale()
	{
		if ( Networking.IsHost )
		{
			var fromBody = ReadMeshUniformScale();
			if ( fromBody > 0.001f && fromBody < 0.95f )
				return fromBody;
		}
		else if ( NetMeshUniformScale > 0.001f && NetMeshUniformScale < 0.95f )
		{
			return NetMeshUniformScale;
		}

		if ( MeshUniformScale > 0.001f && MeshUniformScale < 0.95f )
			return MeshUniformScale;

		return 1f;
	}

	/// <summary>Older client builds hoisted the mesh to root — turn that off so Body scale applies.</summary>
	private void DisableHoistedRootRendererIfPresent()
	{
		var body = FindBodyObject();
		if ( !body.IsValid() )
			return;

		var rootRenderer = Components.Get<ModelRenderer>();
		var bodyRenderer = body.Components.Get<ModelRenderer>();
		if ( !rootRenderer.IsValid() || !bodyRenderer.IsValid() )
			return;

		if ( rootRenderer.GameObject != GameObject )
			return;

		rootRenderer.Enabled = false;
		bodyRenderer.Enabled = true;
	}

	private GameObject FindBodyObject()
	{
		foreach ( var child in GameObject.Children )
		{
			if ( child.IsValid() && child.Name.Equals( "Body", StringComparison.OrdinalIgnoreCase ) )
				return child;
		}

		foreach ( var renderer in Components.GetAll<ModelRenderer>( FindMode.EverythingInSelfAndDescendants ) )
		{
			if ( !renderer.IsValid() || renderer.GameObject == GameObject || renderer.Model is null )
				continue;

			return renderer.GameObject;
		}

		return null;
	}

	private static void EnableHierarchyForRendering( GameObject root )
	{
		if ( !root.IsValid() )
			return;

		VisitDescendants( root, go => go.Enabled = true );

		foreach ( var renderer in root.Components.GetAll<ModelRenderer>( FindMode.EverythingInSelfAndDescendants ) )
		{
			if ( renderer.IsValid() )
				renderer.Enabled = true;
		}
	}

	private static void VisitDescendants( GameObject root, Action<GameObject> visit )
	{
		if ( !root.IsValid() )
			return;

		visit( root );
		foreach ( var child in root.Children )
			VisitDescendants( child, visit );
	}

	private void AdvanceAlongLane()
	{
		var targetSpeed = GetTargetSpeedForPathDistance( pathDistance );
		var accelRate = currentSpeed < targetSpeed ? CarAcceleration : CarDeceleration;
		currentSpeed = MoveTowards( currentSpeed, targetSpeed, accelRate * Time.Delta );

		pathDistance += currentSpeed * Time.Delta;
		if ( pathDistance >= totalPathLength )
		{
			FinishLane();
			return;
		}

		PlaceOnPath( pathDistance );
	}

	private void PlaceOnPath( float distance )
	{
		WorldPosition = SamplePathPosition( distance );

		var flat = SamplePathTangent( distance ).WithZ( 0f );
		if ( flat.LengthSquared > 0.001f )
		{
			travelDir = flat.Normal;
			WorldRotation = Rotation.LookAt( travelDir ) * Rotation.FromYaw( FacingYawOffsetDegrees );
		}

		PublishNetworkState();
	}

	private void PublishNetworkState()
	{
		NetWorldPosition = WorldPosition;
		NetWorldRotation = WorldRotation;
		NetMeshUniformScale = ResolveMeshUniformScale();
		NetCurrentSpeed = currentSpeed;
	}

	private bool IsSpawnerCarTemplate()
	{
		foreach ( var laneSpawner in Scene.GetAllComponents<TrafficSpawner>() )
		{
			if ( !laneSpawner.IsValid() || !laneSpawner.CarTemplate.IsValid() )
				continue;

			if ( laneSpawner.CarTemplate == GameObject )
				return true;
		}

		return false;
	}

	private static bool IsEngineSoundAssigned( SoundEvent soundEvent ) =>
		soundEvent is not null && soundEvent.IsValid();

	private void StartEngineSounds()
	{
		if ( !Game.IsPlaying || !GameObject.IsValid() || IsSpawnerCarTemplate() || engineSoundsActive )
			return;

		if ( !IsEngineSoundAssigned( EngineIdleSound ) && !IsEngineSoundAssigned( EngineDriveSound ) )
			return;

		if ( IsEngineSoundAssigned( EngineIdleSound ) )
		{
			engineIdleHandle = GameObject.PlaySound( EngineIdleSound, EngineSoundLocalOffset );
			engineIdleHandle.Distance = EngineSoundMaxDistance;
			engineIdleHandle.Volume = 0f;
		}

		if ( IsEngineSoundAssigned( EngineDriveSound ) )
		{
			engineDriveHandle = GameObject.PlaySound( EngineDriveSound, EngineSoundLocalOffset );
			engineDriveHandle.Distance = EngineSoundMaxDistance;
			engineDriveHandle.Volume = 0f;
		}

		engineSoundsActive = engineIdleHandle.IsPlaying || engineDriveHandle.IsPlaying;
		UpdateEngineSounds();
	}

	private void UpdateEngineSounds()
	{
		if ( !engineSoundsActive )
			return;

		float driveBlend;
		if ( IsProxy )
		{
			driveBlend = NetDriveBlend;
		}
		else
		{
			var cruise = CruiseSpeed;
			if ( cruise <= 1f )
				return;

			driveBlend = 0f;
			var targetSpeed = GetTargetSpeedForPathDistance( pathDistance );
			var accel = targetSpeed - currentSpeed;
			if ( accel > 0f )
				driveBlend = (accel / Math.Max( CarAcceleration, 1f )).Clamp( 0f, 1f );

			// Keep accel sound off at tiny movement speeds to avoid flutter at spawn/stop.
			var speedFraction = (currentSpeed / cruise).Clamp( 0f, 1f );
			if ( speedFraction < 0.08f )
				driveBlend = 0f;

			NetDriveBlend = driveBlend;
		}

		var blendRate = EngineSoundBlendSharpness.Clamp( 0.5f, 30f );
		engineDriveBlendSmoothed = MathX.Lerp( engineDriveBlendSmoothed, driveBlend, Time.Delta * blendRate );

		var master = EngineSoundVolume.Clamp( 0f, 2f );
		if ( engineIdleHandle.IsPlaying )
			engineIdleHandle.Volume = master * (1f - engineDriveBlendSmoothed);

		if ( engineDriveHandle.IsPlaying )
			engineDriveHandle.Volume = master * engineDriveBlendSmoothed;
	}

	private void StopEngineSounds()
	{
		if ( !engineSoundsActive )
			return;

		if ( engineIdleHandle.IsPlaying )
			engineIdleHandle.Stop( 0.12f );

		if ( engineDriveHandle.IsPlaying )
			engineDriveHandle.Stop( 0.12f );

		engineSoundsActive = false;
		engineIdleHandle = default;
		engineDriveHandle = default;
	}

	private float ReadMeshUniformScale()
	{
		var body = FindBodyObject();
		if ( !body.IsValid() )
			return 1f;

		var scale = body.LocalScale;
		return (scale.x + scale.y + scale.z) / 3f;
	}

	private float GetTargetSpeedForPathDistance( float distance )
	{
		var cruise = CruiseSpeed;
		var mul = SamplePathSpeedMultiplier( distance );
		return cruise * mul;
	}

	private void BuildPathSamples()
	{
		pathSamplePoints.Clear();
		pathSampleDistances.Clear();
		pathSampleBaseSpeedMultipliers.Clear();
		pathSpeedMultipliers.Clear();
		totalPathLength = 0f;

		var lanePoints = CollectLanePoints();
		if ( lanePoints.Count < 2 )
			return;

		BuildFilletPath( lanePoints );
		BuildPathSpeedProfile();
	}

	private void BuildPathSpeedProfile()
	{
		pathSpeedMultipliers.Clear();
		if ( pathSampleDistances.Count == 0 )
			return;

		var lookAhead = CurveSlowLookAhead.Clamp( 0f, 512f );
		for ( var i = 0; i < pathSampleDistances.Count; i++ )
		{
			var startDist = pathSampleDistances[i];
			var worst = pathSampleBaseSpeedMultipliers[i];
			for ( var j = i + 1; j < pathSampleDistances.Count; j++ )
			{
				if ( pathSampleDistances[j] - startDist > lookAhead )
					break;

				worst = MathF.Min( worst, pathSampleBaseSpeedMultipliers[j] );
			}

			pathSpeedMultipliers.Add( worst );
		}
	}

	private static float SpeedMulFromTurnAngle( float turnAngleRad, float minFrac )
	{
		var sharpness = (turnAngleRad / (MathF.PI * 0.5f)).Clamp( 0f, 1f );
		return 1f - sharpness * (1f - minFrac);
	}

	private float SamplePathSpeedMultiplier( float distance )
	{
		if ( pathSpeedMultipliers.Count == 0 )
			return 1f;

		if ( distance <= pathSampleDistances[0] )
			return pathSpeedMultipliers[0];

		if ( distance >= totalPathLength )
			return pathSpeedMultipliers[^1];

		for ( var i = 1; i < pathSampleDistances.Count; i++ )
		{
			if ( pathSampleDistances[i] < distance )
				continue;

			var span = pathSampleDistances[i] - pathSampleDistances[i - 1];
			var t = span > 0.001f ? (distance - pathSampleDistances[i - 1]) / span : 0f;
			return MathX.Lerp( pathSpeedMultipliers[i - 1], pathSpeedMultipliers[i], t );
		}

		return pathSpeedMultipliers[^1];
	}

	private List<Vector3> CollectLanePoints()
	{
		var lanePoints = new List<Vector3>();
		for ( var i = 0; i < waypoints.Count; i++ )
		{
			var wp = waypoints[i];
			if ( !wp.IsValid() )
				continue;

			lanePoints.Add( ApplyLaneHeight( wp.WorldPosition ) );
		}

		return lanePoints;
	}

	private void BuildFilletPath( List<Vector3> points )
	{
		if ( points.Count < 2 )
			return;

		var minFrac = CurveMinSpeedFraction.Clamp( 0.05f, 1f );

		if ( points.Count == 2 )
		{
			AddPathSample( points[0], 1f );
			AppendLineSamples( points[0], points[1], 1f );
			return;
		}

		var cursor = points[0];
		AddPathSample( cursor, 1f );
		var filletRadius = CornerFilletRadius.Clamp( 8f, 512f );
		var arcSamples = CornerArcSamples.Clamp( 3, 32 );

		for ( var i = 1; i < points.Count - 1; i++ )
		{
			var prev = points[i - 1];
			var corner = points[i];
			var next = points[i + 1];

			if ( !TryGetCornerFillet( prev, corner, next, filletRadius, out var arcStart, out var arcEnd, out var arcControl, out var turnAngle ) )
			{
				AppendLineSamples( cursor, corner, 1f );
				cursor = corner;
				continue;
			}

			var cornerMul = SpeedMulFromTurnAngle( turnAngle, minFrac );
			AppendLineSamples( cursor, arcStart, 1f );
			AppendQuadraticBezierSamples( arcStart, arcControl, arcEnd, arcSamples, cornerMul );
			cursor = arcEnd;
		}

		AppendLineSamples( cursor, points[^1], 1f );
	}

	private bool TryGetCornerFillet(
		Vector3 prev,
		Vector3 corner,
		Vector3 next,
		float radius,
		out Vector3 arcStart,
		out Vector3 arcEnd,
		out Vector3 arcControl,
		out float turnAngleRadians )
	{
		arcStart = default;
		arcEnd = default;
		arcControl = corner;
		turnAngleRadians = 0f;

		var inDir = (corner - prev).WithZ( 0f );
		var outDir = (next - corner).WithZ( 0f );
		var inLen = inDir.Length;
		var outLen = outDir.Length;
		if ( inLen < 8f || outLen < 8f )
			return false;

		inDir /= inLen;
		outDir /= outLen;

		var dot = Vector3.Dot( inDir, outDir ).Clamp( -1f, 1f );
		turnAngleRadians = MathF.Acos( dot );
		if ( turnAngleRadians < 0.12f )
			return false;

		var maxRadius = MathF.Min( inLen, outLen ) * 0.45f;
		var useRadius = MathF.Min( radius, maxRadius );
		if ( useRadius < 8f )
			return false;

		arcStart = corner - inDir * useRadius;
		arcEnd = corner + outDir * useRadius;
		return true;
	}

	private void AppendLineSamples( Vector3 from, Vector3 to, float speedMul )
	{
		if ( Vector3.DistanceBetween( from, to ) <= 0.05f )
			return;

		var samples = StraightSegmentSamples.Clamp( 1, 32 );
		for ( var i = 1; i <= samples; i++ )
		{
			var t = i / (float)samples;
			AppendPathSample( Vector3.Lerp( from, to, t ), speedMul );
		}
	}

	private void AppendQuadraticBezierSamples( Vector3 start, Vector3 control, Vector3 end, int samples, float speedMul )
	{
		for ( var i = 1; i <= samples; i++ )
		{
			var t = i / (float)samples;
			var u = 1f - t;
			var point = u * u * start + 2f * u * t * control + t * t * end;
			AppendPathSample( point, speedMul );
		}
	}

	private void AddPathSample( Vector3 point, float speedMul )
	{
		pathSamplePoints.Add( point );
		pathSampleDistances.Add( 0f );
		pathSampleBaseSpeedMultipliers.Add( speedMul );
	}

	private void AppendPathSample( Vector3 point, float speedMul )
	{
		var prev = pathSamplePoints[^1];
		totalPathLength += Vector3.DistanceBetween( prev, point );
		pathSamplePoints.Add( point );
		pathSampleDistances.Add( totalPathLength );
		pathSampleBaseSpeedMultipliers.Add( speedMul );
	}

	private Vector3 SamplePathPosition( float distance )
	{
		if ( pathSamplePoints.Count == 0 )
			return WorldPosition;

		if ( distance <= 0f )
			return pathSamplePoints[0];

		if ( distance >= totalPathLength )
			return pathSamplePoints[^1];

		for ( var i = 1; i < pathSampleDistances.Count; i++ )
		{
			if ( pathSampleDistances[i] < distance )
				continue;

			var span = pathSampleDistances[i] - pathSampleDistances[i - 1];
			var t = span > 0.001f ? (distance - pathSampleDistances[i - 1]) / span : 0f;
			return Vector3.Lerp( pathSamplePoints[i - 1], pathSamplePoints[i], t );
		}

		return pathSamplePoints[^1];
	}

	private Vector3 SamplePathTangent( float distance )
	{
		const float epsilon = 6f;
		var a = SamplePathPosition( MathF.Max( 0f, distance - epsilon ) );
		var b = SamplePathPosition( MathF.Min( totalPathLength, distance + epsilon ) );
		var tangent = (b - a).WithZ( 0f );
		return tangent.LengthSquared > 0.001f ? tangent.Normal : travelDir;
	}

	private static float MoveTowards( float current, float target, float maxDelta )
	{
		if ( MathF.Abs( target - current ) <= maxDelta )
			return target;

		return current + MathF.Sign( target - current ) * maxDelta;
	}

	private void TryKnockdownPlayersInHitBox()
	{
		if ( !spawner.IsValid() || HitHalfExtents.LengthSquared <= 0.001f )
			return;

		var halfExtents = HitHalfExtents;
		var hitTransform = GetHitBoxTransform();
		var now = Time.Now;
		var cooldown = spawner.PlayerHitCooldownSeconds.Clamp( 0.05f, 5f );

		foreach ( var tackle in Scene.GetAllComponents<PlayerTackle>() )
		{
			if ( !tackle.IsValid() || !tackle.GameObject.Enabled )
				continue;

			var playerGo = tackle.GameObject;
			var sample = playerGo.WorldPosition + Vector3.Up * 36f;
			if ( !IsPointInOrientedBox( sample, hitTransform, halfExtents ) )
				continue;

			var playerId = playerGo.Id;
			if ( nextHitAllowedForPlayerId.TryGetValue( playerId, out var allowedAt ) && now < allowedAt )
				continue;

			if ( !tackle.ApplyKnockdownFromHost( travelDir, spawner.KnockdownLaunchSpeed, spawner.KnockdownLaunchArc ) )
				continue;

			nextHitAllowedForPlayerId[playerId] = now + cooldown;
		}
	}

	private void FinishLane()
	{
		if ( spawner.IsValid() )
			spawner.NotifyCarFinished( this );

		GameObject.Destroy();
	}

	private static bool IsPointInOrientedBox( Vector3 worldPoint, Transform boxTransform, Vector3 halfExtents )
	{
		var localPoint = boxTransform.PointToLocal( worldPoint );
		return Math.Abs( localPoint.x ) <= halfExtents.x
			&& Math.Abs( localPoint.y ) <= halfExtents.y
			&& Math.Abs( localPoint.z ) <= halfExtents.z;
	}

	protected override void DrawGizmos()
	{
		if ( HitHalfExtents.LengthSquared <= 0.001f )
			return;

		var prevTransform = Gizmo.Transform;
		Gizmo.Transform = GetHitBoxTransform().WithScale( 1f );
		Gizmo.Draw.Color = HitBoxGizmoColor;
		Gizmo.Draw.LineBBox( new BBox( -HitHalfExtents, HitHalfExtents ) );
		Gizmo.Transform = prevTransform;
	}
}
