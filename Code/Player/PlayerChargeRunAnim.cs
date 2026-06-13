using Sandbox;

/// <summary>
/// Drives the <c>charge_run</c> masked animgraph layer while <see cref="CatchUpSpeedBoost.IsAtChargeSpeed"/>
/// and the player is <b>not</b> holding the ball. Ball carriers cap at sprint and never reach charge speed by design.
/// Separate from <see cref="PlayerBallHoldAnim"/> throw wind-up (<c>throw_windup</c> / <c>throw_charge</c>).
/// </summary>
[Order( 10005 )]
public sealed class PlayerChargeRunAnim : Component
{
	[Property] public SkinnedModelRenderer BodyRenderer { get; set; }
	[Property] public bool UseAnimGraphChargeRunPose { get; set; } = true;
	[Property] public string ChargeRunWeightParamName { get; set; } = "charge_run_weight";
	[Property] public string ChargeRunCycleParamName { get; set; } = "charge_run_cycle";
	[Property] public float ChargeRunCycle { get; set; } = 0f;
	[Property] public float ChargeRunWeightBlendInSeconds { get; set; } = 0.12f;
	[Property] public float ChargeRunWeightBlendOutSeconds { get; set; } = 0.15f;

	private CatchUpSpeedBoost catchUpSpeedBoost;
	private BallGrab ballGrab;
	private BallThrow ballThrow;
	private PlayerTackle playerTackle;
	private PlayerTeam playerTeam;
	private PlayerBallHoldAnim ballHoldAnim;
	private SpeedsterSpeedBlitzUlt speedBlitzUlt;
	private float chargeRunPoseWeight;

	protected override void OnStart()
	{
		catchUpSpeedBoost = Components.Get<CatchUpSpeedBoost>();
		ballGrab = Components.Get<BallGrab>();
		ballThrow = Components.Get<BallThrow>();
		playerTackle = Components.Get<PlayerTackle>();
		playerTeam = Components.Get<PlayerTeam>();
		ballHoldAnim = Components.Get<PlayerBallHoldAnim>();
		speedBlitzUlt = Components.Get<SpeedsterSpeedBlitzUlt>();
		ResolveBodyRenderer();
		ballHoldAnim?.EnsureCustomBodyModel();

		if ( TryGetBodyRenderer( out var renderer ) )
		{
			chargeRunPoseWeight = 0f;
			renderer.Set( ChargeRunWeightParamName, 0f );
		}
	}

	protected override void OnUpdate()
	{
		if ( !UseAnimGraphChargeRunPose )
			return;

		if ( !TryGetBodyRenderer( out var renderer ) )
			return;

		var wantPose = ShouldShowChargeRunPose();
		UpdateChargeRunPose( renderer, wantPose );
	}

	/// <summary> Top movement ramp tier — uses <see cref="CatchUpSpeedBoost.IsAtChargeSpeed"/> so remotes see the overlay (ramp timers + input are owner-only). </summary>
	private bool ShouldShowChargeRunPose()
	{
		if ( ShouldSkipAnim() )
			return false;

		// Speed Blitz dash: same charge-run look (running legs from locomotion + charge pose overlay).
		// Synced phase (IsDashing) so remotes see it too. Wins over ball/throw gating below.
		speedBlitzUlt ??= Components.Get<SpeedsterSpeedBlitzUlt>();
		if ( speedBlitzUlt?.IsDashing == true )
			return true;

		if ( ballGrab?.IsHolding == true )
			return false;

		if ( ballThrow?.IsChargingThrow == true )
			return false;

		if ( catchUpSpeedBoost is null )
			return false;

		return catchUpSpeedBoost.IsAtChargeSpeed;
	}

	private void UpdateChargeRunPose( SkinnedModelRenderer renderer, bool wantPose )
	{
		var targetWeight = wantPose ? 1f : 0f;
		var blendSeconds = wantPose ? ChargeRunWeightBlendInSeconds : ChargeRunWeightBlendOutSeconds;
		chargeRunPoseWeight = blendSeconds <= 0.001f
			? targetWeight
			: chargeRunPoseWeight.Approach( targetWeight, Time.Delta / blendSeconds );

		if ( wantPose )
			renderer.Set( ChargeRunCycleParamName, ChargeRunCycle.Clamp( 0f, 1f ) );

		renderer.Set( ChargeRunWeightParamName, chargeRunPoseWeight );
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
