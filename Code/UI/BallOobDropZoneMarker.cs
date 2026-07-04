using System;
using Sandbox;

/// <summary> World ring + billboard stack at the OOB sky-drop anchor. Spawned by <see cref="BallOobDropZoneHud"/>. </summary>
public sealed class BallOobDropZoneMarker : Component
{
	const string DefaultRingMaterialPath = "materials/turfwarspoly/oob_drop_ring.vmat";
	const string FallbackRingMaterialPath = "materials/turfwarspoly/speed_blitz_preview.vmat";
	const string DefaultRingOutlineMaterialPath = "materials/turfwarspoly/black.vmat";

	private BallOobDropZoneHud settings;
	private GameObject outlineGo;
	private ModelRenderer outlineRenderer;
	private Material outlineMaterial;
	private string loadedOutlineMaterialPath;
	private GameObject ringGo;
	private ModelRenderer ringRenderer;
	private Model ringModel;
	private string loadedRingModelPath;
	private Material ringMaterial;
	private string loadedRingMaterialPath;
	private GameObject stackGo;
	private WorldPanel worldPanel;
	private BallOobDropZonePanelRoot panelRoot;
	private float dropAtTime;

	public void Configure( BallOobDropZoneHud hudSettings, Vector3 groundAnchor, float dropAt )
	{
		settings = hudSettings;
		dropAtTime = dropAt;

		GameObject.WorldPosition = groundAnchor;
		EnsureRing();
		EnsureWorldPanel();
		UpdateCountdown();
	}

	protected override void OnUpdate()
	{
		if ( !settings.IsValid() )
			return;

		UpdateCountdown();
		UpdateRingPulse();
		UpdateStackOrientation();

		if ( Time.Now >= dropAtTime )
			GameObject.Destroy();
	}

	void EnsureRing()
	{
		if ( string.IsNullOrWhiteSpace( settings.RingModelPath ) )
		{
			if ( ringGo.IsValid() )
				ringGo.Enabled = false;
			if ( outlineGo.IsValid() )
				outlineGo.Enabled = false;
			return;
		}

		if ( !EnsureRingMaterial() )
			return;

		var modelPath = settings.RingModelPath.Trim();
		if ( !ringModel.IsValid() || loadedRingModelPath != modelPath )
		{
			ringModel = Model.Load( modelPath );
			loadedRingModelPath = modelPath;
		}

		if ( !ringModel.IsValid() )
		{
			if ( ringGo.IsValid() )
				ringGo.Enabled = false;
			if ( outlineGo.IsValid() )
				outlineGo.Enabled = false;
			return;
		}

		var diameter = MathF.Max( 1f, settings.RingDiameter );
		var baseSize = MathF.Max( 1f, settings.RingModelBaseSize );
		var scale = diameter / baseSize;

		EnsureOutlineDisc();
		EnsureFillDisc( scale );

		ApplyRingMaterial( settings.RingBaseAlpha );
		ApplyOutlineMaterial();
	}

	void EnsureOutlineDisc()
	{
		var extra = settings.RingOutlineExtraDiameter;
		if ( extra <= 0f )
		{
			if ( outlineGo.IsValid() )
				outlineGo.Enabled = false;
			return;
		}

		if ( !outlineGo.IsValid() )
		{
			outlineGo = new GameObject( true, "BallOobDropRingOutline" );
			outlineGo.SetParent( GameObject );
			outlineRenderer = outlineGo.Components.Create<ModelRenderer>();
		}

		var baseSize = MathF.Max( 1f, settings.RingModelBaseSize );
		var outlineDiameter = MathF.Max( 1f, settings.RingDiameter + extra );
		var outlineScale = outlineDiameter / baseSize;

		outlineGo.LocalPosition = Vector3.Up * (settings.RingGroundLift - settings.RingOutlineUnderlayLift);
		outlineGo.LocalRotation = Rotation.Identity;
		outlineGo.LocalScale = new Vector3( outlineScale, outlineScale, 1f );
		outlineRenderer.Model = ringModel;
		outlineGo.Enabled = true;
	}

