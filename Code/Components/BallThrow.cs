using Sandbox;

public sealed class BallThrow : Component
{
	[Property] public string ThrowAction { get; set; } = "attack1";
	[Property] public float ThrowForce { get; set; } = 700f;
	[Property] public float ThrowUpForce { get; set; } = 120f;
	[Property] public float ThrowStartOffset { get; set; } = 40f;
	[Property] public float PickupDelayAfterThrow { get; set; } = 0.25f;
	[Property] public GameObject ThrowDirectionSource { get; set; }

	private BallGrab ballGrab;

	protected override void OnStart()
	{
		ballGrab = Components.Get<BallGrab>();
	}

	protected override void OnUpdate()
	{
		if ( ballGrab is null )
			return;

		if ( !ballGrab.IsHolding )
			return;

		if ( Input.Pressed( ThrowAction ) )
		{
			ThrowHeldBall();
		}
	}

	private void ThrowHeldBall()
	{
		var releasedBall = ballGrab.ReleaseHeldBall();
		if ( !releasedBall.IsValid() )
			return;

		ballGrab.BlockPickupForSeconds( PickupDelayAfterThrow );

		var releasedBallBody = releasedBall.Components.Get<Rigidbody>();
		if ( !releasedBallBody.IsValid() )
			return;

		var directionSource = ThrowDirectionSource.IsValid() ? ThrowDirectionSource : GameObject;
		var throwDirection = directionSource.WorldRotation.Forward;
		var throwStartPosition = releasedBall.Transform.Position + (throwDirection * ThrowStartOffset) + (Vector3.Up * 10f);
		releasedBall.Transform.Position = throwStartPosition;
		releasedBallBody.Velocity = (throwDirection * ThrowForce) + (Vector3.Up * ThrowUpForce);
	}
}
