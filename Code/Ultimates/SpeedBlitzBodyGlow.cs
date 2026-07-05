using System.Collections.Generic;
using Sandbox;

/// <summary>
/// Speed Blitz dasher glow — ult blue tint on avatar skinned meshes (no <see cref="HighlightOutline"/>).
/// Uses <see cref="SceneObject.ColorTint"/> right before draw (citizen clothing covers most bare skin).
/// Wind-up ramps with <see cref="SpeedsterSpeedBlitzUlt.GetWindUpLerp"/>; dash + connect hang hold peak; discharge fades at ragdoll launch.
/// Auto-added by <see cref="SpeedsterSpeedBlitzUlt"/>.
/// </summary>
public sealed class SpeedBlitzBodyGlow : Component
{
	/// <summary> Matches ult preview / comic blue (<c>#24b0ff</c>). </summary>
	static readonly Color UltBlue = new( 36f / 255f, 176f / 255f, 1f, 1f );

	[Property] public Color GlowColor { get; set; } = UltBlue;
	[Property, Range( 0f, 1f )] public float BodyTintStrength { get; set; } = 1f;
	[Property, Range( 0f, 1f )] public float ClothingTintStrength { get; set; } = 1f;
	[Property] public float DischargeFadeSeconds { get; set; } = 0.22f;
	[Property] public bool EnablePointLight { get; set; } = true;
	[Property] public float PointLightRadius { get; set; } = 64f;
	[Property] public float PointLightBrightnessMax { get; set; } = 0.95f;
	[Property] public Vector3 PointLightLocalOffset { get; set; } = new( 0f, 0f, 48f );
	[Property] public SkinnedModelRenderer BodyRenderer { get; set; }

	/// <summary> True while this pawn is showing blitz glow tint / point light. </summary>
	public bool IsGlowVisualActive { get; private set; }

	private SpeedsterSpeedBlitzUlt speedBlitzUlt;
	private readonly Dictionary<SkinnedModelRenderer, GlowVisualSnapshot> visualSnapshots = new();

	private GameObject pointLightObject;
	private PointLight pointLight;

	private bool wasDashing;
	private bool wasConnectPoseFrozen;
	private float missFadeUntil;
	private float missFadeDuration;
	private float dischargeFadeUntil;
	private float dischargeFadeDuration;

	private bool shouldRenderGlow;
	private float renderIntensity;

	internal bool WantsPreDrawGlow => shouldRenderGlow && renderIntensity > 0.001f;

	protected override void OnStart()
	{
		speedBlitzUlt = Components.Get<SpeedsterSpeedBlitzUlt>();
		ResolveBodyRenderer();
	}

	protected override void OnDestroy()
	{
		DestroyPointLight();
		RestorePreDrawGlow();
	}

	protected override void OnDisabled()
	{
		ClearGlowState();
	}

	protected override void OnUpdate()
	{
		TickGlowState();
		SyncPointLightToGlowState();

		if ( !WantsPreDrawGlow && IsGlowVisualActive )
			RestorePreDrawGlow();
	}

	/// <summary> Render-system safety net — proxies must not keep a stale local point light after glow ends. </summary>
	internal void SyncPointLightToGlowState()
	{
		if ( !EnablePointLight || !shouldRenderGlow || renderIntensity <= 0.001f )
		{
			DestroyPointLight();
			return;
		}

		EnsurePointLight();
		if ( !pointLight.IsValid() )
			return;

		pointLight.Enabled = true;
		var visual = renderIntensity.Clamp( 0f, 1f );
		var brightness = PointLightBrightnessMax * visual;
		pointLight.LightColor = new Color(
			GlowColor.r * brightness,
			GlowColor.g * brightness,
			GlowColor.b * brightness,
			1f );
		pointLight.Radius = PointLightRadius;
		pointLight.Attenuation = 3.5f;
	}

