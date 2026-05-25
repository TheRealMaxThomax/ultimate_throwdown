using Sandbox;
using System;

/// <summary>
/// Hold-to-charge throw on the owner. When <see cref="PlayerClass.CurrentClass"/> is set, <see cref="ClassData.ThrowPower"/> scales applied forces and <see cref="ClassData.ThrowChargeSpeedScale"/> scales how fast charge reaches full within the prefab min/max time window.
/// </summary>
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
	private Rigidbody playerBody;
	private bool isChargingThrow;
	/// <summary> Owner writes; host reads for dodge validation. </summary>
	[Sync] private bool NetIsChargingThrow { get; set; }
	private Vector3 lockedChargePosition;
	private float throwChargeStartedAt;
	public bool IsChargingThrow => Network.IsOwner ? isChargingThrow : NetIsChargingThrow;

	protected override void OnStart()
	{
		ballGrab = Components.Get<BallGrab>();
		throwChargeBar = Components.Get<ThrowChargeBar>();
		playerClass = Components.Get<PlayerClass>();
		playerController = Components.Get<PlayerController>();
		playerBody = Components.Get<Rigidbody>();
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
			isChargingThrow = false;
			NetIsChargingThrow = false;
			throwChargeBar?.Hide();
			return;
		}

		if ( Input.Pressed( ThrowAction ) )
		{
			StartThrowCharge();
		}

		if ( isChargingThrow && Input.Released( ThrowAction ) )
		{
			var chargeLerp = GetThrowChargeLerp();
			isChargingThrow = false;
			NetIsChargingThrow = false;
			throwChargeBar?.Hide();
			RequestThrowHeldBallOnHost( chargeLerp, GetThrowDirectionWorld() );
		}

		if ( isChargingThrow )
		{
			// Block locomotion input for everyone; non-Sniper also snaps position / clears physics velocity each frame.
			// Sniper keeps dodge specialty: lateral dodge impulse must survive this tick (see PlayerDodge).
			Input.AnalogMove = Vector3.Zero;
			if ( !IsSniperClass() )
			{
				WorldPosition = lockedChargePosition;
				if ( playerBody.IsValid() )
					playerBody.Velocity = Vector3.Zero;
			}

			throwChargeBar?.SetCharge( GetThrowChargeLerp() );
		}
	}

	private void StartThrowCharge()
	{
		isChargingThrow = true;
		NetIsChargingThrow = true;
		throwChargeStartedAt = Time.Now;
		lockedChargePosition = WorldPosition;
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

		chargeLerp = chargeLerp.Clamp( 0f, 1f );
		var throwForceMultiplier = MinThrowForceMultiplier.LerpTo( 1f, chargeLerp );
		var throwUpForceMultiplier = MinThrowUpForceMultiplier.LerpTo( 1f, chargeLerp );
		var classPower = playerClass?.CurrentClass?.ThrowPower ?? 1f;

		// Use direction from Rpc.Caller so the throw matches local aim. Host-side transform can lag for remote players.
		var throwDirection = throwDirectionFromCaller.Length > 0.001f
			? throwDirectionFromCaller.Normal
			: GetThrowDirectionWorld();
		var throwStartPosition = releasedBall.WorldPosition + (throwDirection * ThrowStartOffset) + (Vector3.Up * 10f);
		releasedBall.WorldPosition = throwStartPosition;
		releasedBallBody.Velocity = (throwDirection * ThrowForce * throwForceMultiplier * classPower) + (Vector3.Up * ThrowUpForce * throwUpForceMultiplier * classPower);

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
	}

	private bool IsMatchGameplayInputAllowed()
	{
		var team = Components.Get<PlayerTeam>();
		return team is null || team.IsMatchGameplayInputAllowed;
	}

	private bool IsSniperClass()
	{
		var name = playerClass?.CurrentClass?.ClassName;
		return name != null && name.Equals( "Sniper", StringComparison.OrdinalIgnoreCase );
	}

	private float GetThrowChargeLerp()
	{
		var chargeScale = MathF.Max( 0.05f, playerClass?.CurrentClass?.ThrowChargeSpeedScale ?? 1f );
		var chargeHeldSeconds = (Time.Now - throwChargeStartedAt) * chargeScale;
		var clampedChargeSeconds = chargeHeldSeconds.Clamp( MinThrowChargeTime, MaxThrowChargeTime );
		return MaxThrowChargeTime <= MinThrowChargeTime
			? 1f
			: (clampedChargeSeconds - MinThrowChargeTime) / (MaxThrowChargeTime - MinThrowChargeTime);
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
