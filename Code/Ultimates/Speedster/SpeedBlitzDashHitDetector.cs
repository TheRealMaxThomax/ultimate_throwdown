using Sandbox;

/// <summary>
/// Shared dash hit geometry — corridor filter, contact cylinder, line-of-sight trace.
/// Host and owner-predict both call the same methods; tunables read from <see cref="SpeedsterSpeedBlitzUlt"/>.
/// </summary>
public sealed class SpeedBlitzDashHitDetector : Component
{
	private SpeedsterSpeedBlitzUlt ult;
	private PlayerTackle playerTackle;
	private PlayerTeam playerTeam;
	private PlayerController playerController;
	private PlayerClass playerClass;

	protected override void OnStart()
	{
		ult = Components.Get<SpeedsterSpeedBlitzUlt>();
		playerTackle = Components.Get<PlayerTackle>();
		playerTeam = Components.Get<PlayerTeam>();
		playerController = Components.Get<PlayerController>();
		playerClass = Components.Get<PlayerClass>();
	}

	/// <summary>
	/// Physical contact hit test along the dash movement segment — corridor is a coarse aim filter only;
	/// dasher must touch the victim (3D), stay within vertical tolerance, and have clear line-of-sight.
	/// </summary>
	public bool TryFindHitAlongSegment(
		Vector3 segStartRaw,
		Vector3 segEndRaw,
		Vector3 corridorOriginRaw,
		out PlayerTackle victim )
	{
		victim = null;

		if ( !ult.IsValid() )
			return false;

		var corridorDir = ult.SyncedCommittedDirection.WithZ( 0f );
		if ( corridorDir.Length < 0.001f )
			return false;

		corridorDir = corridorDir.Normal;
		var corridorOrigin = corridorOriginRaw.WithZ( 0f );
		var halfWidth = ult.HitHalfWidth.Clamp( 4f, 200f );
		var maxAlong = ult.DashRange;
		var segStart = segStartRaw;
		var segEnd = segEndRaw;

		PlayerTackle best = null;
		var bestDist = float.MaxValue;

		foreach ( var candidate in Scene.GetAllComponents<PlayerTackle>() )
		{
			if ( !IsValidDashTarget( candidate ) )
				continue;

			if ( !IsDashTargetInCorridor( candidate, corridorOrigin, corridorDir, halfWidth, maxAlong ) )
				continue;

			var victimPos = candidate.WorldPosition;
			if ( !TryGetDashContactPointOnSegment( segStart, segEnd, victimPos, candidate, out var contactPoint ) )
				continue;

			if ( !IsDashHitPathClear( contactPoint, victimPos, candidate ) )
				continue;

			var dist = contactPoint.Distance( victimPos );
			if ( dist < bestDist )
			{
				best = candidate;
				bestDist = dist;
			}
		}

		if ( !best.IsValid() )
			return false;

		victim = best;
		return true;
	}

	/// <summary> Prevents lagged owner samples from sweeping a huge corridor in one host tick. </summary>
	public Vector3 ClampDashSweepEndPosition( Vector3 segStart, Vector3 segEndRaw )
	{
		var flatDelta = (segEndRaw - segStart).WithZ( 0f );
		var maxStep = GetMaxDashSweepStepDistance();
		if ( flatDelta.Length <= maxStep )
			return segEndRaw;

		var clamped = segStart + flatDelta.Normal * maxStep;
		return new Vector3( clamped.x, clamped.y, segEndRaw.z );
	}

	private bool IsDashTargetInCorridor(
		PlayerTackle candidate,
		Vector3 corridorOrigin,
		Vector3 corridorDir,
		float halfWidth,
		float maxAlong )
	{
		var target = candidate.WorldPosition.WithZ( 0f );
		var along = ProjectAlongDashCorridor( corridorOrigin, corridorDir, target );
		var lateral = LateralDistanceToDashCorridor( corridorOrigin, corridorDir, target );
		var targetBodyRadius = GetDashTargetBodyRadius( candidate );

		if ( lateral + targetBodyRadius > halfWidth )
			return false;

		if ( along + targetBodyRadius < 0f || along - targetBodyRadius > maxAlong )
			return false;

		return true;
	}

	private bool TryGetDashContactPointOnSegment(
		Vector3 segStart,
		Vector3 segEnd,
		Vector3 victimPos,
		PlayerTackle candidate,
		out Vector3 contactPoint )
	{
		contactPoint = default;

		var contactDist = GetDashContactDistance( candidate );
		var closest = ClosestPointOnSegment( segStart, segEnd, victimPos );

		// Same cylinder as tackle — horizontal body radii + vertical band, not 3D distance.
		if ( !PlayerTackle.TryValidateContactCylinder( closest, victimPos, contactDist, ult.MaxHitVerticalSeparation ) )
			return false;

		contactPoint = closest;
		return true;
	}

	private float GetDashContactDistance( PlayerTackle candidate )
	{
		return GetDasherBodyRadius() + GetDashTargetBodyRadius( candidate ) + ult.HitStopContactGap.Clamp( 0f, 32f );
	}

