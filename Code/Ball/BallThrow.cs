using Sandbox;
using System;
/// <summary>
/// Hold-to-charge throw on the owner. When <see cref="PlayerClass.CurrentClass"/> is set, <see cref="ClassData.ThrowPower"/> scales applied forces and <see cref="ClassData.ThrowChargeSpeedScale"/> scales how fast charge reaches full within the prefab min/max time window.
/// </summary>
[Order( 10001 )]
public sealed class BallThrow : Component
{
	[Property] public string ThrowAction { get; set; } = "attack1";
	[Property] public string CancelChargeAction { get; set; } = "attack2";
	[Property] public float ThrowForce { get; set; } = 1000f;
	[Property] public float ThrowUpForce { get; set; } = 150f;
	[Property] public float ThrowStartOffset { get; set; } = 40f;
	[Property] public float PickupDelayAfterThrow { get; set; } = 0.25f;
	/// <summary> Seconds after button release before the host applies throw velocity — tune to the anim release frame. </summary>
	[Property] public float ThrowReleaseDelaySeconds { get; set; } = 0.35f;
	[Property] public GameObject ThrowDirectionSource { get; set; }
	[Property] public float MinThrowChargeTime { get; set; } = 0.05f;
	[Property] public float MaxThrowChargeTime { get; set; } = 3f;
	[Property] public float MinThrowForceMultiplier { get; set; } = 0.35f;
	[Property] public float MinThrowUpForceMultiplier { get; set; } = 0.6f;
	[Property] public bool EnableNetDebugLogs { get; set; } = false;

	private BallGrab ballGrab;
	private ThrowChargeBar throwChargeBar;
	private PlayerClass playerClass;
	private PlayerController playerController;
	private Rigidbody playerBody;
	private PlayerBallHoldAnim playerBallHoldAnim;
	private bool isChargingThrow;
	private bool jumpSpeedSuppressed;
	private float baselineJumpSpeed;
	private bool isPendingThrowRelease;
	/// <summary> Owner writes; host reads for dodge validation. </summary>
	[Sync] private bool NetIsChargingThrow { get; set; }
	/// <summary> Owner writes each frame while charging so remotes can scrub custom charge poses. </summary>
	[Sync] private float NetThrowChargeLerp { get; set; }
	private float throwChargeStartedAt;
	private float pendingThrowReleaseAt;
	private float pendingChargeLerp;
	private Vector3 pendingThrowDirection;
	public bool IsChargingThrow => Network.IsOwner ? isChargingThrow : NetIsChargingThrow;
	public bool IsPendingThrowRelease => Network.IsOwner && isPendingThrowRelease;
	/// <summary> Owner wind-up + release-delay — movement/jump plant (see <see cref="CatchUpSpeedBoost"/>). </summary>
	public bool IsThrowPlantLocked => Network.IsOwner && (isChargingThrow || isPendingThrowRelease);

	public readonly struct ThrowPreviewSnapshot
	{
		public Vector3 ReleasePivotWorldPosition { get; init; }
		public GameObject HeldBall { get; init; }
		public Vector3 ThrowDirection { get; init; }
		public float ChargeLerp { get; init; }
		public ThrowReleaseMath.ReleaseSettings ReleaseSettings { get; init; }
	}

	/// <summary> Owning client: live aim/charge state for <see cref="ThrowTrajectoryPreview"/>. </summary>
	public bool TryGetThrowPreviewSnapshot( out ThrowPreviewSnapshot snapshot )
	{
		snapshot = default;
		if ( !isChargingThrow || ballGrab is null || !ballGrab.IsHolding )
			return false;

		var heldBall = ballGrab.HeldBall;
		if ( !heldBall.IsValid() )
			return false;

		snapshot = new ThrowPreviewSnapshot
		{
			ReleasePivotWorldPosition = ballGrab.GetPredictedThrowReleasePivotPosition(),
			HeldBall = heldBall,
			ThrowDirection = GetThrowDirectionWorld(),
			ChargeLerp = GetThrowChargeLerp(),
			ReleaseSettings = new ThrowReleaseMath.ReleaseSettings
			{
				ThrowForce = ThrowForce,
				ThrowUpForce = ThrowUpForce,
				ThrowStartOffset = ThrowStartOffset,
				MinThrowForceMultiplier = MinThrowForceMultiplier,
				MinThrowUpForceMultiplier = MinThrowUpForceMultiplier,
				ClassThrowPower = playerClass?.CurrentClass?.ThrowPower ?? 1f
			}
		};
		return true;
	}

