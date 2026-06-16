using Sandbox;
using System;

public enum MovementRampTier : byte
{
	Walk,
	Sprint,
	Charge,
}

/// <summary>
/// Walk / sprint / charge tiers: when <see cref="PlayerClass.CurrentClass"/> is set, tier speeds and ramp durations read from that <see cref="ClassData"/> asset; “Fallback” inspector fields apply only if no class is assigned (or for quick tests).
/// </summary>
[Order( -100 )]
public sealed class CatchUpSpeedBoost : Component
{
	/// <summary> Must match Input action name (<c>Input.config</c> uses <c>Forward</c>; scene uses lowercase <c>forward</c>).</summary>
	[Property, Group( "Fallback (no class .cdata)" )] public string ForwardAction { get; set; } = "forward";
	[Property, Group( "Fallback (no class .cdata)" )] public string BackwardAction { get; set; } = "Backward";
	[Property, Group( "Fallback (no class .cdata)" )] public float StartMoveSpeed { get; set; } = 140f;
	[Property, Group( "Fallback (no class .cdata)" )] public float SprintMoveSpeed { get; set; } = 220f;
	[Property, Group( "Fallback (no class .cdata)" )] public float CatchUpMoveSpeed { get; set; } = 320f;
	[Property, Group( "Fallback (no class .cdata)" )] public float TimeToSprintSpeed { get; set; } = 2.0f;
	[Property, Group( "Fallback (no class .cdata)" )] public float TimeToCatchUpSpeed { get; set; } = 4.0f;
	[Property] public float MinForwardInput { get; set; } = 0.1f;

	/// <summary> Extra fraction applied to charge look (both compensation and legacy modes). </summary>
	[Property] public float ChargeLookSensitivityMultiplier { get; set; } = 1f;

	/// <summary> If true: at charge, look uses (ChargeUnifiedLookScale / Preferences.Sensitivity) so the same physical mouse move gives ~the same turn at low or high menu sensitivity. If false: uses PlayerController.LookSensitivity snapshot × multiplier (old behaviour). </summary>
	[Property] public bool ChargeLookCompensatePreferencesSensitivity { get; set; } = true;

	/// <summary> Charge look strength when compensating: effective PlayerController.LookSensitivity ≈ (this / Preferences.Sensitivity) × ChargeLookSensitivityMultiplier.Tune so charge feels right at your reference menu sensitivity (e.g. 1). </summary>
	[Property] public float ChargeUnifiedLookScale { get; set; } = 0.6f;

	/// <summary> If &gt; 0: at charge, effective look cannot exceed this (same units as PlayerController.LookSensitivity). Caps high-sensitivity players; 0 = no cap. </summary>
	[Property] public float ChargeLookSensitivityMax { get; set; } = 0.2f;

	/// <summary> If &gt; 0: at charge, applied yaw chases the controller's target at most this rate (deg/sec). Flick input is not discarded — it catches up over frames. 0 = off. </summary>
	[Property] public float ChargeYawMaxDegreesPerSecond { get; set; } = 130f;

	private BallGrab ballGrab;
	private BallThrow ballThrow;
	private PlayerController playerController;
	private PlayerClass playerClass;
	private PlayerTackle playerTackle;
	private PlayerDodge playerDodge;
	private SpeedsterSpeedBlitzUlt speedBlitzUlt;
	private int dodgeRampApplySeqHandled;
	private int tackleRampStripSeqHandled;
	private int forceWalkRampSeqHandled;
	private float forwardMoveTime;
	private float nonHoldingSprintTime;
	private bool wasKnockedDown;

	/// <summary>Eased toward tier target each frame — <see cref="ClassData.MomentumMultiplier"/> slows the cap blend slightly when &gt;1.</summary>
	private float smoothedMoveSpeedCap;

	/// <summary>Snapshot from prefab’s <see cref="PlayerController"/> before we multiply by <see cref="ClassData.MomentumMultiplier"/>.</summary>
	private float baselineAccelerationTime = 0.2f;