	/// <summary> Called from <see cref="SpeedBlitzBodyGlowRenderSystem"/> after LOD lock, right before draw. </summary>
	internal void ApplyPreDrawGlow()
	{
		if ( !WantsPreDrawGlow )
			return;

		ResolveBodyRenderer();

		foreach ( var renderer in GameObject.Components.GetAll<SkinnedModelRenderer>( FindMode.EverythingInSelfAndDescendants ) )
		{
			if ( !renderer.IsValid() || !renderer.Enabled || !renderer.GameObject.Enabled )
				continue;

			if ( !ShouldGlowRenderer( renderer ) )
				continue;

			var isBody = BodyRenderer.IsValid() && renderer == BodyRenderer;
			var strength = isBody ? BodyTintStrength : ClothingTintStrength;
			var blend = (renderIntensity * strength.Clamp( 0f, 1f )).Clamp( 0f, 1f );
			if ( blend <= 0.001f )
				continue;

			CaptureRendererVisuals( renderer );
			var snapshot = visualSnapshots[renderer];

			renderer.Tint = Color.Lerp( snapshot.RendererTint, GlowColor, blend );

			if ( renderer.SceneObject.IsValid() )
			{
				renderer.SceneObject.Batchable = false;
				renderer.SceneObject.ColorTint = Color.Lerp( snapshot.SceneColorTint, GlowColor, blend );
			}
		}

		IsGlowVisualActive = true;
	}

	internal void RestorePreDrawGlow()
	{
		foreach ( var entry in visualSnapshots )
		{
			if ( !entry.Key.IsValid() )
				continue;

			entry.Key.Tint = entry.Value.RendererTint;

			if ( entry.Value.HasSceneTint && entry.Key.SceneObject.IsValid() )
				entry.Key.SceneObject.ColorTint = entry.Value.SceneColorTint;
		}

		visualSnapshots.Clear();
		IsGlowVisualActive = false;
	}

	private void TickGlowState()
	{
		if ( dischargeFadeUntil > 0f )
		{
			if ( Time.Now < dischargeFadeUntil )
			{
				var fadeT = 1f - ((dischargeFadeUntil - Time.Now) / dischargeFadeDuration).Clamp( 0f, 1f );
				SetGlowState( 1f - fadeT );
				UpdatePhaseTrackers( false, false );
				return;
			}

			dischargeFadeUntil = 0f;
			ClearGlowState();
			UpdatePhaseTrackers( false, false );
			return;
		}

		if ( missFadeUntil > 0f )
		{
			if ( Time.Now < missFadeUntil )
			{
				var fadeT = 1f - ((missFadeUntil - Time.Now) / missFadeDuration).Clamp( 0f, 1f );
				SetGlowState( 1f - fadeT );
				UpdatePhaseTrackers( false, false );
				return;
			}

			missFadeUntil = 0f;
			ClearGlowState();
			UpdatePhaseTrackers( false, false );
			return;
		}

		speedBlitzUlt ??= Components.Get<SpeedsterSpeedBlitzUlt>();
		if ( !speedBlitzUlt.IsValid() )
		{
			ClearGlowState();
			UpdatePhaseTrackers( false, false );
			return;
		}

		var windUp = speedBlitzUlt.IsWindUp;
		var dashing = speedBlitzUlt.IsDashing;
		var connectFrozen = speedBlitzUlt.IsConnectPoseFrozen;
		var phase = speedBlitzUlt.SyncedPhase;

		if ( wasConnectPoseFrozen && !connectFrozen )
		{
			BeginDischargeFade();
			SetGlowState( 1f );
			UpdatePhaseTrackers( dashing, connectFrozen );
			return;
		}

		if ( wasDashing && !dashing && !connectFrozen && phase == SpeedsterSpeedBlitzUlt.SpeedBlitzPhase.None )
		{
			BeginMissFade();
			SetGlowState( 1f );
			UpdatePhaseTrackers( dashing, connectFrozen );
			return;
		}

		if ( windUp )
		{
			SetGlowState( speedBlitzUlt.GetWindUpLerp() );
			UpdatePhaseTrackers( dashing, connectFrozen );
			return;
		}

		if ( dashing || connectFrozen )
		{
			SetGlowState( 1f );
			UpdatePhaseTrackers( dashing, connectFrozen );
			return;
		}

		ClearGlowState();
		UpdatePhaseTrackers( dashing, connectFrozen );
	}

	private void UpdatePhaseTrackers( bool dashing, bool connectFrozen )
	{
		wasDashing = dashing;
		wasConnectPoseFrozen = connectFrozen;
	}

	private void SetGlowState( float intensity )
	{
		intensity = intensity.Clamp( 0f, 1f );
		if ( intensity <= 0.001f )
		{
			ClearGlowState();
			return;
		}

		shouldRenderGlow = true;
		renderIntensity = intensity;
	}

	private void ClearGlowState()
	{
		shouldRenderGlow = false;
		renderIntensity = 0f;
		missFadeUntil = 0f;
		missFadeDuration = 0f;
		dischargeFadeUntil = 0f;
		dischargeFadeDuration = 0f;
		DestroyPointLight();
	}

