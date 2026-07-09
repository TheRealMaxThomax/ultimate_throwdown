using Sandbox;

/// <summary>
/// Owner-only 3-ring ground preview while <see cref="JuggernautQuakeSlamUlt.IsAiming"/>.
/// Inner = filled disc; mid/outer = procedural annulus bands (no nested alpha overlap).
/// </summary>
[Order( 10014 )]
public sealed class QuakeSlamAimPreview : Component
{
	const string DefaultDiscModelPath = "models/main/oob_drop_ring.vmdl";
	const string DefaultRingMaterialPath = "materials/oob_drop_ring.vmat";
	const string FallbackRingMaterialPath = "materials/turfwarspoly/speed_blitz_preview.vmat";

	private sealed class RingSlot
	{
		public GameObject Root;
		public ModelRenderer Renderer;
		public Material Material;
		public string LoadedMaterialPath;
	}

	[Property, Group( "Ring mesh" )] public string RingModelPath { get; set; } = DefaultDiscModelPath;
	/// <summary> Fallback mesh diameter when <see cref="Model.Bounds"/> are unavailable — prefer auto bounds. </summary>
	[Property, Group( "Ring mesh" )] public float RingModelBaseSize { get; set; } = 7.5f;
	[Property, Group( "Ring mesh" )] public string RingMaterialPath { get; set; } = DefaultRingMaterialPath;
	[Property, Group( "Ring mesh" )] public float RingGroundLift { get; set; } = 1.5f;
	/// <summary> Preview-only — tucks band inner edges under neighbors to hide sub-unit seams. </summary>
	[Property, Group( "Ring mesh" )] public float RingSeamOverlap { get; set; } = 2.5f;
	[Property, Group( "Ring mesh" )] public int AnnulusSegmentCount { get; set; } = 64;
	[Property, Group( "Ring mesh" )] public Color InnerRingTint { get; set; } = new( 1f, 0.35f, 0.2f, 1f );
	[Property, Group( "Ring mesh" )] public Color MidRingTint { get; set; } = new( 1f, 0.55f, 0.15f, 1f );
	[Property, Group( "Ring mesh" )] public Color OuterRingTint { get; set; } = new( 1f, 0.75f, 0.1f, 1f );
	[Property, Group( "Ring mesh" )] public float InnerRingAlpha { get; set; } = 0.55f;
	[Property, Group( "Ring mesh" )] public float MidRingAlpha { get; set; } = 0.45f;
	[Property, Group( "Ring mesh" )] public float OuterRingAlpha { get; set; } = 0.35f;

	private JuggernautQuakeSlamUlt ult;
	private Model discModel;
	private string loadedDiscModelPath;
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

		ult.GetAimPreviewParams( out var origin, out var innerRadius, out var midRadius, out var outerRadius );