	private float baselineDeaccelerationTime = 0.2f;

	private bool baselineControllerTimesCaptured;

	// Charge look damp: snapshot user's LookSensitivity when damp starts; restore when it ends (so options / inspector changes apply when not charging).
	private bool chargeLookDampActive;
	private float chargeLookUserBaseline = 1f;

	// After PlayerController applies look, chase yaw at max deg/sec toward a target that persists after mouse stops (fixes quick-flick losing rotation).
	private float chargeYawSmoothedDegrees;
	private float chargeYawCommitTargetDegrees;

	private static float DeltaAngleDegrees( float fromDeg, float toDeg )
	{
		var d = toDeg - fromDeg;
		while ( d > 180f )
			d -= 360f;
		while ( d < -180f )
			d += 360f;
		return d;
	}

	/// <summary>
	/// Charge ramp needs real forward input: discrete <see cref="ForwardAction"/> on keyboard/M+K holds W.
	/// <see cref="Input.AnalogMove"/> axes are unreliable for WASD-only strafe (mapping can bleed into wrong components), so analog stick is gated on <see cref="Input.UsingController"/>.
	/// </summary>
	private bool IsForwardIntentForChargeRamp()
	{
		if ( IsForwardKeyDown() )
			return true;

		if ( !Input.UsingController )
			return false;

		var move = Input.AnalogMove.WithZ( 0f );
		if ( move.LengthSquared < MinForwardInput * MinForwardInput )
			return false;
		if ( move.x <= MinForwardInput )
			return false;
		return move.x >= MathF.Abs( move.y );
	}

	// Owner computes locally; host/other clients read replicated value for tackle checks, UI, etc.
	private bool ownerAtChargeSpeed;
	[Sync] private bool NetAtChargeSpeed { get; set; }
	public bool IsAtChargeSpeed => Network.IsOwner ? ownerAtChargeSpeed : NetAtChargeSpeed;

	/// <summary> Host bumps to force owner ramp back to walk (e.g. Speed Blitz miss). </summary>
	private int netForceWalkRampId;
	[Sync( SyncFlags.FromHost )]
	private int NetForceWalkRampId { get => netForceWalkRampId; set => netForceWalkRampId = value; }

	/// <summary> Host: reset movement ramp to walk tier on the owning client (synced pulse). </summary>
	public void TriggerForceWalkRampOnHost()
	{
		if ( !Networking.IsHost )
			return;

		NetForceWalkRampId++;
	}

	/// <summary> Owner HUD: walk → sprint → charge segment and fill 0–1 for the active segment. </summary>
	public void GetMovementRampDisplay( out MovementRampTier tier, out float progress01 )
	{
		tier = MovementRampTier.Walk;
		progress01 = 0f;

		ballGrab ??= Components.Get<BallGrab>();
		ballThrow ??= Components.Get<BallThrow>();
		playerClass ??= Components.Get<PlayerClass>();
		playerDodge ??= Components.Get<PlayerDodge>();
		playerTackle ??= Components.Get<PlayerTackle>();

		if ( ballThrow?.IsChargingThrow == true )
			return;

		if ( !IsForwardIntentForChargeRamp() )
			return;

		var timeToSprint = ClassStat( playerClass?.CurrentClass?.TimeToSprintSpeed, TimeToSprintSpeed );
		timeToSprint = MathF.Max( timeToSprint, 0.01f );

		if ( forwardMoveTime < timeToSprint )
		{
			tier = MovementRampTier.Walk;
			progress01 = (forwardMoveTime / timeToSprint).Clamp( 0f, 1f );
			return;
		}

		var isHoldingBall = ballGrab?.IsHolding ?? false;
		if ( isHoldingBall )
		{
			tier = MovementRampTier.Sprint;
			progress01 = 1f;
			return;
		}

		var timeToCatchUp = ResolveTimeToCatchUpSpeed( timeToSprint );
		var catchUpDelay = MathF.Max( 0f, timeToCatchUp - timeToSprint );
		if ( catchUpDelay <= 0.01f )
		{
			tier = MovementRampTier.Charge;
			progress01 = 1f;
			return;
		}

		var blockCatchUpRamp = playerDodge != null && Time.Now < playerDodge.SyncedBlockCatchUpUntil;
		if ( blockCatchUpRamp || nonHoldingSprintTime < catchUpDelay )
		{
			tier = MovementRampTier.Sprint;
			progress01 = (nonHoldingSprintTime / catchUpDelay).Clamp( 0f, 1f );
			return;
		}

		tier = MovementRampTier.Charge;
		progress01 = 1f;
	}

