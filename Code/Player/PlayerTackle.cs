using Sandbox;

public sealed class PlayerTackle : Component
{
	[Property] public float TackleDirectionThreshold { get; set; } = 0.5f;
	[Property] public float TackleCooldown { get; set; } = 1f;
	[Property] public float TackleLaunchSpeed { get; set; } = 600f;
	[Property] public float RagdollCameraDistance { get; set; } = 200f;
	[Property] public float RagdollCameraHeight { get; set; } = 80f;
	[Property] public bool EnableTackleDebugLogs { get; set; } = false;

	// Host writes, all machines read
	private bool isRagdolled;
	[Sync( SyncFlags.FromHost )] private bool NetIsRagdolled { get => isRagdolled; set => isRagdolled = value; }
	[Sync( SyncFlags.FromHost )] private Vector3 NetRagdollPosition { get; set; }
	[Sync( SyncFlags.FromHost )] private Vector3 NetStandUpPosition { get; set; }

	private bool isTackleImmune;
	[Sync( SyncFlags.FromHost )] private bool NetIsTackleImmune { get => isTackleImmune; set => isTackleImmune = value; }

	private bool wasRagdolled;
	private float tackleBlockedUntil;

	// Camera state (owning machine only)
	private Vector3 ragdollCameraOffset;

	// Host-only: the spawned ragdoll physics object
	private GameObject ragdollObject;

	// Renderers hidden during ragdoll — cached at tackle time so cosmetics are included
	private readonly System.Collections.Generic.List<SkinnedModelRenderer> hiddenRenderers = new();

	// Colliders disabled during ragdoll — re-enabled on stand-up
	private readonly System.Collections.Generic.List<Collider> disabledColliders = new();

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
		// Host keeps NetRagdollPosition current from the spawned physics ragdoll
		if ( isRagdolled && Networking.IsHost && ragdollObject.IsValid() )
			NetRagdollPosition = ragdollObject.WorldPosition;

		// Re-enforce renderer hide every frame during ragdoll — catches anything that re-enables them
		if ( isRagdolled )
			foreach ( var r in hiddenRenderers )
				if ( r.IsValid() ) r.Enabled = false;

		// Owner tracks the ragdoll position and drives the camera
		if ( isRagdolled && !IsProxy )
		{
			WorldPosition = NetRagdollPosition;

			if ( activeCamera.IsValid() )
			{
				activeCamera.WorldPosition = WorldPosition + ragdollCameraOffset;
				var toPlayerCenter = (WorldPosition + Vector3.Up * 36f - activeCamera.WorldPosition).Normal;
				activeCamera.WorldRotation = Rotation.LookAt( toPlayerCenter, Vector3.Up );
			}
		}

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

			tackleBlockedUntil = Time.Now + TackleCooldown;

			// Flat direction toward victim — consistent regardless of attacker vertical velocity or speed
			var tackleDir = toVictim.Normal;

			if ( EnableTackleDebugLogs )
				Log.Info( $"[Tackle] {GameObject.Name} → {candidate.GameObject.Name} | Dir={tackleDir}" );

