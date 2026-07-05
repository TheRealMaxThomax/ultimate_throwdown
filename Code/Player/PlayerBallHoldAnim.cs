using System;
using Sandbox;

/// <summary>
/// Drives citizen human <c>holditem</c> RH hold pose while <see cref="BallGrab.IsHolding"/> and
/// <c>HoldItem_RH_Throw_Strong</c> on intentional throw release via <c>b_attack</c>.
/// Charge wind-up while <see cref="BallThrow.IsChargingThrow"/> uses a masked layer in the forked citizen
/// animgraph (<see cref="UseAnimGraphChargePose"/>) so the body keeps locomotion/look-at.
/// </summary>
[Order( 10002 )]
public sealed class PlayerBallHoldAnim : Component
{
	/// <summary> Matches <c>holdtype</c> enum value <c>holditem</c> on <c>citizen_human_m.vanmgrph</c>. </summary>
	private const int HoldTypeHoldItem = 4;

	/// <summary> Matches <c>holdtype_handedness</c> enum value <c>RH</c>. </summary>
	private const int HandednessRight = 1;

	[Property] public SkinnedModelRenderer BodyRenderer { get; set; }
	[Property] public float IdleHoldPoseHand { get; set; } = 0.1f;
	/// <summary> <c>holdtype_pose</c> while holding normally (not charging). </summary>
	[Property] public float IdleHoldTypePose { get; set; } = 0f;
	/// <summary> <c>holdtype_attack</c> blend — 0 = medium throw, 1 = strong/far throw. </summary>
	[Property] public float ThrowAttackStrong { get; set; } = 1f;
	/// <summary> Keep <c>holditem</c> pose active after release so the throw clip is not cut off. </summary>
	[Property] public float ThrowPoseHoldSeconds { get; set; } = 0.9f;
	/// <summary> Anim graph playback rate during <see cref="ThrowPoseHoldSeconds"/> (1 = default speed). </summary>
	[Property] public float ThrowPlaybackRate { get; set; } = 0.7f;

	[Property, Group( "Custom human assets" )] public string CustomBodyModelPath { get; set; } = "animation/utd_citizen_human_throw.vmdl";

	[Property, Group( "Anim graph charge pose" )] public bool UseAnimGraphChargePose { get; set; } = true;
	/// <summary> Forked citizen graph with the masked charge layer. Re-applied after cosmetics. </summary>
	[Property, Group( "Anim graph charge pose" )] public string CustomAnimGraphPath { get; set; } = "animation/utd_citizen_human_m.vanmgrph";
	/// <summary> Float parameter (0–1) in the custom graph — Cycle Control scrubs the wind-up clip. </summary>
	[Property, Group( "Anim graph charge pose" )] public string ChargeCycleParamName { get; set; } = "throw_charge";
	/// <summary> Float parameter (0–1) in the custom graph — blends the masked charge layer in/out. </summary>
	[Property, Group( "Anim graph charge pose" )] public string ChargeWeightParamName { get; set; } = "throw_charge_weight";
	/// <summary> Cycle position when charge bar is empty. Usually 0. </summary>
	[Property, Group( "Anim graph charge pose" )] public float ChargeWindupCycleStart { get; set; } = 0f;
	/// <summary> Cycle position when charge bar is full. Lower if wind-up motion only uses the start of the clip (e.g. 0.25). </summary>
	[Property, Group( "Anim graph charge pose" )] public float ChargeWindupCycleEnd { get; set; } = 0.3f;
	[Property, Group( "Anim graph charge pose" )] public float ChargeWeightBlendInSeconds { get; set; } = 0.12f;
	[Property, Group( "Anim graph charge pose" )] public float ChargeWeightBlendOutSeconds { get; set; } = 0.15f;
	/// <summary> RMB / dodge cancel — quick ease off the masked wind-up back to idle hold. </summary>
	[Property, Group( "Anim graph charge pose" )] public float ChargeCancelBlendOutSeconds { get; set; } = 0.1f;

	private BallGrab ballGrab;
	private BallThrow ballThrow;
	private PlayerTackle playerTackle;
	private PlayerTeam playerTeam;
	private bool wasShowingHoldPose;
	private bool wasChargingThrow;
	private float throwPoseUntil;
	private float playbackRateRestoreUntil;
	private float savedPlaybackRate = 1f;
	private float chargePoseWeight;
	private float lastChargeCycle;
	private bool isChargePoseBlendingOut;
	private bool loggedMissingAnimGraph;

	protected override void OnStart()
	{
		ballGrab = Components.Get<BallGrab>();
		ballThrow = Components.Get<BallThrow>();
		playerTackle = Components.Get<PlayerTackle>();
		playerTeam = Components.Get<PlayerTeam>();
		ResolveBodyRenderer();
		EnsureCustomBodyModel();
	}

