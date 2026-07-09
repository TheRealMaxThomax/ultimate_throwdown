using Sandbox;

/// <summary>
/// Owner-only 3-ring ground preview while <see cref="JuggernautQuakeSlamUlt.IsAiming"/>.
/// Nested filled discs (v1). Later: true annulus bands — see SESSION_NOTES Open decisions.
/// </summary>
[Order( 10014 )]
public sealed class QuakeSlamAimPreview : Component
{
	const string DefaultRingModelPath = "models/main/oob_drop_ring.vmdl";
	const string DefaultRingMaterialPath = "materials/oob_drop_ring.vmat";
	const string FallbackRingMaterialPath = "materials/turfwarspoly/speed_blitz_preview.vmat";

	private sealed class RingSlot
	{
		public GameObject Root;
		public ModelRenderer Renderer;
		public Material Material;
		public string LoadedMaterialPath;
	}

	[Property, Group( "Ring mesh" )] public string RingModelPath { get; set; } = DefaultRingModelPath;
	/// <summary> Fallback mesh diameter when <see cref="Model.Bounds"/> are unavailable — prefer auto bounds. </summary>
	[Property, Group( "Ring mesh" )] public float RingModelBaseSize { get; set; } = 7.5f;
	[Property, Group( "Ring mesh" )] public string RingMaterialPath { get; set; } = DefaultRingMaterialPath;
	[Property, Group( "Ring mesh" )] public float RingGroundLift { get; set; } = 1.5f;
	[Property, Group( "Ring mesh" )] public Color InnerRingTint { get; set; } = new( 1f, 0.35f, 0.2f, 1f );
	[Property, Group( "Ring mesh" )] public Color MidRingTint { get; set; } = new( 1f, 0.55f, 0.15f, 1f );
	[Property, Group( "Ring mesh" )] public Color OuterRingTint { get; set; } = new( 1f, 0.75f, 0.1f, 1f );
	[Property, Group( "Ring mesh" )] public float InnerRingAlpha { get; set; } = 0.55f;
	[Property, Group( "Ring mesh" )] public float MidRingAlpha { get; set; } = 0.45f;
	[Property, Group( "Ring mesh" )] public float OuterRingAlpha { get; set; } = 0.35f;

	private JuggernautQuakeSlamUlt ult;
	private Model ringModel;
	private string loadedRingModelPath;
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

		if ( !EnsureRingModel() )
		{
			SetVisible( false );
			return;
		}

		ult.GetAimPreviewParams( out var origin, out var innerRadius, out var midRadius, out var outerRadius );
		UpdateRing( 2, origin, outerRadius, OuterRingTint, OuterRingAlpha );
		UpdateRing( 1, origin, midRadius, MidRingTint, MidRingAlpha );
		UpdateRing( 0, origin, innerRadius, InnerRingTint, InnerRingAlpha );
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

	private bool EnsureRingModel()
	{
		var modelPath = string.IsNullOrWhiteSpace( RingModelPath ) ? DefaultRingModelPath : RingModelPath.Trim();
		if ( !ringModel.IsValid() || loadedRingModelPath != modelPath )
		{
			ringModel = Model.Load( modelPath );
			loadedRingModelPath = modelPath;
		}

		return ringModel.IsValid();
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

	private float ResolveRingMeshDiameter( Model model )
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

	private void UpdateRing( int index, Vector3 origin, float radius, Color tint, float alpha )
	{
		var slot = rings[index];
		if ( slot?.Root is null || slot.Renderer is null || !EnsureRingMaterial( slot ) )
			return;

		var targetDiameter = MathF.Max( 1f, radius ) * 2f;
		var meshDiameter = ResolveRingMeshDiameter( ringModel );
		var scale = targetDiameter / meshDiameter;

		slot.Root.WorldPosition = new Vector3( origin.x, origin.y, origin.z + RingGroundLift );
		slot.Root.WorldRotation = Rotation.Identity;
		slot.Root.WorldScale = new Vector3( scale, scale, 1f );
		slot.Renderer.Model = ringModel;
		slot.Renderer.RenderType = ModelRenderer.ShadowRenderType.Off;
		ApplyRingMaterial( slot.Renderer, slot.Material, tint, alpha );
		slot.Root.Enabled = true;

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
