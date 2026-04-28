using Sandbox;

public sealed class CatchUpSpeedBoost : Component
{
	[Property] public string SprintAction { get; set; } = "run";
	[Property] public float SprintTimeBeforeBoost { get; set; } = 2.0f;
	[Property] public float SprintBoostMultiplier { get; set; } = 1.15f;
	[Property] public float MinMoveInputForSprint { get; set; } = 0.1f;

	private BallGrab ballGrab;
	private PlayerController playerController;
	private float baseRunSpeed;
	private float sprintTime;
	private bool isBoostActive;

	protected override void OnStart()
	{
		ballGrab = Components.Get<BallGrab>();
		playerController = Components.Get<PlayerController>();
		if ( playerController.IsValid() )
		{
			baseRunSpeed = playerController.RunSpeed;
		}
	}

	protected override void OnUpdate()
	{
		if ( !playerController.IsValid() )
		{
			playerController = Components.Get<PlayerController>();
			if ( playerController.IsValid() )
				baseRunSpeed = playerController.RunSpeed;
		}

		if ( !playerController.IsValid() )
			return;

		if ( ballGrab is null )
			ballGrab = Components.Get<BallGrab>();

		var isHoldingBall = ballGrab?.IsHolding ?? false;
		var isSprinting = Input.Down( SprintAction ) && Input.AnalogMove.Length >= MinMoveInputForSprint;

		if ( !isHoldingBall && isSprinting )
		{
			sprintTime += Time.Delta;
		}
		else
		{
			sprintTime = 0f;
		}

		var shouldHaveBoost = !isHoldingBall && isSprinting && sprintTime >= SprintTimeBeforeBoost;

		if ( shouldHaveBoost && !isBoostActive )
		{
			playerController.RunSpeed = baseRunSpeed * SprintBoostMultiplier;
			isBoostActive = true;
		}
		else if ( !shouldHaveBoost && isBoostActive )
		{
			playerController.RunSpeed = baseRunSpeed;
			isBoostActive = false;
		}
	}
}
