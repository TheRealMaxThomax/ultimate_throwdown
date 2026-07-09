using System.Collections.Generic;
using Sandbox;

/// <summary>
/// Owner-only 3-ring ground preview while <see cref="JuggernautQuakeSlamUlt.IsAiming"/>.
/// </summary>
[Order( 10014 )]
public sealed class QuakeSlamAimPreview : Component
{
	const string DefaultRingMaterialPath = "materials/turfwarspoly/speed_blitz_preview.vmat";
	const string DefaultPlaneModelPath = "models/dev/plane.vmdl";

	static readonly Color InnerTint = new( 1f, 0.35f, 0.2f, 0.55f );
	static readonly Color MidTint = new( 1f, 0.55f, 0.15f, 0.45f );
	static readonly Color OuterTint = new( 1f, 0.75f, 0.1f, 0.35f );

	private sealed class RingSlot
	{
		public GameObject Root;
		public ModelRenderer Renderer;
	}

	private static Material ringMaterialBase;
	private static Model planeModel;

	[Property, Group( "Meshes" )] public string PlaneModelPath { get; set; } = DefaultPlaneModelPath;
	[Property, Group( "Meshes" )] public float PlaneBaseDiameter { get; set; } = 100f;
	[Property, Group( "Materials" )] public string RingMaterialPath { get; set; } = DefaultRingMaterialPath;
	[Property, Group( "Layout" )] public float RingLift { get; set; } = 2.5f;

	private JuggernautQuakeSlamUlt ult;
	private Material ringMaterial;
	private readonly RingSlot[] rings = new RingSlot[3];

	protected override void OnStart()
	{
		ult = Components.Get<JuggernautQuakeSlamUlt>();
		EnsureRingSlots();
		SetVisible( false );
	}

	protected override void OnDestroy()
	{
		ClearPreviewObjects();
	}

	protected override void OnDisabled()
	{
		SetVisible( false );
	}

	protected override void OnUpdate()
	{
		if ( IsProxy || !Network.IsOwner )
		{
			SetVisible( false );
			return;
		}

		ult ??= Components.Get<JuggernautQuakeSlamUlt>();
		if ( ult is null || !ult.IsAiming )
		{
			SetVisible( false );
			return;
		}

		if ( !EnsureAssets() )
		{
			SetVisible( false );
			return;
		}

		ult.GetAimPreviewParams( out var origin, out var innerRadius, out var midRadius, out var outerRadius );
		UpdateRing( 0, origin, innerRadius, InnerTint );
		UpdateRing( 1, origin, midRadius, MidTint );
		UpdateRing( 2, origin, outerRadius, OuterTint );
		SetVisible( true );
	}

	private void EnsureRingSlots()
	{
		for ( var i = 0; i < rings.Length; i++ )
		{
			if ( rings[i]?.Root.IsValid() == true )
				continue;

			var root = new GameObject( true, $"QuakeSlamPreviewRing_{i}" );
			root.Parent = GameObject;
			root.Tags.Add( "quake_slam_preview" );
			var renderer = root.Components.Create<ModelRenderer>();
			rings[i] = new RingSlot { Root = root, Renderer = renderer };
		}
	}

	private bool EnsureAssets()
	{
		if ( planeModel is null || planeModel.ResourcePath != PlaneModelPath )
			planeModel = Model.Load( PlaneModelPath );

		if ( ringMaterialBase is null || ringMaterialBase.ResourcePath != RingMaterialPath )
			ringMaterialBase = Material.Load( RingMaterialPath );

		if ( planeModel is null || ringMaterialBase is null )
			return false;

		ringMaterial ??= ringMaterialBase.CreateCopy( "quake_slam_preview_ring" );
		return true;
	}

	private void UpdateRing( int index, Vector3 origin, float radius, Color tint )
	{
		var slot = rings[index];
		if ( slot?.Root is null || slot.Renderer is null )
			return;

		var diameter = MathF.Max( 1f, radius ) * 2f;
		var scale = diameter / MathF.Max( 1f, PlaneBaseDiameter );
		slot.Root.WorldPosition = new Vector3( origin.x, origin.y, origin.z + RingLift );
		slot.Root.WorldRotation = Rotation.From( 0f, 0f, 0f );
		slot.Root.WorldScale = new Vector3( scale, scale, 1f );
		slot.Renderer.Model = planeModel;
		ApplyMaterial( slot.Renderer, ringMaterial, tint );
		slot.Renderer.RenderType = ModelRenderer.ShadowRenderType.Off;
		slot.Root.Enabled = true;

		if ( slot.Renderer.SceneObject.IsValid() )
		{
			slot.Renderer.SceneObject.Flags.CastShadows = false;
			slot.Renderer.SceneObject.Flags.IsTranslucent = true;
		}
	}

	private static void ApplyMaterial( ModelRenderer renderer, Material material, Color tint )
	{
		if ( renderer is null || material is null )
			return;

		material.Set( "g_vColorTint", tint );
		renderer.MaterialOverride = material;
		renderer.Tint = Color.White;
	}

	private void SetVisible( bool visible )
	{
		foreach ( var slot in rings )
		{
			if ( slot?.Root.IsValid() == true )
				slot.Root.Enabled = visible;
		}
	}

	private void ClearPreviewObjects()
	{
		foreach ( var slot in rings )
		{
			slot?.Root?.Destroy();
		}

		for ( var i = 0; i < rings.Length; i++ )
			rings[i] = null;
	}
}
