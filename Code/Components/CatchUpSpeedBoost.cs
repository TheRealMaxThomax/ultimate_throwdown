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
	[Property] public float BallPushBlockRadius { get; set; } = 90f;
	[Property] public float BallPushApproachDot { get; set; } = 0.25f;

	private BallGrab ballGrab;
	private BallThrow ballThrow;
	private PlayerController playerController;
	private Rigidbody playerBody;
	private float forwardMoveTime;
	private float nonHoldingSprintTime;

	protected override void OnStart()
	{
		ballGrab = Components.Get<BallGrab>();
		ballThrow = Components.Get<BallThrow>();
		playerController = Components.Get<PlayerController>();
		playerBody = Components.Get<Rigidbody>();
	}

	protected override void OnUpdate()
	{
		// Networking safety: movement speed authority should come from the
		// authoritative instance, not proxy copies.
		if ( IsProxy )
			return;

		if ( !playerController.IsValid() )
		{
			playerController = Components.Get<PlayerController>();
		}
		if ( !playerBody.IsValid() )
		{
			playerBody = Components.Get<Rigidbody>();
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
			nonHoldingSprintTime = 0f;
			playerController.WalkSpeed = StartMoveSpeed;
			playerController.RunSpeed = StartMoveSpeed;
			return;
		}

		if ( isMovingForward )
			forwardMoveTime += Time.Delta;
		else
			forwardMoveTime = 0f;

		var isInSprintStage = isMovingForward && forwardMoveTime >= TimeToSprintSpeed;
		if ( !isHoldingBall && isInSprintStage )
			nonHoldingSprintTime += Time.Delta;
		else
			nonHoldingSprintTime = 0f;

		var targetSpeed = GetTargetSpeed( isHoldingBall, isMovingForward );
		var isPushBlockActive = ShouldBlockCatchUpNearBall( isHoldingBall );
		if ( isPushBlockActive )
		{
			// Anti-dribble rule: don't allow catch-up charge speed while actively
			// running into the free ball. This still allows normal sprint speed.
			targetSpeed = MathF.Min( targetSpeed, SprintMoveSpeed );
			// Require the usual build-up time again after leaving push-block state.
			nonHoldingSprintTime = 0f;
		}

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
		// If sprint starts at 2s and catch-up is at 4s total,
		// this means 2s in sprint before catch-up.
		var catchUpDelay = TimeToCatchUpSpeed - TimeToSprintSpeed;
		if ( catchUpDelay < 0f )
			catchUpDelay = 0f;

		if ( nonHoldingSprintTime >= catchUpDelay )
			return CatchUpMoveSpeed;

		return SprintMoveSpeed;
	}

	private bool ShouldBlockCatchUpNearBall( bool isHoldingBall )
	{
		if ( isHoldingBall )
			return false;

		var ball = ballGrab?.HeldBall;
		if ( !ball.IsValid() )
			return false;

		var toBall = (ball.WorldPosition - WorldPosition).WithZ( 0f );
		var distance = toBall.Length;
		if ( distance > BallPushBlockRadius || distance <= 0.001f )
			return false;

		var moveVelocity = playerBody.IsValid() ? playerBody.Velocity.WithZ( 0f ) : Vector3.Zero;
		if ( moveVelocity.Length < 5f )
			return false;

		var approachDot = moveVelocity.Normal.Dot( toBall / distance );
		return approachDot >= BallPushApproachDot;
	}
}