	/// <summary> Clothing <c>ApplyAsync</c> resets Body to default citizen — call again after cosmetics. </summary>
	public void EnsureCustomBodyModel()
	{
		if ( string.IsNullOrWhiteSpace( CustomBodyModelPath ) )
			return;

		if ( !TryGetBodyRenderer( out var renderer ) )
			return;

		var customModel = Model.Load( CustomBodyModelPath );
		if ( !customModel.IsValid() )
			return;

		if ( renderer.Model != customModel )
			renderer.Model = customModel;

		EnsureCustomAnimGraph( renderer );
	}

	/// <summary> Cosmetics rebuilds the scene model on the default graph — force re-assign every ensure. </summary>
	private void EnsureCustomAnimGraph( SkinnedModelRenderer renderer )
	{
		if ( !UseAnimGraphChargePose || string.IsNullOrWhiteSpace( CustomAnimGraphPath ) )
			return;

		var customGraph = AnimationGraph.Load( CustomAnimGraphPath );
		if ( customGraph is null )
		{
			if ( !loggedMissingAnimGraph )
			{
				Log.Warning( $"[PlayerBallHoldAnim] Custom anim graph not found at '{CustomAnimGraphPath}' — see CITIZEN_ANIMATION_WORKFLOW.md." );
				loggedMissingAnimGraph = true;
			}
			return;
		}

		renderer.AnimationGraph = null;
		renderer.AnimationGraph = customGraph;
		loggedMissingAnimGraph = false;
	}

	private bool wasKnockedDown;

	protected override void OnUpdate()
	{
		if ( !TryGetBodyRenderer( out var renderer ) )
			return;

		RestorePlaybackRateIfNeeded( renderer );

		var knockedDown = playerTackle?.IsKnockedDown == true;
		if ( wasKnockedDown && !knockedDown )
			ClearHoldPoseAfterKnockdown();

		wasKnockedDown = knockedDown;

		if ( ShouldSkipHoldAnim() )
		{
			if ( wasShowingHoldPose || chargePoseWeight > 0.001f )
				ClearHoldAndThrowPose( renderer );

			ResetGraphChargePose( renderer );
			wasShowingHoldPose = false;
			wasChargingThrow = false;
			isChargePoseBlendingOut = false;
			throwPoseUntil = 0f;
			return;
		}

		var holding = ballGrab.IsHolding;
		var chargingThrow = ballThrow?.IsChargingThrow == true;
		var inThrowPose = Time.Now < throwPoseUntil;
		var showHoldPose = holding || inThrowPose;

		if ( UseAnimGraphChargePose )
		{
			if ( chargingThrow )
				UpdateGraphChargePose( renderer, true, ballThrow.GetThrowChargeLerp() );
			else if ( isChargePoseBlendingOut )
			{
				UpdateGraphChargePose( renderer, false, 0f, ChargeCancelBlendOutSeconds );
				if ( chargePoseWeight <= 0.001f )
					isChargePoseBlendingOut = false;
			}
			else if ( wasChargingThrow )
				UpdateGraphChargePose( renderer, false, 0f );
		}

		wasChargingThrow = chargingThrow;

		if ( showHoldPose )
			ApplyHoldPose( renderer );
		else if ( wasShowingHoldPose || chargePoseWeight > 0.001f )
			ClearHoldAndThrowPose( renderer );

		wasShowingHoldPose = showHoldPose;
	}

	public void ClearHoldPoseAfterKnockdown()
	{
		if ( !TryGetBodyRenderer( out var renderer ) )
			return;

		ClearHoldAndThrowPose( renderer );
		wasShowingHoldPose = false;
		wasChargingThrow = false;
		isChargePoseBlendingOut = false;
		throwPoseUntil = 0f;
		playbackRateRestoreUntil = 0f;
	}

	/// <summary> Owner calls on throw button release; broadcasts so all clients play the throw additive. </summary>
	public void NotifyThrowReleased()
	{
		if ( !Network.IsOwner )
			return;

		var throwPoseEndTime = Time.Now + ThrowPoseHoldSeconds;
		PlayThrowReleaseAnim( throwPoseEndTime );
		PlayThrowReleaseAnimRpc( throwPoseEndTime );
	}

	/// <summary> Owner calls when throw charge is cancelled (RMB, dodge, etc.) — ease back to idle hold pose. </summary>
	public void NotifyThrowChargeCancelled()
	{
		if ( !Network.IsOwner )
			return;

		ApplyThrowChargeCancelledPose();
		NotifyThrowChargeCancelledRpc();
	}

	[Rpc.Broadcast]
	private void NotifyThrowChargeCancelledRpc()
	{
		if ( Network.IsOwner )
			return;

		ApplyThrowChargeCancelledPose();
	}