			ExecuteTackle( candidate, tackleDir );
			break;
		}
	}

	private void ExecuteTackle( PlayerTackle victim, Vector3 tackleDir )
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
					var ballLaunchDir = (tackleDir + Vector3.Up * 0.3f).Normal;
					ballBody.Velocity = ballLaunchDir * ballLaunchForce;
				}

				victimBallGrab.BlockPickupForSeconds( ballLockout );
			}
		}

		// Immediately disable victim's capsule on the host before the ragdoll spawns.
		// ApplyRagdollLocally() runs one frame later — this closes that gap so the ragdoll
		// doesn't spawn inside an active collider and get ejected in a random direction.
		var victimPC = victim.Components.Get<PlayerController>();
		if ( victimPC.IsValid() ) victimPC.Enabled = false;
		foreach ( var col in victim.Components.GetAll<Collider>( FindMode.EverythingInSelfAndDescendants ) )
			col.Enabled = false;

		// Seed position before enabling ragdoll so owner doesn't snap to Vector3.Zero
		victim.NetRagdollPosition = victim.WorldPosition;
		victim.NetIsRagdolled = true;

		SpawnRagdollObject( victim, tackleDir );
		HandleRagdollRecovery( victim );
	}

	// Spawns a host-owned physics ragdoll at the victim's position.
	// NetworkSpawn makes it visible on all clients automatically.
	// Physics runs on the host without client transform ownership conflicts.
	private async void SpawnRagdollObject( PlayerTackle victim, Vector3 tackleDir )
	{
		var ragdollGo = new GameObject( true, "PlayerRagdoll" );
		ragdollGo.WorldPosition = victim.WorldPosition;
		ragdollGo.WorldRotation = victim.WorldRotation;

		// Only copy the base body renderer (first one found).
		// Copying clothing renderers too causes them to appear as a T-pose ghost — additional
		// SkinnedModelRenderers on the ragdoll object aren't driven by ModelPhysics.
		var baseVictimRenderer = victim.Components.Get<SkinnedModelRenderer>( FindMode.EverythingInSelfAndDescendants );
		var primaryRenderer = ragdollGo.AddComponent<SkinnedModelRenderer>();
		primaryRenderer.Model = baseVictimRenderer?.Model;

		var ragdollPhysics = ragdollGo.AddComponent<ModelPhysics>();
		ragdollPhysics.Renderer = primaryRenderer;
		ragdollPhysics.Enabled = true;

		ragdollGo.NetworkSpawn();
		victim.ragdollObject = ragdollGo;

		// Brief delay for physics bodies to fully initialise before applying impulse
		await GameTask.DelaySeconds( 0.05f );
		if ( !ragdollGo.IsValid() ) return;

		// PhysicsGroup.Velocity sets velocity on all ragdoll bodies at once — the correct s&box API.
		// Setting velocity on individual bodies via .Component.Velocity fights the constraint solver.
		var launchDir = (tackleDir + Vector3.Up * 0.35f).Normal;
		ragdollPhysics.PhysicsGroup.Velocity = launchDir * TackleLaunchSpeed;

		if ( EnableTackleDebugLogs )
		{
			Log.Info( $"[Tackle] Ragdoll spawned | Bodies={ragdollPhysics.Bodies.Count} | Launch={launchDir} × {TackleLaunchSpeed}" );
			for ( int i = 0; i < ragdollPhysics.Bodies.Count; i++ )
			{
				var b = ragdollPhysics.Bodies[i];
				Log.Info( $"[Ragdoll] Body[{i}] GameObject={b.Component?.GameObject?.Name} Pos={b.Component?.WorldPosition}" );
			}
		}
	}

	private async void HandleRagdollRecovery( PlayerTackle victim )
	{
		var classData = victim.playerClass?.CurrentClass;
		var ragdollDuration = classData?.RagdollDuration ?? 2f;
		var invincDuration = classData?.PostTackleInvincibilityDuration ?? 1f;

		await GameTask.DelaySeconds( ragdollDuration );
		if ( !victim.IsValid() ) return;

		// Snap stand-up to wherever the ragdoll landed
		victim.NetStandUpPosition = victim.ragdollObject.IsValid()
			? victim.ragdollObject.WorldPosition
			: victim.WorldPosition;

		if ( victim.ragdollObject.IsValid() )
			victim.ragdollObject.Destroy();
		victim.ragdollObject = null;

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
		}

		// Cache all renderers at tackle time — cosmetics are loaded by now, unlike at OnStart
		hiddenRenderers.Clear();
		hiddenRenderers.AddRange( Components.GetAll<SkinnedModelRenderer>( FindMode.EverythingInSelfAndDescendants ) );
		foreach ( var r in hiddenRenderers )
			if ( r.IsValid() ) r.Enabled = false;

		// Disable all explicit colliders so they don't interfere with the spawned ragdoll's physics.
		// PlayerController manages its own internal capsule, but there may also be explicit Collider
		// components on the player; disable both to be safe.
		disabledColliders.Clear();
		disabledColliders.AddRange( Components.GetAll<Collider>( FindMode.EverythingInSelfAndDescendants ) );
		foreach ( var col in disabledColliders )
			if ( col.IsValid() ) col.Enabled = false;

		Log.Info( $"[Tackle] Hiding {hiddenRenderers.Count} renderer(s) on {GameObject.Name}" );
		if ( playerController.IsValid() ) playerController.Enabled = false;
	}

	private void StandUpLocally()
	{
		// Snap to ragdoll landing position before re-enabling the controller
		if ( !IsProxy )
			WorldPosition = NetStandUpPosition;

		foreach ( var r in hiddenRenderers )
			if ( r.IsValid() ) r.Enabled = true;
		hiddenRenderers.Clear();

		foreach ( var col in disabledColliders )
			if ( col.IsValid() ) col.Enabled = true;
		disabledColliders.Clear();

		if ( playerController.IsValid() ) playerController.Enabled = true;

		Log.Info( $"[Tackle] StandUpLocally on {GameObject.Name} | IsProxy={IsProxy}" );
	}
}
