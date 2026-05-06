using Sandbox;
using System;
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
	/// <summary>Max allowed difference between owner-reported positions and host positions for tackle RPC (units). Beyond this, we reject as desync/cheat.</summary>
	[Property] public float TackleRpcPositionSlop { get; set; } = 128f;
	/// <summary>Extra multiplier on tackle radius when validating owner snapshots (latency compensation).</summary>
	[Property] public float TackleRpcRadiusFudge { get; set; } = 1.12f;

	// Host writes, all machines read
	private bool isRagdolled;
	[Sync( SyncFlags.FromHost )] private bool NetIsRagdolled { get => isRagdolled; set => isRagdolled = value; }
	[Sync( SyncFlags.FromHost )] private Vector3 NetRagdollPosition { get; set; }
	[Sync( SyncFlags.FromHost )] private Vector3 NetStandUpPosition { get; set; }

	private bool isTackleImmune;
	[Sync( SyncFlags.FromHost )] private bool NetIsTackleImmune { get => isTackleImmune; set => isTackleImmune = value; }

	private bool wasRagdolled;
	private float tackleBlockedUntil;
	private float netTackleBlockedUntil;
	/// <summary>Host-authoritative; owners read this so remote tackle RPCs line up with cooldown.</summary>
	[Sync( SyncFlags.FromHost )]
	private float NetTackleBlockedUntil { get => netTackleBlockedUntil; set => netTackleBlockedUntil = value; }
	/// <summary>Client-only throttle so we don't spam the host with tackle RPCs every frame.</summary>
	private float nextRemoteTackleRequestAt;

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

	private void ApplyTackleCooldownOnHost()
	{
		tackleBlockedUntil = Time.Now + TackleCooldown;
		NetTackleBlockedUntil = tackleBlockedUntil;
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

		// Remote owners: host often has near-zero Velocity for our pawn (movement runs locally),
		// so host-only sphere checks never see a valid approach vector. Mirror BallThrow: detect
		// locally and request the host using our horizontal move direction.
		if ( Network.IsOwner && !Networking.IsHost )
			TryOwnerRequestTackleOnHost();
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
		if ( !TryFindTackleVictim( Scene, this, WorldPosition, horizontalVelocity, tackleRadius, TackleDirectionThreshold, out var victim, out var tackleDir ) )
			return;

		ApplyTackleCooldownOnHost();

		if ( EnableTackleDebugLogs )
			Log.Info( $"[Tackle] {GameObject.Name} → {victim.GameObject.Name} | Dir={tackleDir}" );

		ExecuteTackle( victim, tackleDir );
	}

	private void TryOwnerRequestTackleOnHost()
	{
		if ( isRagdolled )
			return;
		// Match host cooldown (host rejects Rpc while tackleBlockedUntil is active).
		if ( Time.Now < NetTackleBlockedUntil )
			return;
		if ( Time.Now < nextRemoteTackleRequestAt )
			return;
		if ( speedBoost == null || !speedBoost.IsAtChargeSpeed )
			return;

		var myVelocity = playerController?.Velocity ?? Vector3.Zero;
		var horizontalVelocity = myVelocity.WithZ( 0f );
		if ( horizontalVelocity.Length < 1f )
		{
			var forwardFlat = WorldRotation.Forward.WithZ( 0f );
			if ( forwardFlat.Length < 0.001f )
				return;
			horizontalVelocity = forwardFlat.Normal;
		}

		var tackleRadius = playerClass?.CurrentClass?.TriggerSphereRadius ?? 40f;
		if ( !TryFindTackleVictim( Scene, this, WorldPosition, horizontalVelocity, tackleRadius, TackleDirectionThreshold, out var victim, out _ ) )
			return;

		var moveDir = horizontalVelocity.WithZ( 0f ).Normal;
		var attackerPos = WorldPosition;
		var victimPos = victim.WorldPosition;
		nextRemoteTackleRequestAt = Time.Now + (TackleCooldown * 0.2f).Clamp( 0.05f, 0.25f );
		RequestTackleApplyOnHost( victim.GameObject.Id, moveDir, attackerPos, victimPos );
	}

	[Rpc.Host]
	private void RequestTackleApplyOnHost(
		Guid victimRootId,
		Vector3 horizontalMoveDirectionFromOwner,
		Vector3 attackerWorldPosFromOwner,
		Vector3 victimWorldPosFromOwner )
	{
		if ( Network.Owner is null || Rpc.Caller.SteamId != Network.Owner.SteamId )
			return;
		if ( isRagdolled )
			return;
		if ( Time.Now < tackleBlockedUntil )
			return;
		if ( speedBoost == null )
			return;

		var moveDir = horizontalMoveDirectionFromOwner.WithZ( 0f );
		if ( moveDir.Length < 0.001f )
			return;
		moveDir = moveDir.Normal;

		PlayerTackle victim = null;
		foreach ( var t in Scene.GetAllComponents<PlayerTackle>() )
		{
			if ( t.GameObject.Id != victimRootId )
				continue;
			victim = t;
			break;
		}

		if ( victim is null || victim == this || victim.GameObject == GameObject || victim.IsTackleImmune || victim.IsRagdolled )
		{
			if ( EnableTackleDebugLogs )
				Log.Info( "[Tackle] Rpc reject: invalid victim (null/self/immune/ragdolled)" );
			return;
		}

		var hostAttackerPos = WorldPosition;
		var hostVictimPos = victim.WorldPosition;
		if ( Vector3.DistanceBetween( attackerWorldPosFromOwner, hostAttackerPos ) > TackleRpcPositionSlop
			|| Vector3.DistanceBetween( victimWorldPosFromOwner, hostVictimPos ) > TackleRpcPositionSlop )
		{
			if ( EnableTackleDebugLogs )
				Log.Info( $"[Tackle] Rpc reject: position slop atk={Vector3.DistanceBetween( attackerWorldPosFromOwner, hostAttackerPos ):F0} vic={Vector3.DistanceBetween( victimWorldPosFromOwner, hostVictimPos ):F0}" );
			return;
		}

		var tackleRadius = (playerClass?.CurrentClass?.TriggerSphereRadius ?? 40f) * TackleRpcRadiusFudge;
		var toVictimOwner = (victimWorldPosFromOwner - attackerWorldPosFromOwner).WithZ( 0f );
		if ( toVictimOwner.Length < 0.001f )
			return;

		var distOwner = toVictimOwner.Length;
		if ( distOwner > tackleRadius )
		{
			if ( EnableTackleDebugLogs )
				Log.Info( $"[Tackle] Rpc reject: owner distance {distOwner:F0} > {tackleRadius:F0}" );
			return;
		}

		if ( Vector3.Dot( moveDir, toVictimOwner.Normal ) < TackleDirectionThreshold )
		{
			if ( EnableTackleDebugLogs )
				Log.Info( "[Tackle] Rpc reject: approach cone" );
			return;
		}

		ApplyTackleCooldownOnHost();
		var tackleDir = toVictimOwner.Normal;

		if ( EnableTackleDebugLogs )
			Log.Info( $"[Tackle] Rpc {GameObject.Name} → {victim.GameObject.Name} | Dir={tackleDir}" );

		ExecuteTackle( victim, tackleDir );
	}

	/// <summary>Find one valid tackle target using a horizontal approach direction (world space).</summary>
	private static bool TryFindTackleVictim(
		Scene scene,
		PlayerTackle attacker,
		Vector3 attackerWorldPos,
		Vector3 horizontalVelocity,
		float tackleRadius,
		float directionThreshold,
		out PlayerTackle victim,
		out Vector3 tackleDir )
	{
		victim = null;
		tackleDir = default;

		var hv = horizontalVelocity.WithZ( 0f );
		if ( hv.Length < 1f )
			return false;

		var hvNorm = hv.Normal;

		foreach ( var candidate in scene.GetAllComponents<PlayerTackle>() )
		{
			if ( candidate == attacker )
				continue;
			if ( candidate.GameObject == attacker.GameObject )
				continue;
			if ( candidate.IsTackleImmune )
				continue;
			if ( candidate.IsRagdolled )
				continue;

			var distance = Vector3.DistanceBetween( attackerWorldPos, candidate.WorldPosition );
			if ( distance > tackleRadius )
				continue;

			var toVictim = (candidate.WorldPosition - attackerWorldPos).WithZ( 0f );
			if ( toVictim.Length < 0.001f )
				continue;

			if ( Vector3.Dot( hvNorm, toVictim.Normal ) < directionThreshold )
				continue;

			victim = candidate;
			tackleDir = toVictim.Normal;
			return true;
		}

		return false;
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
