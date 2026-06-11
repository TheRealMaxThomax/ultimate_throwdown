using System;
using Sandbox;

/// <summary>
/// Drives citizen human <c>holditem</c> RH hold pose while <see cref="BallGrab.IsHolding"/> and
/// <c>HoldItem_RH_Throw_Strong</c> on intentional throw release via <c>b_attack</c>.
/// Charge wind-up while <see cref="BallThrow.IsChargingThrow"/>: preferred route is the forked citizen
/// animgraph with a masked right-arm layer (<see cref="UseAnimGraphChargePose"/> — body keeps locomotion/look-at);
/// legacy fallbacks are full-body direct playback (<see cref="EnableCustomChargePose"/>, frozen body) and the
/// built-in holdtype ramp. Built-in throw clip still plays on release.
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

	[Property, Group( "Built-in charge pose" )] public bool EnableBuiltInChargeWindup { get; set; } = false;
	/// <summary> <c>holdtype_pose</c> while holding normally. Citizen holditem uses this through the graph, so idle/legs/look still work. </summary>
	[Property, Group( "Built-in charge pose" )] public float IdleHoldTypePose { get; set; } = 0f;
	[Property, Group( "Built-in charge pose" )] public float ChargeHoldTypePoseStart { get; set; } = 0f;
	[Property, Group( "Built-in charge pose" )] public float ChargeHoldTypePoseEnd { get; set; } = 3f;
	[Property, Group( "Built-in charge pose" )] public float ChargeHoldPoseHandStart { get; set; } = 0.1f;
	[Property, Group( "Built-in charge pose" )] public float ChargeHoldPoseHandEnd { get; set; } = 0.1f;

	/// <summary> Preferred charge route: masked layer in a forked citizen animgraph — body keeps locomotion/look-at while the arm winds up. Needs the custom graph wired (see CITIZEN_ANIMATION_WORKFLOW.md). </summary>
	[Property, Group( "Anim graph charge pose" )] public bool UseAnimGraphChargePose { get; set; } = true;
	/// <summary> Forked citizen graph with the masked charge layer. Re-applied after cosmetics (like the Body model). Empty or missing = no override, falls back to other charge modes. </summary>
	[Property, Group( "Anim graph charge pose" )] public string CustomAnimGraphPath { get; set; } = "animation/utd_citizen_human_m.vanmgrph";
	/// <summary> Float parameter (0–1) in the custom graph that scrubs the wind-up via a Cycle Control node. </summary>
	[Property, Group( "Anim graph charge pose" )] public string ChargeCycleParamName { get; set; } = "throw_charge";
	/// <summary> Float parameter (0–1) in the custom graph that blends the masked charge layer in/out. </summary>
	[Property, Group( "Anim graph charge pose" )] public string ChargeWeightParamName { get; set; } = "throw_charge_weight";
	[Property, Group( "Anim graph charge pose" )] public float ChargeWeightBlendInSeconds { get; set; } = 0.12f;
	[Property, Group( "Anim graph charge pose" )] public float ChargeWeightBlendOutSeconds { get; set; } = 0.15f;

	[Property, Group( "Custom charge pose" )] public bool EnableCustomChargePose { get; set; } = true;
	/// <summary> Extension <c>.vmdl</c> with custom sequences. Re-applied after cosmetics (clothing resets Body to citizen_human). </summary>
	[Property, Group( "Custom charge pose" )] public string CustomBodyModelPath { get; set; } = "animation/utd_citizen_human_throw.vmdl";
	[Property, Group( "Custom charge pose" )] public string HoldReadySequenceName { get; set; } = "hold_ready";
	[Property, Group( "Custom charge pose" )] public string ChargeMinSequenceName { get; set; } = "charge_min";
	[Property, Group( "Custom charge pose" )] public string ChargeMaxSequenceName { get; set; } = "charge_max";
	/// <summary> Optional single wind-up sequence (recommended). When present on the Body model, charge lerp scrubs 0→1 across one clip — no phase pops. </summary>
	[Property, Group( "Custom charge pose" )] public string ChargeWindupSequenceName { get; set; } = "throw_windup";
	/// <summary> End of <see cref="HoldReadySequenceName"/> on the throw charge bar (0–1). Used only when <see cref="ChargeWindupSequenceName"/> is missing. </summary>
	[Property, Group( "Custom charge pose" )] public float ChargeHoldReadyPhaseEnd { get; set; } = 0.2f;
	/// <summary> End of <see cref="ChargeMinSequenceName"/> on the throw charge bar (0–1). Used only when <see cref="ChargeWindupSequenceName"/> is missing. </summary>
	[Property, Group( "Custom charge pose" )] public float ChargeMinPhaseEnd { get; set; } = 0.5f;
	/// <summary> Play <c>*_delta</c> sequences (ModelDoc AnimSubtract). Off by default — deltas work in holdtype Add nodes, not via direct-playback scrub (pancake). </summary>
	[Property, Group( "Custom charge pose" )] public bool UseDeltaChargeSequences { get; set; } = false;
	[Property, Group( "Custom charge pose" )] public string ChargeSequenceDeltaSuffix { get; set; } = "_delta";

	private BallGrab ballGrab;
	private BallThrow ballThrow;
	private PlayerTackle playerTackle;
	private PlayerTeam playerTeam;
	private bool wasShowingHoldPose;
	private bool wasChargingThrow;
	private bool customChargePoseActive;
	private bool useSingleChargeWindupSequence;
	private string activeChargeSequence;
	private bool loggedMissingChargeSequences;
	private bool chargeSequenceDurationsCached;
	private float holdReadyDuration = 1f;
	private float chargeMinDuration = 1f;
	private float chargeMaxDuration = 1f;
	private float throwPoseUntil;
	private float playbackRateRestoreUntil;
	private float savedPlaybackRate = 1f;
	private float chargePoseWeight;
	private bool customAnimGraphApplied;

	protected override void OnStart()
	{
		ballGrab = Components.Get<BallGrab>();
		ballThrow = Components.Get<BallThrow>();
		playerTackle = Components.Get<PlayerTackle>();
		playerTeam = Components.Get<PlayerTeam>();
		ResolveBodyRenderer();
		ApplyThrowWindupDefaults();
		EnsureCustomBodyModel();
	}

	private void ApplyThrowWindupDefaults()
	{
		if ( string.IsNullOrWhiteSpace( ChargeWindupSequenceName )
			|| string.Equals( ChargeWindupSequenceName, "charge_windup", StringComparison.OrdinalIgnoreCase ) )
		{
			ChargeWindupSequenceName = "throw_windup";
		}

		if ( string.Equals( ChargeWindupSequenceName, "throw_windup", StringComparison.OrdinalIgnoreCase ) )
		{
			EnableCustomChargePose = true;
			EnableBuiltInChargeWindup = false;
		}
	}

	/// <summary> Clothing <c>ApplyAsync</c> resets Body to default citizen — call again after cosmetics. </summary>
	public void EnsureCustomBodyModel()
	{
		if ( !EnableCustomChargePose || string.IsNullOrWhiteSpace( CustomBodyModelPath ) )
			return;

		if ( !TryGetBodyRenderer( out var renderer ) )
			return;

		var customModel = Model.Load( CustomBodyModelPath );
		if ( !customModel.IsValid() )
			return;

		if ( renderer.Model != customModel )
		{
			renderer.Model = customModel;
			loggedMissingChargeSequences = false;
		}

		EnsureCustomAnimGraph( renderer );
	}

	/// <summary> Applies the forked citizen graph (masked charge layer). Cosmetics can reset the renderer, so this runs on spawn and after <c>ClothingContainer.ApplyAsync</c>. </summary>
	private void EnsureCustomAnimGraph( SkinnedModelRenderer renderer )
	{
		if ( !UseAnimGraphChargePose || string.IsNullOrWhiteSpace( CustomAnimGraphPath ) )
			return;

		var customGraph = AnimationGraph.Load( CustomAnimGraphPath );
		if ( customGraph is null )
		{
			if ( !customAnimGraphApplied )
				Log.Warning( $"[PlayerBallHoldAnim] Custom anim graph not found at '{CustomAnimGraphPath}' — charge wind-up will use fallback pose. Create the graph (see CITIZEN_ANIMATION_WORKFLOW.md) or clear CustomAnimGraphPath." );
			customAnimGraphApplied = true;
			return;
		}

		// Cosmetics ApplyAsync can rebuild the SceneModel on the model's default graph while the component
		// property still reports the override — always force re-assignment so the live instance matches.
		renderer.AnimationGraph = null;
		renderer.AnimationGraph = customGraph;
		Log.Info( $"[PlayerBallHoldAnim] Applied custom anim graph '{customGraph.ResourcePath}' to '{GameObject.Name}'." );
		customAnimGraphApplied = true;
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

			CancelCustomChargePose( renderer );
			ResetGraphChargePose( renderer );
			wasShowingHoldPose = false;
			wasChargingThrow = false;
			throwPoseUntil = 0f;
			return;
		}

		var holding = ballGrab.IsHolding;
		var chargingThrow = ballThrow?.IsChargingThrow == true;
		var inThrowPose = Time.Now < throwPoseUntil;
		var showHoldPose = holding || inThrowPose;
		float? builtInChargeLerp = null;

		if ( chargingThrow )
		{
			var chargeLerp = ballThrow.GetThrowChargeLerp();
			if ( UseAnimGraphChargePose )
			{
				UpdateGraphChargePose( renderer, true, chargeLerp );
			}
			else if ( EnableCustomChargePose )
			{
				UpdateCustomChargePose( renderer, chargeLerp, !wasChargingThrow );
			}
			else if ( EnableBuiltInChargeWindup )
			{
				builtInChargeLerp = chargeLerp.Clamp( 0f, 1f );
				if ( wasChargingThrow )
					CancelCustomChargePose( renderer );
			}
		}
		else
		{
			if ( wasChargingThrow )
			{
				CancelCustomChargePose( renderer );
				chargeSequenceDurationsCached = false;
			}

			if ( UseAnimGraphChargePose )
				UpdateGraphChargePose( renderer, false, 0f );
		}

		wasChargingThrow = chargingThrow;

		if ( showHoldPose )
			ApplyHoldPose( renderer, builtInChargeLerp );
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

		CancelCustomChargePose( renderer );
		throwPoseUntil = Time.Now + ThrowPoseHoldSeconds;
		ApplyHoldPose( renderer );
		renderer.Set( "holdtype_attack", ThrowAttackStrong );
		renderer.Set( "b_attack", true );
		ApplyThrowPlaybackRate( renderer );
	}

	/// <summary>
	/// Drives the masked charge layer in the forked citizen graph: <see cref="ChargeCycleParamName"/> scrubs the
	/// wind-up sequence via a Cycle Control node, <see cref="ChargeWeightParamName"/> blends the layer in/out so the
	/// body keeps locomotion and look-at the whole time. Runs on every client (remotes read synced charge lerp).
	/// </summary>
	private void UpdateGraphChargePose( SkinnedModelRenderer renderer, bool charging, float chargeLerp )
	{
		var previousWeight = chargePoseWeight;
		var targetWeight = charging ? 1f : 0f;
		var blendSeconds = charging ? ChargeWeightBlendInSeconds : ChargeWeightBlendOutSeconds;
		chargePoseWeight = blendSeconds <= 0.001f
			? targetWeight
			: chargePoseWeight.Approach( targetWeight, Time.Delta / blendSeconds );

		if ( !charging && previousWeight <= 0f )
			return;

		if ( charging )
			renderer.Set( ChargeCycleParamName, chargeLerp.Clamp( 0f, 1f ) );

		renderer.Set( ChargeWeightParamName, chargePoseWeight );
	}

	private void ResetGraphChargePose( SkinnedModelRenderer renderer )
	{
		if ( !UseAnimGraphChargePose || chargePoseWeight <= 0f )
			return;

		chargePoseWeight = 0f;
		renderer.Set( ChargeWeightParamName, 0f );
	}

	private void UpdateCustomChargePose( SkinnedModelRenderer renderer, float chargeLerp, bool chargeJustStarted )
	{
		if ( !EnableCustomChargePose )
			return;

		if ( renderer.SceneModel is not { } sceneModel )
			return;

		var directPlayback = sceneModel.DirectPlayback;
		if ( directPlayback is null )
			return;

		if ( chargeJustStarted )
		{
			activeChargeSequence = null;
			chargeSequenceDurationsCached = false;
			useSingleChargeWindupSequence = TryResolveSingleChargeWindupSequence( directPlayback, out _ );
		}

		if ( useSingleChargeWindupSequence )
		{
			if ( !TryResolveSingleChargeWindupSequence( directPlayback, out var windupSequenceName ) )
				return;

			if ( !TryPlayChargeSequence( directPlayback, windupSequenceName ) )
				return;

			customChargePoseActive = true;
			ScrubDirectPlayback( directPlayback, chargeLerp.Clamp( 0f, 1f ) );
			return;
		}

		EnsureChargeSequenceDurationsCached( directPlayback );

		if ( !TryGetChargeWindupSample( chargeLerp, out var sequenceName, out var segmentLerp ) )
			return;

		if ( !TryPlayChargeSequence( directPlayback, sequenceName ) )
			return;

		customChargePoseActive = true;
		ScrubDirectPlayback( directPlayback, segmentLerp );
	}

	private bool TryResolveSingleChargeWindupSequence( AnimGraphDirectPlayback directPlayback, out string sequenceName )
	{
		sequenceName = null;
		if ( string.IsNullOrWhiteSpace( ChargeWindupSequenceName ) )
			return false;

		if ( !TryResolveChargePlaybackSequence( directPlayback, ChargeWindupSequenceName, out sequenceName ) )
			return false;

		return true;
	}

	/// <summary> Prefer <c>name_delta</c> (additive / AnimSubtract) when present — same pattern as built-in <c>HoldItem_RH_Throw_*_delta</c>. </summary>
	private bool TryResolveChargePlaybackSequence( AnimGraphDirectPlayback directPlayback, string baseSequenceName, out string playbackSequenceName )
	{
		playbackSequenceName = null;
		if ( string.IsNullOrWhiteSpace( baseSequenceName ) )
			return false;

		if ( UseDeltaChargeSequences )
		{
			var deltaName = baseSequenceName + ChargeSequenceDeltaSuffix;
			if ( HasChargeSequence( directPlayback, deltaName ) )
			{
				playbackSequenceName = deltaName;
				return true;
			}
		}

		if ( !HasChargeSequence( directPlayback, baseSequenceName ) )
			return false;

		playbackSequenceName = baseSequenceName;
		return true;
	}

	private void EnsureChargeSequenceDurationsCached( AnimGraphDirectPlayback directPlayback )
	{
		if ( chargeSequenceDurationsCached )
			return;

		holdReadyDuration = MeasureSequenceDuration( directPlayback, HoldReadySequenceName );
		chargeMinDuration = MeasureSequenceDuration( directPlayback, ChargeMinSequenceName );
		chargeMaxDuration = MeasureSequenceDuration( directPlayback, ChargeMaxSequenceName );
		chargeSequenceDurationsCached = true;
	}

	private float MeasureSequenceDuration( AnimGraphDirectPlayback directPlayback, string baseSequenceName )
	{
		if ( string.IsNullOrWhiteSpace( baseSequenceName ) )
			return 1f;

		if ( !TryResolveChargePlaybackSequence( directPlayback, baseSequenceName, out var sequenceName ) )
			return 1f;

		var sequences = directPlayback.Sequences;
		if ( sequences is null )
			return 1f;

		for ( var i = 0; i < sequences.Count; i++ )
		{
			if ( !string.Equals( sequences[i], sequenceName, StringComparison.OrdinalIgnoreCase ) )
				continue;

			directPlayback.Play( sequenceName );
			var duration = directPlayback.Duration;
			if ( duration > 0.001f )
				return duration;

			return 1f;
		}

		return 1f;
	}

	/// <summary> Maps throw charge lerp across hold_ready → charge_min → charge_max, weighted by clip length. </summary>
	private bool TryGetChargeWindupSample( float chargeLerp, out string sequenceName, out float segmentLerp )
	{
		sequenceName = null;
		segmentLerp = 0f;

		var holdEnd = ChargeHoldReadyPhaseEnd.Clamp( 0.05f, 0.95f );
		var minEnd = ChargeMinPhaseEnd.Clamp( holdEnd + 0.05f, 0.99f );
		var clampedLerp = chargeLerp.Clamp( 0f, 1f );

		var phase0Weight = holdEnd * holdReadyDuration;
		var phase1Weight = (minEnd - holdEnd) * chargeMinDuration;
		var phase2Weight = (1f - minEnd) * chargeMaxDuration;
		var totalWeight = phase0Weight + phase1Weight + phase2Weight;

		if ( totalWeight > 0.001f )
		{
			var timeline = clampedLerp * totalWeight;

			if ( timeline <= phase0Weight )
			{
				sequenceName = HoldReadySequenceName;
				segmentLerp = phase0Weight > 0.001f ? timeline / phase0Weight : 0f;
				return true;
			}

			timeline -= phase0Weight;
			if ( timeline <= phase1Weight )
			{
				sequenceName = ChargeMinSequenceName;
				segmentLerp = phase1Weight > 0.001f ? timeline / phase1Weight : 0f;
				return true;
			}

			sequenceName = ChargeMaxSequenceName;
			segmentLerp = phase2Weight > 0.001f ? timeline / phase2Weight : 1f;
			return true;
		}

		if ( clampedLerp <= holdEnd )
		{
			sequenceName = HoldReadySequenceName;
			segmentLerp = clampedLerp / holdEnd;
			return true;
		}

		if ( clampedLerp <= minEnd )
		{
			sequenceName = ChargeMinSequenceName;
			segmentLerp = (clampedLerp - holdEnd) / (minEnd - holdEnd);
			return true;
		}

		sequenceName = ChargeMaxSequenceName;
		segmentLerp = (clampedLerp - minEnd) / (1f - minEnd);
		return true;
	}

	private static void ScrubDirectPlayback( AnimGraphDirectPlayback directPlayback, float segmentLerp )
	{
		var duration = directPlayback.Duration;
		if ( duration <= 0.001f )
			return;

		directPlayback.StartTime = Time.Now - (segmentLerp.Clamp( 0f, 1f ) * duration);
	}

	private bool TryPlayChargeSequence( AnimGraphDirectPlayback directPlayback, string baseSequenceName )
	{
		if ( !TryResolveChargePlaybackSequence( directPlayback, baseSequenceName, out var sequenceName ) )
		{
			LogMissingChargeSequencesOnce( directPlayback );
			return false;
		}

		if ( activeChargeSequence == sequenceName && customChargePoseActive )
			return true;

		directPlayback.Play( sequenceName );
		activeChargeSequence = sequenceName;
		return true;
	}

	private bool HasChargeSequence( AnimGraphDirectPlayback directPlayback, string sequenceName )
	{
		var sequences = directPlayback.Sequences;
		if ( sequences is null || sequences.Count == 0 )
			return false;

		for ( var i = 0; i < sequences.Count; i++ )
		{
			if ( string.Equals( sequences[i], sequenceName, StringComparison.OrdinalIgnoreCase ) )
				return true;
		}

		return false;
	}

	private void LogMissingChargeSequencesOnce( AnimGraphDirectPlayback directPlayback )
	{
		if ( loggedMissingChargeSequences )
			return;

		loggedMissingChargeSequences = true;
		Log.Warning( $"[PlayerBallHoldAnim] Custom charge sequences not found on {GameObject.Name}. Import FBXs into the Body model in ModelDoc (see CITIZEN_ANIMATION_WORKFLOW.md). Available: {FormatSequenceList( directPlayback )}" );
	}

	private static string FormatSequenceList( AnimGraphDirectPlayback directPlayback )
	{
		var sequences = directPlayback.Sequences;
		if ( sequences is null || sequences.Count == 0 )
			return "(none)";

		return string.Join( ", ", sequences );
	}

	private void CancelCustomChargePose( SkinnedModelRenderer renderer )
	{
		if ( !customChargePoseActive )
			return;

		renderer.SceneModel?.DirectPlayback?.Cancel();
		customChargePoseActive = false;
		activeChargeSequence = null;
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

	private void ApplyHoldPose( SkinnedModelRenderer renderer, float? chargeLerp = null )
	{
		renderer.Set( "holdtype", HoldTypeHoldItem );
		renderer.Set( "holdtype_handedness", HandednessRight );
		if ( chargeLerp.HasValue && EnableBuiltInChargeWindup )
		{
			var t = chargeLerp.Value.Clamp( 0f, 1f );
			renderer.Set( "holdtype_pose", ChargeHoldTypePoseStart.LerpTo( ChargeHoldTypePoseEnd, t ) );
			renderer.Set( "holdtype_pose_hand", ChargeHoldPoseHandStart.LerpTo( ChargeHoldPoseHandEnd, t ) );
			return;
		}

		renderer.Set( "holdtype_pose", IdleHoldTypePose );
		renderer.Set( "holdtype_pose_hand", IdleHoldPoseHand );
	}

	private void ClearHoldPose( SkinnedModelRenderer renderer )
	{
		renderer.Set( "holdtype", 0 );
		renderer.Set( "holdtype_pose", 0f );
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
