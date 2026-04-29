using Sandbox;

public sealed class BallClientFeel : Component
{
	[Property] public float FreeBallVisualFollowSharpness { get; set; } = 14f;
	[Property] public float ContactBoostSharpness { get; set; } = 42f;
	[Property] public float ContactBoostDuration { get; set; } = 0.12f;

	private BallGrab ballGrab;
	private float contactBoostUntilTime;
	private bool appliedHeldProxyState;

	protected override void OnStart()
	{
		ballGrab = Components.Get<BallGrab>();
	}

	protected override void OnUpdate()
	{
		if ( Networking.IsHost )
			return;

		if ( ballGrab is null )
		{
			ballGrab = Components.Get<BallGrab>();
			if ( ballGrab is null )
				return;
		}

		var ball = ballGrab.HeldBall;
		if ( !ball.IsValid() )
			return;

		var isHolding = ballGrab.IsHolding;
		if ( isHolding || appliedHeldProxyState )
		{
			ApplyClientProxyBallState( ball, isHolding );
			appliedHeldProxyState = isHolding;
		}

		if ( !Network.IsOwner )
			return;

		if ( isHolding )
		{
			ball.WorldPosition = ballGrab.SyncedBallWorldPosition;
			ball.WorldRotation = ballGrab.SyncedBallWorldRotation;
			return;
		}

		TryTriggerContactVisualBoost( ball );

		var visualSharpness = Time.Now < contactBoostUntilTime ? ContactBoostSharpness : FreeBallVisualFollowSharpness;
		ball.WorldPosition = Vector3.Lerp( ball.WorldPosition, ballGrab.SyncedBallWorldPosition, Time.Delta * visualSharpness );
		ball.WorldRotation = Rotation.Slerp( ball.WorldRotation, ballGrab.SyncedBallWorldRotation, Time.Delta * visualSharpness );
	}

	private void TryTriggerContactVisualBoost( GameObject ball )
	{
		var toBall = (ball.WorldPosition - WorldPosition).WithZ( 0f );
		var distance = toBall.Length;
		if ( distance > 42f )
			return;

		var moveInput = Input.AnalogMove.WithZ( 0f );
		if ( moveInput.Length < 0.15f )
			return;

		var moveDirection = moveInput.Normal;
		var approachDot = moveDirection.Dot( toBall.Normal );
		if ( approachDot < 0.25f )
			return;

		contactBoostUntilTime = Time.Now + ContactBoostDuration;
	}

	private static void ApplyClientProxyBallState( GameObject ball, bool holding )
	{
		foreach ( var body in ball.Components.GetAll<Rigidbody>( FindMode.EverythingInSelfAndDescendants ) )
		{
			if ( !body.IsValid() )
				continue;

			body.Enabled = !holding;
			if ( holding )
			{
				body.Velocity = Vector3.Zero;
				body.AngularVelocity = Vector3.Zero;
			}
		}

		foreach ( var collider in ball.Components.GetAll<Collider>( FindMode.EverythingInSelfAndDescendants ) )
		{
			if ( !collider.IsValid() )
				continue;

			collider.Enabled = !holding;
		}
	}
}
