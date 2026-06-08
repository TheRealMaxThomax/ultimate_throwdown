using Sandbox;
using System;
/// <summary>
/// Hold-to-charge throw on the owner. When <see cref="PlayerClass.CurrentClass"/> is set, <see cref="ClassData.ThrowPower"/> scales applied forces and <see cref="ClassData.ThrowChargeSpeedScale"/> scales how fast charge reaches full within the prefab min/max time window.
/// </summary>
[Order( 10001 )]
public sealed class BallThrow : Component
{
	[Property] public string ThrowAction { get; set; } = "attack1";
	[Property] public float ThrowForce { get; set; } = 1000f;
	[Property] public float ThrowUpForce { get; set; } = 150f;
	[Property] public float ThrowStartOffset { get; set; } = 40f;
	[Property] public float PickupDelayAfterThrow { get; set; } = 0.25f;
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
	private bool isChargingThrow;
	/// <summary> Owner writes; host reads for dodge validation. </summary>
	[Sync] private bool NetIsChargingThrow { get; set; }
	private float throwChargeStartedAt;
	private bool inputControlsSuppressed;
	private bool savedUseInputControls = true;
	public bool IsChargingThrow => Network.IsOwner ? isChargingThrow : NetIsChargingThrow;

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
	}

	protected override void OnUpdate()
	{
		if ( ballGrab is null )
			return;
		if ( !Network.IsOwner )
			return;

		if ( !IsMatchGameplayInputAllowed() )
		{
			ClearThrowChargeLocal();
			return;
		}

		if ( !ballGrab.IsHolding )
		{
			ClearThrowChargeLocal();
			return;
		}

		if ( Input.Pressed( ThrowAction ) )
		{
			StartThrowCharge();
		}

		if ( isChargingThrow && Input.Released( ThrowAction ) )
		{
			var chargeLerp = GetThrowChargeLerp();
			ClearThrowChargeLocal();
			RequestThrowHeldBallOnHost( chargeLerp, GetThrowDirectionWorld() );
		}

		if ( isChargingThrow )
		{
			// Block locomotion input; <see cref="OnFixedUpdate"/> disables built-in jump/move on <see cref="PlayerController"/>.
			Input.AnalogMove = Vector3.Zero;
			throwChargeBar?.SetCharge( GetThrowChargeLerp() );
		}
	}

	protected override void OnFixedUpdate()
	{
		if ( !Network.IsOwner )
			return;

		playerController ??= Components.Get<PlayerController>();
		if ( !playerController.IsValid() )
			return;

		if ( isChargingThrow )
		{
			if ( !inputControlsSuppressed )
			{
				savedUseInputControls = playerController.UseInputControls;
				inputControlsSuppressed = true;
			}

			playerController.UseInputControls = false;
			playerController.WishVelocity = Vector3.Zero;
			return;
		}

		RestoreInputControlsIfNeeded();
	}

	private void StartThrowCharge()
	{
		isChargingThrow = true;
		NetIsChargingThrow = true;
		throwChargeStartedAt = Time.Now;
		throwChargeBar?.Show();
		throwChargeBar?.SetCharge( 0f );
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
	}

	/// <summary> Owning client: cancel windup without throwing (e.g. approved dodge). </summary>
	public void ClearThrowChargeLocal()
	{
		isChargingThrow = false;
		NetIsChargingThrow = false;
		throwChargeBar?.Hide();
		RestoreInputControlsIfNeeded();
	}

	private void RestoreInputControlsIfNeeded()
	{
		if ( !inputControlsSuppressed )
			return;

		playerController ??= Components.Get<PlayerController>();
		if ( playerController.IsValid() )
			playerController.UseInputControls = savedUseInputControls;

		inputControlsSuppressed = false;
	}

	private bool IsMatchGameplayInputAllowed()
	{
		var team = Components.Get<PlayerTeam>();
		return team is null || team.IsMatchGameplayInputAllowed;
	}

	public float GetThrowChargeLerp() => ThrowReleaseMath.GetChargeLerp(
		throwChargeStartedAt,
		MinThrowChargeTime,
		MaxThrowChargeTime,
		playerClass?.CurrentClass?.ThrowChargeSpeedScale ?? 1f );

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
