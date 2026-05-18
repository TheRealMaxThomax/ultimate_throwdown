using Sandbox;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

// PlayerTackle — quick map (editor Outline or Ctrl+F the symbol name)
//   Inspector properties ........ top of class
//   Sync / net state ............ isRagdolled, ragdoll/attack slow-catch windows, tackle strip id, cooldowns
//   OnStart / OnUpdate .......... camera ref, ragdoll transitions, owner camera + free look
//   Host detection .............. TryDetectAndApplyHostTackle, ApplyTackleCooldownOnHost
//   Client → host ............... TryOwnerRequestTackleOnHost, RequestTackleApplyOnHost
//   Victim pick ................. TryFindTackleVictim (cone vs horizontal view forward, not velocity)
//   Hit + ball .................. ExecuteTackle
//   Ragdoll (host only) ......... SpawnRagdollObject, AddVictimClothingToRagdoll, HandleRagdollRecovery, IsRagdollGroundedAndSettled
//   Juggernaut ramp ............. StepTackleChargeBonus (host + owner mirror for RPC)
//   Local down / up ............. ApplyRagdollLocally, StandUpLocally
//   Stand-up camera blend ....... OnPreRender (lerp last ragdoll cam -> PlayerController camera this frame)
public sealed class PlayerTackle : Component
{
	private const string PracticeNpcTag = "practice_npc";

	/// <summary>Min dot product between horizontal <see cref="PlayerController.EyeAngles"/> forward and direction to victim (1 = straight at them).</summary>
	[Property] public float TackleDirectionThreshold { get; set; } = 0.95f;
	[Property] public float TackleCooldown { get; set; } = 1f;
	[Property] public float TackleLaunchSpeed { get; set; } = 500f;
	[Property] public float TackleLaunchArc { get; set; } = 1f; // upward blend vs flat tackleDir for ragdoll + tackled ball knock-off
	/// <summary>Host: attacker cannot auto-grab the ball for this long after tackling someone who was holding it (stops instant vacuum after knock-off). 0 = no lockout.</summary>
	[Property] public float AttackerPickupLockoutAfterCarrierTackle { get; set; } = 0.45f;
	[Property] public float RagdollCameraDistance { get; set; } = 200f;
	[Property] public float RagdollCameraHeight { get; set; } = 80f;
	[Property] public bool EnableTackleDebugLogs { get; set; } = false;
	/// <summary>Max allowed difference between owner-reported positions and host positions for tackle RPC (units). Beyond this, we reject as desync/cheat.</summary>
	[Property] public float TackleRpcPositionSlop { get; set; } = 128f;
	/// <summary>Extra multiplier on tackle radius when validating owner snapshots (latency compensation).</summary>
	[Property] public float TackleRpcRadiusFudge { get; set; } = 1.12f;
	/// <summary>Host: max seconds to poll for ragdoll physics bodies before launch + <c>NetworkSpawn</c>. Impulse runs as soon as bodies exist (often &lt; 1 frame). Too low can miss launch; too high delays replication.</summary>
	[Property] public float RagdollPhysicsInitDelay { get; set; } = 0.08f;
	/// <summary>Seconds to ease main camera from ragdoll orbit to normal third-person after stand-up. 0 = hand off immediately.</summary>
	[Property] public float StandUpCameraBlendDuration { get; set; } = 0.6f;
	/// <summary>Host: after stand-up from ragdoll, for this many seconds use <see cref="ClassData.TimeToCatchUpSpeedAfterRagdoll"/> for charge ramp when that value &gt; 0.</summary>
	[Property] public float PostRagdollCatchUpRampDuration { get; set; } = 5f;
	/// <summary>Host: after this pawn <b>lands</b> a tackle, for this many seconds use <see cref="ClassData.TimeToCatchUpSpeedAfterAttack"/> for charge ramp when that value &gt; 0.</summary>
	[Property] public float PostAttackCatchUpRampDuration { get; set; } = 5f;

