using System;
using System.Collections.Generic;
using Sandbox;

/// <summary>
/// Owner-only aim telegraph while <see cref="SpeedsterSpeedBlitzUlt.IsAiming"/>.
/// Add manually on the same GameObject as <see cref="SpeedsterSpeedBlitzUlt"/>.
/// Straight max-range segmented corridor + end marker (slice 2b).
/// </summary>
[Order( 10012 )]
public sealed class SpeedBlitzAimPreview : Component
{
	const string DefaultMarkerMaterialPath = "materials/turfwarspoly/ball_translucent.vmat";
	const string DefaultMarkerModelPath = "models/dev/box.vmdl";

	private sealed class SegmentSlot
	{
		public GameObject Root;
		public ModelRenderer Renderer;
	}

	private static Material markerMaterialBase;

	[Property, Group( "Corridor" )] public Color CorridorTint { get; set; } = new( 0.3f, 0.55f, 1f, 1f );
	[Property, Group( "Corridor" )] public float CorridorAlpha { get; set; } = 0.38f;
	[Property, Group( "Corridor" )] public float CorridorHeight { get; set; } = 10f;
	[Property, Group( "Corridor" )] public float CorridorLift { get; set; } = 0.5f;
	[Property, Group( "Corridor" )] public float SegmentSpacing { get; set; } = 48f;
	[Property, Group( "Corridor" )] public float MinSegmentLength { get; set; } = 4f;
	[Property, Group( "Corridor" )] public int MaxSegments { get; set; } = 48;

	[Property, Group( "End marker" )] public Color MarkerTint { get; set; } = new( 0.35f, 0.6f, 1f, 1f );
	[Property, Group( "End marker" )] public float MarkerAlpha { get; set; } = 0.55f;
	[Property, Group( "End marker" )] public float MarkerHeight { get; set; } = 18f;

	[Property] public string MarkerMaterialPath { get; set; } = DefaultMarkerMaterialPath;
	[Property] public string MarkerModelPath { get; set; } = DefaultMarkerModelPath;
	[Property] public float MarkerLift { get; set; } = 0.5f;
	/// <summary> Native size of <see cref="MarkerModelPath"/> (dev box ≈ 50). World size = hit width / this. </summary>
	[Property] public float MarkerModelBaseSize { get; set; } = 50f;

	private SpeedsterSpeedBlitzUlt ult;
	private Model markerModel;
	private Material corridorMaterial;
	private Material markerMaterial;

	private readonly List<SegmentSlot> segmentPool = new();
	private readonly List<Vector3> pathSamples = new();
	private GameObject markerGo;
	private ModelRenderer markerRenderer;

	protected override void OnStart()
	{
		ult = Components.Get<SpeedsterSpeedBlitzUlt>( FindMode.EverythingInSelf );
	}

	protected override void OnDestroy()
	{
		foreach ( var slot in segmentPool )
		{
			if ( slot.Root.IsValid() )
				slot.Root.Destroy();
		}

		segmentPool.Clear();

		if ( markerGo.IsValid() )
			markerGo.Destroy();
	}

	protected override void OnUpdate()
	{
		if ( IsProxy || !Network.IsOwner )
		{
			SetVisible( false );
			return;
		}

		ult ??= Components.Get<SpeedsterSpeedBlitzUlt>( FindMode.EverythingInSelf );
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

		ult.GetAimPreviewParams(
			out var origin,
			out var direction,
			out var dashRange,
			out var hitHalfWidth );

		var moveDir = direction.WithZ( 0f );
		if ( moveDir.Length < 0.001f )
		{
			SetVisible( false );
			return;
		}

		moveDir = moveDir.Normal;

		var corridorWidth = hitHalfWidth.Clamp( 4f, 200f ) * 2f;
		var modelBase = MarkerModelBaseSize.Clamp( 1f, 500f );

		BuildStraightPath( origin.WithZ( 0f ), moveDir, dashRange, pathSamples );
		UpdateCorridorSegments( pathSamples, moveDir, corridorWidth, modelBase );
		UpdateEndMarker( pathSamples[^1], moveDir, corridorWidth, modelBase );
	}

	void BuildStraightPath( Vector3 startFlat, Vector3 moveDir, float dashRange, List<Vector3> samples )
	{
		samples.Clear();
		samples.Add( SnapToGround( startFlat ) );

		var spacing = SegmentSpacing.Clamp( 8f, 128f );
		var traveled = spacing;

		while ( traveled < dashRange - MinSegmentLength * 0.5f )
		{
			samples.Add( SnapToGround( startFlat + moveDir * traveled ) );
			traveled += spacing;
		}

		samples.Add( SnapToGround( startFlat + moveDir * dashRange ) );
	}