	private void BeginMissFade()
	{
		if ( missFadeUntil > 0f || dischargeFadeUntil > 0f )
			return;

		missFadeDuration = (speedBlitzUlt?.MissVfxFadeSeconds ?? 0.25f).Clamp( 0.05f, 1.5f );
		missFadeUntil = Time.Now + missFadeDuration;
	}

	private void BeginDischargeFade()
	{
		if ( dischargeFadeUntil > 0f )
			return;

		dischargeFadeDuration = DischargeFadeSeconds.Clamp( 0.05f, 1f );
		dischargeFadeUntil = Time.Now + dischargeFadeDuration;
	}

	private void CaptureRendererVisuals( SkinnedModelRenderer renderer )
	{
		if ( visualSnapshots.ContainsKey( renderer ) )
			return;

		var hasSceneTint = renderer.SceneObject.IsValid();
		visualSnapshots[renderer] = new GlowVisualSnapshot
		{
			RendererTint = renderer.Tint,
			SceneColorTint = hasSceneTint ? renderer.SceneObject.ColorTint : Color.White,
			HasSceneTint = hasSceneTint
		};
	}

	private static bool ShouldGlowRenderer( SkinnedModelRenderer renderer )
	{
		var slotCount = renderer.Materials.Count;
		if ( slotCount <= 0 )
			return true;

		for ( var slot = 0; slot < slotCount; slot++ )
		{
			if ( ShouldGlowMaterialSlot( renderer, slot ) )
				return true;
		}

		return false;
	}

	private static bool ShouldGlowMaterialSlot( SkinnedModelRenderer renderer, int slot )
	{
		var original = renderer.Materials.GetOriginal( slot );
		if ( !original.IsValid() )
			return false;

		var path = original.ResourcePath?.ToLowerInvariant() ?? string.Empty;
		if ( path.Contains( "eye" ) || path.Contains( "cornea" ) || path.Contains( "teeth" ) || path.Contains( "saliva" ) )
			return false;

		return true;
	}

	private void EnsurePointLight()
	{
		if ( pointLight.IsValid() )
			return;

		pointLightObject = new GameObject( true, "SpeedBlitzBodyGlowLight" );
		pointLightObject.NetworkMode = NetworkMode.Never;
		pointLightObject.Parent = GameObject;
		pointLightObject.LocalPosition = PointLightLocalOffset;
		pointLightObject.LocalRotation = Rotation.Identity;
		pointLight = pointLightObject.Components.Create<PointLight>();
		pointLight.Shadows = false;
	}

	private void DestroyPointLight()
	{
		if ( pointLightObject.IsValid() )
			pointLightObject.Destroy();

		pointLightObject = null;
		pointLight = null;
	}

	private void ResolveBodyRenderer()
	{
		var dresser = Components.Get<Dresser>( FindMode.EverythingInSelfAndDescendants );
		if ( dresser.IsValid() && dresser.BodyTarget.IsValid() )
		{
			BodyRenderer = dresser.BodyTarget;
			return;
		}

		if ( BodyRenderer.IsValid() )
			return;

		BodyRenderer = Components.Get<SkinnedModelRenderer>( FindMode.EverythingInDescendants );
	}

	private readonly struct GlowVisualSnapshot
	{
		public Color RendererTint { get; init; }
		public Color SceneColorTint { get; init; }
		public bool HasSceneTint { get; init; }
	}
}

/// <summary>
/// Applies <see cref="SpeedBlitzBodyGlow"/> tints after citizen LOD lock — component <c>OnPreRender</c> order across GameObjects is not guaranteed.
/// </summary>
public sealed class SpeedBlitzBodyGlowRenderSystem : GameObjectSystem<SpeedBlitzBodyGlowRenderSystem>
{
	public SpeedBlitzBodyGlowRenderSystem( Scene scene )
		: base( scene )
	{
		Listen( Stage.Interpolation, 10_001, ApplyAllGlows, nameof( SpeedBlitzBodyGlowRenderSystem ) );
	}

	private void ApplyAllGlows()
	{
		foreach ( var glow in Scene.GetAllComponents<SpeedBlitzBodyGlow>() )
		{
			if ( !glow.IsValid() || !glow.GameObject.IsValid() || !glow.GameObject.Enabled )
				continue;

			if ( glow.WantsPreDrawGlow )
				glow.ApplyPreDrawGlow();
			else if ( glow.IsGlowVisualActive )
				glow.RestorePreDrawGlow();

			glow.SyncPointLightToGlowState();
		}
	}
}