	protected override void OnStart()
	{
		ballGrab = Components.Get<BallGrab>();
		ballThrow = Components.Get<BallThrow>();
		playerController = Components.Get<PlayerController>();
		playerClass = Components.Get<PlayerClass>();
		playerDodge = Components.Get<PlayerDodge>();
		dodgeRampApplySeqHandled = playerDodge?.DodgeApplySequence ?? 0;
		playerTackle = Components.Get<PlayerTackle>();
		tackleRampStripSeqHandled = playerTackle?.TackleStripRampSequence ?? 0;
		forceWalkRampSeqHandled = NetForceWalkRampId;
		if ( playerController.IsValid() )
		{
			var y = playerController.EyeAngles.yaw;
			chargeYawSmoothedDegrees = y;
			chargeYawCommitTargetDegrees = y;
			smoothedMoveSpeedCap = ClassStat( playerClass?.CurrentClass?.StartMoveSpeed, StartMoveSpeed );
		}
	}

	protected override void OnFixedUpdate()
	{
		if ( IsProxy )
			return;

		// PlayerController reads AnalogMove in FixedUpdate — patch before movement integrates.
		ApplyMutuallyExclusiveForwardBackwardInput();
	}

	protected override void OnUpdate()
	{
		if ( IsProxy )
			return;

		if ( !playerController.IsValid() )
			playerController = Components.Get<PlayerController>();

		if ( !playerController.IsValid() )
		{
			ownerAtChargeSpeed = false;
			NetAtChargeSpeed = false;
			chargeLookDampActive = false;
			return;
		}

		TryCaptureBaselineControllerTimes();

		ApplyMutuallyExclusiveForwardBackwardInput();

		if ( !IsMatchGameplayInputAllowed() )
		{
			ApplyFrozenMovement();
			return;
		}

		if ( ballGrab is null )
			ballGrab = Components.Get<BallGrab>();
		if ( ballThrow is null )
			ballThrow = Components.Get<BallThrow>();
		if ( playerDodge is null )
			playerDodge = Components.Get<PlayerDodge>();
		if ( speedBlitzUlt is null )
			speedBlitzUlt = Components.Get<SpeedsterSpeedBlitzUlt>();

		ApplySyncedDodgeRampPulse();
		ApplySyncedTackleStripPulse();
		ApplySyncedForceWalkRampPulse();

		playerTackle ??= Components.Get<PlayerTackle>();
		var knockedDown = playerTackle is { IsKnockedDown: true };
		if ( knockedDown && !wasKnockedDown )
			ApplyWalkRampResetLocal();

		wasKnockedDown = knockedDown;

		if ( knockedDown )
		{
			ownerAtChargeSpeed = false;
			NetAtChargeSpeed = false;
			ApplyChargeLookDamp( atChargeSpeed: false );
			return;
		}

		var isHoldingBall = ballGrab?.IsHolding ?? false;
		var isChargingThrow = ballThrow?.IsChargingThrow ?? false;
		var isMovingForward = IsForwardIntentForChargeRamp();

		if ( isChargingThrow || IsSpeedBlitzPlantedChannel() )
		{
			ApplyPlantedMovementChannelLock();
			return;
		}

		if ( isMovingForward )
			forwardMoveTime += Time.Delta;
		else
			forwardMoveTime = 0f;

		var timeToSprint = ClassStat( playerClass?.CurrentClass?.TimeToSprintSpeed, TimeToSprintSpeed );
		var isInSprintStage = isMovingForward && forwardMoveTime >= timeToSprint;
		var blockCatchUpRamp = playerDodge != null && Time.Now < playerDodge.SyncedBlockCatchUpUntil;
		if ( !isHoldingBall && isInSprintStage )
		{
			if ( blockCatchUpRamp )
				nonHoldingSprintTime = 0f;
			else
				nonHoldingSprintTime += Time.Delta;
		}
		else
			nonHoldingSprintTime = 0f;

		var targetMoveCap = GetTargetSpeed( isHoldingBall, isMovingForward );
		BlendMoveSpeedCapTowardTarget( targetMoveCap );
		playerController.WalkSpeed = smoothedMoveSpeedCap;
		playerController.RunSpeed = smoothedMoveSpeedCap;
		ApplyMomentumTimesToPlayerController();
		NetAtChargeSpeed = ownerAtChargeSpeed;
	}

