using Sandbox;
using System;
using System.Collections.Generic;

/// <summary>
/// Shared throw release + first-arc simulation used by <see cref="BallThrow"/> and <see cref="ThrowTrajectoryPreview"/>.
/// </summary>
public static class ThrowReleaseMath
{
	public readonly struct ReleaseSettings
	{
		public float ThrowForce { get; init; }
		public float ThrowUpForce { get; init; }
		public float ThrowStartOffset { get; init; }
		public float MinThrowForceMultiplier { get; init; }
		public float MinThrowUpForceMultiplier { get; init; }
		public float ClassThrowPower { get; init; }
	}

	/// <summary> Scene + ball rigidbody values used to match host physics on the first arc. </summary>
	public readonly struct BallFlightParameters
	{
		public Vector3 Gravity { get; init; }
		public float LinearDamping { get; init; }
		public float TraceRadius { get; init; }
	}

	public static bool TryGetBallFlightParameters( Scene scene, GameObject ball, out BallFlightParameters parameters )
	{
		parameters = default;
		if ( scene is null || !ball.IsValid() )
			return false;

		var body = ball.Components.Get<Rigidbody>( FindMode.EverythingInSelfAndDescendants );
		var gravity = Vector3.Zero;
		if ( body.IsValid() && body.Gravity )
			gravity = scene.PhysicsWorld.Gravity * body.GravityScale;

		parameters = new BallFlightParameters
		{
			Gravity = gravity,
			LinearDamping = body.IsValid() ? body.LinearDamping : 0f,
			TraceRadius = GetBallTraceRadius( ball )
		};
		return true;
	}

	private static float GetBallTraceRadius( GameObject ball )
	{
		var maxRadius = 0f;
		var scale = ball.WorldTransform.Scale;
		var uniformScale = MathF.Max( scale.x, MathF.Max( scale.y, scale.z ) );

		foreach ( var sphere in ball.Components.GetAll<SphereCollider>( FindMode.EverythingInSelfAndDescendants ) )
		{
			if ( !sphere.IsValid() )
				continue;

			maxRadius = MathF.Max( maxRadius, sphere.Radius * uniformScale );
		}

		return maxRadius > 0f ? maxRadius : GameNetworkManager.GetBallGroundClearance( ball );
	}

	public static float GetChargeLerp(
		float chargeStartedAt,
		float minThrowChargeTime,
		float maxThrowChargeTime,
		float throwChargeSpeedScale )
	{
		var chargeScale = MathF.Max( 0.05f, throwChargeSpeedScale );
		var chargeHeldSeconds = (Time.Now - chargeStartedAt) * chargeScale;
		var clampedChargeSeconds = chargeHeldSeconds.Clamp( minThrowChargeTime, maxThrowChargeTime );
		return maxThrowChargeTime <= minThrowChargeTime
			? 1f
			: (clampedChargeSeconds - minThrowChargeTime) / (maxThrowChargeTime - minThrowChargeTime);
	}

	public static void ComputeRelease(
		Vector3 releasePivotWorldPosition,
		Vector3 throwDirectionWorld,
		float chargeLerp,
		ReleaseSettings settings,
		out Vector3 releasePosition,
		out Vector3 releaseVelocity )
	{
		chargeLerp = chargeLerp.Clamp( 0f, 1f );
		var throwDirection = throwDirectionWorld.Length > 0.001f
			? throwDirectionWorld.Normal
			: Vector3.Forward;
		var throwForceMultiplier = settings.MinThrowForceMultiplier.LerpTo( 1f, chargeLerp );
		var throwUpForceMultiplier = settings.MinThrowUpForceMultiplier.LerpTo( 1f, chargeLerp );
		var classPower = settings.ClassThrowPower;

		releasePosition = releasePivotWorldPosition + (throwDirection * settings.ThrowStartOffset) + (Vector3.Up * 10f);
		releaseVelocity = (throwDirection * settings.ThrowForce * throwForceMultiplier * classPower)
			+ (Vector3.Up * settings.ThrowUpForce * throwUpForceMultiplier * classPower);
	}

	/// <summary>
	/// Steps a gravity arc until the first world hit or <paramref name="maxSimTime"/> elapses.
	/// </summary>
	public static bool TrySimulateFirstImpact(
		Scene scene,
		Vector3 startPosition,
		Vector3 startVelocity,
		BallFlightParameters flight,
		float maxSimTime,
		float stepSeconds,
		GameObject ignoreHierarchy,
		GameObject alsoIgnoreHierarchy,
		List<Vector3> arcPoints,
		out Vector3 impactPosition,
		out Vector3 impactNormal )
	{
		arcPoints.Clear();
		arcPoints.Add( startPosition );

		var position = startPosition;
		var velocity = startVelocity;
		var elapsed = 0f;
		impactPosition = startPosition;
		impactNormal = Vector3.Up;

		stepSeconds = MathF.Max( 0.01f, stepSeconds );
		maxSimTime = MathF.Max( stepSeconds, maxSimTime );

		while ( elapsed < maxSimTime )
		{
			var dt = MathF.Min( stepSeconds, maxSimTime - elapsed );
			var nextVelocity = velocity + (flight.Gravity * dt) - (velocity * (flight.LinearDamping * dt));
			var nextPosition = position + (velocity + nextVelocity) * 0.5f * dt;

			var trace = flight.TraceRadius > 0f
				? scene.Trace.Sphere( flight.TraceRadius, position, nextPosition )
				: scene.Trace.Ray( position, nextPosition );
			if ( ignoreHierarchy.IsValid() )
				trace = trace.IgnoreGameObjectHierarchy( ignoreHierarchy );
			if ( alsoIgnoreHierarchy.IsValid() )
				trace = trace.IgnoreGameObjectHierarchy( alsoIgnoreHierarchy );

			var tr = trace.Run();
			if ( tr.Hit )
			{
				// Ball center at contact — not the surface hit point (which sits ahead on angled approaches).
				var centerAtHit = tr.Fraction > 0f
					? position.LerpTo( nextPosition, tr.Fraction )
					: nextPosition;
				arcPoints.Add( centerAtHit );
				impactPosition = centerAtHit;
				impactNormal = tr.Normal;
				return true;
			}

			arcPoints.Add( nextPosition );
			position = nextPosition;
			velocity = nextVelocity;
			elapsed += dt;
		}

		impactPosition = position;
		return false;
	}
}
