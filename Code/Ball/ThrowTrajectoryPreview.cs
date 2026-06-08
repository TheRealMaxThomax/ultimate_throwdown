using Sandbox;
using System;
using System.Collections.Generic;

/// <summary>
/// Owner-only dashed throw arc + first-impact landing marker while <see cref="BallThrow.IsChargingThrow"/>.
/// </summary>
[Order( 10002 )]
public sealed class ThrowTrajectoryPreview : Component
{
	const string LandingSphereModelPath = "models/dev/sphere.vmdl";
	const string FallbackBallMaterialPath = "materials/turfwarspoly/ball.vmat";

	[Property] public Color ArcDashColor { get; set; } = new( 1f, 1f, 1f, 0.4f );
	[Property] public float LandingMarkerAlpha { get; set; } = 0.4f;
	[Property] public float ArcDashLength { get; set; } = 14f;
	[Property] public float ArcGapLength { get; set; } = 10f;
	[Property] public float ArcDashScrollSpeed { get; set; } = 120f;
	[Property] public float LandingMarkerRadius { get; set; } = 10f;
	[Property] public float LandingMarkerLift { get; set; } = 1.5f;
	[Property] public float SimulationStepSeconds { get; set; } = 0.02f;
	[Property] public float MaxSimulationSeconds { get; set; } = 6f;

	private static Model landingSphereModel;
	private static Material fallbackBallMaterial;

	private BallThrow ballThrow;
	private GameObject landingMarkerGo;
	private ModelRenderer landingMarkerRenderer;
	private readonly List<Vector3> arcPoints = new();

	protected override void OnStart()
	{
		ballThrow = Components.Get<BallThrow>();
	}

	protected override void OnDestroy()
	{
		if ( landingMarkerGo.IsValid() )
			landingMarkerGo.Destroy();
	}

	protected override void OnUpdate()
	{
		if ( IsProxy || !Network.IsOwner )
		{
			SetLandingMarkerVisible( false );
			return;
		}

		ballThrow ??= Components.Get<BallThrow>();
		if ( ballThrow is null || !ballThrow.TryGetThrowPreviewSnapshot( out var snapshot ) )
		{
			SetLandingMarkerVisible( false );
			return;
		}

		ThrowReleaseMath.ComputeRelease(
			snapshot.ReleasePivotWorldPosition,
			snapshot.ThrowDirection,
			snapshot.ChargeLerp,
			snapshot.ReleaseSettings,
			out var releasePosition,
			out var releaseVelocity );

		if ( !ThrowReleaseMath.TryGetBallFlightParameters( Scene, snapshot.HeldBall, out var flight ) )
		{
			SetLandingMarkerVisible( false );
			return;
		}

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

		var scrollPhase = ArcDashScrollSpeed > 0f ? Time.Now * ArcDashScrollSpeed : 0f;
		DrawDashedPolyline(
			arcPoints,
			ArcDashLength,
			ArcGapLength,
			scrollPhase,
			SimulationStepSeconds,
			ArcDashColor );

		if ( !hit )
		{
			SetLandingMarkerVisible( false );
			return;
		}

		// Solid marker on the surface under the ball center at first contact.
		var groundContact = impactPosition - impactNormal * flight.TraceRadius;
		UpdateLandingMarker(
			groundContact + impactNormal * LandingMarkerLift,
			LandingMarkerRadius,
			snapshot.HeldBall );
	}

	void EnsureLandingMarker()
	{
		if ( landingMarkerGo.IsValid() )
			return;

		landingSphereModel ??= Model.Load( LandingSphereModelPath );

		landingMarkerGo = Scene.CreateObject();
		landingMarkerGo.Name = "ThrowLandingMarker";

		landingMarkerRenderer = landingMarkerGo.Components.Create<ModelRenderer>();
		landingMarkerRenderer.Model = landingSphereModel;
		landingMarkerRenderer.RenderOptions.Overlay = true;

		if ( landingMarkerRenderer.SceneObject.IsValid() )
		{
			landingMarkerRenderer.SceneObject.Flags.CastShadows = false;
			landingMarkerRenderer.SceneObject.Flags.IsTranslucent = true;
		}
	}

