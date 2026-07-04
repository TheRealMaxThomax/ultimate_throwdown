using Sandbox;
using System;

/// <summary> Double-tap strafe dodge: host-validated iframe, cooldown, and tier penalties. Non-Sniper cannot dodge while charging a throw; Sniper can dodge anytime and keeps throw charge when dodging during charge (<see cref="RequestDodgeOnHostRpc"/>).</summary>
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
	/// <summary> Seconds until host-synced dodge cooldown ends (0 = ready). </summary>
	public float DodgeCooldownRemaining => MathF.Max( 0f, netNextDodgeAllowedAfter - Time.Now );

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

		if ( !IsMatchGameplayInputAllowed() )
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

		// No dodge during an ult (Speed Blitz wind-up has no cancel; dash is committed).
		if ( Components.Get<SpeedsterSpeedBlitzUlt>() is { IsActive: true } )
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

		if ( !IsMatchGameplayInputAllowed() )
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
		if ( chargingThrow )
		{
			var mul = Math.Clamp( classData?.ThrowChargeDodgeDistanceMultiplier ?? 1f, 0.25f, 3f );
			dist *= mul;
		}

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
			ballThrow?.CancelActiveThrowCharge();

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

	private bool IsMatchGameplayInputAllowed()
	{
		var team = Components.Get<PlayerTeam>();
		return team is null || team.IsMatchGameplayInputAllowed;
	}
}