	// Host writes, all machines read
	private bool isRagdolled;
	[Sync( SyncFlags.FromHost )] private bool NetIsRagdolled { get => isRagdolled; set => isRagdolled = value; }
	[Sync( SyncFlags.FromHost )] private Vector3 NetRagdollPosition { get; set; }
	[Sync( SyncFlags.FromHost )] private Vector3 NetStandUpPosition { get; set; }
	/// <summary>Used only for <see cref="PracticeNpcTag"/> dummies: eye angles to restore after stand-up (host sets with pre-tackle snapshot).</summary>
	[Sync( SyncFlags.FromHost )] private Angles NetPracticeNpcStandEyeAngles { get; set; }

	private bool isTackleImmune;
	[Sync( SyncFlags.FromHost )] private bool NetIsTackleImmune { get => isTackleImmune; set => isTackleImmune = value; }

	/// <summary>Host: practice NPC pre-tackle pose for snap-back respawn after ragdoll.</summary>
	private bool practiceNpcPreTackleCaptured;
	private Vector3 practiceNpcPreTackleWorldPosition;
	private Angles practiceNpcPreTackleEyeAngles;

	private bool wasRagdolled;
	private float tackleBlockedUntil;
	private float netTackleBlockedUntil;
	/// <summary>Host-authoritative; owners read this so remote tackle RPCs line up with cooldown.</summary>
	[Sync( SyncFlags.FromHost )]
	private float NetTackleBlockedUntil { get => netTackleBlockedUntil; set => netTackleBlockedUntil = value; }
	/// <summary>Increments on host when this pawn lands a tackle; owning <see cref="CatchUpSpeedBoost"/> resets charge ramp (drop to sprint tier).</summary>
	private int netTackleStripRampId;
	[Sync( SyncFlags.FromHost )]
	private int NetTackleStripRampId { get => netTackleStripRampId; set => netTackleStripRampId = value; }

	/// <summary>Host-authored; replicated. After ragdoll stand-up: sprint→charge uses <see cref="ClassData.TimeToCatchUpSpeedAfterRagdoll"/>.</summary>
	private float netPostRagdollSlowCatchUpUntil;
	[Sync( SyncFlags.FromHost )]
	private float NetPostRagdollSlowCatchUpUntil { get => netPostRagdollSlowCatchUpUntil; set => netPostRagdollSlowCatchUpUntil = value; }

	/// <summary>Host-authored; replicated. After landing a tackle: sprint→charge uses <see cref="ClassData.TimeToCatchUpSpeedAfterAttack"/>.</summary>
	private float netPostAttackSlowCatchUpUntil;
	[Sync( SyncFlags.FromHost )]
	private float NetPostAttackSlowCatchUpUntil { get => netPostAttackSlowCatchUpUntil; set => netPostAttackSlowCatchUpUntil = value; }

	/// <summary> Client-only throttle so we don't spam the host with tackle RPCs every frame.</summary>
	private float nextRemoteTackleRequestAt;

	// Host-only: Juggernaut-style tackle ramp (see ClassData.TackleChargeRampRate / MaxTackleChargeBonus)
	private float tackleChargeBonus;

	/// <summary>Owner mirror of <see cref="tackleChargeBonus"/> for remote tackle RPC (host NetAtChargeSpeed can lag).</summary>
	private float ownerTackleChargeBonus;

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
	/// <summary>Host bumps after successful tackles; <see cref="CatchUpSpeedBoost"/> consumes changes to strip charge speed.</summary>
	public int TackleStripRampSequence => netTackleStripRampId;
	public bool IsPostRagdollSlowCatchUpRampActive => netPostRagdollSlowCatchUpUntil > 0f && Time.Now < netPostRagdollSlowCatchUpUntil;
	public bool IsPostAttackSlowCatchUpRampActive => netPostAttackSlowCatchUpUntil > 0f && Time.Now < netPostAttackSlowCatchUpUntil;

	/// <summary> Host: end ragdoll immediately (match reset). Skips post-tackle invincibility. </summary>
	public void ForceStandUpFromHost()
	{
		if ( !Networking.IsHost || !isRagdolled )
			return;

		NetStandUpPosition = ComputeStandUpPositionFromRagdoll();
		DestroyRagdollObjectOnHost();
		NetPostRagdollSlowCatchUpUntil = 0f;
		NetIsRagdolled = false;
	}

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