	protected override void OnStart()
	{
		ballGrab = Components.Get<BallGrab>();
		throwChargeBar = Components.Get<ThrowChargeBar>();
		playerClass = Components.Get<PlayerClass>();
		playerController = Components.Get<PlayerController>();
		playerBallHoldAnim = Components.Get<PlayerBallHoldAnim>();
	}

	protected override void OnUpdate()
	{
		if ( ballGrab is null )
			return;
		if ( !Network.IsOwner )
			return;

		if ( !IsMatchGameplayInputAllowed() )
		{
			ClearLocalThrowState();
			return;
		}

		if ( !ballGrab.IsHolding )
		{
			ClearLocalThrowState();
			return;
		}

		if ( isPendingThrowRelease )
		{
			TickPendingThrowRelease();
			return;
		}

		if ( Input.Pressed( ThrowAction ) )
		{
			StartThrowCharge();
		}

		if ( isChargingThrow && Input.Pressed( CancelChargeAction ) )
		{
			CancelActiveThrowCharge();
			return;
		}

		if ( isChargingThrow && Input.Released( ThrowAction ) )
		{
			BeginThrowRelease( GetThrowChargeLerp(), GetThrowDirectionWorld() );
		}

		if ( isChargingThrow )
		{
			// Block locomotion input; <see cref="OnFixedUpdate"/> plants movement (jump off, horizontal vel zero).
			Input.AnalogMove = Vector3.Zero;
			var chargeLerp = GetThrowChargeLerp();
			NetThrowChargeLerp = chargeLerp;
			throwChargeBar?.SetCharge( chargeLerp );
		}
	}

	protected override void OnFixedUpdate()
	{
		if ( !Network.IsOwner )
			return;

		playerController ??= Components.Get<PlayerController>();
		if ( !playerController.IsValid() )
			return;

		if ( isChargingThrow || isPendingThrowRelease )
			ApplyThrowPlantMovementLock();
		else
			RestoreJumpSpeedAfterThrowPlant();
	}

	private void BeginThrowRelease( float chargeLerp, Vector3 throwDirection )
	{
		ClearThrowChargeLocal();
		playerBallHoldAnim?.NotifyThrowReleased();

		if ( ThrowReleaseDelaySeconds <= 0f )
		{
			RequestThrowHeldBallOnHost( chargeLerp, throwDirection );
			return;
		}

		isPendingThrowRelease = true;
		pendingThrowReleaseAt = Time.Now + ThrowReleaseDelaySeconds;
		pendingChargeLerp = chargeLerp;
		pendingThrowDirection = throwDirection;
	}

	private void TickPendingThrowRelease()
	{
		Input.AnalogMove = Vector3.Zero;

		if ( Time.Now < pendingThrowReleaseAt )
			return;

		isPendingThrowRelease = false;
		RequestThrowHeldBallOnHost( pendingChargeLerp, pendingThrowDirection );
	}

	private void ClearLocalThrowState()
	{
		ClearThrowChargeLocal();
		CancelPendingThrowRelease();
	}

	private void CancelPendingThrowRelease()
	{
		isPendingThrowRelease = false;
	}

	private void StartThrowCharge()
	{
		isChargingThrow = true;
		NetIsChargingThrow = true;
		throwChargeStartedAt = Time.Now;
		throwChargeBar?.Show();
		throwChargeBar?.SetCharge( 0f );
		ZeroOwnerHorizontalVelocity();
	}

