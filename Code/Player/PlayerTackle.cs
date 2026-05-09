using Sandbox;
using System;
using System.Collections.Generic;

// PlayerTackle — quick map (editor Outline or Ctrl+F the symbol name)
//   Inspector properties ........ top of class
//   Sync / net state ............ isRagdolled, NetRagdollPosition, NetStandUpPosition, cooldowns
//   OnStart / OnUpdate .......... camera ref, ragdoll transitions, owner camera + free look
//   Host detection .............. TryDetectAndApplyHostTackle, ApplyTackleCooldownOnHost
//   Client → host ............... TryOwnerRequestTackleOnHost, RequestTackleApplyOnHost
//   Victim pick ................. TryFindTackleVictim
//   Hit + ball .................. ExecuteTackle
//   Ragdoll (host only) ......... SpawnRagdollObject, AddVictimClothingToRagdoll, HandleRagdollRecovery, IsRagdollGroundedAndSettled
//   Juggernaut ramp ............. UpdateTackleChargeBonus
//   Local down / up ............. ApplyRagdollLocally, StandUpLocally
//   Stand-up camera blend ....... OnPreRender (lerp last ragdoll cam -> PlayerController camera this frame)
public sealed class PlayerTackle : Component
{
	[Property] public float TackleDirectionThreshold { get; set; } = 0.5f;
	[Property] public float TackleCooldown { get; set; } = 1f;
	[Property] public float TackleLaunchSpeed { get; set; } = 600f;
	[Property] public float TackleLaunchArc { get; set; } = 0.35f; // upward blend vs flat tackleDir for ragdoll + tackled ball knock-off
	/// <summary>Host: attacker cannot auto-grab the ball for this long after tackling someone who was holding it (stops instant vacuum after knock-off). 0 = no lockout.</summary>
	[Property] public float AttackerPickupLockoutAfterCarrierTackle { get; set; } = 0.45f;
	[Property] public float RagdollCameraDistance { get; set; } = 200f;
	[Property] public float RagdollCameraHeight { get; set; } = 80f;
	[Property] public bool EnableTackleDebugLogs { get; set; } = false;
	/// <summary>Max allowed difference between owner-reported positions and host positions for tackle RPC (units). Beyond this, we reject as desync/cheat.</summary>
	[Property] public float TackleRpcPositionSlop { get; set; } = 128f;
	/// <summary>Extra multiplier on tackle radius when validating owner snapshots (latency compensation).</summary>
	[Property] public float TackleRpcRadiusFudge { get; set; } = 1.12f;
	/// <summary>Host: seconds to wait after networking the ragdoll before ApplyImpulse (lets local physics bodies exist). Too low can weaken launch; too high leaves a visible stall before flight.</summary>
	[Property] public float RagdollPhysicsInitDelay { get; set; } = 0.05f;
	/// <summary>Seconds to ease main camera from ragdoll orbit to normal third-person after stand-up. 0 = hand off immediately.</summary>
	[Property] public float StandUpCameraBlendDuration { get; set; } = 0.6f;

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

	// Last ragdoll orbit camera (owner); used as blend start when standing up
	private Vector3 lastRagdollCameraPos;
	private Rotation lastRagdollCameraRot;
	private Vector3 standUpCameraBlendFromPos;
	private Rotation standUpCameraBlendFromRot;
	/// <summary>&lt; 0 = not blending. Otherwise Time.Now when stand-up blend started.</summary>
	private float standUpCameraBlendStartTime = -1f;

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

			// Free look while down: PlayerController is disabled, so apply look here and keep EyeAngles
			// in sync for stand-up. Camera uses the same angles (third-person orbit).
			if ( playerController.IsValid() )
			{
				playerController.EyeAngles += Input.AnalogLook;
				var look = playerController.EyeAngles;
				look.pitch = MathX.Clamp( look.pitch, -89f, 89f );
				playerController.EyeAngles = look;
			}

