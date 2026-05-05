using Sandbox;

public sealed class PlayerTackle : Component
{
	[Property] public float BaseTackleForce { get; set; } = 800f;
	[Property] public float TackleImpulseMultiplier { get; set; } = 1f;
	[Property] public float TackleDirectionThreshold { get; set; } = 0.5f;
	[Property] public float TackleCooldown { get; set; } = 1f;
	[Property] public bool EnableTackleDebugLogs { get; set; } = false;

	// Host writes, all machines read and react locally
	private bool isRagdolled;
	[Sync( SyncFlags.FromHost )] private bool NetIsRagdolled { get => isRagdolled; set => isRagdolled = value; }
	[Sync( SyncFlags.FromHost )] private Vector3 NetRagdollImpulse { get; set; }

	private bool isTackleImmune;
	[Sync( SyncFlags.FromHost )] private bool NetIsTackleImmune { get => isTackleImmune; set => isTackleImmune = value; }

	private bool wasRagdolled;
	private float tackleBlockedUntil;

	private CatchUpSpeedBoost speedBoost;
	private PlayerClass playerClass;
	private PlayerController playerController;
	private ModelPhysics modelPhysics;

	public bool IsTackleImmune => isTackleImmune;
	public bool IsRagdolled => isRagdolled;

	protected override void OnStart()
	{
		speedBoost = Components.Get<CatchUpSpeedBoost>();
		playerClass = Components.Get<PlayerClass>();
		playerController = Components.Get<PlayerController>();
		modelPhysics = Components.Get<ModelPhysics>( FindMode.EverythingInSelfAndDescendants );
	}

	protected override void OnUpdate()
	{
		// Every machine reacts to ragdoll state changes locally
		if ( isRagdolled != wasRagdolled )
		{
			Log.Info( $"[Tackle] Ragdoll state change on {GameObject.Name}: {wasRagdolled} → {isRagdolled} | IsHost={Networking.IsHost}" );

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

	private void ExecuteTackle( PlayerTackle victim, Vector3 impulse )
	{
		var classData = victim.playerClass?.CurrentClass;
		var ballLaunchForce = classData?.BallLaunchForceOnTackle ?? 500f;
		var ballLockout = classData?.BallPickupLockoutAfterTackle ?? 1.5f;

		// Drop and launch ball if victim is holding it
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

		// Broadcast ragdoll to all machines via sync
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

	// Called locally on every machine when ragdoll starts
	private async void ApplyRagdollLocally()
	{
		Log.Info( $"[Tackle] ApplyRagdollLocally on {GameObject.Name} | IsHost={Networking.IsHost} | Controller={playerController.IsValid()} | ModelPhysics={modelPhysics.IsValid()}" );

		if ( playerController.IsValid() ) playerController.Enabled = false;
		if ( modelPhysics.IsValid() ) modelPhysics.Enabled = true;

		// Poll until PhysicsGroup is ready — can take several frames after ModelPhysics is enabled
		PhysicsGroup physGroup = null;
		var deadline = Time.Now + 1f;
		while ( physGroup == null && Time.Now < deadline )
		{
			await GameTask.DelaySeconds( 0.05f );
			if ( !IsValid ) return;
			physGroup = modelPhysics?.PhysicsGroup;
		}

		Log.Info( $"[Tackle] PhysicsGroup on {GameObject.Name} | Found={physGroup != null} | BodyCount={physGroup?.BodyCount}" );

		if ( physGroup != null && physGroup.BodyCount > 0 )
		{
			physGroup.GetBody( 0 ).ApplyImpulse( NetRagdollImpulse );
			Log.Info( $"[Tackle] Impulse applied on {GameObject.Name}: {NetRagdollImpulse.Length:F0}" );
		}
	}

	// Called locally on every machine when ragdoll ends
	private void StandUpLocally()
	{
		if ( modelPhysics.IsValid() ) modelPhysics.Enabled = false;
		if ( playerController.IsValid() ) playerController.Enabled = true;

		Log.Info( $"[Tackle] StandUpLocally on {GameObject.Name} | IsHost={Networking.IsHost} | Controller re-enabled={playerController.IsValid()}" );
	}
}