	private bool IsDashHitPathClear( Vector3 fromPos, Vector3 toPos, PlayerTackle victim )
	{
		var from = fromPos + Vector3.Up * 32f;
		var to = toPos + Vector3.Up * 32f;
		var dist = from.Distance( to );
		if ( dist <= 0.001f )
			return true;

		var trace = BuildDashHitTrace( from, to, victim?.GameObject ).Run();
		if ( !trace.Hit )
			return true;

		var hitDist = trace.HitPosition.Distance( from );
		const float slop = 16f;
		return hitDist >= dist - slop;
	}

	private SceneTrace BuildDashHitTrace( Vector3 from, Vector3 to, GameObject victimRoot )
	{
		var trace = Scene.Trace.Ray( from, to )
			.WithoutTags( "ragdoll" )
			.IgnoreGameObjectHierarchy( GameObject );

		if ( victimRoot.IsValid() )
			trace = trace.IgnoreGameObjectHierarchy( victimRoot );

		foreach ( var tackle in Scene.GetAllComponents<PlayerTackle>() )
		{
			if ( !tackle.IsValid() || tackle.GameObject == GameObject )
				continue;

			if ( victimRoot.IsValid() && tackle.GameObject == victimRoot )
				continue;

			trace = trace.IgnoreGameObjectHierarchy( tackle.GameObject );
		}

		foreach ( var go in Scene.GetAllObjects( true ) )
		{
			if ( go.IsValid() && go.Name == "main_ball" )
			{
				trace = trace.IgnoreGameObjectHierarchy( go );
				break;
			}
		}

		return trace;
	}

	private static Vector3 ClosestPointOnSegment( Vector3 segStart, Vector3 segEnd, Vector3 point )
	{
		var ab = segEnd - segStart;
		var lenSq = ab.LengthSquared;
		if ( lenSq <= 0.0001f )
			return segStart;

		var t = Vector3.Dot( point - segStart, ab ) / lenSq;
		t = t.Clamp( 0f, 1f );
		return segStart + ab * t;
	}

	private float GetMaxDashSweepStepDistance()
	{
		var tick = Time.Delta.Clamp( 0.008f, 0.05f );
		return (ult.DashSpeed * tick * ult.DashSweepStepMultiplier.Clamp( 1f, 6f )).Clamp( 16f, 160f );
	}

	private float GetDasherBodyRadius()
	{
		if ( playerController.IsValid() && playerController.BodyRadius > 0f )
			return playerController.BodyRadius;

		var classData = playerClass?.CurrentClass;
		if ( classData is not null && classData.CapsuleRadius > 0f )
			return classData.CapsuleRadius;

		return ult.DefaultTargetBodyRadius.Clamp( 1f, 64f );
	}

	private float GetDashTargetBodyRadius( PlayerTackle candidate )
	{
		if ( !candidate.IsValid() )
			return ult.DefaultTargetBodyRadius.Clamp( 1f, 64f );

		var controller = candidate.Components.Get<PlayerController>();
		if ( controller.IsValid() && controller.BodyRadius > 0f )
			return controller.BodyRadius;

		var classData = candidate.Components.Get<PlayerClass>()?.CurrentClass;
		if ( classData is not null && classData.CapsuleRadius > 0f )
			return classData.CapsuleRadius;

		return ult.DefaultTargetBodyRadius.Clamp( 1f, 64f );
	}

	private bool IsValidDashTarget( PlayerTackle candidate )
	{
		if ( !candidate.IsValid() || candidate == playerTackle || candidate.GameObject == GameObject )
			return false;

		if ( candidate.IsTackleImmune || candidate.IsKnockedDown )
			return false;

		if ( candidate.Components.Get<PlayerDodge>() is { IsImmuneToTackle: true } )
			return false;

		if ( candidate.GameObject.Tags.Has( CitizenAvatarLod.PracticeNpcTag ) )
			return true;

		var victimTeam = candidate.Components.Get<PlayerTeam>();
		if ( playerTeam is null || !playerTeam.IsValid() || victimTeam is null || !victimTeam.IsValid() )
			return false;

		if ( !MatchTeamIds.IsValid( playerTeam.TeamId ) || !MatchTeamIds.IsValid( victimTeam.TeamId ) )
			return false;

		return playerTeam.TeamId != victimTeam.TeamId;
	}

	private static float ProjectAlongDashCorridor( Vector3 corridorOrigin, Vector3 corridorDir, Vector3 point )
	{
		return Vector3.Dot( point.WithZ( 0f ) - corridorOrigin, corridorDir );
	}

	private static float LateralDistanceToDashCorridor( Vector3 corridorOrigin, Vector3 corridorDir, Vector3 point )
	{
		var flat = point.WithZ( 0f ) - corridorOrigin;
		var along = Vector3.Dot( flat, corridorDir );
		var closest = corridorOrigin + corridorDir * along;
		return (point.WithZ( 0f ) - closest).Length;
	}
}