	void ApplyThrowChargeCancelledPose()
	{
		if ( !TryGetBodyRenderer( out var renderer ) )
			return;

		if ( playerTackle?.IsKnockedDown == true )
			return;

		if ( chargePoseWeight <= 0.001f )
		{
			ResetGraphChargePose( renderer );
			isChargePoseBlendingOut = false;
		}
		else
		{
			isChargePoseBlendingOut = true;
		}

		wasChargingThrow = false;

		if ( ballGrab?.IsHolding == true )
		{
			ApplyHoldPose( renderer );
			wasShowingHoldPose = true;
		}
		else
		{
			wasShowingHoldPose = false;
		}
	}

	[Rpc.Broadcast]
	private void PlayThrowReleaseAnimRpc( float throwPoseEndTime )
	{
		if ( Network.IsOwner )
			return;

		PlayThrowReleaseAnim( throwPoseEndTime );
	}

	private void PlayThrowReleaseAnim( float throwPoseEndTime )
	{
		if ( !TryGetBodyRenderer( out var renderer ) )
			return;

		if ( playerTackle?.IsKnockedDown == true )
			return;

		ResetGraphChargePose( renderer );
		throwPoseUntil = throwPoseEndTime;
		ApplyHoldPose( renderer );
		renderer.Set( "holdtype_attack", ThrowAttackStrong );
		renderer.Set( "b_attack", true );
		ApplyThrowPlaybackRate( renderer );
	}

	private void UpdateGraphChargePose( SkinnedModelRenderer renderer, bool charging, float chargeLerp, float? blendOutSecondsOverride = null )
	{
		var previousWeight = chargePoseWeight;
		var targetWeight = charging ? 1f : 0f;
		var blendSeconds = charging
			? ChargeWeightBlendInSeconds
			: (blendOutSecondsOverride ?? ChargeWeightBlendOutSeconds);
		chargePoseWeight = blendSeconds <= 0.001f
			? targetWeight
			: chargePoseWeight.Approach( targetWeight, Time.Delta / blendSeconds );

		if ( !charging && previousWeight <= 0f )
			return;

		if ( charging )
		{
			lastChargeCycle = ChargeWindupCycleStart.LerpTo( ChargeWindupCycleEnd, chargeLerp.Clamp( 0f, 1f ) );
			renderer.Set( ChargeCycleParamName, lastChargeCycle );
		}
		else
		{
			var unwindT = 1f - chargePoseWeight.Clamp( 0f, 1f );
			var cycle = lastChargeCycle.LerpTo( ChargeWindupCycleStart, unwindT );
			renderer.Set( ChargeCycleParamName, cycle );
		}

		renderer.Set( ChargeWeightParamName, chargePoseWeight );
	}

	private void ResetGraphChargePose( SkinnedModelRenderer renderer )
	{
		if ( !UseAnimGraphChargePose )
			return;

		chargePoseWeight = 0f;
		lastChargeCycle = ChargeWindupCycleStart;
		isChargePoseBlendingOut = false;
		renderer.Set( ChargeWeightParamName, 0f );
		renderer.Set( ChargeCycleParamName, ChargeWindupCycleStart );
	}

	private void ApplyThrowPlaybackRate( SkinnedModelRenderer renderer )
	{
		if ( ThrowPlaybackRate <= 0f || Math.Abs( ThrowPlaybackRate - 1f ) < 0.001f )
			return;

		savedPlaybackRate = renderer.PlaybackRate;
		renderer.PlaybackRate = ThrowPlaybackRate;
		playbackRateRestoreUntil = throwPoseUntil;
	}

	private void RestorePlaybackRateIfNeeded( SkinnedModelRenderer renderer )
	{
		if ( playbackRateRestoreUntil <= 0f || Time.Now < playbackRateRestoreUntil )
			return;

		renderer.PlaybackRate = savedPlaybackRate;
		playbackRateRestoreUntil = 0f;
	}

	private bool ShouldSkipHoldAnim()
	{
		if ( playerTackle?.IsKnockedDown == true )
			return true;

		if ( Components.Get<BlitzConnectPoseFreeze>() is { IsBodyPoseFrozen: true } )
			return true;

		if ( playerTeam is not null && !playerTeam.IsMatchGameplayInputAllowed )
			return true;

		return false;
	}

	private void ApplyHoldPose( SkinnedModelRenderer renderer )
	{
		renderer.Set( "holdtype", HoldTypeHoldItem );
		renderer.Set( "holdtype_handedness", HandednessRight );
		renderer.Set( "holdtype_pose", IdleHoldTypePose );
		renderer.Set( "holdtype_pose_hand", IdleHoldPoseHand );
	}

	private void ClearHoldAndThrowPose( SkinnedModelRenderer renderer )
	{
		ResetGraphChargePose( renderer );
		renderer.Set( "holdtype", 0 );
		renderer.Set( "holdtype_handedness", 0 );
		renderer.Set( "holdtype_pose", 0f );
		renderer.Set( "holdtype_pose_hand", 0f );
		renderer.Set( "holdtype_attack", 0f );
		renderer.Set( "b_attack", false );
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