	void UpdateLandingMarker( Vector3 position, float radius, GameObject ball )
	{
		EnsureLandingMarker();

		var baseRadius = GetLandingSphereBaseRadius();
		var uniformScale = baseRadius > 0.0001f ? radius / baseRadius : 1f;
		var markerStyle = GetBallLandingMarkerStyle( ball );

		landingMarkerGo.WorldPosition = position;
		landingMarkerGo.WorldScale = uniformScale;
		landingMarkerRenderer.MaterialOverride = markerStyle.Material;
		landingMarkerRenderer.Tint = markerStyle.Tint.WithAlpha( LandingMarkerAlpha );
		landingMarkerGo.Enabled = true;
	}

	static (Color Tint, Material Material) GetBallLandingMarkerStyle( GameObject ball )
	{
		fallbackBallMaterial ??= Material.Load( FallbackBallMaterialPath );

		if ( ball.IsValid() )
		{
			var ballRenderer = ball.Components.Get<ModelRenderer>( FindMode.EverythingInSelfAndDescendants );
			if ( ballRenderer.IsValid() )
			{
				return (
					ballRenderer.Tint,
					ballRenderer.MaterialOverride.IsValid() ? ballRenderer.MaterialOverride : fallbackBallMaterial );
			}
		}

		return ( Color.White, fallbackBallMaterial );
	}

	void SetLandingMarkerVisible( bool visible )
	{
		if ( landingMarkerGo.IsValid() )
			landingMarkerGo.Enabled = visible;
	}

	static float GetLandingSphereBaseRadius()
	{
		landingSphereModel ??= Model.Load( LandingSphereModelPath );
		if ( !landingSphereModel.IsValid() )
			return 32f;

		var size = landingSphereModel.Bounds.Size;
		return MathF.Max( size.x, MathF.Max( size.y, size.z ) ) * 0.5f;
	}

	void DrawDashedPolyline(
		IReadOnlyList<Vector3> points,
		float dashLength,
		float gapLength,
		float scrollPhase,
		float stepSeconds,
		Color color )
	{
		if ( points.Count < 2 || dashLength <= 0f )
			return;

		var patternLength = dashLength + MathF.Max( 0f, gapLength );
		if ( patternLength <= 0f )
			return;

		stepSeconds = MathF.Max( 0.01f, stepSeconds );
		var totalFlightTime = (points.Count - 1) * stepSeconds;
		var spacingScale = ComputeArcLength( points ) / totalFlightTime;

		for ( var i = 1; i < points.Count; i++ )
		{
			var from = points[i - 1];
			var to = points[i];
			var delta = to - from;
			var length = delta.Length;

			if ( length <= 0.0001f )
				continue;

			var direction = delta / length;
			var segmentStartFlightTime = (i - 1) * stepSeconds;
			var traveled = 0f;

			while ( traveled < length )
			{
				var segmentT = traveled / length;
				var flightTime = segmentStartFlightTime + (segmentT * stepSeconds);
				var local = Mod( (flightTime * spacingScale) - scrollPhase, patternLength );
				var remainingInPattern = local < dashLength
					? dashLength - local
					: patternLength - local;

				var step = MathF.Min( remainingInPattern, length - traveled );

				if ( local < dashLength )
				{
					var dashStart = from + direction * traveled;
					var dashEnd = from + direction * (traveled + step);
					DebugOverlay.Line( dashStart, dashEnd, color, overlay: true );
				}

				traveled += step;
			}
		}
	}

	static float ComputeArcLength( IReadOnlyList<Vector3> points )
	{
		var length = 0f;

		for ( var i = 1; i < points.Count; i++ )
			length += (points[i] - points[i - 1]).Length;

		return length;
	}

	static float Mod( float value, float length )
	{
		if ( length <= 0f )
			return 0f;

		var result = value % length;
		return result < 0f ? result + length : result;
	}
}
