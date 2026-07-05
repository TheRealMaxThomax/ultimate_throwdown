using System;
using System.Collections.Generic;
using Sandbox;
using Sandbox.Movement;

/// <summary>
/// Owner-only aim telegraph while <see cref="SpeedsterSpeedBlitzUlt.IsAiming"/>.
/// Flat ground corridor strips + end cap (slice 2b / v3 art).
/// Corridor half-width = <see cref="SpeedsterSpeedBlitzUlt.HitHalfWidth"/> (outer body-edge lines; host subtracts victim body radius).
/// Ground samples ignore players + <c>main_ball</c>; rise per segment capped to <see cref="MoveModeWalk.StepUpHeight"/>.
/// </summary>
[Order( 10012 )]
public sealed class SpeedBlitzAimPreview : Component
{
	const string DefaultCorridorMaterialPath = "materials/turfwarspoly/speed_blitz_preview.vmat";
	const string DefaultMarkerMaterialPath = "materials/turfwarspoly/speed_blitz_preview.vmat";
	const string DefaultPlaneModelPath = "models/dev/plane.vmdl";
	const string MainBallName = "main_ball";

	/// <summary>Matches ult preview / comic blue (<c>#24b0ff</c>).</summary>
	static readonly Color UltPreviewBlue = new( 36f / 255f, 176f / 255f, 1f, 1f );

	private sealed class SegmentSlot
	{
		public GameObject Root;
		public ModelRenderer Renderer;
	}

	private static Material corridorMaterialBase;
	private static Material markerMaterialBase;
	private static Model planeModel;

	[Property, Group( "Meshes" )] public string CorridorModelPath { get; set; } = DefaultPlaneModelPath;
	[Property, Group( "Meshes" )] public string MarkerModelPath { get; set; } = "models/dev/box.vmdl";
	/// <summary>Native mesh width (local Y) used to scale corridor half-width × 2 — tune hit-width read without shrinking segment length.</summary>
	[Property, Group( "Meshes" )] public float PlaneWidthBaseSize { get; set; } = 175f;
	/// <summary>Native mesh length (local X) used to scale each segment along the dash.</summary>
	[Property, Group( "Meshes" )] public float PlaneLengthBaseSize { get; set; } = 100f;

	[Property, Group( "Materials" )] public string CorridorMaterialPath { get; set; } = DefaultCorridorMaterialPath;
	[Property, Group( "Materials" )] public string MarkerMaterialPath { get; set; } = DefaultMarkerMaterialPath;

	[Property, Group( "Corridor" )] public Color CorridorTint { get; set; } = UltPreviewBlue;
	[Property, Group( "Corridor" )] public float CorridorAlpha { get; set; } = 0.8f;
	[Property, Group( "Corridor" )] public float CorridorLift { get; set; } = 3f;
	[Property, Group( "Corridor" )] public float SegmentSpacing { get; set; } = 48f;
	[Property, Group( "Corridor" )] public float MinSegmentLength { get; set; } = 4f;
	[Property, Group( "Corridor" )] public int MaxSegments { get; set; } = 48;

	[Property, Group( "End marker" )] public Color MarkerTint { get; set; } = new Color( 1f, 0.14118f, 0.14118f, 1f );
	[Property, Group( "End marker" )] public float MarkerAlpha { get; set; } = 0.8f;
	[Property, Group( "End marker" )] public float MarkerLift { get; set; } = 0.5f;

	/// <summary> Used when <see cref="MoveModeWalk"/> is missing. Otherwise reads prefab <c>Step Up Height</c>. </summary>
	[Property, Group( "Corridor" )] public float StepUpHeightFallback { get; set; } = 20f;

	private string loadedCorridorModelPath;
	private string loadedCorridorMaterialSourcePath;
	private string loadedMarkerMaterialSourcePath;

	private SpeedsterSpeedBlitzUlt ult;
	private MoveModeWalk moveModeWalk;
	private Material corridorMaterial;
	private Material markerMaterial;

