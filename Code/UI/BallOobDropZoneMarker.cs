using System;
using Sandbox;

/// <summary> World ring + billboard stack at the OOB sky-drop anchor. Spawned by <see cref="BallOobDropZoneHud"/>. </summary>
public sealed class BallOobDropZoneMarker : Component
{
	const string DefaultRingModelPath = "models/dev/plane.vmdl";
	const string DefaultRingMaterialPath = "materials/turfwarspoly/speed_blitz_preview.vmat";

	private BallOobDropZoneHud settings;
	private GameObject ringGo;
	private ModelRenderer ringRenderer;
	private Material ringMaterial;
	private string loadedRingMaterialPath;
	private WorldPanel worldPanel;
	private BallOobDropZonePanelRoot panelRoot;
	private float dropAtTime;

	public void Configure( BallOobDropZoneHud hudSettings, Vector3 groundAnchor, float dropAt )
	{
		settings = hudSettings;
		dropAtTime = dropAt;

		EnsureRing( groundAnchor );
		EnsureWorldPanel( groundAnchor );
		UpdateCountdown();
	}

	protected override void OnUpdate()
	{
		if ( !settings.IsValid() )
			return;

		UpdateCountdown();
		UpdateRingPulse();

		if ( Time.Now >= dropAtTime )
			GameObject.Destroy();
	}

	void EnsureRing( Vector3 groundAnchor )
	{
		if ( !ringGo.IsValid() )
		{
			ringGo = new GameObject( true, "BallOobDropRing" );
			ringGo.SetParent( GameObject );
			ringRenderer = ringGo.Components.Create<ModelRenderer>();
		}

		var modelPath = string.IsNullOrWhiteSpace( settings.RingModelPath ) ? DefaultRingModelPath : settings.RingModelPath;
		var model = Model.Load( modelPath );
		if ( !model.IsValid() )
			return;

		var materialPath = string.IsNullOrWhiteSpace( settings.RingMaterialPath ) ? DefaultRingMaterialPath : settings.RingMaterialPath;
		if ( !ringMaterial.IsValid() || loadedRingMaterialPath != materialPath )
		{
			ringMaterial = Material.Load( materialPath )?.CreateCopy();
			loadedRingMaterialPath = materialPath;
		}

		ringGo.WorldPosition = groundAnchor + Vector3.Up * settings.RingGroundLift;
		ringGo.WorldRotation = Rotation.From( 90f, 0f, 0f );
		var diameter = settings.RingDiameter;
		var baseSize = MathF.Max( 1f, settings.RingPlaneBaseSize );
		ringGo.WorldScale = new Vector3( diameter / baseSize, diameter / baseSize, 1f );

		ringRenderer.Model = model;
		ApplyRingMaterial( settings.RingBaseAlpha );
		ringGo.Enabled = true;
	}

	void EnsureWorldPanel( Vector3 groundAnchor )
	{
		if ( !worldPanel.IsValid() )
		{
			worldPanel = Components.Create<WorldPanel>();
			panelRoot = Components.GetOrCreate<BallOobDropZonePanelRoot>();
		}

		worldPanel.PanelSize = settings.StackPanelSize;
		worldPanel.LookAtCamera = true;
		GameObject.WorldPosition = groundAnchor + Vector3.Up * settings.StackHeightAboveGround;
		panelRoot.RefreshAppearance();
	}

	void UpdateCountdown()
	{
		var seconds = (int)MathF.Ceiling( MathF.Max( 0f, dropAtTime - Time.Now ) );
		panelRoot?.SetCountdownSeconds( seconds );
	}

	void UpdateRingPulse()
	{
		if ( !ringMaterial.IsValid() || !ringRenderer.IsValid() )
			return;

		var pulse = 0.5f + 0.5f * MathF.Sin( Time.Now * settings.RingPulseSpeed );
		var alpha = MathX.Lerp( settings.RingAlphaMin, settings.RingAlphaMax, pulse );
		ApplyRingMaterial( alpha );
	}

	void ApplyRingMaterial( float alpha )
	{
		if ( !ringMaterial.IsValid() )
			return;

		ringMaterial.Set( "g_vColorTint", settings.RingTint );
		ringMaterial.Set( "g_flOpacityScale", alpha );
		ringRenderer.Tint = Color.White;
		ringRenderer.MaterialOverride = ringMaterial;
	}
}
