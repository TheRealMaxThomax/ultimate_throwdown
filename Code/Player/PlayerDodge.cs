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
	/// <summary> Max time to spend the lateral slide (class <c>DodgeDistance</c> is literal travel units). </summary>
	[Property] public float DodgeChannelDurationSeconds { get; set; } = 0.2f;
	[Property] public bool EnableDodgeDebugLogs { get; set; }

	private float lastLeftStrafeTapTime = -999f;
	private float lastRightStrafeTapTime = -999f;
	private int lastConsumedDodgeApplyId;

	private BallGrab ballGrab;
	private BallThrow ballThrow;
	private PlayerClass playerClass;
	private PlayerTackle playerTackle;
	private PlayerController playerController;
	private Rigidbody playerBody;
	private CatchUpSpeedBoost speedBoostRef;

	private bool ownerDodgeChannelActive;
	private Vector3 ownerDodgeLateralDir;
	private float ownerDodgeDistanceRemaining;
	private float ownerDodgeSlideSpeed;
	private float ownerDodgeChannelEndsAt;

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
	public bool IsDodging => (Network.IsOwner && ownerDodgeChannelActive) || Time.Now < netDodgeMovementUntil;
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
		playerController = Components.Get<PlayerController>( FindMode.EverythingInSelfAndDescendants );
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

	protected override void OnFixedUpdate()
	{
		if ( !Network.IsOwner )
			return;

		if ( ownerDodgeChannelActive )
			OwnerDriveDodgeChannel();
	}

	private void TryDetectDoubleTapDodge()
	{
		if ( ownerDodgeChannelActive )
			return;

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
		if ( ownerDodgeChannelActive )
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
		var channelDuration = DodgeChannelDurationSeconds.Clamp( 0.05f, 1f );

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
		NetDodgeMovementUntil = now + channelDuration;

		if ( EnableDodgeDebugLogs )
		{
			Log.Info( $"[Dodge] OK dir={directionSign} penalty={penaltyKind} iframe={iframe:F2}s cd={baseCd * cdMul:F2}s dist={dist:F0} chargeStrip={wasAtChargeSpeed}" );
		}
	}

	private void TryConsumeDodgeApplyOnOwner()
	{
		if ( NetDodgeApplyId == lastConsumedDodgeApplyId )
			return;

		lastConsumedDodgeApplyId = NetDodgeApplyId;

		if ( NetDodgeClearsThrowCharge )
			ballThrow?.CancelActiveThrowCharge();

		BeginOwnerDodgeChannel( NetLastDodgeDirectionSign, NetLastDodgeDistanceStat );
	}

	private void BeginOwnerDodgeChannel( int directionSign, float dodgeDistanceStat )
	{
		var lateral = ResolveLateralDirection( directionSign );
		if ( lateral.Length < 0.001f )
			return;

		var channelDuration = DodgeChannelDurationSeconds.Clamp( 0.05f, 1f );
		var travelDistance = MathF.Max( 0f, dodgeDistanceStat );

		ownerDodgeLateralDir = lateral;
		ownerDodgeDistanceRemaining = travelDistance;
		ownerDodgeSlideSpeed = travelDistance > 0f
			? travelDistance / channelDuration
			: 0f;
		ownerDodgeChannelEndsAt = Time.Now + channelDuration;
		ownerDodgeChannelActive = true;

		if ( EnableDodgeDebugLogs )
			Log.Info( $"[Dodge] Owner channel start dist={travelDistance:F0} speed={ownerDodgeSlideSpeed:F0} dur={channelDuration:F2}s" );
	}

	/// <summary> Owner: capped lateral slide (ground or air); hard horizontal stop when the channel ends — same idea as Speed Blitz dash end. </summary>
	private void OwnerDriveDodgeChannel()
	{
		playerTackle ??= Components.Get<PlayerTackle>();
		if ( playerTackle is { IsRagdolled: true } )
		{
			EndOwnerDodgeChannel( "ragdoll" );
			return;
		}

		if ( Time.Now >= ownerDodgeChannelEndsAt || ownerDodgeDistanceRemaining <= 0f || ownerDodgeSlideSpeed <= 0f )
		{
			EndOwnerDodgeChannel( "done" );
			return;
		}

		var dt = Time.Delta;
		var stepDistance = MathF.Min( ownerDodgeSlideSpeed * dt, ownerDodgeDistanceRemaining );
		if ( stepDistance <= 0f )
		{
			EndOwnerDodgeChannel( "done" );
			return;
		}

		if ( TryGetWallBlockedStepDistance( stepDistance, out var allowedStep ) )
		{
			stepDistance = allowedStep;
			if ( stepDistance <= 0.5f )
			{
				EndOwnerDodgeChannel( "wall" );
				return;
			}
		}

		ownerDodgeDistanceRemaining = MathF.Max( 0f, ownerDodgeDistanceRemaining - stepDistance );

		playerController ??= Components.Get<PlayerController>( FindMode.EverythingInSelfAndDescendants );
		playerBody ??= Components.Get<Rigidbody>();

		var horizontal = ownerDodgeLateralDir * ownerDodgeSlideSpeed;

		if ( playerController.IsValid() )
			playerController.WishVelocity = horizontal;

		if ( playerBody.IsValid() )
			playerBody.Velocity = horizontal.WithZ( playerBody.Velocity.z );

		if ( ownerDodgeDistanceRemaining <= 0f || Time.Now >= ownerDodgeChannelEndsAt )
			EndOwnerDodgeChannel( "done" );
	}

	private bool TryGetWallBlockedStepDistance( float stepDistance, out float allowedStep )
	{
		allowedStep = stepDistance;
		if ( stepDistance <= 0f )
			return false;

		var from = GameObject.WorldPosition;
		var to = from + ownerDodgeLateralDir * stepDistance;
		var trace = Scene.Trace.Ray( from, to )
			.IgnoreGameObjectHierarchy( GameObject )
			.Run();

		if ( !trace.Hit )
			return false;

		const float wallBackoff = 4f;
		allowedStep = MathF.Max( 0f, trace.HitPosition.Distance( from ) - wallBackoff );
		return true;
	}

	private void EndOwnerDodgeChannel( string reason )
	{
		if ( !ownerDodgeChannelActive )
			return;

		ownerDodgeChannelActive = false;
		ownerDodgeDistanceRemaining = 0f;
		ownerDodgeSlideSpeed = 0f;
		OwnerZeroHorizontalVelocity();

		if ( EnableDodgeDebugLogs )
			Log.Info( $"[Dodge] Owner channel end ({reason})" );
	}

	private void OwnerZeroHorizontalVelocity()
	{
		playerController ??= Components.Get<PlayerController>( FindMode.EverythingInSelfAndDescendants );
		playerBody ??= Components.Get<Rigidbody>();

		if ( playerController.IsValid() )
			playerController.WishVelocity = Vector3.Zero;

		if ( playerBody.IsValid() )
			playerBody.Velocity = new Vector3( 0f, 0f, playerBody.Velocity.z );
	}

	private Vector3 ResolveLateralDirection( int directionSign )
	{
		directionSign = directionSign < 0 ? -1 : 1;

		playerController ??= Components.Get<PlayerController>( FindMode.EverythingInSelfAndDescendants );

		// EyeAngles drive third-person steering; WorldRotation can lag on spawn. Use same ToRotation() as ragdoll camera / view.
		// Strafe axis = Right (not Cross(Up, Forward)) so it matches input even if yaw/pitch convention differs from FromYaw.
		if ( playerController.IsValid() )
		{
			var lateralFlat = playerController.EyeAngles.ToRotation().Right.WithZ( 0 );
			if ( lateralFlat.Length < 0.001f )
			{
				var ff = WorldRotation.Forward.WithZ( 0 );
				if ( ff.Length < 0.001f )
					ff = Vector3.Forward;
				lateralFlat = Vector3.Cross( Vector3.Up, ff.Normal ).Normal;
			}
			else
				lateralFlat = lateralFlat.Normal;

			return lateralFlat * directionSign;
		}

		var flatForward = WorldRotation.Forward.WithZ( 0 );
		if ( flatForward.Length < 0.001f )
			flatForward = Vector3.Forward;
		var flatForwardN = flatForward.Normal;
		return Vector3.Cross( Vector3.Up, flatForwardN ).Normal * directionSign;
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