	void EnsureFillDisc( float scale )
	{
		if ( !ringGo.IsValid() )
		{
			ringGo = new GameObject( true, "BallOobDropRing" );
			ringGo.SetParent( GameObject );
			ringRenderer = ringGo.Components.Create<ModelRenderer>();
		}

		ringGo.LocalPosition = Vector3.Up * settings.RingGroundLift;
		ringGo.LocalRotation = Rotation.Identity;
		ringGo.LocalScale = new Vector3( scale, scale, 1f );
		ringRenderer.Model = ringModel;
		ringGo.Enabled = true;
	}

	bool EnsureRingMaterial()
	{
		var materialPath = string.IsNullOrWhiteSpace( settings.RingMaterialPath )
			? DefaultRingMaterialPath
			: settings.RingMaterialPath;

		if ( ringMaterial.IsValid() && loadedRingMaterialPath == materialPath )
			return true;

		ringMaterial = Material.Load( materialPath )?.CreateCopy();
		if ( !ringMaterial.IsValid() && materialPath != FallbackRingMaterialPath )
			ringMaterial = Material.Load( FallbackRingMaterialPath )?.CreateCopy();

		loadedRingMaterialPath = materialPath;
		return ringMaterial.IsValid();
	}

	bool EnsureOutlineMaterial()
	{
		var materialPath = string.IsNullOrWhiteSpace( settings.RingOutlineMaterialPath )
			? DefaultRingOutlineMaterialPath
			: settings.RingOutlineMaterialPath;

		if ( outlineMaterial.IsValid() && loadedOutlineMaterialPath == materialPath )
			return true;

		outlineMaterial = Material.Load( materialPath )?.CreateCopy();
		loadedOutlineMaterialPath = materialPath;
		return outlineMaterial.IsValid();
	}

	void EnsureWorldPanel()
	{
		if ( !stackGo.IsValid() )
		{
			stackGo = new GameObject( true, "BallOobDropStack" );
			stackGo.SetParent( GameObject );
			worldPanel = stackGo.Components.Create<WorldPanel>();
			panelRoot = stackGo.Components.Create<BallOobDropZonePanelRoot>();
		}

		stackGo.LocalPosition = Vector3.Up * settings.StackHeightAboveGround;
		worldPanel.PanelSize = settings.StackPanelSize;
		worldPanel.LookAtCamera = false;
		panelRoot.RefreshAppearance();
		UpdateStackOrientation();
	}

	void UpdateStackOrientation()
	{
		if ( !stackGo.IsValid() )
			return;

		if ( !settings.StackFaceCameraYaw )
		{
			stackGo.LocalRotation = Rotation.FromYaw( settings.StackFixedYawDegrees );
			return;
		}

		var camera = Scene.Camera;
		if ( camera is null )
			return;

		var toCamera = camera.WorldPosition - stackGo.WorldPosition;
		toCamera = toCamera.WithZ( 0f );
		if ( toCamera.Length < 0.001f )
			return;

		stackGo.WorldRotation = Rotation.LookAt( toCamera.Normal, Vector3.Up );
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
		if ( !ringMaterial.IsValid() || !ringRenderer.IsValid() )
			return;

		ringMaterial.Set( "g_vColorTint", settings.RingTint );
		ringMaterial.Set( "g_flOpacityScale", alpha );
		ringRenderer.Tint = Color.White;
		ringRenderer.MaterialOverride = ringMaterial;
	}

	void ApplyOutlineMaterial()
	{
		if ( !outlineRenderer.IsValid() || !outlineGo.IsValid() || !outlineGo.Enabled )
			return;

		if ( !EnsureOutlineMaterial() )
			return;

		outlineMaterial.Set( "g_vColorTint", settings.RingOutlineTint );
		outlineRenderer.Tint = Color.White;
		outlineRenderer.MaterialOverride = outlineMaterial;
	}
}