	/// <summary> W and S cannot combine — while W is held S is ignored; while S is held (alone) W is ignored. </summary>
	private void ApplyMutuallyExclusiveForwardBackwardInput()
	{
		var forwardDown = IsForwardKeyDown();
		var backwardDown = IsBackwardKeyDown();

		if ( !forwardDown && !backwardDown )
			return;

		// Both held: forward wins (S does nothing — fixes charge + accidental brake).
		if ( forwardDown && backwardDown )
			backwardDown = false;

		var move = Input.AnalogMove;
		var strafe = move.y;

		if ( forwardDown )
		{
			// AnalogMove.x = forward/back (s&box convention); .y = strafe.
			var forward = Input.UsingController ? MathF.Max( move.x, MinForwardInput ) : 1f;
			Input.AnalogMove = new Vector3( forward, strafe, move.z );
			return;
		}

		var back = Input.UsingController ? MathF.Min( move.x, -MinForwardInput ) : -1f;
		Input.AnalogMove = new Vector3( back, strafe, move.z );
	}

	private bool IsForwardKeyDown()
	{
		if ( Input.Down( ForwardAction ) )
			return true;

		return ForwardAction.Equals( "forward", StringComparison.OrdinalIgnoreCase ) && Input.Down( "Forward" );
	}

	private bool IsBackwardKeyDown()
	{
		if ( Input.Down( BackwardAction ) )
			return true;

		return BackwardAction.Equals( "backward", StringComparison.OrdinalIgnoreCase ) && Input.Down( "Backward" );
	}

	private void ApplyWalkRampResetLocal()
	{
		playerClass ??= Components.Get<PlayerClass>();
		var walkSpeed = ClassStat( playerClass?.CurrentClass?.StartMoveSpeed, StartMoveSpeed );

		forwardMoveTime = 0f;
		nonHoldingSprintTime = 0f;
		ownerAtChargeSpeed = false;
		NetAtChargeSpeed = false;
		smoothedMoveSpeedCap = walkSpeed;

		if ( playerController.IsValid() )
		{
			playerController.WalkSpeed = walkSpeed;
			playerController.RunSpeed = walkSpeed;
		}
	}

	private bool IsMatchGameplayInputAllowed()
	{
		var team = Components.Get<PlayerTeam>();
		return team is null || team.IsMatchGameplayInputAllowed;
	}

	private void ApplyFrozenMovement()
	{
		forwardMoveTime = 0f;
		nonHoldingSprintTime = 0f;
		ownerAtChargeSpeed = false;
		NetAtChargeSpeed = false;
		smoothedMoveSpeedCap = 0f;
		playerController.WalkSpeed = 0f;
		playerController.RunSpeed = 0f;
		ResetPlayerControllerMomentumTimesToBaseline();
		ApplyChargeLookDamp( atChargeSpeed: false );
		Input.AnalogMove = Vector3.Zero;

		var body = Components.Get<Rigidbody>();
		if ( body.IsValid() )
			body.Velocity = Vector3.Zero;
	}

