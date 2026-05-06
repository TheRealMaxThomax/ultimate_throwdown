using Sandbox;
using System.Collections.Generic;

public sealed partial class PlayerTackle : Component
{
	[Property] public float TackleDirectionThreshold { get; set; } = 0.5f;
	[Property] public float TackleCooldown { get; set; } = 1f;
	[Property] public float TackleLaunchSpeed { get; set; } = 600f;
	[Property] public float TackleLaunchArc { get; set; } = 0.35f; // upward blend vs flat tackleDir for ragdoll + tackled ball knock-off
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

	// Host-only: Juggernaut-style tackle ramp (see ClassData.TackleChargeRampRate / MaxTackleChargeBonus)
	private float tackleChargeBonus;

	// Camera state (owning machine only)
	private Vector3 ragdollCameraOffset;

	// Host-only: the spawned ragdoll physics object
	private GameObject ragdollObject;

	// Renderers hidden during ragdoll — cached at tackle time so cosmetics are included
	private readonly List<SkinnedModelRenderer> hiddenRenderers = new();

	// Colliders disabled during ragdoll — re-enabled on stand-up
	private readonly List<Collider> disabledColliders = new();

	private CatchUpSpeedBoost speedBoost;
	private PlayerClass playerClass;
	private PlayerController playerController;
	private CameraComponent activeCamera;

	public bool IsTackleImmune => isTackleImmune;
	public bool IsRagdolled => isRagdolled;

	// Pelvis world position synced from host; RagdollClientFeel reads this on owning clients
	public Vector3 SyncedRagdollPelvisPosition => NetRagdollPosition;

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

		// React to ragdoll state first so ragdoll/camera behave correctly this frame (RagdollClientFeel clears its own buffer on transitions)
		if ( isRagdolled != wasRagdolled )
		{
			if ( isRagdolled )
				ApplyRagdollLocally();
			else
				StandUpLocally();

			wasRagdolled = isRagdolled;
		}

		// Re-enforce renderer hide every frame during ragdoll — catches anything that re-enables them
		if ( isRagdolled )
			foreach ( var r in hiddenRenderers )
				if ( r.IsValid() ) r.Enabled = false;

		// Owner: host snaps to simulated pelvis; owning client smoothing is handled by RagdollClientFeel
		if ( isRagdolled && !IsProxy )
		{
			if ( Networking.IsHost )
				WorldPosition = NetRagdollPosition;

			if ( activeCamera.IsValid() )
			{
				activeCamera.WorldPosition = WorldPosition + ragdollCameraOffset;
				var toPlayerCenter = (WorldPosition + Vector3.Up * 36f - activeCamera.WorldPosition).Normal;
				activeCamera.WorldRotation = Rotation.LookAt( toPlayerCenter, Vector3.Up );
			}
		}

		if ( Networking.IsHost )
		{
			if ( isRagdolled )
				tackleChargeBonus = 0f;
			else
				UpdateTackleChargeBonus();
		}

		if ( Networking.IsHost )
			TryDetectAndApplyHostTackle();
	}

	private void TryDetectAndApplyHostTackle()
	{
		if ( isRagdolled )
			return;
		if ( Time.Now < tackleBlockedUntil )
			return;
		if ( speedBoost == null || !speedBoost.IsAtChargeSpeed )
			return;

		var myVelocity = playerController?.Velocity ?? Vector3.Zero;
		var horizontalVelocity = myVelocity.WithZ( 0f );
		if ( horizontalVelocity.Length < 1f )
			return;

		var tackleRadius = playerClass?.CurrentClass?.TriggerSphereRadius ?? 40f;

		foreach ( var candidate in Scene.GetAllComponents<PlayerTackle>() )
		{
			if ( candidate == this )
				continue;
			if ( candidate.IsTackleImmune )
				continue;
			if ( candidate.IsRagdolled )
				continue;

			var distance = Vector3.DistanceBetween( WorldPosition, candidate.WorldPosition );
			if ( distance > tackleRadius )
				continue;

			var toVictim = (candidate.WorldPosition - WorldPosition).WithZ( 0f );
			if ( toVictim.Length < 0.001f )
				continue;

			var dot = Vector3.Dot( horizontalVelocity.Normal, toVictim.Normal );
			if ( dot < TackleDirectionThreshold )
				continue;

			tackleBlockedUntil = Time.Now + TackleCooldown;

			var tackleDir = toVictim.Normal;

			if ( EnableTackleDebugLogs )
				Log.Info( $"[Tackle] {GameObject.Name} → {candidate.GameObject.Name} | Dir={tackleDir}" );

			ExecuteTackle( candidate, tackleDir );
			break;
		}
	}

	private void ExecuteTackle( PlayerTackle victim, Vector3 tackleDir )
	{
		var attackerMass = playerClass?.CurrentClass?.Mass ?? 80f;
		var victimMass = victim.playerClass?.CurrentClass?.Mass ?? 80f;
		if ( attackerMass <= 0f ) attackerMass = 80f;
		if ( victimMass <= 0f ) victimMass = 80f;

		var massRatio = MathX.Clamp( attackerMass / victimMass, 0.5f, 2.5f );
		var juggMult = 1f + tackleChargeBonus;
		var tacklePower = massRatio * juggMult;
		var effectiveLaunchSpeed = TackleLaunchSpeed * tacklePower;

		if ( EnableTackleDebugLogs )
			Log.Info( $"[Tackle] Power massRatio={massRatio:F2} juggMult={juggMult:F2} → launchSpeed={effectiveLaunchSpeed:F0}" );

		var classData = victim.playerClass?.CurrentClass;
		var ballLaunchForce = (classData?.BallLaunchForceOnTackle ?? 500f) * tacklePower;
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
					var ballLaunchDir = (tackleDir + Vector3.Up * TackleLaunchArc).Normal;
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

		SpawnRagdollObject( victim, tackleDir, effectiveLaunchSpeed );
		HandleRagdollRecovery( victim );
	}

	private void UpdateTackleChargeBonus()
	{
		var c = playerClass?.CurrentClass;
		var rate = c?.TackleChargeRampRate ?? 0f;
		var maxBonus = c?.MaxTackleChargeBonus ?? 0f;
		if ( rate <= 0f || maxBonus <= 0f )
		{
			tackleChargeBonus = 0f;
			return;
		}

		if ( speedBoost != null && speedBoost.IsAtChargeSpeed )
			tackleChargeBonus = MathX.Clamp( tackleChargeBonus + rate * Time.Delta, 0f, maxBonus );
		else
			tackleChargeBonus = 0f;
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