		// Ragdoll root: host snaps to pelvis; owning client smoothing is handled by RagdollClientFeel.
		// MainCamera orbit + AnalogLook apply only when this networked object IsOwner — scene NPCs share one MainCamera and must not hijack the player's view when tackled.
		if ( isRagdolled && !IsProxy )
		{
			if ( Networking.IsHost )
				WorldPosition = NetRagdollPosition;

			if ( this.Network.IsOwner )
			{
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
		}

		if ( Networking.IsHost )
		{
			if ( isRagdolled )
				tackleChargeBonus = 0f;
			else
				StepTackleChargeBonus( ref tackleChargeBonus );
		}

		if ( Network.IsOwner && !Networking.IsHost )
		{
			if ( isRagdolled )
				ownerTackleChargeBonus = 0f;
			else
				StepTackleChargeBonus( ref ownerTackleChargeBonus );
		}

		if ( Networking.IsHost && IsMatchGameplayInputAllowed() )
			TryDetectAndApplyHostTackle();

		// Remote owners: host often has near-zero Velocity for our pawn (movement runs locally),
		// so host-only sphere checks never see a valid approach vector. Mirror BallThrow: detect
		// locally and request the host using our horizontal move direction.
		if ( this.Network.IsOwner && !Networking.IsHost && IsMatchGameplayInputAllowed() )
			TryOwnerRequestTackleOnHost();
	}

	private bool IsMatchGameplayInputAllowed()
	{
		var team = Components.Get<PlayerTeam>();
		return team is null || team.IsMatchGameplayInputAllowed;
	}

	protected override void OnPreRender()
	{
		// After all updates, PlayerController has already placed the camera. Lerp toward that exact
		// transform so we match its third-person math (CameraOffset, collision, etc.) — avoids a snap at t=1.
		if ( isRagdolled || !this.Network.IsOwner )
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

		var approachDir = GetHorizontalTackleApproachDirection();
		if ( approachDir.Length < 0.001f )
			return;

		var tackleRadius = playerClass?.CurrentClass?.TriggerSphereRadius ?? 40f;
		if ( !TryFindTackleVictim( Scene, this, WorldPosition, approachDir, tackleRadius, TackleDirectionThreshold, out var victim, out var tackleDir ) )
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
			return;

		var approachDir = GetHorizontalTackleApproachDirection();
		if ( approachDir.Length < 0.001f )
			return;

		var tackleRadius = playerClass?.CurrentClass?.TriggerSphereRadius ?? 40f;
		if ( !TryFindTackleVictim( Scene, this, WorldPosition, approachDir, tackleRadius, TackleDirectionThreshold, out var victim, out _ ) )
			return;

		var attackerPos = WorldPosition;
		var victimPos = victim.WorldPosition;
		nextRemoteTackleRequestAt = Time.Now + (TackleCooldown * 0.2f).Clamp( 0.05f, 0.25f );
		RequestTackleApplyOnHost( victim.GameObject.Id, approachDir, attackerPos, victimPos, ownerTackleChargeBonus );
	}

	[Rpc.Host]
	private void RequestTackleApplyOnHost(
		Guid victimRootId,
		Vector3 horizontalApproachDirectionFromOwner,
		Vector3 attackerWorldPosFromOwner,
		Vector3 victimWorldPosFromOwner,
		float chargeBonusFromOwner )
	{
		if ( this.Network.Owner is null || Rpc.Caller.SteamId != this.Network.Owner.SteamId )
			return;
		if ( isRagdolled )
			return;
		if ( Time.Now < tackleBlockedUntil )
			return;
		if ( speedBoost == null )
			return;

		var moveDir = horizontalApproachDirectionFromOwner.WithZ( 0f );
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
		// Launch uses owner snapshot (client detected the hit). Host-only distance/cone checks
		// waited for host positions to catch up and made tackles feel late.
		var tackleDir = toVictimOwner.Normal;
		var maxBonus = playerClass?.CurrentClass?.MaxTackleChargeBonus ?? 0f;
		var clampedOwnerBonus = MathX.Clamp( chargeBonusFromOwner, 0f, maxBonus );

		if ( EnableTackleDebugLogs )
			Log.Info( $"[Tackle] Rpc {GameObject.Name} → {victim.GameObject.Name} | Dir={tackleDir} hostBonus={tackleChargeBonus:F3} ownerBonus={clampedOwnerBonus:F3}" );

		ExecuteTackle( victim, tackleDir, clampedOwnerBonus );
	}