	private void TryCaptureBaselineControllerTimes()
	{
		if ( baselineControllerTimesCaptured || !playerController.IsValid() )
			return;

		baselineAccelerationTime = MathF.Max( playerController.AccelerationTime, 0.04f );
		baselineDeaccelerationTime = MathF.Max( playerController.DeaccelerationTime, 0.04f );
		baselineControllerTimesCaptured = true;
	}

	/// <summary> Engine times (seconds toward requested speed): baseline × multiplier — main source of noticeable “mass” alongside cap easing. </summary>
	private void ApplyMomentumTimesToPlayerController()
	{
		var mult = ClassStat( playerClass?.CurrentClass?.MomentumMultiplier, 1f );
		mult = mult.Clamp( 0.35f, 2.75f );
		playerController.AccelerationTime = baselineAccelerationTime * mult;
		playerController.DeaccelerationTime = baselineDeaccelerationTime * mult;
	}

	private void ResetPlayerControllerMomentumTimesToBaseline()
	{
		playerController.AccelerationTime = baselineAccelerationTime;
		playerController.DeaccelerationTime = baselineDeaccelerationTime;
	}

	/// <summary>
	/// <see cref="ClassData.MomentumMultiplier"/>: <b>1</b> = prefab baseline; <b>&gt;1</b> slower accel/decel + slower cap ease; <b>&lt;1</b> snappier.
	/// Tackle tier still comes from <see cref="GetTargetSpeed"/>; only movement response is weighted.
	/// </summary>
	private void BlendMoveSpeedCapTowardTarget( float targetCap )
	{
		var mult = ClassStat( playerClass?.CurrentClass?.MomentumMultiplier, 1f );
		mult = mult.Clamp( 0.35f, 2.75f );
		// Seconds time-constant toward cap; scales with mult so 0.8 vs 1.3 is clearly different.
		var tau = 0.28f * mult;
		var alpha = 1f - MathF.Exp( -Time.Delta / MathF.Max( tau, 0.03f ) );
		smoothedMoveSpeedCap += (targetCap - smoothedMoveSpeedCap) * alpha;
		if ( MathF.Abs( targetCap - smoothedMoveSpeedCap ) < 0.75f )
			smoothedMoveSpeedCap = targetCap;
	}

	/// <summary> After PlayerController integrates look: slew yaw toward commit target at max deg/sec. </summary>
	protected override void OnPreRender()
	{
		if ( IsProxy )
			return;
		if ( !playerController.IsValid() )
			return;

		playerTackle ??= Components.Get<PlayerTackle>();
		var ragdolled = playerTackle?.IsRagdolled == true;
		var wantClamp = ownerAtChargeSpeed && playerController.Enabled && !ragdolled;

		if ( ChargeYawMaxDegreesPerSecond > 0f && wantClamp )
		{
			var yPc = playerController.EyeAngles.yaw;

			var lookIn = Input.AnalogLook;
			var hasLookInput = MathF.Abs( lookIn.yaw ) > 0.00001f || MathF.Abs( lookIn.pitch ) > 0.00001f;
			if ( hasLookInput )
				chargeYawCommitTargetDegrees = yPc;

			var maxStep = ChargeYawMaxDegreesPerSecond * Time.Delta;
			var toward = DeltaAngleDegrees( chargeYawSmoothedDegrees, chargeYawCommitTargetDegrees );
			var step = Math.Clamp( toward, -maxStep, maxStep );
			chargeYawSmoothedDegrees += step;

			var remainToCommit = DeltaAngleDegrees( chargeYawSmoothedDegrees, chargeYawCommitTargetDegrees );
			if ( !hasLookInput && MathF.Abs( remainToCommit ) < 0.02f )
				chargeYawCommitTargetDegrees = chargeYawSmoothedDegrees;

			var look = playerController.EyeAngles;
			look.yaw = chargeYawSmoothedDegrees;
			playerController.EyeAngles = look;
		}
		else
		{
			var yPc = playerController.EyeAngles.yaw;
			chargeYawSmoothedDegrees = yPc;
			chargeYawCommitTargetDegrees = yPc;
		}

		// Late frame — after PlayerController integrates look.
		ApplyChargeLookDamp( ownerAtChargeSpeed );
	}

