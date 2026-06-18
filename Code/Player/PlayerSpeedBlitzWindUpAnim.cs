using Sandbox;

/// <summary>
/// Drives the <c>speedblitz_windup</c> masked animgraph layer while
/// <see cref="SpeedsterSpeedBlitzUlt.IsWindUp"/> — synced phase so remotes see the Olympic pose.
/// Separate from <see cref="PlayerChargeRunAnim"/> (off during wind-up by design).
/// </summary>
[Order( 10006 )]
public sealed class PlayerSpeedBlitzWindUpAnim : Component
{
	[Property] public SkinnedModelRenderer BodyRenderer { get; set; }
	[Property] public bool UseAnimGraphWindUpPose { get; set; } = true;
	[Property] public string WindUpCycleParamName { get; set; } = "blitz_windup";
	[Property] public string WindUpWeightParamName { get; set; } = "blitz_windup_weight";
	[Property] public float WindUpCycleStart { get; set; } = 0f;
	[Property] public float WindUpCycleEnd { get; set; } = 1f;
	[Property] public float WindUpWeightBlendInSeconds { get; set; } = 0.3f;
	[Property] public float WindUpWeightBlendOutSeconds { get; set; } = 0.15f;

	private PlayerTackle playerTackle;
	private PlayerTeam playerTeam;
	private PlayerBallHoldAnim ballHoldAnim;
	private SpeedsterSpeedBlitzUlt speedBlitzUlt;
	private float windUpPoseWeight;

	protected override void OnStart()
	{
		playerTackle = Components.Get<PlayerTackle>();
		playerTeam = Components.Get<PlayerTeam>();
		ballHoldAnim = Components.Get<PlayerBallHoldAnim>();
		speedBlitzUlt = Components.Get<SpeedsterSpeedBlitzUlt>();
		ResolveBodyRenderer();
		ballHoldAnim?.EnsureCustomBodyModel();

		if ( TryGetBodyRenderer( out var renderer ) )
		{
			windUpPoseWeight = 0f;
			renderer.Set( WindUpWeightParamName, 0f );
			renderer.Set( WindUpCycleParamName, WindUpCycleStart );
		}
	}

	protected override void OnUpdate()
	{
		if ( !UseAnimGraphWindUpPose )
			return;

		if ( Components.Get<BlitzConnectPoseFreeze>() is { IsBodyPoseFrozen: true } )
			return;

		if ( !TryGetBodyRenderer( out var renderer ) )
			return;

		speedBlitzUlt ??= Components.Get<SpeedsterSpeedBlitzUlt>();
		var wantPose = ShouldShowWindUpPose();
		UpdateWindUpPose( renderer, wantPose );
	}

	private bool ShouldShowWindUpPose()
	{
		if ( ShouldSkipAnim() )
			return false;

		if ( speedBlitzUlt is null || !speedBlitzUlt.IsWindUp )
			return false;

		return true;
	}

	private void UpdateWindUpPose( SkinnedModelRenderer renderer, bool wantPose )
	{
		var targetWeight = wantPose ? 1f : 0f;
		var blendSeconds = wantPose ? WindUpWeightBlendInSeconds : WindUpWeightBlendOutSeconds;
		windUpPoseWeight = blendSeconds <= 0.001f
			? targetWeight
			: windUpPoseWeight.Approach( targetWeight, Time.Delta / blendSeconds );

		if ( wantPose )
		{
			var lerp = speedBlitzUlt?.GetWindUpLerp() ?? 0f;
			var cycle = MathX.Lerp( WindUpCycleStart, WindUpCycleEnd, lerp );
			renderer.Set( WindUpCycleParamName, cycle.Clamp( 0f, 1f ) );
		}

		renderer.Set( WindUpWeightParamName, windUpPoseWeight );
	}

	private bool ShouldSkipAnim()
	{
		if ( playerTackle?.IsRagdolled == true )
			return true;

		if ( playerTeam is not null && !playerTeam.IsMatchGameplayInputAllowed )
			return true;

		return false;
	}

	private void ResolveBodyRenderer()
	{
		if ( BodyRenderer.IsValid() )
			return;

		BodyRenderer = Components.Get<SkinnedModelRenderer>( FindMode.EverythingInDescendants );
	}

	private bool TryGetBodyRenderer( out SkinnedModelRenderer renderer )
	{
		ResolveBodyRenderer();
		renderer = BodyRenderer;
		return renderer.IsValid() && renderer.UseAnimGraph;
	}
}
