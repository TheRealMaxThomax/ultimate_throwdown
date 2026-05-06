using Sandbox;

public sealed class PlayerTackle : Component
{
	[Property] public float BaseTackleForce { get; set; } = 800f;
	[Property] public float TackleImpulseMultiplier { get; set; } = 1f;
	[Property] public float TackleDirectionThreshold { get; set; } = 0.5f;
	[Property] public float TackleCooldown { get; set; } = 1f;
	[Property] public float TackleLaunchSpeed { get; set; } = 600f;
	[Property] public float RagdollGravity { get; set; } = 800f;
	[Property] public float RagdollCameraDistance { get; set; } = 200f;
	[Property] public float RagdollCameraHeight { get; set; } = 80f;
	[Property] public bool EnableTackleDebugLogs { get; set; } = false;

	// Host writes, all machines read and react locally
	private bool isRagdolled;
	[Sync( SyncFlags.FromHost )] private bool NetIsRagdolled { get => isRagdolled; set => isRagdolled = value; }
	[Sync( SyncFlags.FromHost )] private Vector3 NetRagdollImpulse { get; set; }

	private bool isTackleImmune;
	[Sync( SyncFlags.FromHost )] private bool NetIsTackleImmune { get => isTackleImmune; set => isTackleImmune = value; }

	private bool wasRagdolled;
	private float tackleBlockedUntil;

	// Ragdoll simulation state (owning machine only)
	private Vector3 ragdollVelocity;
	private float ragdollGroundZ;
	private Vector3 ragdollCameraOffset;

	private CatchUpSpeedBoost speedBoost;
	private PlayerClass playerClass;
	private PlayerController playerController;
	private CameraComponent activeCamera;

	public bool IsTackleImmune => isTackleImmune;
	public bool IsRagdolled => isRagdolled;

	protected override void OnStart()
	{
		speedBoost = Components.Get<CatchUpSpeedBoost>();
		playerClass = Components.Get<PlayerClass>();
		playerController = Components.Get<PlayerController>();

		foreach ( var cam in Scene.GetAllComponents<CameraComponent>() )
		{
			if ( cam.IsMainCamera )
			{
				activeCamera = cam;
				break;
			}
		}
	}

	protected override void OnUpdate()
	{
		// Owning machine simulates position and drives camera during ragdoll
		if ( isRagdolled && !IsProxy )
			SimulateRagdoll();

		// Every machine reacts to ragdoll state changes locally
		if ( isRagdolled != wasRagdolled )
		{
			if ( isRagdolled )
				ApplyRagdollLocally();
			else
				StandUpLocally();

			wasRagdolled = isRagdolled;
		}

		// Tackle detection — host only
		if ( !Networking.IsHost ) return;
		if ( isRagdolled ) return;
		if ( Time.Now < tackleBlockedUntil ) return;
		if ( speedBoost == null || !speedBoost.IsAtChargeSpeed ) return;

		var myVelocity = playerController?.Velocity ?? Vector3.Zero;
		var horizontalVelocity = myVelocity.WithZ( 0f );
		if ( horizontalVelocity.Length < 1f ) return;

		var tackleRadius = playerClass?.CurrentClass?.TriggerSphereRadius ?? 40f;

		foreach ( var candidate in Scene.GetAllComponents<PlayerTackle>() )
		{
			if ( candidate == this ) continue;
			if ( candidate.IsTackleImmune ) continue;
			if ( candidate.IsRagdolled ) continue;

			var distance = Vector3.DistanceBetween( WorldPosition, candidate.WorldPosition );
			if ( distance > tackleRadius ) continue;

			var toVictim = (candidate.WorldPosition - WorldPosition).WithZ( 0f );
			if ( toVictim.Length < 0.001f ) continue;

			var dot = Vector3.Dot( horizontalVelocity.Normal, toVictim.Normal );
			if ( dot < TackleDirectionThreshold ) continue;

			var myMass = playerClass?.CurrentClass?.Mass ?? 80f;
			var victimMass = candidate.playerClass?.CurrentClass?.Mass ?? 80f;
			var massRatio = (myMass / victimMass).Clamp( 0.5f, 2.5f );
			var impulse = myVelocity * BaseTackleForce * massRatio * TackleImpulseMultiplier;

			tackleBlockedUntil = Time.Now + TackleCooldown;

			if ( EnableTackleDebugLogs )
				Log.Info( $"[Tackle] {GameObject.Name} → {candidate.GameObject.Name} | MassRatio={massRatio:F2} | Impulse={impulse.Length:F0}" );

			ExecuteTackle( candidate, impulse );
			break;
		}
	}

