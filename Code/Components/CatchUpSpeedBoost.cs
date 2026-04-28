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
	private PlayerController playerController;
	private float forwardMoveTime;

	protected override void OnStart()
	{
		ballGrab = Components.Get<BallGrab>();
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

		var isHoldingBall = ballGrab?.IsHolding ?? false;
		var isMovingForward = Input.Down( ForwardAction ) || Input.AnalogMove.y > MinForwardInput;

		if ( isMovingForward )
			forwardMoveTime += Time.Delta;
		else
			forwardMoveTime = 0f;

		var targetSpeed = GetTargetSpeed( isHoldingBall, isMovingForward );
		playerController.WalkSpeed = targetSpeed;
		playerController.RunSpeed = targetSpeed;
	}

	private float GetTargetSpeed( bool isHoldingBall, bool isMovingForward )
	{
		if ( !isMovingForward )
			return StartMoveSpeed;

		if ( forwardMoveTime >= TimeToCatchUpSpeed && !isHoldingBall )
			return CatchUpMoveSpeed;

		if ( forwardMoveTime >= TimeToSprintSpeed )
			return SprintMoveSpeed;

		return StartMoveSpeed;
	}
}
