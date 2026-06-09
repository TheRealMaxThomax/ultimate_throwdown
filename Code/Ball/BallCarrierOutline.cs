using System;
using System.Collections.Generic;
using Sandbox;

/// <summary>
/// Gold <see cref="HighlightOutline"/> on the ball while any player is carrying it.
/// Hidden for the carrier, hidden through walls, colour pulse at all ranges.
/// Optional emissive breathe for non-carrier viewers. Outline ring width scales with distance.
/// Requires <see cref="Highlight"/> on the main camera (see <see cref="EnemyOutlineCameraSetup"/>).
/// </summary>
public sealed class BallCarrierOutline : Component
{
	const string FallbackBallMaterialPath = "materials/turfwarspoly/ball.vmat";

	[Property] public Color GlowColorDim { get; set; } = new Color( 1f, 0.75f, 0.2f, 0.55f );
	[Property] public Color GlowColorBright { get; set; } = new Color( 1f, 0.92f, 0.35f, 1f );
	[Property] public Color GlowInsideColorDim { get; set; } = new Color( 1f, 0.7f, 0.18f, 0.2f );
	[Property] public Color GlowInsideColorBright { get; set; } = new Color( 1f, 0.82f, 0.25f, 0.4f );
	[Property] public float OutlineWidth { get; set; } = 2.5f;
	[Property] public float PulseSeconds { get; set; } = 1.1f;
	[Property] public float WidthReferenceDistance { get; set; } = 280f;
	[Property] public float MinWidthScale { get; set; } = 0.28f;
	[Property] public float MaxWidthScale { get; set; } = 1.1f;
	[Property] public bool EnableEmissivePulse { get; set; } = true;
	[Property] public float EmissiveBrightnessMin { get; set; } = 0.2f;
	[Property] public float EmissiveBrightnessMax { get; set; } = 1f;
	[Property] public float EmissiveScaleMin { get; set; } = 0.95f;
	[Property] public float EmissiveScaleMax { get; set; } = 1.45f;

	private HighlightOutline outline;
	private ModelRenderer ballRenderer;
	private Material originalMaterialOverride;
	private Material carriedPulseMaterial;
	private bool capturedOriginalMaterial;

	protected override void OnStart()
	{
		ballRenderer = Components.Get<ModelRenderer>( FindMode.EverythingInSelfAndDescendants );
		outline = Components.GetOrCreate<HighlightOutline>();
		CaptureOriginalMaterial();
		ApplyBaseStyle();
		outline.Enabled = false;
	}

	protected override void OnDestroy()
	{
		RestoreBallMaterial();
	}

	protected override void OnUpdate()
	{
		if ( !outline.IsValid() )
			return;

		var carrier = FindCarrierGrab( Scene, GameObject );
		if ( !carrier.IsValid() || !ShouldShowForLocalViewer( carrier ) )
		{
			outline.Enabled = false;
			RestoreBallMaterial();
			return;
		}

		outline.Enabled = true;

		var pulse = GetPulse01();
		var distanceScale = GetDistanceWidthScale( GetDistanceToCamera() );
		var ringColor = Color.Lerp( GlowColorDim, GlowColorBright, pulse );
		var insideColor = Color.Lerp( GlowInsideColorDim, GlowInsideColorBright, pulse );

		outline.Width = OutlineWidth * distanceScale;
		outline.Color = ringColor;
		outline.InsideColor = insideColor;
		outline.ObscuredColor = Color.Transparent;
		outline.InsideObscuredColor = Color.Transparent;

		if ( EnableEmissivePulse )
			ApplyEmissivePulse( pulse );
		else
			RestoreBallMaterial();
	}

	void CaptureOriginalMaterial()
	{
		if ( capturedOriginalMaterial || !ballRenderer.IsValid() )
			return;

		originalMaterialOverride = ballRenderer.MaterialOverride;
		capturedOriginalMaterial = true;
	}

	void EnsureCarriedPulseMaterial()
	{
		if ( carriedPulseMaterial.IsValid() )
			return;

		CaptureOriginalMaterial();

		var source = originalMaterialOverride;
		if ( !source.IsValid() )
			source = Material.Load( FallbackBallMaterialPath );

		if ( !source.IsValid() )
			return;

		carriedPulseMaterial = source.CreateCopy( "ball_carrier_pulse" );
	}

	void ApplyEmissivePulse( float pulse )
	{
		if ( !ballRenderer.IsValid() )
			return;

		EnsureCarriedPulseMaterial();
		if ( !carriedPulseMaterial.IsValid() )
			return;

		var brightness = MathX.Lerp( EmissiveBrightnessMin, EmissiveBrightnessMax, pulse );
		var scale = MathX.Lerp( EmissiveScaleMin, EmissiveScaleMax, pulse );
		carriedPulseMaterial.Set( "g_flSelfIllumBrightness", brightness );
		carriedPulseMaterial.Set( "g_flSelfIllumScale", scale );
		ballRenderer.MaterialOverride = carriedPulseMaterial;
	}

	void RestoreBallMaterial()
	{
		if ( !ballRenderer.IsValid() || !capturedOriginalMaterial )
			return;

		ballRenderer.MaterialOverride = originalMaterialOverride;
	}

	float GetPulse01()
	{
		if ( PulseSeconds <= 0.01f )
			return 1f;

		return 0.5f + (0.5f * MathF.Sin( (Time.Now / PulseSeconds) * MathF.PI * 2f ));
	}

	float GetDistanceToCamera()
	{
		var camera = Scene.Camera;
		if ( camera is null )
			return WidthReferenceDistance;

		return Vector3.DistanceBetween( camera.WorldPosition, GameObject.WorldPosition );
	}

	float GetDistanceWidthScale( float distance )
	{
		if ( distance <= 0.01f )
			return MaxWidthScale;

		var reference = MathF.Max( 1f, WidthReferenceDistance );
		return MathX.Clamp( reference / distance, MinWidthScale, MaxWidthScale );
	}

	bool ShouldShowForLocalViewer( BallGrab carrier )
	{
		if ( !carrier.IsValid() || !carrier.IsHolding )
			return false;

		var local = Connection.Local;
		if ( local is null )
			return true;

		var owner = carrier.Network.Owner;
		return owner is null || owner.SteamId != local.SteamId;
	}

	void ApplyBaseStyle()
	{
		if ( !outline.IsValid() )
			return;

		outline.ObscuredColor = Color.Transparent;
		outline.InsideObscuredColor = Color.Transparent;
		outline.Width = OutlineWidth;

		if ( !ballRenderer.IsValid() )
			return;

		outline.OverrideTargets = true;
		outline.Targets ??= new List<Renderer>();
		outline.Targets.Clear();
		outline.Targets.Add( ballRenderer );
	}

	public static BallGrab FindCarrierGrab( Scene scene, GameObject ball )
	{
		if ( scene is null || !ball.IsValid() )
			return null;

		foreach ( var grab in scene.GetAllComponents<BallGrab>() )
		{
			if ( !grab.IsValid() || !grab.IsHolding )
				continue;

			var held = grab.HeldBall;
			if ( held.IsValid() && held == ball )
				return grab;
		}

		return null;
	}

	public static bool IsCarriedByAnyone( Scene scene, GameObject ball )
	{
		return FindCarrierGrab( scene, ball ).IsValid();
	}
}