		var seam = MathF.Max( 0f, RingSeamOverlap );
		UpdateAnnulusBand( 2, origin, midRadius - seam, outerRadius, OuterRingTint, OuterRingAlpha );
		UpdateAnnulusBand( 1, origin, innerRadius - seam, midRadius, MidRingTint, MidRingAlpha );
		UpdateInnerDisc( 0, origin, innerRadius, InnerRingTint, InnerRingAlpha );
		SetVisible( true );
	}

	private void EnsureRingSlots()
	{
		for ( var i = 0; i < rings.Length; i++ )
		{
			if ( rings[i]?.Root.IsValid() == true )
				continue;

			var root = Scene.CreateObject();
			root.Name = $"QuakeSlamPreviewRing_{i}";
			root.NetworkMode = NetworkMode.Never;
			root.Tags.Add( "quake_slam_preview" );

			var renderer = root.Components.Create<ModelRenderer>();
			renderer.RenderOptions.Overlay = false;
			if ( renderer.SceneObject.IsValid() )
			{
				renderer.SceneObject.Flags.CastShadows = false;
				renderer.SceneObject.Flags.IsTranslucent = true;
			}

			rings[i] = new RingSlot { Root = root, Renderer = renderer };
		}
	}

	private bool EnsureDiscModel()
	{
		var modelPath = string.IsNullOrWhiteSpace( RingModelPath ) ? DefaultDiscModelPath : RingModelPath.Trim();
		if ( !discModel.IsValid() || loadedDiscModelPath != modelPath )
		{
			discModel = Model.Load( modelPath );
			loadedDiscModelPath = modelPath;
		}

		return discModel.IsValid();
	}

	private bool EnsureRingMaterial( RingSlot slot )
	{
		var materialPath = string.IsNullOrWhiteSpace( RingMaterialPath ) ? DefaultRingMaterialPath : RingMaterialPath.Trim();
		if ( slot.Material.IsValid() && slot.LoadedMaterialPath == materialPath )
			return true;

		slot.Material = Material.Load( materialPath )?.CreateCopy( $"quake_slam_preview_ring_{slot.Root.Name}" );
		if ( !slot.Material.IsValid() && materialPath != FallbackRingMaterialPath )
			slot.Material = Material.Load( FallbackRingMaterialPath )?.CreateCopy( $"quake_slam_preview_ring_{slot.Root.Name}_fb" );

		slot.LoadedMaterialPath = materialPath;
		return slot.Material.IsValid();
	}

	private float ResolveDiscMeshDiameter( Model model )
	{
		if ( model.IsValid() )
		{
			var size = model.Bounds.Size;
			var xyDiameter = MathF.Max( size.x, size.y );
			if ( xyDiameter > 0.01f )
				return xyDiameter;
		}

		return MathF.Max( 1f, RingModelBaseSize );
	}

	private void UpdateInnerDisc( int index, Vector3 origin, float radius, Color tint, float alpha )
	{
		var slot = rings[index];
		if ( slot?.Root is null || slot.Renderer is null || !EnsureDiscModel() || !EnsureRingMaterial( slot ) )
		{
			if ( slot?.Root.IsValid() == true )
				slot.Root.Enabled = false;
			return;
		}

		var targetDiameter = MathF.Max( 1f, radius ) * 2f;
		var meshDiameter = ResolveDiscMeshDiameter( discModel );
		var scale = targetDiameter / meshDiameter;

		ApplySlotTransform( slot, origin, scale );
		slot.Renderer.Model = discModel;
		ApplyRingMaterial( slot.Renderer, slot.Material, tint, alpha );
		slot.Root.Enabled = true;
	}

	private void UpdateAnnulusBand( int index, Vector3 origin, float innerRadius, float outerRadius, Color tint, float alpha )
	{
		var slot = rings[index];
		if ( slot?.Root is null || slot.Renderer is null || !EnsureRingMaterial( slot ) )
		{
			if ( slot?.Root.IsValid() == true )
				slot.Root.Enabled = false;
			return;
		}

		var annulusModel = QuakeSlamPreviewAnnulusMesh.GetAnnulusModel( innerRadius, outerRadius, AnnulusSegmentCount );
		if ( !annulusModel.IsValid() )
		{
			slot.Root.Enabled = false;
			return;
		}

		ApplySlotTransform( slot, origin, 1f );
		slot.Renderer.Model = annulusModel;
		ApplyRingMaterial( slot.Renderer, slot.Material, tint, alpha );
		slot.Root.Enabled = true;
	}

	private void ApplySlotTransform( RingSlot slot, Vector3 origin, float uniformScale )
	{
		slot.Root.WorldPosition = new Vector3( origin.x, origin.y, origin.z + RingGroundLift );
		slot.Root.WorldRotation = Rotation.Identity;
		slot.Root.WorldScale = new Vector3( uniformScale, uniformScale, 1f );
		slot.Renderer.RenderType = ModelRenderer.ShadowRenderType.Off;

		if ( slot.Renderer.SceneObject.IsValid() )
		{
			slot.Renderer.SceneObject.Flags.CastShadows = false;
			slot.Renderer.SceneObject.Flags.IsTranslucent = true;
			slot.Renderer.SceneObject.Batchable = false;
		}
	}

	static void ApplyRingMaterial( ModelRenderer renderer, Material material, Color tint, float alpha )
	{
		if ( !material.IsValid() )
			return;

		material.Set( "g_vColorTint", tint );
		material.Set( "g_flOpacityScale", alpha.Clamp( 0.05f, 1f ) );
		renderer.Tint = Color.White;
		renderer.MaterialOverride = material;
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
