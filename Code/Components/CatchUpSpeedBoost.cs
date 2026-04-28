using Sandbox;

public sealed class CatchUpSpeedBoost : Component
{
	[Property] public string ForwardAction { get; set; } = "forward";
	[Property] public float StartMoveSpeed { get; set; } = 140f;
	[Property] public float SprintMoveSpeed { get; set; } = 220f;
	[Property] public float CatchUpMoveSpeed { get; set; } = 320f;
	[Property] public float TimeToSprintSpeed { get; set; } = 2.0f;
	[Property] public float TimeToCatchUpSpeed { get; set; } = 4.0f;
	[Property] public float MinForwardInput { get; set; } = 0.1f;

	private BallGrab ballGrab;
	private BallThrow ballThrow;
	private PlayerController playerController;
	private float forwardMoveTime;
	private float nonHoldingForwardMoveTime;

	protected override void OnStart()
	{
		ballGrab = Components.Get<BallGrab>();
		ballThrow = Components.Get<BallThrow>();
		playerController = Components.Get<PlayerController>();
	}

	protected override void OnUpdate()
	{
		if ( !playerController.IsValid() )
		{
			playerController = Components.Get<PlayerController>();
		}

		if ( !playerController.IsValid() )
			return;

		if ( ballGrab is null )
			ballGrab = Components.Get<BallGrab>();
		if ( ballThrow is null )
			ballThrow = Components.Get<BallThrow>();

		var isHoldingBall = ballGrab?.IsHolding ?? false;
		var isChargingThrow = ballThrow?.IsChargingThrow ?? false;
		var isMovingForward = Input.Down( ForwardAction ) || Input.AnalogMove.y > MinForwardInput;

		if ( isChargingThrow )
		{
			forwardMoveTime = 0f;
			nonHoldingForwardMoveTime = 0f;
			playerController.WalkSpeed = StartMoveSpeed;
			playerController.RunSpeed = StartMoveSpeed;
			return;
		}

		if ( isMovingForward )
			forwardMoveTime += Time.Delta;
		else
			forwardMoveTime = 0f;

		if ( !isHoldingBall && isMovingForward )
			nonHoldingForwardMoveTime += Time.Delta;
		else
			nonHoldingForwardMoveTime = 0f;

		var targetSpeed = GetTargetSpeed( isHoldingBall, isMovingForward );
		playerController.WalkSpeed = targetSpeed;
		playerController.RunSpeed = targetSpeed;
	}

	private float GetTargetSpeed( bool isHoldingBall, bool isMovingForward )
	{
		if ( !isMovingForward )
			return StartMoveSpeed;

		if ( forwardMoveTime < TimeToSprintSpeed )
			return StartMoveSpeed;

		if ( isHoldingBall )
			return SprintMoveSpeed;

		// Catch-up timer only runs while not holding the ball.
		// This keeps a clear 3-stage flow:
		// Start -> Sprint at TimeToSprintSpeed -> CatchUp at TimeToCatchUpSpeed.
		if ( nonHoldingForwardMoveTime >= TimeToCatchUpSpeed )
			return CatchUpMoveSpeed;

		return SprintMoveSpeed;
	}
}
