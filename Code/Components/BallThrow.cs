using Sandbox;

public sealed class BallThrow : Component
{
	[Property] public string ThrowAction { get; set; } = "attack1";
	[Property] public float ThrowForce { get; set; } = 700f;
	[Property] public float ThrowUpForce { get; set; } = 120f;
	[Property] public float ThrowStartOffset { get; set; } = 40f;
	[Property] public float PickupDelayAfterThrow { get; set; } = 0.25f;
	[Property] public GameObject ThrowDirectionSource { get; set; }
	[Property] public float MinThrowChargeTime { get; set; } = 0.05f;
	[Property] public float MaxThrowChargeTime { get; set; } = 1.0f;
	[Property] public float MinThrowForceMultiplier { get; set; } = 0.35f;
	[Property] public float MinThrowUpForceMultiplier { get; set; } = 0.6f;

	private BallGrab ballGrab;
	private bool isChargingThrow;
	private float throwChargeStartedAt;

	protected override void OnStart()
	{
		ballGrab = Components.Get<BallGrab>();
	}

	protected override void OnUpdate()
	{
		if ( ballGrab is null )
			return;

		if ( !ballGrab.IsHolding )
		{
			isChargingThrow = false;
			return;
		}

		if ( Input.Pressed( ThrowAction ) )
		{
			StartThrowCharge();
		}

		if ( isChargingThrow && Input.Released( ThrowAction ) )
		{
			ThrowHeldBall();
		}
	}

	private void StartThrowCharge()
	{
		isChargingThrow = true;
		throwChargeStartedAt = Time.Now;
	}

	private void ThrowHeldBall()
	{
		isChargingThrow = false;

		var releasedBall = ballGrab.ReleaseHeldBall();
		if ( !releasedBall.IsValid() )
			return;

		ballGrab.BlockPickupForSeconds( PickupDelayAfterThrow );

		var releasedBallBody = releasedBall.Components.Get<Rigidbody>();
		if ( !releasedBallBody.IsValid() )
			return;

		var chargeHeldSeconds = Time.Now - throwChargeStartedAt;
		var clampedChargeSeconds = chargeHeldSeconds.Clamp( MinThrowChargeTime, MaxThrowChargeTime );
		var chargeLerp = MaxThrowChargeTime <= MinThrowChargeTime
			? 1f
			: (clampedChargeSeconds - MinThrowChargeTime) / (MaxThrowChargeTime - MinThrowChargeTime);
		var throwForceMultiplier = MinThrowForceMultiplier.LerpTo( 1f, chargeLerp );
		var throwUpForceMultiplier = MinThrowUpForceMultiplier.LerpTo( 1f, chargeLerp );

		var directionSource = ThrowDirectionSource.IsValid() ? ThrowDirectionSource : GameObject;
		var throwDirection = directionSource.WorldRotation.Forward;
		var throwStartPosition = releasedBall.Transform.Position + (throwDirection * ThrowStartOffset) + (Vector3.Up * 10f);
		releasedBall.Transform.Position = throwStartPosition;
		releasedBallBody.Velocity = (throwDirection * ThrowForce * throwForceMultiplier) + (Vector3.Up * ThrowUpForce * throwUpForceMultiplier);
	}
}