	private readonly List<SegmentSlot> segmentPool = new();
	private readonly List<Vector3> pathSamples = new();
	private readonly List<GameObject> groundTraceIgnores = new();
	private GameObject markerGo;
	private ModelRenderer markerRenderer;

	protected override void OnStart()
	{
		ult = Components.Get<SpeedsterSpeedBlitzUlt>( FindMode.EverythingInSelf );
		moveModeWalk = Components.Get<MoveModeWalk>( FindMode.EverythingInSelf );
	}

	protected override void OnDestroy()
	{
		ClearPreviewObjects();
	}

	protected override void OnDisabled()
	{
		ClearPreviewObjects();
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
		var widthBase = PlaneWidthBaseSize.Clamp( 1f, 500f );
		var lengthBase = PlaneLengthBaseSize.Clamp( 1f, 500f );

		BuildStraightPath( origin.WithZ( 0f ), moveDir, dashRange, pathSamples );
		UpdateCorridorSegments( pathSamples, moveDir, corridorWidth, widthBase, lengthBase );
		UpdateEndMarker( pathSamples[^1], moveDir, corridorWidth, widthBase );
	}

	void BuildStraightPath( Vector3 startFlat, Vector3 moveDir, float dashRange, List<Vector3> samples )
	{
		samples.Clear();
		RefreshGroundTraceIgnores();

		var stepUp = GetPreviewStepUpHeight();
		var previousZ = SampleGroundHeight( startFlat, previousZ: null, stepUp ).z;
		samples.Add( new Vector3( startFlat.x, startFlat.y, previousZ ) );

		var spacing = SegmentSpacing.Clamp( 8f, 128f );
		var traveled = spacing;

		while ( traveled < dashRange - MinSegmentLength * 0.5f )
		{
			var flat = startFlat + moveDir * traveled;
			var z = SampleGroundHeight( flat, previousZ, stepUp ).z;
			samples.Add( new Vector3( flat.x, flat.y, z ) );
			previousZ = z;
			traveled += spacing;
		}

		var endFlat = startFlat + moveDir * dashRange;
		samples.Add( SampleGroundHeight( endFlat, previousZ, stepUp ) );
	}

