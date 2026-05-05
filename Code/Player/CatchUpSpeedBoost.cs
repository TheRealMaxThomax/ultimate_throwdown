using Sandbox;
using System;

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
	private PlayerClass playerClass;
	private float forwardMoveTime;
	private float nonHoldingSprintTime;
	public bool IsAtChargeSpeed { get; private set; }

	protected override void OnStart()
	{
		ballGrab = Components.Get<BallGrab>();
		ballThrow = Components.Get<BallThrow>();
		playerController = Components.Get<PlayerController>();
		playerClass = Components.Get<PlayerClass>();
	}

	protected override void OnUpdate()
	{
		if ( IsProxy )
			return;

		if ( !playerController.IsValid() )
			playerController = Components.Get<PlayerController>();

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
			nonHoldingSprintTime = 0f;
			IsAtChargeSpeed = false;
			var startSpeed = ClassStat( playerClass?.CurrentClass?.StartMoveSpeed, StartMoveSpeed );
			playerController.WalkSpeed = startSpeed;
			playerController.RunSpeed = startSpeed;
			return;
		}

		if ( isMovingForward )
			forwardMoveTime += Time.Delta;
		else
			forwardMoveTime = 0f;

		var timeToSprint = ClassStat( playerClass?.CurrentClass?.TimeToSprintSpeed, TimeToSprintSpeed );
		var isInSprintStage = isMovingForward && forwardMoveTime >= timeToSprint;
		if ( !isHoldingBall && isInSprintStage )
			nonHoldingSprintTime += Time.Delta;
		else
			nonHoldingSprintTime = 0f;

		playerController.WalkSpeed = GetTargetSpeed( isHoldingBall, isMovingForward );
		playerController.RunSpeed = playerController.WalkSpeed;
	}

	private float GetTargetSpeed( bool isHoldingBall, bool isMovingForward )
	{
		var startSpeed = ClassStat( playerClass?.CurrentClass?.StartMoveSpeed, StartMoveSpeed );
		var sprintSpeed = ClassStat( playerClass?.CurrentClass?.SprintMoveSpeed, SprintMoveSpeed );
		var catchUpSpeed = ClassStat( playerClass?.CurrentClass?.CatchUpMoveSpeed, CatchUpMoveSpeed );
		var timeToSprint = ClassStat( playerClass?.CurrentClass?.TimeToSprintSpeed, TimeToSprintSpeed );
		var timeToCatchUp = ClassStat( playerClass?.CurrentClass?.TimeToCatchUpSpeed, TimeToCatchUpSpeed );

		if ( !isMovingForward )
		{
			IsAtChargeSpeed = false;
			return startSpeed;
		}

		if ( forwardMoveTime < timeToSprint )
		{
			IsAtChargeSpeed = false;
			return startSpeed;
		}

		if ( isHoldingBall )
		{
			IsAtChargeSpeed = false;
			return sprintSpeed;
		}

		var catchUpDelay = MathF.Max( 0f, timeToCatchUp - timeToSprint );
		var atCatchUp = nonHoldingSprintTime >= catchUpDelay;
		IsAtChargeSpeed = atCatchUp;
		return atCatchUp ? catchUpSpeed : sprintSpeed;
	}

	private static float ClassStat( float? classStat, float fallback )
	{
		return classStat ?? fallback;
	}
}