	/// <summary> Owner only: at charge, apply damped look; restore snapshot when leaving charge. </summary>
	private void ApplyChargeLookDamp( bool atChargeSpeed )
	{
		if ( !playerController.IsValid() )
			return;

		playerTackle ??= Components.Get<PlayerTackle>();
		var ragdolled = playerTackle?.IsRagdolled == true;
		var wantDamp = atChargeSpeed && playerController.Enabled && !ragdolled;

		if ( wantDamp )
		{
			if ( !chargeLookDampActive )
				chargeLookUserBaseline = playerController.LookSensitivity;
			chargeLookDampActive = true;

			float scaled;
			if ( ChargeLookCompensatePreferencesSensitivity )
			{
				// AnalogLook is scaled by Preferences.Sensitivity; divide it back out here so deg ~ proportional to raw mouse.
				var s = Preferences.Sensitivity;
				if ( s < 0.0001f )
					s = 0.0001f;
				scaled = (ChargeUnifiedLookScale / s) * ChargeLookSensitivityMultiplier;
			}
			else
				scaled = chargeLookUserBaseline * ChargeLookSensitivityMultiplier;

			if ( ChargeLookSensitivityMax > 0f )
				scaled = MathF.Min( scaled, ChargeLookSensitivityMax );
			playerController.LookSensitivity = scaled;
		}
		else
		{
			if ( chargeLookDampActive )
				playerController.LookSensitivity = chargeLookUserBaseline;
			chargeLookDampActive = false;
		}
	}

	private bool IsSpeedBlitzPlantedChannel()
	{
		return speedBlitzUlt?.IsWindUp == true || speedBlitzUlt?.IsConnectPoseFrozen == true;
	}

	/// <summary> Throw charge + Speed Blitz wind-up / connect hang — planted on ground, no charge tier. </summary>
	private void ApplyPlantedMovementChannelLock()
	{
		forwardMoveTime = 0f;
		nonHoldingSprintTime = 0f;
		ownerAtChargeSpeed = false;
		NetAtChargeSpeed = false;
		ApplyChargeLookDamp( atChargeSpeed: false );

		// Grounded channel = planted. Airborne throw wind-up keeps gravity (BallThrow disables built-in input + wish velocity).
		if ( playerController.IsOnGround )
		{
			smoothedMoveSpeedCap = 0f;
			playerController.WalkSpeed = 0f;
			playerController.RunSpeed = 0f;
			ResetPlayerControllerMomentumTimesToBaseline();
		}
		else
		{
			var airMoveCap = ClassStat( playerClass?.CurrentClass?.StartMoveSpeed, StartMoveSpeed );
			playerController.WalkSpeed = airMoveCap;
			playerController.RunSpeed = airMoveCap;
		}
	}