	// Owning machine simulates fly-back each frame.
	// WorldPosition is replicated to host so the host sees the victim fly.
	private void SimulateRagdoll()
	{
		ragdollVelocity.z -= RagdollGravity * Time.Delta;
		var nextPos = WorldPosition + ragdollVelocity * Time.Delta;

		if ( nextPos.z < ragdollGroundZ )
		{
			nextPos = nextPos.WithZ( ragdollGroundZ );
			ragdollVelocity = ragdollVelocity.WithZ( 0f );
		}

		WorldPosition = nextPos;

		if ( activeCamera.IsValid() )
		{
			activeCamera.WorldPosition = WorldPosition + ragdollCameraOffset;
			var toPlayerCenter = (WorldPosition + Vector3.Up * 36f - activeCamera.WorldPosition).Normal;
			activeCamera.WorldRotation = Rotation.LookAt( toPlayerCenter, Vector3.Up );
		}
	}

	private void ExecuteTackle( PlayerTackle victim, Vector3 impulse )
	{
		var classData = victim.playerClass?.CurrentClass;
		var ballLaunchForce = classData?.BallLaunchForceOnTackle ?? 500f;
		var ballLockout = classData?.BallPickupLockoutAfterTackle ?? 1.5f;

		var victimBallGrab = victim.Components.Get<BallGrab>();
		if ( victimBallGrab?.IsHolding == true )
		{
			var droppedBall = victimBallGrab.ReleaseHeldBall();
			if ( droppedBall.IsValid() )
			{
				var ballBody = droppedBall.Components.Get<Rigidbody>( FindMode.EverythingInSelfAndDescendants );
				if ( ballBody.IsValid() )
				{
					var launchDir = (impulse.Normal + Vector3.Up * 0.3f).Normal;
					ballBody.Velocity = launchDir * ballLaunchForce;
				}

				victimBallGrab.BlockPickupForSeconds( ballLockout );
			}
		}

		victim.NetRagdollImpulse = impulse;
		victim.NetIsRagdolled = true;

		HandleRagdollRecovery( victim );
	}

	private async void HandleRagdollRecovery( PlayerTackle victim )
	{
		var classData = victim.playerClass?.CurrentClass;
		var ragdollDuration = classData?.RagdollDuration ?? 2f;
		var invincDuration = classData?.PostTackleInvincibilityDuration ?? 1f;

		await GameTask.DelaySeconds( ragdollDuration );
		if ( !victim.IsValid() ) return;

		victim.NetIsRagdolled = false;

		victim.NetIsTackleImmune = true;
		await GameTask.DelaySeconds( invincDuration );
		if ( !victim.IsValid() ) return;

		victim.NetIsTackleImmune = false;
	}

	private void ApplyRagdollLocally()
	{
		Log.Info( $"[Tackle] ApplyRagdollLocally on {GameObject.Name} | IsProxy={IsProxy}" );

		if ( !IsProxy )
		{
			var playerForward = WorldRotation.Forward.WithZ( 0f );
			if ( playerForward.LengthSquared > 0.001f ) playerForward = playerForward.Normal;
			ragdollCameraOffset = -playerForward * RagdollCameraDistance + Vector3.Up * RagdollCameraHeight;

			ragdollGroundZ = WorldPosition.z;
			var launchDir = (NetRagdollImpulse.Normal + Vector3.Up * 0.7f).Normal;
			ragdollVelocity = launchDir * TackleLaunchSpeed;

			Log.Info( $"[Tackle] Launch: {launchDir} × {TackleLaunchSpeed}" );
		}

		if ( playerController.IsValid() ) playerController.Enabled = false;
	}

	private void StandUpLocally()
	{
		if ( playerController.IsValid() ) playerController.Enabled = true;
		ragdollVelocity = Vector3.Zero;
		Log.Info( $"[Tackle] StandUpLocally on {GameObject.Name} | IsProxy={IsProxy}" );
	}
}
