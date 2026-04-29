using Sandbox;

public sealed class BallThrow : Component
{
	[Property] public string ThrowAction { get; set; } = "attack1";
	[Property] public float ThrowForce { get; set; } = 1200f;
	[Property] public float ThrowUpForce { get; set; } = 120f;
	[Property] public float ThrowStartOffset { get; set; } = 40f;
	[Property] public float PickupDelayAfterThrow { get; set; } = 0.25f;
	[Property] public GameObject ThrowDirectionSource { get; set; }
	[Property] public float MinThrowChargeTime { get; set; } = 0.05f;
	[Property] public float MaxThrowChargeTime { get; set; } = 2.0f;
	[Property] public float MinThrowForceMultiplier { get; set; } = 0.35f;
	[Property] public float MinThrowUpForceMultiplier { get; set; } = 0.6f;
	[Property] public bool EnableNetDebugLogs { get; set; } = false;

	private BallGrab ballGrab;
	private ThrowChargeBar throwChargeBar;
	private Rigidbody playerBody;
	private bool isChargingThrow;
	private Vector3 lockedChargePosition;
	private float throwChargeStartedAt;
	public bool IsChargingThrow => isChargingThrow;

	protected override void OnStart()
	{
		ballGrab = Components.Get<BallGrab>();
		throwChargeBar = Components.Get<ThrowChargeBar>();
		playerBody = Components.Get<Rigidbody>();
	}

	protected override void OnUpdate()
	{
		if ( ballGrab is null )
			return;

		if ( !ballGrab.IsHolding )
		{
			isChargingThrow = false;
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
			throwChargeBar?.Hide();
			RequestThrowHeldBallOnHost( chargeLerp );
		}

		if ( isChargingThrow )
		{
			// Keep aiming active, but freeze movement while charging.
			Input.AnalogMove = Vector3.Zero;
			WorldPosition = lockedChargePosition;
			if ( playerBody.IsValid() )
			{
				playerBody.Velocity = Vector3.Zero;
			}
			throwChargeBar?.SetCharge( GetThrowChargeLerp() );
		}
	}

	private void StartThrowCharge()
	{
		isChargingThrow = true;
		throwChargeStartedAt = Time.Now;
		lockedChargePosition = WorldPosition;
		throwChargeBar?.Show();
		throwChargeBar?.SetCharge( 0f );
	}

	[Rpc.Host]
	private void RequestThrowHeldBallOnHost( float chargeLerp )
	{
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

		var directionSource = ThrowDirectionSource.IsValid() ? ThrowDirectionSource : GameObject;
		var throwDirection = directionSource.WorldRotation.Forward;
		var throwStartPosition = releasedBall.WorldPosition + (throwDirection * ThrowStartOffset) + (Vector3.Up * 10f);
		releasedBall.WorldPosition = throwStartPosition;
		releasedBallBody.Velocity = (throwDirection * ThrowForce * throwForceMultiplier) + (Vector3.Up * ThrowUpForce * throwUpForceMultiplier);
		if ( EnableNetDebugLogs )
		{
			Log.Info( $"[NetDebug] Host applied throw. Speed={releasedBallBody.Velocity.Length}" );
		}
	}

	private float GetThrowChargeLerp()
	{
		var chargeHeldSeconds = Time.Now - throwChargeStartedAt;
		var clampedChargeSeconds = chargeHeldSeconds.Clamp( MinThrowChargeTime, MaxThrowChargeTime );
		return MaxThrowChargeTime <= MinThrowChargeTime
			? 1f
			: (clampedChargeSeconds - MinThrowChargeTime) / (MaxThrowChargeTime - MinThrowChargeTime);
	}
}
