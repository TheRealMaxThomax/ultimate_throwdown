using Sandbox;

/// <summary>
/// Blitz-only connect hang: freeze attacker + victim <see cref="SkinnedModelRenderer"/> pose during
/// <see cref="SpeedsterSpeedBlitzUlt.KnockdownPreLaunchPauseSeconds"/> via <c>PlaybackRate = 0</c>.
/// On Speedster player prefab; enabled via <see cref="PlayerLoadout.ConfigureSpeedsterOnlyComponentsOnHost"/>.
/// </summary>
[Order( 10004 )]
public sealed class BlitzConnectPoseFreeze : Component
{
	private const string ChargeRunCycleParamName = "charge_run_cycle";
	private const string ChargeRunWeightParamName = "charge_run_weight";

	[Property] public SkinnedModelRenderer BodyRenderer { get; set; }

	private PlayerTackle playerTackle;
	private SpeedsterSpeedBlitzUlt speedBlitzUlt;

	private bool bodyPoseFrozen;
	private float savedPlaybackRate = 1f;

	/// <summary>True while this pawn's body anim graph is held for a Speed Blitz connect hang.</summary>
	public bool IsBodyPoseFrozen => bodyPoseFrozen;

	protected override void OnStart()
	{
		playerTackle = Components.Get<PlayerTackle>();
		speedBlitzUlt = Components.Get<SpeedsterSpeedBlitzUlt>();
		ResolveBodyRenderer();
	}

	protected override void OnUpdate()
	{
		var wantFreeze = ShouldFreezeBodyPose();
		if ( wantFreeze )
		{
			if ( !bodyPoseFrozen )
				EnterBodyPoseFreeze();

			if ( BodyRenderer.IsValid() )
				BodyRenderer.PlaybackRate = 0f;

			return;
		}

		if ( bodyPoseFrozen )
			ExitBodyPoseFreeze();
	}

	private bool ShouldFreezeBodyPose()
	{
		if ( playerTackle?.IsAwaitingSpeedBlitzRagdollLaunch == true )
			return true;

		if ( speedBlitzUlt?.IsConnectPoseFrozen == true )
			return true;

		return false;
	}

	private void EnterBodyPoseFreeze()
	{
		if ( !TryGetBodyRenderer( out var renderer ) )
			return;

		bodyPoseFrozen = true;
		savedPlaybackRate = renderer.PlaybackRate;

		if ( speedBlitzUlt?.IsConnectPoseFrozen == true )
			ApplyAttackerConnectPoseSnap( renderer );

		renderer.PlaybackRate = 0f;
	}

	private void ApplyAttackerConnectPoseSnap( SkinnedModelRenderer renderer )
	{
		if ( !speedBlitzUlt.IsValid() )
			return;

		var cycle = speedBlitzUlt.ConnectImpactChargeRunCycle;
		if ( cycle >= 0f )
			renderer.Set( ChargeRunCycleParamName, cycle.Clamp( 0f, 1f ) );

		// Dash phase may already have ended — hold charge-run overlay through the hang.
		renderer.Set( ChargeRunWeightParamName, 1f );
	}

	private void ExitBodyPoseFreeze()
	{
		bodyPoseFrozen = false;

		if ( !TryGetBodyRenderer( out var renderer ) )
			return;

		renderer.PlaybackRate = savedPlaybackRate > 0.0001f ? savedPlaybackRate : 1f;
	}

	private void ResolveBodyRenderer()
	{
		if ( BodyRenderer.IsValid() )
			return;

		var dresser = Components.Get<Dresser>( FindMode.EverythingInSelfAndDescendants );
		if ( dresser.IsValid() && dresser.BodyTarget.IsValid() )
		{
			BodyRenderer = dresser.BodyTarget;
			return;
		}

		BodyRenderer = Components.Get<SkinnedModelRenderer>( FindMode.EverythingInDescendants );
	}

	private bool TryGetBodyRenderer( out SkinnedModelRenderer renderer )
	{
		ResolveBodyRenderer();
		renderer = BodyRenderer;
		return renderer.IsValid() && renderer.UseAnimGraph;
	}
}