			if ( activeCamera.IsValid() )
			{
				var lookRot = playerController.IsValid()
					? playerController.EyeAngles.ToRotation()
					: WorldRotation;
				var orbit = -lookRot.Forward * RagdollCameraDistance + Vector3.Up * RagdollCameraHeight;
				activeCamera.WorldPosition = WorldPosition + orbit;
				activeCamera.WorldRotation = lookRot;
				lastRagdollCameraPos = activeCamera.WorldPosition;
				lastRagdollCameraRot = activeCamera.WorldRotation;
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

	protected override void OnPreRender()
	{
		// After all updates, PlayerController has already placed the camera. Lerp toward that exact
		// transform so we match its third-person math (CameraOffset, collision, etc.) — avoids a snap at t=1.
		if ( IsProxy || isRagdolled )
			return;
		if ( !activeCamera.IsValid() )
			return;
		if ( standUpCameraBlendStartTime < 0f )
			return;

		var duration = StandUpCameraBlendDuration <= 0.0001f ? 0.0001f : StandUpCameraBlendDuration;
		var elapsed = Time.Now - standUpCameraBlendStartTime;
		var tLin = MathX.Clamp( elapsed / duration, 0f, 1f );
		var t = tLin * tLin * (3f - 2f * tLin );
		if ( tLin >= 1f )
			standUpCameraBlendStartTime = -1f;

		var toPos = activeCamera.WorldPosition;
		var toRot = activeCamera.WorldRotation;
		activeCamera.WorldPosition = Vector3.Lerp( standUpCameraBlendFromPos, toPos, t );
		activeCamera.WorldRotation = Rotation.Slerp( standUpCameraBlendFromRot, toRot, t );
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

		if ( victim is null || victim == this || victim.GameObject == GameObject || victim.IsTackleImmune || victim.IsRagdolled
			|| victim.Components.Get<PlayerDodge>() is { IsImmuneToTackle: true } )
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
			if ( candidate.Components.Get<PlayerDodge>() is { IsImmuneToTackle: true } )
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

			var attackerGrab = Components.Get<BallGrab>();
			if ( attackerGrab.IsValid() && AttackerPickupLockoutAfterCarrierTackle > 0f )
			{
				attackerGrab.BlockPickupForSeconds( AttackerPickupLockoutAfterCarrierTackle );
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

	// Spawns a host-owned physics ragdoll at the victim's position.
	// NetworkSpawn makes it visible on all clients automatically.
	// Physics runs on the host without client transform ownership conflicts.
	private async void SpawnRagdollObject( PlayerTackle victim, Vector3 tackleDir, float effectiveLaunchSpeed )
	{
		var ragdollGo = new GameObject( true, "PlayerRagdoll" );
		ragdollGo.WorldPosition = victim.WorldPosition + Vector3.Up * 10f;
		ragdollGo.WorldRotation = victim.WorldRotation;

		// Body = Dresser target when present (matches cosmetics); otherwise first skinned mesh.
		var dresser = victim.Components.Get<Dresser>( FindMode.EverythingInSelfAndDescendants );
		var baseVictimRenderer = dresser.IsValid() && dresser.BodyTarget.IsValid()
			? dresser.BodyTarget
			: victim.Components.Get<SkinnedModelRenderer>( FindMode.EverythingInSelfAndDescendants );

		var primaryRenderer = ragdollGo.AddComponent<SkinnedModelRenderer>();
		primaryRenderer.Model = baseVictimRenderer?.Model;

		var ragdollPhysics = ragdollGo.AddComponent<ModelPhysics>();
		ragdollPhysics.Renderer = primaryRenderer;
		ragdollPhysics.MotionEnabled = true;
		ragdollPhysics.IgnoreRoot = false;
		ragdollPhysics.Enabled = true;

		if ( baseVictimRenderer != null )
			ragdollPhysics.CopyBonesFrom( baseVictimRenderer, true );

		if ( baseVictimRenderer != null )
			AddVictimClothingToRagdoll( victim, ragdollGo, primaryRenderer, baseVictimRenderer );

		// Network as soon as the mesh exists so we don't sit in a hole where the player is hidden
		// (NetIsRagdolled) but the ragdoll object hasn't replicated yet. Impulse is applied after
		// a short host delay so physics bodies exist (see RagdollPhysicsInitDelay).
		ragdollGo.Tags.Add( "ragdoll" );
		victim.ragdollObject = ragdollGo;
		ragdollGo.NetworkSpawn();

		var initDelay = RagdollPhysicsInitDelay.Clamp( 0.01f, 0.25f );
		await GameTask.DelaySeconds( initDelay );
		if ( !ragdollGo.IsValid() )
			return;

		var mp = ragdollGo.Components.Get<ModelPhysics>();
		if ( mp == null || mp.Bodies.Count == 0 )
			return;

		var pb0 = mp.Bodies[0].Component?.PhysicsBody;
		var group = pb0?.PhysicsGroup;

		if ( EnableTackleDebugLogs )
			Log.Info( $"[Tackle] Post-network impulse | Bodies={mp.Bodies.Count} BodyType[0]={pb0?.BodyType} Group={group != null}" );

		var launchDir = (tackleDir + Vector3.Up * TackleLaunchArc).Normal;
		var launchVelocity = launchDir * effectiveLaunchSpeed;

		// Pelvis-only Velocity = launchVelocity killed travel distance: pelvis mass is a small
		// fraction of the whole ragdoll, so linear momentum was tiny and the solver drained it
		// through joints. Applying one impulse M×v at the pelvis (≈ whole-body COM) matches
		// total momentum of "every body at launchVelocity" while joints can still flex limbs
		// relative to the core during flight.
		var totalMass = mp.Mass;
		if ( pb0 != null && totalMass > 0f )
			pb0.ApplyImpulse( launchVelocity * totalMass );

		if ( EnableTackleDebugLogs && pb0 != null )
			Log.Info( $"[Tackle] After velocity | Group={group != null} Vel={pb0.Velocity}" );

		// Tag every physics body for stand-up floor traces.
		foreach ( var body in mp.Bodies )
			body.Component?.GameObject?.Tags.Add( "ragdoll" );
	}

	/// <summary>
	/// Replicates the victim's extra skinned meshes (cosmetics) on the ragdoll by merging skinning to the physics body.
	/// </summary>
	private static void AddVictimClothingToRagdoll(
		PlayerTackle victim,
		GameObject ragdollRoot,
		SkinnedModelRenderer ragdollBody,
		SkinnedModelRenderer victimBody )
	{
		foreach ( var src in victim.Components.GetAll<SkinnedModelRenderer>( FindMode.EverythingInSelfAndDescendants ) )
		{
			if ( !src.IsValid() || src == victimBody || src.Model is null )
				continue;

			var pieceGo = new GameObject( true, src.GameObject.Name );
			pieceGo.Parent = ragdollRoot;
			pieceGo.LocalPosition = Vector3.Zero;
			pieceGo.LocalRotation = Rotation.Identity;
			pieceGo.LocalScale = 1f;
			pieceGo.Tags.Add( "ragdoll" );

			var dst = pieceGo.AddComponent<SkinnedModelRenderer>();
			dst.CopyFrom( src );
			dst.BoneMergeTarget = ragdollBody;
			dst.UseAnimGraph = false;
			dst.CreateBoneObjects = false;
			dst.LodOverride = 0;
			dst.Enabled = src.Enabled;
		}
	}

	private async void HandleRagdollRecovery( PlayerTackle victim )
	{
		var classData = victim.playerClass?.CurrentClass;
		var downTimeAfterGrounded = classData?.RagdollDuration ?? 2f;
		var maxTotalRagdoll = classData?.RagdollMaxDuration ?? 8f;
		var groundSpeedMax = classData?.RagdollGroundSpeedMax ?? 160f;
		var groundTraceDown = classData?.RagdollGroundTraceDown ?? 120f;
		var groundTraceUp = classData?.RagdollGroundTraceUp ?? 24f;
		var invincDuration = classData?.PostTackleInvincibilityDuration ?? 1f;

		var scene = victim.Scene;
		var started = Time.Now;
		var groundedAccum = 0f;
		const float pollSeconds = 0.05f;

		// SpawnRagdollObject is async; wait briefly so ragdollObject exists on the host.
		while ( victim.IsValid() && !victim.ragdollObject.IsValid() && Time.Now - started < 2f )
			await GameTask.DelaySeconds( pollSeconds );

		while ( victim.IsValid() )
		{
			if ( Time.Now - started >= maxTotalRagdoll )
				break;

			var ragdoll = victim.ragdollObject;
			if ( !ragdoll.IsValid() )
				break;

			if ( IsRagdollGroundedAndSettled( ragdoll, scene, groundTraceUp, groundTraceDown, groundSpeedMax ) )
				groundedAccum += pollSeconds;
			else
				groundedAccum = 0f;

			if ( groundedAccum >= downTimeAfterGrounded )
				break;

			await GameTask.DelaySeconds( pollSeconds );
		}

		if ( !victim.IsValid() )
			return;

		// Trace straight down from the ragdoll's pelvis to find the actual floor.
		// ragdollObject.WorldPosition is the pelvis (IgnoreRoot=false) — waist height above the floor.
		// Without this, the player stands up floating at pelvis height and falls in an idle animation
		// until their controller finds the ground.
		var ragdollPos = victim.ragdollObject.IsValid()
			? victim.ragdollObject.WorldPosition
			: victim.WorldPosition;

		var tr = scene.Trace
			.Ray( ragdollPos + Vector3.Up * 30f, ragdollPos + Vector3.Down * 200f )
			.WithoutTags( "ragdoll" )
			.Run();

		victim.NetStandUpPosition = tr.Hit ? tr.HitPosition : ragdollPos;

		if ( victim.ragdollObject.IsValid() )
			victim.ragdollObject.Destroy();
		victim.ragdollObject = null;

		victim.NetIsRagdolled = false;

		victim.NetIsTackleImmune = true;
		await GameTask.DelaySeconds( invincDuration );
		if ( !victim.IsValid() )
			return;

		victim.NetIsTackleImmune = false;
	}

	/// <summary>Pelvis near floor (trace) and not still moving fast from flight or bounce.</summary>
	private static bool IsRagdollGroundedAndSettled(
		GameObject ragdollRoot,
		Scene scene,
		float traceUp,
		float traceDown,
		float maxPelvisSpeed )
	{
		if ( !ragdollRoot.IsValid() )
			return false;

		var pos = ragdollRoot.WorldPosition;
		var tr = scene.Trace
			.Ray( pos + Vector3.Up * traceUp, pos + Vector3.Down * traceDown )
			.WithoutTags( "ragdoll" )
			.Run();
		if ( !tr.Hit )
			return false;

		var mp = ragdollRoot.Components.Get<ModelPhysics>();
		if ( mp == null || mp.Bodies.Count == 0 )
			return false;

		var pelvisBody = mp.Bodies[0].Component?.PhysicsBody;
		if ( pelvisBody == null )
			return false;

		return pelvisBody.Velocity.Length <= maxPelvisSpeed;
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

		standUpCameraBlendStartTime = -1f;

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

		if ( !IsProxy && StandUpCameraBlendDuration > 0.001f && activeCamera.IsValid() )
		{
			standUpCameraBlendFromPos = lastRagdollCameraPos;
			standUpCameraBlendFromRot = lastRagdollCameraRot;
			standUpCameraBlendStartTime = Time.Now;
		}
		else
			standUpCameraBlendStartTime = -1f;

		Log.Info( $"[Tackle] StandUpLocally on {GameObject.Name} | IsProxy={IsProxy}" );
	}
}
