using System;
using Sandbox;

/// <summary>
/// Drives citizen human <c>holditem</c> RH hold pose while <see cref="BallGrab.IsHolding"/> and
/// <c>HoldItem_RH_Throw_Strong</c> on intentional throw release via <c>b_attack</c>.
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
	/// <summary> <c>holdtype_attack</c> blend — 0 = medium throw, 1 = strong/far throw. </summary>
	[Property] public float ThrowAttackStrong { get; set; } = 0f;
	/// <summary> Keep <c>holditem</c> pose active after release so the throw clip is not cut off. </summary>
	[Property] public float ThrowPoseHoldSeconds { get; set; } = 0.9f;
	/// <summary> Anim graph playback rate during <see cref="ThrowPoseHoldSeconds"/> (1 = default speed). </summary>
	[Property] public float ThrowPlaybackRate { get; set; } = 0.7f;

	private BallGrab ballGrab;
	private PlayerTackle playerTackle;
	private PlayerTeam playerTeam;
	private bool wasShowingHoldPose;
	private float throwPoseUntil;
	private float playbackRateRestoreUntil;
	private float savedPlaybackRate = 1f;

	protected override void OnStart()
	{
		ballGrab = Components.Get<BallGrab>();
		playerTackle = Components.Get<PlayerTackle>();
		playerTeam = Components.Get<PlayerTeam>();
		ResolveBodyRenderer();
	}

	protected override void OnUpdate()
	{
		if ( !TryGetBodyRenderer( out var renderer ) )
			return;

		RestorePlaybackRateIfNeeded( renderer );

		if ( ShouldSkipHoldAnim() )
		{
			if ( wasShowingHoldPose )
				ClearHoldPose( renderer );

			wasShowingHoldPose = false;
			throwPoseUntil = 0f;
			return;
		}

		var holding = ballGrab.IsHolding;
		var inThrowPose = Time.Now < throwPoseUntil;
		var showHoldPose = holding || inThrowPose;

		if ( showHoldPose )
			ApplyHoldPose( renderer );
		else if ( wasShowingHoldPose )
			ClearHoldPose( renderer );

		wasShowingHoldPose = showHoldPose;
	}

	/// <summary> Owner calls on throw button release; broadcasts so all clients play the throw additive. </summary>
	public void NotifyThrowReleased()
	{
		if ( !Network.IsOwner )
			return;

		PlayThrowReleaseAnim();
		PlayThrowReleaseAnimRpc();
	}

	[Rpc.Broadcast]
	private void PlayThrowReleaseAnimRpc()
	{
		if ( Network.IsOwner )
			return;

		PlayThrowReleaseAnim();
	}

	private void PlayThrowReleaseAnim()
	{
		if ( !TryGetBodyRenderer( out var renderer ) )
			return;

		if ( playerTackle?.IsRagdolled == true )
			return;

		throwPoseUntil = Time.Now + ThrowPoseHoldSeconds;
		ApplyHoldPose( renderer );
		renderer.Set( "holdtype_attack", ThrowAttackStrong );
		renderer.Set( "b_attack", true );
		ApplyThrowPlaybackRate( renderer );
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
		if ( playerTackle?.IsRagdolled == true )
			return true;

		if ( playerTeam is not null && !playerTeam.IsMatchGameplayInputAllowed )
			return true;

		return false;
	}

	private void ApplyHoldPose( SkinnedModelRenderer renderer )
	{
		renderer.Set( "holdtype", HoldTypeHoldItem );
		renderer.Set( "holdtype_handedness", HandednessRight );
		renderer.Set( "holdtype_pose_hand", IdleHoldPoseHand );
	}

	private void ClearHoldPose( SkinnedModelRenderer renderer )
	{
		renderer.Set( "holdtype", 0 );
		renderer.Set( "holdtype_pose_hand", 0f );
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