	[Rpc.Host]
	private void RequestThrowHeldBallOnHost( float chargeLerp, Vector3 throwDirectionFromCaller )
	{
		if ( !IsMatchGameplayInputAllowed() )
			return;

		if ( EnableNetDebugLogs )
		{
			Log.Info( $"[NetDebug] Host throw request received. Caller={Rpc.Caller.DisplayName} IsHolding={(ballGrab?.IsHolding ?? false)} Charge={chargeLerp}" );
		}

		if ( ballGrab is null || !ballGrab.IsHolding )
			return;

		ballGrab.TransferBallOwnershipToHost();
		var releasedBall = ballGrab.ReleaseHeldBall();
		if ( !releasedBall.IsValid() )
			return;

		ballGrab.BlockPickupForSeconds( PickupDelayAfterThrow );

		var releasedBallBody = releasedBall.Components.Get<Rigidbody>();
		if ( !releasedBallBody.IsValid() )
			return;

		// Use direction from Rpc.Caller so the throw matches local aim. Host-side transform can lag for remote players.
		var throwDirection = throwDirectionFromCaller.Length > 0.001f
			? throwDirectionFromCaller
			: GetThrowDirectionWorld();
		var releaseSettings = new ThrowReleaseMath.ReleaseSettings
		{
			ThrowForce = ThrowForce,
			ThrowUpForce = ThrowUpForce,
			ThrowStartOffset = ThrowStartOffset,
			MinThrowForceMultiplier = MinThrowForceMultiplier,
			MinThrowUpForceMultiplier = MinThrowUpForceMultiplier,
			ClassThrowPower = playerClass?.CurrentClass?.ThrowPower ?? 1f
		};
		ThrowReleaseMath.ComputeRelease(
			releasedBall.WorldPosition,
			throwDirection,
			chargeLerp,
			releaseSettings,
			out var throwStartPosition,
			out var throwVelocity );
		releasedBall.WorldPosition = throwStartPosition;
		releasedBallBody.Velocity = throwVelocity;

		if ( EnableNetDebugLogs )
		{
			Log.Info( $"[NetDebug] Host applied throw. Speed={releasedBallBody.Velocity.Length}" );
		}

		BallPassAssistState.GetOrCreate( releasedBall )?.NotifyThrowOnHost( GameObject );
		BallLastTouchLedger.GetOrCreate( releasedBall )?.NotifyTouchOnHost( GameObject, GameObject.WorldPosition );
	}

	/// <summary> Owning client: cancel windup without throwing (charge bar, sync, movement plant). </summary>
	public void CancelActiveThrowCharge()
	{
		ClearThrowChargeLocal();
		RestoreJumpSpeedAfterThrowPlant();
		playerBallHoldAnim ??= Components.Get<PlayerBallHoldAnim>();
		playerBallHoldAnim?.NotifyThrowChargeCancelled();
	}

	/// <summary> Owning client: cancel charge and pending release (knockdown, etc.). </summary>
	public void CancelThrowAimingState()
	{
		CancelActiveThrowCharge();
		CancelPendingThrowRelease();
	}

	public void ClearThrowChargeLocal()
	{
		isChargingThrow = false;
		NetIsChargingThrow = false;
		NetThrowChargeLerp = 0f;
		throwChargeBar?.Hide();
	}

	void ApplyThrowPlantMovementLock()
	{
		playerController ??= Components.Get<PlayerController>();
		playerBody ??= Components.Get<Rigidbody>();

		if ( playerController.IsValid() )
		{
			playerController.WishVelocity = Vector3.Zero;

			if ( !jumpSpeedSuppressed )
			{
				baselineJumpSpeed = playerController.JumpSpeed;
				jumpSpeedSuppressed = true;
			}

			playerController.JumpSpeed = 0f;
		}

		ZeroOwnerHorizontalVelocity();
	}

	void RestoreJumpSpeedAfterThrowPlant()
	{
		if ( !jumpSpeedSuppressed )
			return;

		playerController ??= Components.Get<PlayerController>();
		if ( playerController.IsValid() )
			playerController.JumpSpeed = baselineJumpSpeed;

		jumpSpeedSuppressed = false;
	}

	void ZeroOwnerHorizontalVelocity()
	{
		playerBody ??= Components.Get<Rigidbody>();
		if ( !playerBody.IsValid() )
			return;

		playerBody.Velocity = new Vector3( 0f, 0f, playerBody.Velocity.z );
	}

	private bool IsMatchGameplayInputAllowed()
	{
		var team = Components.Get<PlayerTeam>();
		return team is null || team.IsMatchGameplayInputAllowed;
	}

	public float GetThrowChargeLerp()
	{
		if ( Network.IsOwner )
		{
			return ThrowReleaseMath.GetChargeLerp(
				throwChargeStartedAt,
				MinThrowChargeTime,
				MaxThrowChargeTime,
				playerClass?.CurrentClass?.ThrowChargeSpeedScale ?? 1f );
		}

		return NetIsChargingThrow ? NetThrowChargeLerp : 0f;
	}

	/// <summary>
	/// World throw direction at release. Prefers <see cref="ThrowDirectionSource"/> when wired; otherwise
	/// <see cref="PlayerController.EyeAngles"/> (body <see cref="GameObject.WorldRotation"/> lags look during charge).
	/// </summary>
	private Vector3 GetThrowDirectionWorld()
	{
		if ( ThrowDirectionSource.IsValid() )
			return ThrowDirectionSource.WorldRotation.Forward;

		playerController ??= Components.Get<PlayerController>();
		if ( playerController.IsValid() )
			return playerController.EyeAngles.ToRotation().Forward;

		if ( Scene.Camera.IsValid() )
			return Scene.Camera.WorldRotation.Forward;

		return WorldRotation.Forward;
	}
}