	/// <summary>Horizontal view-forward for tackle cone; same basis as dodge shove (EyeAngles, not velocity).</summary>
	private Vector3 GetHorizontalTackleApproachDirection()
	{
		if ( playerController is null )
			return default;

		var fwd = playerController.EyeAngles.ToRotation().Forward.WithZ( 0f );
		if ( fwd.Length >= 0.001f )
			return fwd.Normal;

		var right = playerController.EyeAngles.ToRotation().Right.WithZ( 0f );
		if ( right.Length >= 0.001f )
			return right.Normal;

		return default;
	}

	/// <summary>Find one valid tackle target using a horizontal approach direction in world space (unit-ish vector; normalized inside).</summary>
	private static bool TryFindTackleVictim(
		Scene scene,
		PlayerTackle attacker,
		Vector3 attackerWorldPos,
		Vector3 horizontalApproachDirection,
		float tackleRadius,
		float directionThreshold,
		out PlayerTackle victim,
		out Vector3 tackleDir )
	{
		victim = null;
		tackleDir = default;

		var hv = horizontalApproachDirection.WithZ( 0f );
		if ( hv.Length < 0.001f )
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

	private void ExecuteTackle( PlayerTackle victim, Vector3 tackleDir, float chargeBonusFromOwner = -1f )
	{
		if ( Networking.IsHost )
		{
			NetTackleStripRampId++;
			var atkClass = playerClass?.CurrentClass;
			var atkSlow = atkClass?.TimeToCatchUpSpeedAfterAttack ?? 0f;
			if ( atkSlow > 0f && PostAttackCatchUpRampDuration > 0f )
				NetPostAttackSlowCatchUpUntil = Time.Now + PostAttackCatchUpRampDuration;
			else
				NetPostAttackSlowCatchUpUntil = 0f;
		}

		CapturePracticeNpcPreTacklePoseIfTagged( victim );

		var attackerMass = playerClass?.CurrentClass?.Mass ?? 80f;
		var victimMass = victim.playerClass?.CurrentClass?.Mass ?? 80f;
		if ( attackerMass <= 0f ) attackerMass = 80f;
		if ( victimMass <= 0f ) victimMass = 80f;

		var massRatio = MathX.Clamp( attackerMass / victimMass, 0.5f, 2.5f );
		var chargeBonus = tackleChargeBonus;
		if ( chargeBonusFromOwner >= 0f )
			chargeBonus = MathF.Max( chargeBonus, chargeBonusFromOwner );
		var juggMult = 1f + chargeBonus;
		var tacklePower = massRatio * juggMult;
		var effectiveLaunchSpeed = TackleLaunchSpeed * tacklePower;

		if ( EnableTackleDebugLogs )
			Log.Info( $"[Tackle] Power massRatio={massRatio:F2} chargeBonus={chargeBonus:F3} juggMult={juggMult:F2} → launchSpeed={effectiveLaunchSpeed:F0}" );

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
		if ( Networking.IsHost )
		{
			victim.NetPostRagdollSlowCatchUpUntil = 0f;
			victim.NetPostAttackSlowCatchUpUntil = 0f;
		}

		victim.NetIsRagdolled = true;

		SpawnRagdollObject( victim, tackleDir, effectiveLaunchSpeed );
		HandleRagdollRecovery( victim );
	}

	private static void CapturePracticeNpcPreTacklePoseIfTagged( PlayerTackle victim )
	{
		if ( !victim.GameObject.Tags.Has( PracticeNpcTag ) )
		{
			victim.practiceNpcPreTackleCaptured = false;
			return;
		}

		victim.practiceNpcPreTackleWorldPosition = victim.WorldPosition;
		var pc = victim.playerController;
		if ( pc is null || !pc.IsValid() )
			pc = victim.Components.Get<PlayerController>();
		victim.practiceNpcPreTackleEyeAngles = pc.IsValid() ? pc.EyeAngles : default;
		victim.practiceNpcPreTackleCaptured = true;
	}

	// Spawns a host-owned physics ragdoll at the victim's position.
	// NetworkSpawn makes it visible on all clients automatically.
	// Physics runs on the host without client transform ownership conflicts.
	private async void SpawnRagdollObject( PlayerTackle victim, Vector3 tackleDir, float effectiveLaunchSpeed )
	{
		var ragdollGo = new GameObject( true, "PlayerRagdoll" );
		ragdollGo.WorldPosition = victim.WorldPosition + Vector3.Up * 10f;
		ragdollGo.WorldRotation = victim.WorldRotation;

		var modelScale = victim.playerClass?.CurrentClass?.ModelScale ?? 1f;
		if ( modelScale <= 0f )
			modelScale = 1f;
		ragdollGo.LocalScale = Vector3.One * modelScale;

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

		// Impulse on host while still local, then NetworkSpawn — clients' first ragdoll snapshot
		// should already be in flight (avoids a stationary ragdoll + late launch on replication).
		ragdollGo.Tags.Add( "ragdoll" );
		victim.ragdollObject = ragdollGo;
		ragdollGo.Components.GetOrCreate<RagdollEnemyOutline>().ConfigureFromVictim( victim );

		var waitForBodies = RagdollPhysicsInitDelay.Clamp( 0.01f, 0.25f );
		var launched = await TryApplyRagdollLaunchImpulseAsync( ragdollGo, tackleDir, effectiveLaunchSpeed, waitForBodies );
		if ( !ragdollGo.IsValid() )
			return;

		if ( EnableTackleDebugLogs )
			Log.Info( $"[Tackle] NetworkSpawn after impulse | launched={launched}" );

		ragdollGo.NetworkSpawn();
	}

	/// <summary>Poll until <see cref="ModelPhysics"/> bodies exist, apply pelvis impulse, tag bodies. Returns false if timed out.</summary>
	private async Task<bool> TryApplyRagdollLaunchImpulseAsync(
		GameObject ragdollGo,
		Vector3 tackleDir,
		float effectiveLaunchSpeed,
		float maxWaitSeconds )
	{
		const float pollStepSeconds = 0.008f;
		var started = Time.Now;

		while ( ragdollGo.IsValid() && Time.Now - started < maxWaitSeconds )
		{
			if ( TryApplyRagdollLaunchImpulse( ragdollGo, tackleDir, effectiveLaunchSpeed, out var bodyCount ) )
			{
				if ( EnableTackleDebugLogs )
					Log.Info( $"[Tackle] Impulse ready in {(Time.Now - started) * 1000f:F0}ms | Bodies={bodyCount}" );
				return true;
			}

			await GameTask.DelaySeconds( pollStepSeconds );
		}

		if ( ragdollGo.IsValid() && TryApplyRagdollLaunchImpulse( ragdollGo, tackleDir, effectiveLaunchSpeed, out var finalBodies ) )
		{
			if ( EnableTackleDebugLogs )
				Log.Info( $"[Tackle] Impulse on timeout edge | Bodies={finalBodies}" );
			return true;
		}

		if ( EnableTackleDebugLogs )
			Log.Warning( $"[Tackle] Impulse failed after {maxWaitSeconds * 1000f:F0}ms — spawning without launch" );
		return false;
	}

	private bool TryApplyRagdollLaunchImpulse(
		GameObject ragdollGo,
		Vector3 tackleDir,
		float effectiveLaunchSpeed,
		out int bodyCount )
	{
		bodyCount = 0;
		var mp = ragdollGo.Components.Get<ModelPhysics>();
		if ( mp == null || mp.Bodies.Count == 0 )
			return false;

		bodyCount = mp.Bodies.Count;
		var pb0 = mp.Bodies[0].Component?.PhysicsBody;
		if ( pb0 == null )
			return false;

		var launchDir = (tackleDir + Vector3.Up * TackleLaunchArc).Normal;
		var launchVelocity = launchDir * effectiveLaunchSpeed;
		var totalMass = mp.Mass;
		if ( totalMass <= 0f )
			return false;

		pb0.ApplyImpulse( launchVelocity * totalMass );

		foreach ( var body in mp.Bodies )
			body.Component?.GameObject?.Tags.Add( "ragdoll" );

		if ( EnableTackleDebugLogs )
			Log.Info( $"[Tackle] ApplyImpulse | Bodies={bodyCount} Vel={pb0.Velocity}" );

		return true;
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
		while ( victim.IsValid() && victim.IsRagdolled && !victim.ragdollObject.IsValid() && Time.Now - started < 2f )
			await GameTask.DelaySeconds( pollSeconds );

		while ( victim.IsValid() && victim.IsRagdolled )
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

		if ( !victim.IsValid() || !victim.IsRagdolled )
			return;

		// Trace straight down from the ragdoll's pelvis to find the actual floor.
		// ragdollObject.WorldPosition is the pelvis (IgnoreRoot=false) — waist height above the floor.
		// Without this, the player stands up floating at pelvis height and falls in an idle animation
		// until their controller finds the ground.
		var ragdollPos = victim.ragdollObject.IsValid()
			? victim.ragdollObject.WorldPosition
			: victim.WorldPosition;

		if ( victim.GameObject.Tags.Has( PracticeNpcTag ) && victim.practiceNpcPreTackleCaptured )
		{
			victim.NetStandUpPosition = victim.practiceNpcPreTackleWorldPosition;
			victim.NetPracticeNpcStandEyeAngles = victim.practiceNpcPreTackleEyeAngles;
			victim.practiceNpcPreTackleCaptured = false;
		}
		else
		{
			victim.NetStandUpPosition = TraceStandUpPosition( scene, ragdollPos );
		}

		victim.DestroyRagdollObjectOnHost();

		var ragdollSlowCatch = victim.playerClass?.CurrentClass?.TimeToCatchUpSpeedAfterRagdoll ?? 0f;
		if ( Networking.IsHost )
		{
			if ( ragdollSlowCatch > 0f && victim.PostRagdollCatchUpRampDuration > 0f )
				victim.NetPostRagdollSlowCatchUpUntil = Time.Now + victim.PostRagdollCatchUpRampDuration;
			else
				victim.NetPostRagdollSlowCatchUpUntil = 0f;
		}

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

	private void StepTackleChargeBonus( ref float bonus )
	{
		var c = playerClass?.CurrentClass;
		var rate = c?.TackleChargeRampRate ?? 0f;
		var maxBonus = c?.MaxTackleChargeBonus ?? 0f;
		if ( rate <= 0f || maxBonus <= 0f )
		{
			bonus = 0f;
			return;
		}

		if ( speedBoost != null && speedBoost.IsAtChargeSpeed )
			bonus = MathX.Clamp( bonus + rate * Time.Delta, 0f, maxBonus );
		else
			bonus = 0f;
	}

	private Vector3 ComputeStandUpPositionFromRagdoll()
	{
		if ( GameObject.Tags.Has( PracticeNpcTag ) && practiceNpcPreTackleCaptured )
		{
			practiceNpcPreTackleCaptured = false;
			return practiceNpcPreTackleWorldPosition;
		}

		var ragdollPos = ragdollObject.IsValid() ? ragdollObject.WorldPosition : WorldPosition;
		return TraceStandUpPosition( Scene, ragdollPos );
	}

	private static Vector3 TraceStandUpPosition( Scene scene, Vector3 ragdollPos )
	{
		var tr = scene.Trace
			.Ray( ragdollPos + Vector3.Up * 30f, ragdollPos + Vector3.Down * 200f )
			.WithoutTags( "ragdoll" )
			.Run();

		return tr.Hit ? tr.HitPosition : ragdollPos;
	}

	private void DestroyRagdollObjectOnHost()
	{
		if ( ragdollObject.IsValid() )
			ragdollObject.Destroy();
		ragdollObject = null;
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

		if ( GameObject.Tags.Has( PracticeNpcTag ) && playerController.IsValid() )
			playerController.EyeAngles = NetPracticeNpcStandEyeAngles;

		foreach ( var r in hiddenRenderers )
			if ( r.IsValid() ) r.Enabled = true;
		hiddenRenderers.Clear();

		foreach ( var col in disabledColliders )
			if ( col.IsValid() ) col.Enabled = true;
		disabledColliders.Clear();

		if ( playerController.IsValid() ) playerController.Enabled = true;

		if ( this.Network.IsOwner && StandUpCameraBlendDuration > 0.001f && activeCamera.IsValid() )
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