	void UpdateCorridorSegments(
		IReadOnlyList<Vector3> samples,
		Vector3 moveDir,
		float corridorWidth,
		float widthBase,
		float lengthBase )
	{
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
			slot.Root.WorldPosition = new Vector3( mid.x, mid.y, mid.z + CorridorLift );
			slot.Root.WorldRotation = Rotation.FromYaw( yawDegrees );
			// Dev plane: local X = segment length, local Y = corridor width (flat on ground).
			slot.Root.WorldScale = new Vector3(
				length / lengthBase,
				corridorWidth / widthBase,
				1f );

			slot.Renderer.Model = planeModel;
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
		float widthBase )
	{
		var yawDegrees = MathF.Atan2( moveDir.y, moveDir.x ) * (180f / MathF.PI);

		EnsureMarkerObject();

		markerGo.WorldPosition = new Vector3( endGround.x, endGround.y, endGround.z + MarkerLift );
		markerGo.WorldRotation = Rotation.FromYaw( yawDegrees );
		markerGo.WorldScale = new Vector3(
			corridorWidth / widthBase,
			corridorWidth / widthBase,
			1f );

		markerRenderer.Model = planeModel;
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

	Vector3 SampleGroundHeight( Vector3 horizontalPos, float? previousZ, float stepUpHeight )
	{
		var flat = horizontalPos.WithZ( 0f );
		var z = TraceGroundHeight( flat ) ?? previousZ ?? flat.z;

		if ( previousZ.HasValue && z > previousZ.Value + stepUpHeight )
			z = previousZ.Value + stepUpHeight;

		return new Vector3( flat.x, flat.y, z );
	}

	float GetPreviewStepUpHeight()
	{
		moveModeWalk ??= Components.Get<MoveModeWalk>( FindMode.EverythingInSelf );
		if ( moveModeWalk.IsValid() && moveModeWalk.StepUpHeight > 0f )
			return moveModeWalk.StepUpHeight;

		return StepUpHeightFallback.Clamp( 1f, 128f );
	}

	void RefreshGroundTraceIgnores()
	{
		groundTraceIgnores.Clear();

		foreach ( var tackle in Scene.GetAllComponents<PlayerTackle>() )
		{
			if ( tackle.IsValid() && tackle.GameObject.IsValid() && tackle.GameObject != GameObject )
				groundTraceIgnores.Add( tackle.GameObject );
		}

		var ball = FindMainBallObject();
		if ( ball.IsValid() && ball != GameObject )
			groundTraceIgnores.Add( ball );
	}

	GameObject FindMainBallObject()
	{
		foreach ( var go in Scene.GetAllObjects( true ) )
		{
			if ( go.Name == MainBallName )
				return go;
		}

		return null;
	}

	SceneTrace BuildGroundTrace( Vector3 start, Vector3 end )
	{
		var trace = Scene.Trace.Ray( start, end )
			.WithoutTags( "ragdoll" )
			.IgnoreGameObjectHierarchy( GameObject );

		foreach ( var ignore in groundTraceIgnores )
		{
			if ( ignore.IsValid() )
				trace = trace.IgnoreGameObjectHierarchy( ignore );
		}

		return trace;
	}

	float? TraceGroundHeight( Vector3 horizontalPos )
	{
		var trace = BuildGroundTrace(
			horizontalPos + Vector3.Up * 256f,
			horizontalPos + Vector3.Down * 512f ).Run();

		return trace.Hit ? trace.HitPosition.z : null;
	}

	bool EnsureAssets()
	{
		var corridorModelPath = string.IsNullOrWhiteSpace( CorridorModelPath )
			? DefaultPlaneModelPath
			: CorridorModelPath;

		if ( !planeModel.IsValid() || loadedCorridorModelPath != corridorModelPath )
		{
			planeModel = Model.Load( corridorModelPath );
			loadedCorridorModelPath = corridorModelPath;
		}

		if ( !planeModel.IsValid() )
			return false;

		var corridorMatPath = string.IsNullOrWhiteSpace( CorridorMaterialPath )
			? DefaultCorridorMaterialPath
			: CorridorMaterialPath;

		if ( !corridorMaterialBase.IsValid() || loadedCorridorMaterialSourcePath != corridorMatPath )
		{
			corridorMaterialBase = Material.Load( corridorMatPath );
			loadedCorridorMaterialSourcePath = corridorMatPath;
			corridorMaterial = default;
		}

		if ( !corridorMaterialBase.IsValid() )
			return false;

		var markerMatPath = string.IsNullOrWhiteSpace( MarkerMaterialPath )
			? DefaultMarkerMaterialPath
			: MarkerMaterialPath;

		if ( !markerMaterialBase.IsValid() || loadedMarkerMaterialSourcePath != markerMatPath )
		{
			markerMaterialBase = Material.Load( markerMatPath );
			loadedMarkerMaterialSourcePath = markerMatPath;
			markerMaterial = default;
		}

		if ( !markerMaterialBase.IsValid() )
			markerMaterialBase = corridorMaterialBase;

		if ( !corridorMaterial.IsValid() )
			corridorMaterial = corridorMaterialBase.CreateCopy( "speed_blitz_aim_corridor" );

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
		go.NetworkMode = NetworkMode.Never;

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
		markerGo.NetworkMode = NetworkMode.Never;

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
		if ( !visible )
			ClearPreviewObjects();
	}

	void ClearPreviewObjects()
	{
		foreach ( var slot in segmentPool )
		{
			if ( slot.Root.IsValid() )
				slot.Root.Destroy();
		}

		segmentPool.Clear();

		if ( markerGo.IsValid() )
			markerGo.Destroy();

		markerGo = null;
		markerRenderer = null;
	}
}
