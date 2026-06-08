using Sandbox;
using System.Collections.Generic;

/// <summary>
/// Owner-only dotted throw arc + first-impact landing marker while <see cref="BallThrow.IsChargingThrow"/>.
/// </summary>
[Order( 10002 )]
public sealed class ThrowTrajectoryPreview : Component
{
	[Property] public Color ArcDotColor { get; set; } = new( 1f, 0.95f, 0.7f, 0.35f );
	[Property] public Color LandingMarkerColor { get; set; } = new( 1f, 0.9f, 0.5f, 0.55f );
	[Property] public float ArcDotRadius { get; set; } = 3f;
	[Property] public float LandingMarkerRadius { get; set; } = 10f;
	[Property] public float LandingMarkerLift { get; set; } = 1.5f;
	[Property] public int ArcDotStride { get; set; } = 2;
	[Property] public float SimulationStepSeconds { get; set; } = 0.02f;
	[Property] public float MaxSimulationSeconds { get; set; } = 6f;

	private BallThrow ballThrow;
	private readonly List<Vector3> arcPoints = new();

	protected override void OnStart()
	{
		ballThrow = Components.Get<BallThrow>();
	}

	protected override void OnUpdate()
	{
		if ( IsProxy || !Network.IsOwner )
			return;

		ballThrow ??= Components.Get<BallThrow>();
		if ( ballThrow is null || !ballThrow.TryGetThrowPreviewSnapshot( out var snapshot ) )
			return;

		ThrowReleaseMath.ComputeRelease(
			snapshot.ReleasePivotWorldPosition,
			snapshot.ThrowDirection,
			snapshot.ChargeLerp,
			snapshot.ReleaseSettings,
			out var releasePosition,
			out var releaseVelocity );

		if ( !ThrowReleaseMath.TryGetBallFlightParameters( Scene, snapshot.HeldBall, out var flight ) )
			return;

		var hit = ThrowReleaseMath.TrySimulateFirstImpact(
			Scene,
			releasePosition,
			releaseVelocity,
			flight,
			MaxSimulationSeconds,
			SimulationStepSeconds,
			GameObject,
			snapshot.HeldBall,
			arcPoints,
			out var impactPosition,
			out var impactNormal );

		var stride = ArcDotStride.Clamp( 1, 8 );
		for ( var i = 0; i < arcPoints.Count; i += stride )
			DebugOverlay.Sphere( new Sphere( arcPoints[i], ArcDotRadius ), ArcDotColor );

		if ( !hit )
			return;

		// Marker on the surface under the ball center at first contact.
		var groundContact = impactPosition - impactNormal * flight.TraceRadius;
		DebugOverlay.Sphere( new Sphere( groundContact + impactNormal * LandingMarkerLift, LandingMarkerRadius ), LandingMarkerColor );
	}
}