	private float GetTargetSpeed( bool isHoldingBall, bool isMovingForward )
	{
		var startSpeed = ClassStat( playerClass?.CurrentClass?.StartMoveSpeed, StartMoveSpeed );
		var sprintSpeed = ClassStat( playerClass?.CurrentClass?.SprintMoveSpeed, SprintMoveSpeed );
		var catchUpSpeed = ClassStat( playerClass?.CurrentClass?.CatchUpMoveSpeed, CatchUpMoveSpeed );
		var timeToSprint = ClassStat( playerClass?.CurrentClass?.TimeToSprintSpeed, TimeToSprintSpeed );

		playerTackle ??= Components.Get<PlayerTackle>();
		var timeToCatchUp = ResolveTimeToCatchUpSpeed( timeToSprint );

		if ( !isMovingForward )
		{
			ownerAtChargeSpeed = false;
			return startSpeed;
		}

		if ( forwardMoveTime < timeToSprint )
		{
			ownerAtChargeSpeed = false;
			return startSpeed;
		}

		if ( isHoldingBall )
		{
			ownerAtChargeSpeed = false;
			return sprintSpeed;
		}

		var catchUpDelay = MathF.Max( 0f, timeToCatchUp - timeToSprint );
		var atCatchUp = nonHoldingSprintTime >= catchUpDelay;
		ownerAtChargeSpeed = atCatchUp;
		return atCatchUp ? catchUpSpeed : sprintSpeed;
	}

	private float ResolveTimeToCatchUpSpeed( float timeToSprint )
	{
		playerClass ??= Components.Get<PlayerClass>();
		playerTackle ??= Components.Get<PlayerTackle>();
		var cd = playerClass?.CurrentClass;
		var timeToCatchUpBase = ClassStat( cd?.TimeToCatchUpSpeed, TimeToCatchUpSpeed );
		var ragSlow = cd != null && cd.TimeToCatchUpSpeedAfterRagdoll > 0f && playerTackle is { IsPostRagdollSlowCatchUpRampActive: true };
		var atkSlow = cd != null && cd.TimeToCatchUpSpeedAfterAttack > 0f && playerTackle is { IsPostAttackSlowCatchUpRampActive: true };
		if ( ragSlow || atkSlow )
		{
			var sub = timeToSprint;
			if ( ragSlow )
				sub = MathF.Max( sub, MathF.Max( cd.TimeToCatchUpSpeedAfterRagdoll, timeToSprint ) );
			if ( atkSlow )
				sub = MathF.Max( sub, MathF.Max( cd.TimeToCatchUpSpeedAfterAttack, timeToSprint ) );
			return sub;
		}

		return timeToCatchUpBase;
	}

	private static float ClassStat( float? classStat, float fallback )
	{
		return classStat ?? fallback;
	}

	private void ApplySyncedDodgeRampPulse()
	{
		playerDodge ??= Components.Get<PlayerDodge>();
		if ( playerDodge == null )
			return;
		var seq = playerDodge.DodgeApplySequence;
		if ( seq == dodgeRampApplySeqHandled )
			return;
		dodgeRampApplySeqHandled = seq;

		switch ( playerDodge.LatestPenaltyKind )
		{
			case PlayerDodge.DodgePenaltyKind.StripChargeKeepSprint:
				nonHoldingSprintTime = 0f;
				break;
			case PlayerDodge.DodgePenaltyKind.ForceWalkResetRamp:
				forwardMoveTime = 0f;
				nonHoldingSprintTime = 0f;
				break;
		}
	}

	/// <summary>Host bumps tackle strip counter on hit; owner resets charge tier like dodge <c>StripChargeKeepSprint</c>.</summary>
	private void ApplySyncedTackleStripPulse()
	{
		playerTackle ??= Components.Get<PlayerTackle>();
		if ( playerTackle == null )
			return;
		var seq = playerTackle.TackleStripRampSequence;
		if ( seq == tackleRampStripSeqHandled )
			return;
		tackleRampStripSeqHandled = seq;
		nonHoldingSprintTime = 0f;
	}

	/// <summary>Host bumps force-walk counter (e.g. Speed Blitz miss); owner resets full ramp to walk.</summary>
	private void ApplySyncedForceWalkRampPulse()
	{
		var seq = NetForceWalkRampId;
		if ( seq == forceWalkRampSeqHandled )
			return;
		forceWalkRampSeqHandled = seq;

		ApplyWalkRampResetLocal();
	}
}
