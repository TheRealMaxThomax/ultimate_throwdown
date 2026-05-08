using Sandbox;
using System;

public sealed class CatchUpSpeedBoost : Component
{
	[Property] public string ForwardAction { get; set; } = "forward";
	[Property] public float StartMoveSpeed { get; set; } = 140f;
	[Property] public float SprintMoveSpeed { get; set; } = 220f;
	[Property] public float CatchUpMoveSpeed { get; set; } = 320f;
	[Property] public float TimeToSprintSpeed { get; set; } = 2.0f;
	[Property] public float TimeToCatchUpSpeed { get; set; } = 4.0f;
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
	private int dodgeRampApplySeqHandled;
	private float forwardMoveTime;
	private float nonHoldingSprintTime;

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

	// Owner computes locally; host/other clients read replicated value for tackle checks, UI, etc.
	private bool ownerAtChargeSpeed;
	[Sync] private bool NetAtChargeSpeed { get; set; }
	public bool IsAtChargeSpeed => Network.IsOwner ? ownerAtChargeSpeed : NetAtChargeSpeed;

	protected override void OnStart()
	{
		ballGrab = Components.Get<BallGrab>();
		ballThrow = Components.Get<BallThrow>();
		playerController = Components.Get<PlayerController>();
		playerClass = Components.Get<PlayerClass>();
		playerDodge = Components.Get<PlayerDodge>();
		dodgeRampApplySeqHandled = playerDodge?.DodgeApplySequence ?? 0;
		if ( playerController.IsValid() )
		{
			var y = playerController.EyeAngles.yaw;
			chargeYawSmoothedDegrees = y;
			chargeYawCommitTargetDegrees = y;
		}
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


		if ( ballGrab is null )
			ballGrab = Components.Get<BallGrab>();
		if ( ballThrow is null )
			ballThrow = Components.Get<BallThrow>();
		if ( playerDodge is null )
			playerDodge = Components.Get<PlayerDodge>();

		ApplySyncedDodgeRampPulse();

		var isHoldingBall = ballGrab?.IsHolding ?? false;
		var isChargingThrow = ballThrow?.IsChargingThrow ?? false;
		var isMovingForward = Input.Down( ForwardAction ) || Input.AnalogMove.y > MinForwardInput;

		if ( isChargingThrow )
		{
			forwardMoveTime = 0f;
			nonHoldingSprintTime = 0f;
			ownerAtChargeSpeed = false;
			NetAtChargeSpeed = false;
			var startSpeed = ClassStat( playerClass?.CurrentClass?.StartMoveSpeed, StartMoveSpeed );
			playerController.WalkSpeed = startSpeed;
			playerController.RunSpeed = startSpeed;
			ApplyChargeLookDamp( atChargeSpeed: false );
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

		playerController.WalkSpeed = GetTargetSpeed( isHoldingBall, isMovingForward );
		playerController.RunSpeed = playerController.WalkSpeed;
		NetAtChargeSpeed = ownerAtChargeSpeed;
		ApplyChargeLookDamp( ownerAtChargeSpeed );
	}

	/// <summary> After PlayerController integrates look: slew yaw toward commit target at max deg/sec. Commit stores last full wish while look input is active so brief flicks still complete after input ends. </summary>
	protected override void OnPreRender()
	{
		if ( IsProxy || ChargeYawMaxDegreesPerSecond <= 0f )
			return;
		if ( !playerController.IsValid() )
			return;

		playerTackle ??= Components.Get<PlayerTackle>();
		var ragdolled = playerTackle?.IsRagdolled == true;
		var wantClamp = ownerAtChargeSpeed && playerController.Enabled && !ragdolled;

		var yPc = playerController.EyeAngles.yaw;
		if ( !wantClamp )
		{
			chargeYawSmoothedDegrees = yPc;
			chargeYawCommitTargetDegrees = yPc;
			return;
		}

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

	private float GetTargetSpeed( bool isHoldingBall, bool isMovingForward )
	{
		var startSpeed = ClassStat( playerClass?.CurrentClass?.StartMoveSpeed, StartMoveSpeed );
		var sprintSpeed = ClassStat( playerClass?.CurrentClass?.SprintMoveSpeed, SprintMoveSpeed );
		var catchUpSpeed = ClassStat( playerClass?.CurrentClass?.CatchUpMoveSpeed, CatchUpMoveSpeed );
		var timeToSprint = ClassStat( playerClass?.CurrentClass?.TimeToSprintSpeed, TimeToSprintSpeed );
		var timeToCatchUp = ClassStat( playerClass?.CurrentClass?.TimeToCatchUpSpeed, TimeToCatchUpSpeed );

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
}

/// <summary> Double-tap strafe dodge: host-validated iframe, cooldown, and tier penalties. </summary>
public sealed class PlayerDodge : Component
{
	public enum DodgePenaltyKind : byte
	{
		StripChargeKeepSprint = 0,
		ForceWalkResetRamp = 1,
	}

	[Property] public string LeftStrafeAction { get; set; } = "left";
	[Property] public string RightStrafeAction { get; set; } = "right";
	[Property] public float DoubleTapMaxInterval { get; set; } = 0.28f;
	[Property] public float CarrierDodgeCooldownFactor { get; set; } = 0.88f;
	[Property] public float RechargeBlockedAfterChargeDodge { get; set; } = 2f;
	/// <summary> Horizontal impulse = ClassData DodgeDistance × this (applied to Rigidbody Velocity). </summary>
	[Property] public float ShoveVelocityMultiplier { get; set; } = 6.5f;
	[Property] public bool EnableDodgeDebugLogs { get; set; }

	private float lastLeftStrafeTapTime = -999f;
	private float lastRightStrafeTapTime = -999f;
	private int lastConsumedDodgeApplyId;

	private BallGrab ballGrab;
	private BallThrow ballThrow;
	private PlayerClass playerClass;
	private PlayerTackle playerTackle;
	private Rigidbody playerBody;
	private CatchUpSpeedBoost speedBoostRef;

	private float netTackleIframeUntil;
	[Sync( SyncFlags.FromHost )]
	private float NetTackleIframeUntil { get => netTackleIframeUntil; set => netTackleIframeUntil = value; }

	private float netNextDodgeAllowedAfter;
	[Sync( SyncFlags.FromHost )]
	private float NetNextDodgeAllowedAfter { get => netNextDodgeAllowedAfter; set => netNextDodgeAllowedAfter = value; }

	private float netBlockCatchUpUntil;
	[Sync( SyncFlags.FromHost )]
	private float NetBlockCatchUpUntil { get => netBlockCatchUpUntil; set => netBlockCatchUpUntil = value; }

	private byte netPenaltyKindByte;
	[Sync( SyncFlags.FromHost )]
	private byte NetPenaltyKindByte { get => netPenaltyKindByte; set => netPenaltyKindByte = value; }

	private int netDodgeApplyId;
	[Sync( SyncFlags.FromHost )]
	private int NetDodgeApplyId { get => netDodgeApplyId; set => netDodgeApplyId = value; }

	private float netDodgeMovementUntil;
	[Sync( SyncFlags.FromHost )]
	private float NetDodgeMovementUntil { get => netDodgeMovementUntil; set => netDodgeMovementUntil = value; }

	private bool netDodgeClearsThrowCharge = true;
	[Sync( SyncFlags.FromHost )]
	private bool NetDodgeClearsThrowCharge { get => netDodgeClearsThrowCharge; set => netDodgeClearsThrowCharge = value; }

	private int netLastDodgeDirectionSign = 1;
	[Sync( SyncFlags.FromHost )]
	private int NetLastDodgeDirectionSign { get => netLastDodgeDirectionSign; set => netLastDodgeDirectionSign = value; }

	private float netLastDodgeDistanceStat = 260f;
	[Sync( SyncFlags.FromHost )]
	private float NetLastDodgeDistanceStat { get => netLastDodgeDistanceStat; set => netLastDodgeDistanceStat = value; }

	public bool IsImmuneToTackle => Time.Now < netTackleIframeUntil;
	public bool IsDodging => Time.Now < netDodgeMovementUntil;

	public DodgePenaltyKind LatestPenaltyKind => (DodgePenaltyKind)netPenaltyKindByte;
	public int DodgeApplySequence => netDodgeApplyId;
	public float SyncedBlockCatchUpUntil => netBlockCatchUpUntil;

	protected override void OnStart()
	{
		ballGrab = Components.Get<BallGrab>();
		ballThrow = Components.Get<BallThrow>();
		playerClass = Components.Get<PlayerClass>();
		playerTackle = Components.Get<PlayerTackle>();
		playerBody = Components.Get<Rigidbody>();
		speedBoostRef = Components.Get<CatchUpSpeedBoost>();

		lastConsumedDodgeApplyId = NetDodgeApplyId;
		if ( Networking.IsHost )
			NetNextDodgeAllowedAfter = 0f;
	}

	protected override void OnUpdate()
	{
		if ( Network.IsOwner )
			TryConsumeDodgeApplyOnOwner();

		if ( !Network.IsOwner )
			return;

		playerTackle ??= Components.Get<PlayerTackle>();
		if ( playerTackle is { IsRagdolled: true } )
			return;

		playerBody ??= Components.Get<Rigidbody>();

		TryDetectDoubleTapDodge();
	}

	private void TryDetectDoubleTapDodge()
	{
		if ( Time.Now < NetNextDodgeAllowedAfter )
			return;

		playerTackle ??= Components.Get<PlayerTackle>();
		if ( playerTackle is { IsRagdolled: true } )
			return;

		var charging = ballThrow?.IsChargingThrow == true;
		if ( charging && !IsSniper() )
			return;

		var now = Time.Now;

		if ( Input.Pressed( LeftStrafeAction ) )
		{
			if ( now - lastLeftStrafeTapTime <= DoubleTapMaxInterval )
				TryFireDodgeRequest( -1 );
			lastLeftStrafeTapTime = now;
			return;
		}

		if ( Input.Pressed( RightStrafeAction ) )
		{
			if ( now - lastRightStrafeTapTime <= DoubleTapMaxInterval )
				TryFireDodgeRequest( 1 );
			lastRightStrafeTapTime = now;
		}
	}

	private void TryFireDodgeRequest( int directionSign )
	{
		playerTackle ??= Components.Get<PlayerTackle>();
		if ( playerTackle is { IsRagdolled: true } )
			return;
		if ( Time.Now < NetNextDodgeAllowedAfter )
			return;

		var charging = ballThrow?.IsChargingThrow == true;
		if ( charging && !IsSniper() )
			return;

		directionSign = directionSign < 0 ? -1 : 1;
		RequestDodgeOnHostRpc( directionSign );
	}

	[Rpc.Host]
	private void RequestDodgeOnHostRpc( int directionSign )
	{
		directionSign = directionSign < 0 ? -1 : 1;

		if ( Network.Owner is null || Rpc.Caller.SteamId != Network.Owner.SteamId )
			return;

		ballGrab ??= Components.Get<BallGrab>();
		ballThrow ??= Components.Get<BallThrow>();
		playerClass ??= Components.Get<PlayerClass>();
		playerTackle ??= Components.Get<PlayerTackle>();
		speedBoostRef ??= Components.Get<CatchUpSpeedBoost>();

		if ( playerTackle is { IsRagdolled: true } )
			return;

		var now = Time.Now;
		if ( now < NetNextDodgeAllowedAfter )
		{
			if ( EnableDodgeDebugLogs )
				Log.Info( "[Dodge] Reject — cooldown." );
			return;
		}

		var chargingThrow = ballThrow?.IsChargingThrow == true;
		if ( chargingThrow && !IsSniper() )
		{
			if ( EnableDodgeDebugLogs )
				Log.Info( "[Dodge] Reject — throw charge." );
			return;
		}

		var wasAtChargeSpeed = speedBoostRef != null && speedBoostRef.IsAtChargeSpeed;

		var penaltyKind = wasAtChargeSpeed
			? DodgePenaltyKind.StripChargeKeepSprint
			: DodgePenaltyKind.ForceWalkResetRamp;

		var clearsThrow = !(IsSniper() && chargingThrow);

		var classData = playerClass?.CurrentClass;
		var baseCd = classData?.DodgeCooldown ?? 3.5f;
		var iframe = classData?.DodgeInvincibilityWindow ?? 0.14f;
		var dist = classData?.DodgeDistance ?? 260f;

		var holdingBall = ballGrab?.IsHolding ?? false;
		var cdMul = holdingBall ? CarrierDodgeCooldownFactor : 1f;

		NetDodgeClearsThrowCharge = clearsThrow;
		NetTackleIframeUntil = now + iframe;
		NetNextDodgeAllowedAfter = now + baseCd * cdMul;

		NetBlockCatchUpUntil = penaltyKind == DodgePenaltyKind.StripChargeKeepSprint
			? now + RechargeBlockedAfterChargeDodge
			: 0f;

		NetPenaltyKindByte = (byte)penaltyKind;
		NetLastDodgeDirectionSign = directionSign;
		NetLastDodgeDistanceStat = dist;
		NetDodgeApplyId = NetDodgeApplyId + 1;
		NetDodgeMovementUntil = now + 0.2f;

		if ( EnableDodgeDebugLogs )
		{
			Log.Info( $"[Dodge] OK dir={directionSign} penalty={penaltyKind} iframe={iframe:F2}s cd={baseCd * cdMul:F2}s chargeStrip={wasAtChargeSpeed}" );
		}
	}

	private void TryConsumeDodgeApplyOnOwner()
	{
		if ( NetDodgeApplyId == lastConsumedDodgeApplyId )
			return;

		lastConsumedDodgeApplyId = NetDodgeApplyId;

		if ( NetDodgeClearsThrowCharge )
			ballThrow?.ClearThrowChargeLocal();

		ApplyShoveVelocity( NetLastDodgeDirectionSign, NetLastDodgeDistanceStat );
	}

	private void ApplyShoveVelocity( int directionSign, float dodgeDistanceStat )
	{
		playerBody ??= Components.Get<Rigidbody>();
		if ( !playerBody.IsValid() )
			return;

		// EyeAngles drive third-person steering; WorldRotation can lag on spawn. Use same ToRotation() as ragdoll camera / view.
		// Strafe axis = Right (not Cross(Up, Forward)) so it matches input even if yaw/pitch convention differs from FromYaw.
		var pc = Components.Get<PlayerController>( FindMode.EverythingInSelfAndDescendants );
		Vector3 lateral;
		if ( pc.IsValid() )
		{
			var lateralFlat = pc.EyeAngles.ToRotation().Right.WithZ( 0 );
			if ( lateralFlat.Length < 0.001f )
			{
				var ff = WorldRotation.Forward.WithZ( 0 );
				if ( ff.Length < 0.001f )
					ff = Vector3.Forward;
				lateralFlat = Vector3.Cross( Vector3.Up, ff.Normal ).Normal;
			}
			else
				lateralFlat = lateralFlat.Normal;

			lateral = lateralFlat * directionSign;
		}
		else
		{
			var flatForward = WorldRotation.Forward.WithZ( 0 );
			if ( flatForward.Length < 0.001f )
				flatForward = Vector3.Forward;
			var flatForwardN = flatForward.Normal;
			lateral = Vector3.Cross( Vector3.Up, flatForwardN ).Normal * directionSign;
		}
		var add = lateral * (dodgeDistanceStat * ShoveVelocityMultiplier).Clamp( 0f, 6000f );
		var v = playerBody.Velocity;
		playerBody.Velocity = v + add;
	}

	private bool IsSniper()
	{
		var name = playerClass?.CurrentClass?.ClassName;
		return name != null && name.Equals( "Sniper", StringComparison.OrdinalIgnoreCase );
	}
}
