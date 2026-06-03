using System;
using System.Collections.Generic;
using Sandbox;

/// <summary>
/// Host-driven lane traffic: straight segments + rounded corner fillets, gradual accel/decel.
/// </summary>
public sealed class TrafficCar : Component, Component.ExecuteInEditor
{
	[Property] public Color HitBoxGizmoColor { get; set; } = new( 1f, 0.35f, 0.1f, 0.35f );

	private TrafficSpawner spawner;
	private IReadOnlyList<GameObject> waypoints;
	private Vector3 travelDir = Vector3.Forward;
	private readonly Dictionary<Guid, float> nextHitAllowedForPlayerId = new();

	private readonly List<Vector3> pathSamplePoints = new();
	private readonly List<float> pathSampleDistances = new();
	private float totalPathLength;
	private float pathDistance;
	private float currentSpeed;

	private float CruiseSpeed => spawner.IsValid() ? spawner.CarSpeed : 0f;
	private float CarAcceleration => spawner.IsValid() ? spawner.CarAcceleration : 140f;
	private float CarDeceleration => spawner.IsValid() ? spawner.CarDeceleration : 260f;
	private float CornerFilletRadius => spawner.IsValid() ? spawner.CornerFilletRadius : 90f;
	private int CornerArcSamples => spawner.IsValid() ? spawner.CornerArcSamples : 10;
	private int StraightSegmentSamples => spawner.IsValid() ? spawner.StraightSegmentSamples : 4;
	private float CurveSlowLookAhead => spawner.IsValid() ? spawner.CurveSlowLookAhead : 120f;
	private float CurveMinSpeedFraction => spawner.IsValid() ? spawner.CurveMinSpeedFraction : 0.4f;
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

	protected override void OnUpdate()
	{
		if ( !Networking.IsHost || waypoints is null || waypoints.Count < 2 || totalPathLength <= 0f )
			return;

		AdvanceAlongLane();
		TryKnockdownPlayersInHitBox();
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
		if ( flat.LengthSquared <= 0.001f )
			return;

		travelDir = flat.Normal;
		WorldRotation = Rotation.LookAt( travelDir ) * Rotation.FromYaw( FacingYawOffsetDegrees );
	}

	private float GetTargetSpeedForPathDistance( float distance )
	{
		var cruise = CruiseSpeed;
		var lookAhead = CurveSlowLookAhead;
		if ( lookAhead <= 0f || totalPathLength <= 0f )
			return cruise;

		var tangentNow = SamplePathTangent( distance );
		var tangentAhead = SamplePathTangent( distance + lookAhead );
		var flatNow = tangentNow.WithZ( 0f );
		var flatAhead = tangentAhead.WithZ( 0f );
		if ( flatNow.LengthSquared <= 0.001f || flatAhead.LengthSquared <= 0.001f )
			return cruise;

		flatNow = flatNow.Normal;
		flatAhead = flatAhead.Normal;

		var dot = Vector3.Dot( flatNow, flatAhead );
		if ( dot >= 0.995f )
			return cruise;

		var minFrac = CurveMinSpeedFraction.Clamp( 0.05f, 1f );
		var bend = (1f - dot) * 0.5f;
		var slowMul = 1f - bend * (1f - minFrac);
		return cruise * slowMul.Clamp( minFrac, 1f );
	}

	private void BuildPathSamples()
	{
		pathSamplePoints.Clear();
		pathSampleDistances.Clear();
		totalPathLength = 0f;

		var lanePoints = CollectLanePoints();
		if ( lanePoints.Count < 2 )
			return;

		var polyline = BuildFilletPath( lanePoints );
		if ( polyline.Count < 2 )
			return;

		AddPathSample( polyline[0] );
		for ( var i = 1; i < polyline.Count; i++ )
			AppendPathSample( polyline[i] );
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

	private List<Vector3> BuildFilletPath( List<Vector3> points )
	{
		var path = new List<Vector3>();
		if ( points.Count < 2 )
			return path;

		if ( points.Count == 2 )
		{
			AppendLineSamples( path, points[0], points[1] );
			return path;
		}

		var cursor = points[0];
		path.Add( cursor );
		var filletRadius = CornerFilletRadius.Clamp( 8f, 512f );
		var arcSamples = CornerArcSamples.Clamp( 3, 32 );

		for ( var i = 1; i < points.Count - 1; i++ )
		{
			var prev = points[i - 1];
			var corner = points[i];
			var next = points[i + 1];

			if ( !TryGetCornerFillet( prev, corner, next, filletRadius, out var arcStart, out var arcEnd, out var arcControl ) )
			{
				AppendLineSamples( path, cursor, corner );
				cursor = corner;
				continue;
			}

			AppendLineSamples( path, cursor, arcStart );
			AppendQuadraticBezierSamples( path, arcStart, arcControl, arcEnd, arcSamples );
			cursor = arcEnd;
		}

		AppendLineSamples( path, cursor, points[^1] );
		return path;
	}

	private bool TryGetCornerFillet(
		Vector3 prev,
		Vector3 corner,
		Vector3 next,
		float radius,
		out Vector3 arcStart,
		out Vector3 arcEnd,
		out Vector3 arcControl )
	{
		arcStart = default;
		arcEnd = default;
		arcControl = corner;

		var inDir = (corner - prev).WithZ( 0f );
		var outDir = (next - corner).WithZ( 0f );
		var inLen = inDir.Length;
		var outLen = outDir.Length;
		if ( inLen < 8f || outLen < 8f )
			return false;

		inDir /= inLen;
		outDir /= outLen;

		var dot = Vector3.Dot( inDir, outDir ).Clamp( -1f, 1f );
		var turnAngle = MathF.Acos( dot );
		if ( turnAngle < 0.12f )
			return false;

		var maxRadius = MathF.Min( inLen, outLen ) * 0.45f;
		var useRadius = MathF.Min( radius, maxRadius );
		if ( useRadius < 8f )
			return false;

		arcStart = corner - inDir * useRadius;
		arcEnd = corner + outDir * useRadius;
		return true;
	}

	private void AppendLineSamples( List<Vector3> path, Vector3 from, Vector3 to )
	{
		if ( Vector3.DistanceBetween( from, to ) <= 0.05f )
			return;

		var samples = StraightSegmentSamples.Clamp( 1, 32 );
		for ( var i = 1; i <= samples; i++ )
		{
			var t = i / (float)samples;
			path.Add( Vector3.Lerp( from, to, t ) );
		}
	}

	private void AppendQuadraticBezierSamples( List<Vector3> path, Vector3 start, Vector3 control, Vector3 end, int samples )
	{
		for ( var i = 1; i <= samples; i++ )
		{
			var t = i / (float)samples;
			var u = 1f - t;
			var point = u * u * start + 2f * u * t * control + t * t * end;
			path.Add( point );
		}
	}

	private void AddPathSample( Vector3 point )
	{
		pathSamplePoints.Add( point );
		pathSampleDistances.Add( 0f );
	}

	private void AppendPathSample( Vector3 point )
	{
		var prev = pathSamplePoints[^1];
		totalPathLength += Vector3.DistanceBetween( prev, point );
		pathSamplePoints.Add( point );
		pathSampleDistances.Add( totalPathLength );
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