	void UpdateCorridorSegments(
		IReadOnlyList<Vector3> samples,
		Vector3 moveDir,
		float corridorWidth,
		float modelBase )
	{
		var corridorHeight = CorridorHeight.Clamp( 4f, 200f );
		var activeCount = 0;

		for ( var i = 0; i < samples.Count - 1 && activeCount < MaxSegments; i++ )
		{
			var a = samples[i];
			var b = samples[i + 1];
			var delta = b - a;
			var length = delta.Length;
			if ( length < MinSegmentLength )
				continue;

			var forward = delta.WithZ( 0f );
			if ( forward.Length < 0.001f )
				forward = moveDir;
			forward = forward.Normal;

			var yawDegrees = MathF.Atan2( forward.y, forward.x ) * (180f / MathF.PI);
			var slot = EnsureSegmentSlot( activeCount );
			activeCount++;

			var mid = (a + b) * 0.5f;
			slot.Root.WorldPosition = mid + Vector3.Up * (corridorHeight * 0.5f + CorridorLift);
			slot.Root.WorldRotation = Rotation.FromYaw( yawDegrees );
			// Dev box + FromYaw: local X = segment length, local Y = corridor width.
			slot.Root.WorldScale = new Vector3(
				length / modelBase,
				corridorWidth / modelBase,
				corridorHeight / modelBase );

			slot.Renderer.Model = markerModel;
			ApplyMaterial( slot.Renderer, corridorMaterial, CorridorTint, CorridorAlpha );
			slot.Root.Enabled = true;
		}

		for ( var i = activeCount; i < segmentPool.Count; i++ )
			segmentPool[i].Root.Enabled = false;
	}

	void UpdateEndMarker(
		Vector3 endGround,
		Vector3 moveDir,
		float corridorWidth,
		float modelBase )
	{
		var markerHeight = MarkerHeight.Clamp( 4f, 200f );
		var yawDegrees = MathF.Atan2( moveDir.y, moveDir.x ) * (180f / MathF.PI);

		EnsureMarkerObject();

		markerGo.WorldPosition = endGround + Vector3.Up * (markerHeight * 0.5f + MarkerLift);
		markerGo.WorldRotation = Rotation.FromYaw( yawDegrees );
		markerGo.WorldScale = new Vector3(
			corridorWidth / modelBase,
			corridorWidth / modelBase,
			markerHeight / modelBase );

		markerRenderer.Model = markerModel;
		ApplyMaterial( markerRenderer, markerMaterial, MarkerTint, MarkerAlpha );
		markerGo.Enabled = true;
	}

	static void ApplyMaterial( ModelRenderer renderer, Material material, Color tint, float alpha )
	{
		if ( !material.IsValid() )
			return;

		material.Set( "g_vColorTint", tint );
		material.Set( "g_flOpacityScale", alpha );
		renderer.Tint = Color.White;
		renderer.MaterialOverride = material;
	}

	Vector3 SnapToGround( Vector3 horizontalPos )
	{
		var trace = Scene.Trace.Ray( horizontalPos + Vector3.Up * 256f, horizontalPos + Vector3.Down * 512f )
			.IgnoreGameObjectHierarchy( GameObject )
			.Run();

		return trace.Hit ? trace.EndPosition : horizontalPos;
	}

	bool EnsureAssets()
	{
		if ( !markerModel.IsValid() )
			markerModel = Model.Load( MarkerModelPath );

		if ( !markerModel.IsValid() )
			return false;

		var path = string.IsNullOrWhiteSpace( MarkerMaterialPath )
			? DefaultMarkerMaterialPath
			: MarkerMaterialPath;

		markerMaterialBase ??= Material.Load( path );
		if ( !markerMaterialBase.IsValid() )
			return false;

		if ( !corridorMaterial.IsValid() )
			corridorMaterial = markerMaterialBase.CreateCopy( "speed_blitz_aim_corridor" );

		if ( !markerMaterial.IsValid() )
			markerMaterial = markerMaterialBase.CreateCopy( "speed_blitz_aim_end" );

		return corridorMaterial.IsValid() && markerMaterial.IsValid();
	}

	SegmentSlot EnsureSegmentSlot( int index )
	{
		while ( segmentPool.Count <= index )
			segmentPool.Add( CreateSegmentSlot( $"SpeedBlitzAimSegment_{segmentPool.Count}" ) );

		return segmentPool[index];
	}

	SegmentSlot CreateSegmentSlot( string name )
	{
		var go = Scene.CreateObject();
		go.Name = name;

		var renderer = go.Components.Create<ModelRenderer>();
		renderer.RenderOptions.Overlay = false;

		if ( renderer.SceneObject.IsValid() )
		{
			renderer.SceneObject.Flags.CastShadows = false;
			renderer.SceneObject.Flags.IsTranslucent = true;
		}

		return new SegmentSlot { Root = go, Renderer = renderer };
	}

	void EnsureMarkerObject()
	{
		if ( markerGo.IsValid() )
			return;

		markerGo = Scene.CreateObject();
		markerGo.Name = "SpeedBlitzAimEnd";

		markerRenderer = markerGo.Components.Create<ModelRenderer>();
		markerRenderer.RenderOptions.Overlay = false;

		if ( markerRenderer.SceneObject.IsValid() )
		{
			markerRenderer.SceneObject.Flags.CastShadows = false;
			markerRenderer.SceneObject.Flags.IsTranslucent = true;
		}
	}

	void SetVisible( bool visible )
	{
		foreach ( var slot in segmentPool )
		{
			if ( slot.Root.IsValid() )
				slot.Root.Enabled = visible;
		}

		if ( markerGo.IsValid() )
			markerGo.Enabled = visible;
	}
}
