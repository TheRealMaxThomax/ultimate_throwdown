using Sandbox;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

// PlayerTackle — quick map (editor Outline or Ctrl+F the symbol name)
//   Inspector properties ........ top of class
//   Sync / net state ............ isRagdolled, ragdoll/attack slow-catch windows, tackle strip id, cooldowns
//   OnStart / OnUpdate .......... camera ref, ragdoll transitions, owner camera + free look
//   Host detection .............. TryDetectAndApplyHostTackle, ApplyTackleCooldownOnHost
//   Client → host ............... TryOwnerRequestTackleOnHost, RequestTackleApplyOnHost, owner predict feel (Tier A1)
//   Victim pick ................. TryFindTackleVictim (cone vs horizontal view forward, not velocity)
//   Hit + ball .................. ExecuteTackle, ApplyKnockdownFromHost
//   Ragdoll (host only) ......... SpawnRagdollObject, AddVictimClothingToRagdoll, HandleRagdollRecovery, IsRagdollGroundedAndSettled
//   Juggernaut ramp ............. StepTackleChargeBonus (host + owner mirror for RPC)
//   Local down / up ............. ApplyRagdollLocally, StandUpLocally
//   Stand-up camera blend ....... OnPreRender (lerp last ragdoll cam -> PlayerController camera this frame)
public sealed class PlayerTackle : Component
{
	private const string PracticeNpcTag = "practice_npc";

	/// <summary>Min dot product between horizontal <see cref="PlayerController.EyeAngles"/> forward and direction to victim (1 = straight at them).</summary>
	[Property] public float TackleDirectionThreshold { get; set; } = 0.95f;
	/// <summary>Max |ΔZ| between attacker and victim for a tackle to connect — jumpers within this band still get hit.</summary>
	[Property] public float MaxTackleVerticalSeparation { get; set; } = 56f;
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
	/// <summary>Host: seconds to hold the ragdoll frozen after <c>NetworkSpawn</c> before <c>ApplyImpulse</c>. Everyone sees a brief hang; <b>0</b> = legacy impulse-then-spawn. Try ~0.05 with <see cref="TackleImpactFeel.HitstopDurationSeconds"/>.</summary>
	[Property] public float PreLaunchPauseSeconds { get; set; } = 0.05f;
	/// <summary>Comic text + ball knock-off power for hazard knockdowns (traffic, etc.). Default above <see cref="TackleComicTextHud.ChaosImpactThreshold"/> — tune down for Sans.</summary>
	[Property] public float HazardKnockdownComicPower { get; set; } = 1.55f;
	/// <summary>Seconds to ease main camera from ragdoll orbit to normal third-person after stand-up. 0 = hand off immediately.</summary>
	[Property] public float StandUpCameraBlendDuration { get; set; } = 0.6f;
	/// <summary>Host: after stand-up from ragdoll, for this many seconds use <see cref="ClassData.TimeToCatchUpSpeedAfterRagdoll"/> for charge ramp when that value &gt; 0.</summary>
	[Property] public float PostRagdollCatchUpRampDuration { get; set; } = 5f;
	/// <summary>Host: after this pawn <b>lands</b> a tackle, for this many seconds use <see cref="ClassData.TimeToCatchUpSpeedAfterAttack"/> for charge ramp when that value &gt; 0.</summary>
	[Property] public float PostAttackCatchUpRampDuration { get; set; } = 5f;

	// Host writes, all machines read
	private bool isRagdolled;
	[Sync( SyncFlags.FromHost )] private bool NetIsRagdolled { get => isRagdolled; set => isRagdolled = value; }
	/// <summary>Host: victim frozen in place with body visible while <see cref="PreLaunchPauseSeconds"/> runs before ragdoll spawn.</summary>
	private bool netAwaitingRagdollLaunch;
	[Sync( SyncFlags.FromHost )]
	private bool NetAwaitingRagdollLaunch { get => netAwaitingRagdollLaunch; set => netAwaitingRagdollLaunch = value; }
	/// <summary>Host: last knockdown had no player attacker (traffic/hazard) — client victim predict picks hazard feel path.</summary>
	private bool netLastKnockdownWasHazard;
	[Sync( SyncFlags.FromHost )]
	private bool NetLastKnockdownWasHazard { get => netLastKnockdownWasHazard; set => netLastKnockdownWasHazard = value; }
	/// <summary>Host: last knockdown was Speed Blitz — client victim predict + feel RPC use blitz impact profile.</summary>
	private bool netLastKnockdownWasSpeedBlitz;
	[Sync( SyncFlags.FromHost )]
	private bool NetLastKnockdownWasSpeedBlitz { get => netLastKnockdownWasSpeedBlitz; set => netLastKnockdownWasSpeedBlitz = value; }
	[Sync( SyncFlags.FromHost )] private Vector3 NetKnockdownFreezePosition { get; set; }
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

	/// <summary>Client: scene <see cref="PracticeNpcTag"/> dummies stay at contact position — snapshot lag must not rewind to host freeze pos.</summary>
	private bool practiceNpcClientContactFreezePinned;
	private Vector3 practiceNpcClientContactFreezePos;

	private bool wasRagdolled;
	private float tackleBlockedUntil;
	private float netTackleBlockedUntil;
	/// <summary>Host-authoritative; owners read this so remote tackle RPCs line up with cooldown.</summary>
	[Sync( SyncFlags.FromHost )]
	private float NetTackleBlockedUntil { get => netTackleBlockedUntil; set => netTackleBlockedUntil = value; }
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

	// Pre-launch pause: controller/colliders off but renderers stay visible until ragdoll launches.
	private bool knockdownAwaitingFreezeApplied;
	private readonly List<Collider> awaitingDisabledColliders = new();

	private CatchUpSpeedBoost speedBoost;
	private PlayerClass playerClass;
	private PlayerController playerController;
	private TackleImpactFeel tackleImpactFeel;
	private CombatFeelPredictDedupe combatFeelDedupe;
	private PracticeNpcPatrolHostState practiceNpcPatrol;
	private CameraComponent activeCamera;

	// Last ragdoll orbit camera (owner); used as blend start when standing up
	private Vector3 lastRagdollCameraPos;
	private Rotation lastRagdollCameraRot;
	private Vector3 standUpCameraBlendFromPos;
	private Rotation standUpCameraBlendFromRot;
	/// <summary>&lt; 0 = not blending. Otherwise Time.Now when stand-up blend started.</summary>
	private float standUpCameraBlendStartTime = -1f;
	private Vector3 ragdollEnterBlendFromPos;
	private Rotation ragdollEnterBlendFromRot;
	private float ragdollEnterBlendStartTime = -1f;
	private bool deferringRagdollCamForImpactFeel;

	public bool IsTackleImmune => isTackleImmune;

	/// <summary>True while the tackle cooldown is active (attacker cannot tackle again). Practice patrol NPCs pause movement during this window so the victim gets a clean launch.</summary>
	public bool IsInTackleCooldown => Time.Now < netTackleBlockedUntil;

	/// <summary>Host: force tackle immunity on/off (e.g. invulnerable during a Speed Blitz dash).</summary>
	public void SetHostTackleImmune( bool immune )
	{
		if ( !Networking.IsHost )
			return;

		NetIsTackleImmune = immune;
	}

	public bool IsRagdolled => isRagdolled;
	/// <summary>Host pause window: down for tackles, body still visible (not ragdoll mesh yet).</summary>
	public bool IsAwaitingRagdollLaunch => netAwaitingRagdollLaunch;
	/// <summary>Ragdolled or in pre-launch knockdown freeze (cannot be tackled again).</summary>
	public bool IsKnockedDown => isRagdolled || netAwaitingRagdollLaunch;
	/// <summary>Speed Blitz victim hang — body visible, pre-ragdoll; drives <see cref="BlitzConnectPoseFreeze"/>.</summary>
	public bool IsAwaitingSpeedBlitzRagdollLaunch => netAwaitingRagdollLaunch && netLastKnockdownWasSpeedBlitz;
	/// <summary> Owning client: easing main camera from ragdoll orbit back to <see cref="PlayerController"/> third-person. </summary>
	public bool IsStandUpCameraBlending => standUpCameraBlendStartTime >= 0f;
	public bool IsPostRagdollSlowCatchUpRampActive => netPostRagdollSlowCatchUpUntil > 0f && Time.Now < netPostRagdollSlowCatchUpUntil;
	public bool IsPostAttackSlowCatchUpRampActive => netPostAttackSlowCatchUpUntil > 0f && Time.Now < netPostAttackSlowCatchUpUntil;

	/// <summary>Practice dummies must never drive Main Camera ragdoll orbit (even if network-owned on host).</summary>
	private bool ShouldApplyOwnerKnockdownCamera => Network.IsOwner && !GameObject.Tags.Has( PracticeNpcTag );

	/// <summary> Host: end ragdoll immediately (match reset). Skips post-tackle invincibility. </summary>
	public void ForceStandUpFromHost()
	{
		if ( !Networking.IsHost || !IsKnockedDown )
			return;

		if ( isRagdolled )
			NetStandUpPosition = ComputeStandUpPositionFromRagdoll();
		else
			NetStandUpPosition = NetKnockdownFreezePosition;

		DestroyRagdollObjectOnHost();
		NetPostRagdollSlowCatchUpUntil = 0f;
		NetAwaitingRagdollLaunch = false;
		Components.Get<CatchUpSpeedBoost>()?.TriggerForceWalkRampOnHost();
		NetIsRagdolled = false;
	}

	// Pelvis world position synced from host; RagdollClientFeel reads this on owning clients
	public Vector3 SyncedRagdollPelvisPosition => NetRagdollPosition;

	/// <summary>Owner: ragdoll third-person orbit target (used by <see cref="TackleImpactFeel"/> shake baseline).</summary>
	public bool TryGetRagdollOrbitCamera( out Vector3 position, out Rotation rotation )
	{
		if ( !isRagdolled )
		{
			position = default;
			rotation = default;
			return false;
		}

		ComputeRagdollOrbitCamera( out position, out rotation );
		return true;
	}

	protected override void OnStart()
	{
		speedBoost = Components.Get<CatchUpSpeedBoost>();
		playerClass = Components.Get<PlayerClass>();
		playerController = Components.Get<PlayerController>();
		tackleImpactFeel = Components.Get<TackleImpactFeel>();
		combatFeelDedupe = Components.Get<CombatFeelPredictDedupe>();
		practiceNpcPatrol = Components.Get<PracticeNpcPatrolHostState>();

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
			{
				if ( Network.IsOwner && !Networking.IsHost )
					OwnerTryApplyPredictedVictimFeelForDirectRagdoll();

				ApplyRagdollLocally();
			}
			else
			{
				combatFeelDedupe ??= Components.Get<CombatFeelPredictDedupe>();
				combatFeelDedupe?.ResetVictimKnockdownPredictState();
				StandUpLocally();
			}

			wasRagdolled = isRagdolled;
		}

		// Re-enforce renderer hide every frame during ragdoll — catches anything that re-enables them
		if ( isRagdolled )
			foreach ( var r in hiddenRenderers )
				if ( r.IsValid() ) r.Enabled = false;

		if ( netAwaitingRagdollLaunch && !isRagdolled )
		{
			if ( Network.IsOwner && !Networking.IsHost && !knockdownAwaitingFreezeApplied )
				OwnerApplyPredictedVictimFeel( hazardKnockdown: false );

			ApplyKnockdownAwaitingFreezeLocally();
			ApplyKnockdownFreezeWorldPosition();
		}
		else if ( knockdownAwaitingFreezeApplied )
			ClearKnockdownAwaitingFreezeLocally();

		// Ragdoll root: host snaps to pelvis; owning client smoothing is handled by RagdollClientFeel.
		// MainCamera orbit + AnalogLook apply only when this networked object IsOwner — scene NPCs share one MainCamera and must not hijack the player's view when tackled.
		if ( isRagdolled && !IsProxy )
		{
			if ( Networking.IsHost )
				WorldPosition = NetRagdollPosition;

			if ( this.Network.IsOwner && ShouldApplyOwnerKnockdownCamera )
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

		if ( Networking.IsHost && IsMatchGameplayInputAllowed() && CanUseHostTackleDetection() )
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
		if ( !ShouldApplyOwnerKnockdownCamera || !activeCamera.IsValid() )
			return;

		// Place ragdoll orbit last so later components (e.g. charge camera offset restore) cannot overwrite it.
		tackleImpactFeel ??= Components.Get<TackleImpactFeel>();
		if ( isRagdolled )
		{
			if ( tackleImpactFeel?.IsHazardImpact == true )
			{
				deferringRagdollCamForImpactFeel = true;
				return;
			}

			ComputeRagdollOrbitCamera( out var targetPos, out var targetRot );

			if ( deferringRagdollCamForImpactFeel )
			{
				ragdollEnterBlendFromPos = activeCamera.WorldPosition;
				ragdollEnterBlendFromRot = activeCamera.WorldRotation;
				ragdollEnterBlendStartTime = Time.Now;
				deferringRagdollCamForImpactFeel = false;
			}

			const float enterBlendDuration = 0.2f;
			if ( ragdollEnterBlendStartTime >= 0f )
			{
				var enterElapsed = Time.Now - ragdollEnterBlendStartTime;
				var enterTLin = MathX.Clamp( enterElapsed / enterBlendDuration, 0f, 1f );
				var enterT = enterTLin * enterTLin * (3f - 2f * enterTLin );
				activeCamera.WorldPosition = Vector3.Lerp( ragdollEnterBlendFromPos, targetPos, enterT );
				activeCamera.WorldRotation = Rotation.Slerp( ragdollEnterBlendFromRot, targetRot, enterT );
				if ( enterTLin >= 1f )
					ragdollEnterBlendStartTime = -1f;
			}
			else
			{
				activeCamera.WorldPosition = targetPos;
				activeCamera.WorldRotation = targetRot;
			}

			lastRagdollCameraPos = activeCamera.WorldPosition;
			lastRagdollCameraRot = activeCamera.WorldRotation;
			return;
		}

		deferringRagdollCamForImpactFeel = false;
		ragdollEnterBlendStartTime = -1f;

		// After all updates, PlayerController has already placed the camera. Lerp toward that exact
		// transform so we match its third-person math (CameraOffset, collision, etc.) — avoids a snap at t=1.
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

	private bool CanUseHostTackleDetection()
	{
		if ( !GameObject.Tags.Has( PracticeNpcTag ) )
			return true;

		practiceNpcPatrol ??= Components.Get<PracticeNpcPatrolHostState>();
		return practiceNpcPatrol?.IsPatrollingAtChargeSpeed == true;
	}

	private void TryDetectAndApplyHostTackle()
	{
		if ( isRagdolled )
			return;
		if ( Time.Now < tackleBlockedUntil )
			return;
		if ( speedBoost == null || !speedBoost.IsAtChargeSpeed )
			return;

		if ( !TryGetHostTackleMove( out var horizontalVelocity, out var approachDir ) )
			return;

		var tackleRadius = playerClass?.CurrentClass?.TriggerSphereRadius ?? 40f;
		if ( !TryFindTackleVictim( Scene, this, WorldPosition, approachDir, tackleRadius, TackleDirectionThreshold, MaxTackleVerticalSeparation, out var victim, out var tackleDir ) )
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
		if ( !TryFindTackleVictim( Scene, this, WorldPosition, approachDir, tackleRadius, TackleDirectionThreshold, MaxTackleVerticalSeparation, out var victim, out _ ) )
			return;

		OwnerApplyPredictedTackleAttackerFeel();

		var attackerPos = WorldPosition;
		var victimPos = victim.WorldPosition;
		nextRemoteTackleRequestAt = Time.Now + (TackleCooldown * 0.2f).Clamp( 0.05f, 0.25f );
		RequestTackleApplyOnHost( victim.GameObject.Id, approachDir, attackerPos, victimPos, ownerTackleChargeBonus );
	}

	/// <summary>Client owner only: early attacker feel when local victim find matches the RPC we are about to send.</summary>
	private void OwnerApplyPredictedTackleAttackerFeel()
	{
		combatFeelDedupe ??= Components.GetOrCreate<CombatFeelPredictDedupe>();
		combatFeelDedupe.MarkOwnerPredictedAttackerFeel();
		tackleImpactFeel ??= Components.Get<TackleImpactFeel>();
		tackleImpactFeel?.TriggerAsAttacker();

		if ( EnableTackleDebugLogs )
			Log.Info( $"[Tackle] {GameObject.Name}: owner predict attacker feel" );
	}

	/// <summary>Client owner: early victim feel aligned with pre-launch freeze (tackle / blitz — Tier A2).</summary>
	private void OwnerApplyPredictedVictimFeel( bool hazardKnockdown )
	{
		combatFeelDedupe ??= Components.GetOrCreate<CombatFeelPredictDedupe>();
		if ( !combatFeelDedupe.TryBeginOwnerPredictedVictimFeel() )
			return;

		tackleImpactFeel ??= Components.Get<TackleImpactFeel>();

		if ( hazardKnockdown )
			tackleImpactFeel?.TriggerAsHazardVictim();
		else if ( netLastKnockdownWasSpeedBlitz )
			tackleImpactFeel?.TriggerAsVictim( SpeedsterSpeedBlitzUlt.DefaultKnockdownImpactFeelOverrides );
		else
			tackleImpactFeel?.TriggerAsVictim();

		if ( EnableTackleDebugLogs )
			Log.Info( $"[Tackle] {GameObject.Name}: owner predict victim feel (hazard={hazardKnockdown})" );
	}

	/// <summary>Client owner: direct ragdoll knockdown (traffic / no pre-launch pause) — Tier A2b.</summary>
	private void OwnerTryApplyPredictedVictimFeelForDirectRagdoll()
	{
		OwnerApplyPredictedVictimFeel( hazardKnockdown: netLastKnockdownWasHazard );
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

		if ( victim is null || victim == this || victim.GameObject == GameObject || victim.IsTackleImmune || victim.IsKnockedDown
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
		if ( !TryValidateTackleHitGeometry(
			attackerWorldPosFromOwner,
			victimWorldPosFromOwner,
			moveDir,
			tackleRadius,
			TackleDirectionThreshold,
			MaxTackleVerticalSeparation,
			out var tackleDir ) )
		{
			if ( EnableTackleDebugLogs )
				Log.Info( "[Tackle] Rpc reject: geometry (vertical / horizontal radius / cone)" );
			return;
		}

		ApplyTackleCooldownOnHost();
		// Launch uses owner snapshot (client detected the hit). Host-only distance/cone checks
		// waited for host positions to catch up and made tackles feel late.
		var maxBonus = playerClass?.CurrentClass?.MaxTackleChargeBonus ?? 0f;
		var clampedOwnerBonus = MathX.Clamp( chargeBonusFromOwner, 0f, maxBonus );

		if ( EnableTackleDebugLogs )
			Log.Info( $"[Tackle] Rpc {GameObject.Name} → {victim.GameObject.Name} | Dir={tackleDir} hostBonus={tackleChargeBonus:F3} ownerBonus={clampedOwnerBonus:F3}" );

		ExecuteTackle( victim, tackleDir, clampedOwnerBonus );
	}

	/// <summary> Host tackle gate — practice patrol dummies use synced move dir/speed (disabled PC, no physics velocity). </summary>
	private bool TryGetHostTackleMove( out Vector3 horizontalVelocity, out Vector3 approachDirection )
	{
		horizontalVelocity = default;
		approachDirection = default;

		var patrol = Components.Get<PracticeNpcPatrolHostState>();
		if ( patrol.IsValid() && patrol.TryGetHostTackleMove( out horizontalVelocity, out approachDirection ) )
			return true;

		horizontalVelocity = (playerController?.Velocity ?? Vector3.Zero).WithZ( 0f );
		if ( horizontalVelocity.Length < 1f )
			return false;

		approachDirection = GetHorizontalTackleApproachDirection();
		return approachDirection.Length >= 0.001f;
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
		float maxVerticalSeparation,
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
			if ( candidate.IsKnockedDown )
				continue;

			if ( !TryValidateTackleHitGeometry(
				attackerWorldPos,
				candidate.WorldPosition,
				hvNorm,
				tackleRadius,
				directionThreshold,
				maxVerticalSeparation,
				out var candidateTackleDir ) )
				continue;

			victim = candidate;
			tackleDir = candidateTackleDir;
			return true;
		}

		return false;
	}

	/// <summary>
	/// Horizontal-radius cylinder + vertical band — jumpers within the band still connect.
	/// Shared by tackle and Speed Blitz dash contact (not 3D distance).
	/// </summary>
	internal static bool TryValidateContactCylinder(
		Vector3 referenceWorldPos,
		Vector3 victimWorldPos,
		float horizontalRadius,
		float maxVerticalSeparation )
	{
		var maxVertical = maxVerticalSeparation.Clamp( 8f, 256f );
		if ( MathF.Abs( victimWorldPos.z - referenceWorldPos.z ) > maxVertical )
			return false;

		var horizDist = (victimWorldPos - referenceWorldPos).WithZ( 0f ).Length;
		return horizDist <= horizontalRadius;
	}

	/// <summary>Horizontal tackle radius + vertical band + approach cone (shared by host find and owner RPC).</summary>
	private static bool TryValidateTackleHitGeometry(
		Vector3 attackerWorldPos,
		Vector3 victimWorldPos,
		Vector3 horizontalApproachDirection,
		float tackleRadius,
		float directionThreshold,
		float maxVerticalSeparation,
		out Vector3 tackleDir )
	{
		tackleDir = default;

		if ( !TryValidateContactCylinder( attackerWorldPos, victimWorldPos, tackleRadius, maxVerticalSeparation ) )
			return false;

		var toVictimHoriz = (victimWorldPos - attackerWorldPos).WithZ( 0f );
		var horizDist = toVictimHoriz.Length;

		var hvNorm = horizontalApproachDirection.WithZ( 0f );
		if ( hvNorm.Length < 0.001f )
			return false;

		hvNorm = hvNorm.Normal;

		if ( horizDist >= 0.001f )
		{
			if ( Vector3.Dot( hvNorm, toVictimHoriz.Normal ) < directionThreshold )
				return false;

			tackleDir = toVictimHoriz.Normal;
			return true;
		}

		// Directly above/below within horizontal radius — still in the tackle cylinder.
		tackleDir = hvNorm;
		return true;
	}

	/// <summary>Host: hazard knockdown (traffic, etc.) — same ragdoll path as tackle without attacker bookkeeping.</summary>
	/// <returns><c>true</c> if knockdown started.</returns>
	public bool ApplyKnockdownFromHost( Vector3 launchDir, float launchSpeed, float launchArc = -1f, PlayerTackle attacker = null, float? preLaunchPauseSecondsOverride = null, float? preLaunchPauseStartedAt = null )
	{
		if ( !Networking.IsHost )
			return false;

		if ( IsTackleImmune || IsKnockedDown
			|| Components.Get<PlayerDodge>() is { IsImmuneToTackle: true } )
			return false;

		var arc = launchArc >= 0f ? launchArc : TackleLaunchArc;
		ApplyVictimKnockdownFromHost( this, launchDir, launchSpeed, arc, HazardKnockdownComicPower, attackerGrabForCarrierLockout: null, attacker, preLaunchPauseSecondsOverride, preLaunchPauseStartedAt );
		return true;
	}

	private void ExecuteTackle( PlayerTackle victim, Vector3 tackleDir, float chargeBonusFromOwner = -1f )
	{
		if ( Networking.IsHost )
		{
			Components.Get<PlayerUltCharge>()?.TryGrantTackleChargeOnHost( victim );

			speedBoost ??= Components.Get<CatchUpSpeedBoost>();
			speedBoost?.TriggerForceWalkRampOnHost();
			var atkClass = playerClass?.CurrentClass;
			var atkSlow = atkClass?.TimeToCatchUpSpeedAfterAttack ?? 0f;
			if ( atkSlow > 0f && PostAttackCatchUpRampDuration > 0f )
				NetPostAttackSlowCatchUpUntil = Time.Now + PostAttackCatchUpRampDuration;
			else
				NetPostAttackSlowCatchUpUntil = 0f;
		}

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

		ApplyVictimKnockdownFromHost( victim, tackleDir, effectiveLaunchSpeed, TackleLaunchArc, tacklePower, Components.Get<BallGrab>(), this );
	}

	private void ApplyVictimKnockdownFromHost(
		PlayerTackle victim,
		Vector3 launchDir,
		float effectiveLaunchSpeed,
		float launchArc,
		float tacklePowerForBall,
		BallGrab attackerGrabForCarrierLockout,
		PlayerTackle attacker,
		float? preLaunchPauseSecondsOverride = null,
		float? preLaunchPauseStartedAt = null )
	{
		CapturePracticeNpcPreTacklePoseIfTagged( victim );

		var speedBlitzKnockdown = preLaunchPauseSecondsOverride.HasValue;
		if ( Networking.IsHost )
		{
			victim.NetLastKnockdownWasHazard = !attacker.IsValid();
			victim.NetLastKnockdownWasSpeedBlitz = speedBlitzKnockdown;
		}

		var classData = victim.playerClass?.CurrentClass;
		var ballLaunchForce = (classData?.BallLaunchForceOnTackle ?? 500f) * tacklePowerForBall;
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
					var ballLaunchDir = (launchDir + Vector3.Up * launchArc).Normal;
					ballBody.Velocity = ballLaunchDir * ballLaunchForce;
				}

				victimBallGrab.BlockPickupForSeconds( ballLockout );

				if ( Networking.IsHost && attacker.IsValid() && TryIsEnemyPlayerTackle( attacker, victim ) )
					BallPassAssistState.GetOrCreate( droppedBall )?.VoidOnEnemyTackleCarrierOnHost();
			}

			if ( attackerGrabForCarrierLockout.IsValid() && AttackerPickupLockoutAfterCarrierTackle > 0f )
				attackerGrabForCarrierLockout.BlockPickupForSeconds( AttackerPickupLockoutAfterCarrierTackle );
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
			victim.Components.Get<CatchUpSpeedBoost>()?.TriggerForceWalkRampOnHost();
		}

		NotifyTackleImpactFeel( attacker, victim, speedBlitzKnockdown );
		if ( !speedBlitzKnockdown )
		{
			try
			{
				TackleComicTextHud.NotifyHostKnockdown( Scene, victim.WorldPosition, tacklePowerForBall, launchDir );
			}
			catch ( System.Exception ex )
			{
				Log.Warning( $"[Tackle] Comic text spawn failed: {ex.Message}" );
			}
		}

		// Moving hazards (traffic) skip pause — freeze holds the victim in world space while the car drives through.
		var effectivePreLaunchPause = preLaunchPauseSecondsOverride ?? PreLaunchPauseSeconds;
		var usePreLaunchPause = attacker.IsValid() && effectivePreLaunchPause > 0.0001f;
		if ( usePreLaunchPause )
		{
			victim.NetKnockdownFreezePosition = victim.WorldPosition;
			victim.NetAwaitingRagdollLaunch = true;
			BroadcastPracticeNpcFreezeForClient( attacker, victim, victim.NetKnockdownFreezePosition, speedBlitzKnockdown );
		}
		else
		{
			victim.NetIsRagdolled = true;
			BroadcastPracticeNpcRagdollForClient( attacker, victim );
		}

		SpawnRagdollObject(
			victim,
			launchDir,
			effectiveLaunchSpeed,
			launchArc,
			usePreLaunchPause,
			effectivePreLaunchPause,
			speedBlitzKnockdown,
			attacker,
			tacklePowerForBall,
			preLaunchPauseStartedAt );
		HandleRagdollRecovery( victim );
	}

	[Rpc.Broadcast]
	private void PlaySpeedBlitzLaunchSoundRpc( Vector3 worldPosition, string soundResourcePath, float volume )
	{
		var sound = ResourceLibrary.Get<SoundEvent>( soundResourcePath );
		SpeedsterSpeedBlitzUlt.PlayLaunchSoundAt( worldPosition, sound, volume );
	}

	internal void BroadcastSpeedBlitzConnectImpactSound( Vector3 worldPosition, string soundResourcePath, float volume, Guid dasherRootId )
	{
		PlaySpeedBlitzConnectImpactSoundRpc( worldPosition, soundResourcePath, volume, dasherRootId );
	}

	[Rpc.Broadcast]
	private void PlaySpeedBlitzConnectImpactSoundRpc( Vector3 worldPosition, string soundResourcePath, float volume, Guid dasherRootId )
	{
		if ( dasherRootId != Guid.Empty
			&& SpeedsterSpeedBlitzUlt.TryConsumeHostConnectSoundDedupeForDasher( Scene, dasherRootId ) )
			return;

		var sound = ResourceLibrary.Get<SoundEvent>( soundResourcePath );
		SpeedsterSpeedBlitzUlt.PlayConnectImpactSoundAt( worldPosition, sound, volume );
	}

	/// <summary>Host: owner-only hitstop / shake / punch on attacker and victim clients.</summary>
	private static void NotifyTackleImpactFeel( PlayerTackle attacker, PlayerTackle victim, bool speedBlitzKnockdown = false )
	{
		if ( !Networking.IsHost )
			return;

		if ( attacker.IsValid() && attacker != victim )
		{
			var attackerDedupe = attacker.Components.GetOrCreate<CombatFeelPredictDedupe>();
			var applyId = attackerDedupe.AllocateCombatFeelApplyIdOnHost();
			attacker.TriggerTackleImpactFeelAsAttackerRpc( applyId, speedBlitzKnockdown );
		}

		if ( victim.IsValid() )
		{
			var victimDedupe = victim.Components.GetOrCreate<CombatFeelPredictDedupe>();
			var applyId = victimDedupe.AllocateCombatFeelApplyIdOnHost();
			victim.TriggerTackleImpactFeelAsVictimRpc( applyId, hazardKnockdown: !attacker.IsValid(), speedBlitzKnockdown );
		}
	}

	[Rpc.Owner]
	private void TriggerTackleImpactFeelAsAttackerRpc( int combatFeelApplyId, bool speedBlitzKnockdown = false )
	{
		combatFeelDedupe ??= Components.Get<CombatFeelPredictDedupe>();
		if ( combatFeelDedupe.IsValid() && combatFeelDedupe.TryConsumeHostAttackerFeelDedupe( combatFeelApplyId ) )
			return;

		var feel = Components.Get<TackleImpactFeel>();
		if ( !feel.IsValid() )
			return;

		if ( speedBlitzKnockdown )
			feel.TriggerAsAttacker( ResolveSpeedBlitzImpactFeelOverrides() );
		else
			feel.TriggerAsAttacker();
	}

	[Rpc.Owner]
	private void TriggerTackleImpactFeelAsVictimRpc( int combatFeelApplyId, bool hazardKnockdown, bool speedBlitzKnockdown = false )
	{
		combatFeelDedupe ??= Components.Get<CombatFeelPredictDedupe>();
		if ( combatFeelDedupe.IsValid() && combatFeelDedupe.TryConsumeHostVictimFeelDedupe( combatFeelApplyId ) )
			return;

		var feel = Components.Get<TackleImpactFeel>();
		if ( !feel.IsValid() )
			return;

		if ( hazardKnockdown )
			feel.TriggerAsHazardVictim();
		else if ( speedBlitzKnockdown )
			feel.TriggerAsVictim( SpeedsterSpeedBlitzUlt.DefaultKnockdownImpactFeelOverrides );
		else
			feel.TriggerAsVictim();
	}

	private TackleImpactFeelOverrides ResolveSpeedBlitzImpactFeelOverrides()
	{
		var ult = Components.Get<SpeedsterSpeedBlitzUlt>();
		return ult.IsValid() ? ult.GetKnockdownImpactFeelOverrides() : SpeedsterSpeedBlitzUlt.DefaultKnockdownImpactFeelOverrides;
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
	private async void SpawnRagdollObject(
		PlayerTackle victim,
		Vector3 tackleDir,
		float effectiveLaunchSpeed,
		float launchArc,
		bool usePreLaunchPause,
		float preLaunchPauseSeconds,
		bool speedBlitzKnockdown = false,
		PlayerTackle attacker = null,
		float comicTacklePower = 0f,
		float? preLaunchPauseStartedAt = null )
	{
		// Pause timer starts at knockdown — body init runs in parallel so launch aligns with connect hang end.
		var pauseStartedAt = usePreLaunchPause
			? (preLaunchPauseStartedAt ?? Time.Now)
			: 0f;

		var ragdollGo = new GameObject( true, "PlayerRagdoll" );
		var spawnPos = usePreLaunchPause ? victim.NetKnockdownFreezePosition : victim.WorldPosition + Vector3.Up * 10f;
		ragdollGo.WorldPosition = spawnPos;
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
		if ( baseVictimRenderer.IsValid() )
		{
			// CopyFrom carries the skin material override + body groups + LOD the ClothingContainer
			// applied to the standing body; setting Model alone left the ragdoll on the model's default skin.
			primaryRenderer.CopyFrom( baseVictimRenderer );
			primaryRenderer.UseAnimGraph = false;
			primaryRenderer.CreateBoneObjects = false;
			primaryRenderer.LodOverride = 0;
		}
		else
		{
			primaryRenderer.Model = null;
		}

		var ragdollPhysics = ragdollGo.AddComponent<ModelPhysics>();
		ragdollPhysics.Renderer = primaryRenderer;
		ragdollPhysics.MotionEnabled = false;
		ragdollPhysics.IgnoreRoot = false;
		ragdollPhysics.Enabled = true;

		if ( baseVictimRenderer != null )
			ragdollPhysics.CopyBonesFrom( baseVictimRenderer, true );

		if ( baseVictimRenderer != null )
			AddVictimClothingToRagdoll( victim, ragdollGo, primaryRenderer, baseVictimRenderer );

		if ( usePreLaunchPause )
			SetRagdollRenderersEnabled( ragdollGo, false );

		ragdollGo.Tags.Add( "ragdoll" );
		victim.ragdollObject = ragdollGo;
		ragdollGo.Components.GetOrCreate<RagdollEnemyOutline>().ConfigureFromVictim( victim );

		ragdollPhysics.MotionEnabled = true;

		var waitForBodies = RagdollPhysicsInitDelay.Clamp( 0.01f, 0.25f );
		var bodiesReady = await WaitForRagdollBodiesAsync( ragdollGo, waitForBodies );
		if ( !ragdollGo.IsValid() )
			return;

		var pause = preLaunchPauseSeconds.Clamp( 0f, 1.5f );
		if ( usePreLaunchPause && pause > 0.0001f )
		{
			if ( baseVictimRenderer != null )
				ragdollPhysics.CopyBonesFrom( baseVictimRenderer, true );

			FreezeRagdollPhysics( ragdollPhysics );

			var elapsedPause = Time.Now - pauseStartedAt;
			var remainingPause = Math.Max( 0f, pause - elapsedPause );

			if ( EnableTackleDebugLogs )
				Log.Info( $"[Tackle] Pre-launch pause | bodies={bodiesReady} pause={pause * 1000f:F0}ms remaining={remainingPause * 1000f:F0}ms victim-visible" );

			if ( remainingPause > 0.0001f )
				await GameTask.DelaySeconds( remainingPause );

			if ( !ragdollGo.IsValid() || !victim.IsValid() )
				return;

			SetRagdollRenderersEnabled( ragdollGo, true );
			ragdollPhysics.MotionEnabled = true;
			var launched = TryApplyRagdollLaunchImpulse( ragdollGo, tackleDir, effectiveLaunchSpeed, launchArc, out var bodyCount );

			if ( speedBlitzKnockdown && launched && Networking.IsHost )
			{
				attacker?.Components.Get<SpeedsterSpeedBlitzUlt>()?.EndConnectHangOnHostAtLaunch();

				victim.PlaySpeedBlitzLaunchSoundRpc(
					spawnPos,
					SpeedsterSpeedBlitzUlt.ResolveLaunchSoundResourcePath( attacker ),
					SpeedsterSpeedBlitzUlt.ResolveLaunchSoundVolume( attacker ) );

				try
				{
					TackleComicTextHud.NotifyHostKnockdown(
						Scene,
						ragdollGo.WorldPosition,
						comicTacklePower,
						tackleDir,
						TackleComicTextHud.ComicBurstPalette.Ult );
				}
				catch ( System.Exception ex )
				{
					Log.Warning( $"[Tackle] Speed Blitz comic text spawn failed: {ex.Message}" );
				}
			}

			ragdollGo.NetworkSpawn();

			victim.NetAwaitingRagdollLaunch = false;
			victim.NetRagdollPosition = ragdollGo.WorldPosition;
			victim.NetIsRagdolled = true;
			BroadcastPracticeNpcRagdollForClient( attacker, victim );

			if ( EnableTackleDebugLogs )
				Log.Info( $"[Tackle] Post-pause impulse + spawn | launched={launched} bodies={bodyCount}" );
		}
		else
		{
			// Legacy: impulse on host while still local, then NetworkSpawn — first snapshot already in flight.
			var launched = bodiesReady && TryApplyRagdollLaunchImpulse( ragdollGo, tackleDir, effectiveLaunchSpeed, launchArc, out _ );
			if ( !ragdollGo.IsValid() )
				return;

			if ( EnableTackleDebugLogs )
				Log.Info( $"[Tackle] NetworkSpawn after impulse | launched={launched}" );

			ragdollGo.NetworkSpawn();
		}
	}

	/// <summary>Poll until <see cref="ModelPhysics"/> bodies exist (no impulse).</summary>
	private async Task<bool> WaitForRagdollBodiesAsync( GameObject ragdollGo, float maxWaitSeconds )
	{
		const float pollStepSeconds = 0.008f;
		var started = Time.Now;

		while ( ragdollGo.IsValid() && Time.Now - started < maxWaitSeconds )
		{
			if ( HasRagdollLaunchBodies( ragdollGo ) )
			{
				if ( EnableTackleDebugLogs )
					Log.Info( $"[Tackle] Bodies ready in {(Time.Now - started) * 1000f:F0}ms" );
				return true;
			}

			await GameTask.DelaySeconds( pollStepSeconds );
		}

		return ragdollGo.IsValid() && HasRagdollLaunchBodies( ragdollGo );
	}

	private static bool HasRagdollLaunchBodies( GameObject ragdollGo )
	{
		var mp = ragdollGo.Components.Get<ModelPhysics>();
		if ( mp == null || mp.Bodies.Count == 0 )
			return false;

		return mp.Bodies[0].Component?.PhysicsBody != null;
	}

	private static void SetRagdollRenderersEnabled( GameObject ragdollGo, bool enabled )
	{
		foreach ( var renderer in ragdollGo.Components.GetAll<SkinnedModelRenderer>( FindMode.EverythingInSelfAndDescendants ) )
		{
			if ( renderer.IsValid() )
				renderer.Enabled = enabled;
		}
	}

	private void ApplyKnockdownAwaitingFreezeLocally()
	{
		if ( knockdownAwaitingFreezeApplied )
			return;

		if ( Network.IsOwner )
			Components.Get<BallThrow>()?.CancelThrowAimingState();

		knockdownAwaitingFreezeApplied = true;

		awaitingDisabledColliders.Clear();
		awaitingDisabledColliders.AddRange( Components.GetAll<Collider>( FindMode.EverythingInSelfAndDescendants ) );
		foreach ( var col in awaitingDisabledColliders )
			if ( col.IsValid() ) col.Enabled = false;

		if ( playerController.IsValid() )
			playerController.Enabled = false;
	}

	private void ClearKnockdownAwaitingFreezeLocally()
	{
		if ( !knockdownAwaitingFreezeApplied )
			return;

		knockdownAwaitingFreezeApplied = false;

		foreach ( var col in awaitingDisabledColliders )
			if ( col.IsValid() ) col.Enabled = true;
		awaitingDisabledColliders.Clear();

		if ( playerController.IsValid() && !isRagdolled )
			playerController.Enabled = true;
	}

	private static void FreezeRagdollPhysics( ModelPhysics ragdollPhysics )
	{
		if ( ragdollPhysics == null )
			return;

		ragdollPhysics.MotionEnabled = false;

		foreach ( var body in ragdollPhysics.Bodies )
		{
			var pb = body.Component?.PhysicsBody;
			if ( pb == null )
				continue;

			pb.Velocity = Vector3.Zero;
			pb.AngularVelocity = Vector3.Zero;
		}
	}

	private bool TryApplyRagdollLaunchImpulse(
		GameObject ragdollGo,
		Vector3 tackleDir,
		float effectiveLaunchSpeed,
		float launchArc,
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

		var launchDir = (tackleDir + Vector3.Up * launchArc).Normal;
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

		while ( victim.IsValid() && victim.IsAwaitingRagdollLaunch && !victim.IsRagdolled && Time.Now - started < 3f )
			await GameTask.DelaySeconds( pollSeconds );

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

		if ( Networking.IsHost )
			victim.NetPostRagdollSlowCatchUpUntil = 0f;

		victim.NetIsRagdolled = false;
		BroadcastPracticeNpcStandUpForClient( victim );

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
		// Practice patrol NPC: skip the warm-up ramp and use a fixed designer-tunable bonus.
		// This runs host-side only (NPC is host-owned, owner+!host path never fires for it).
		practiceNpcPatrol ??= Components.Get<PracticeNpcPatrolHostState>();
		if ( practiceNpcPatrol?.IsPatrollingAtChargeSpeed == true )
		{
			var patrolBonus = practiceNpcPatrol.PatrolTackleChargeBonus;
			if ( patrolBonus > 0f )
			{
				bonus = patrolBonus;
				return;
			}
		}

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

		if ( Network.IsOwner )
			Components.Get<BallThrow>()?.CancelThrowAimingState();

		ClearKnockdownAwaitingFreezeLocally();
		standUpCameraBlendStartTime = -1f;
		ragdollEnterBlendStartTime = -1f;
		deferringRagdollCamForImpactFeel = false;

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
		ClearKnockdownAwaitingFreezeLocally();

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

		playerClass?.ApplyClassAppearance();

		if ( this.Network.IsOwner && StandUpCameraBlendDuration > 0.001f && activeCamera.IsValid() && ShouldApplyOwnerKnockdownCamera )
		{
			standUpCameraBlendFromPos = lastRagdollCameraPos;
			standUpCameraBlendFromRot = lastRagdollCameraRot;
			standUpCameraBlendStartTime = Time.Now;
		}
		else
			standUpCameraBlendStartTime = -1f;

		Log.Info( $"[Tackle] StandUpLocally on {GameObject.Name} | IsProxy={IsProxy}" );
	}

	void ComputeRagdollOrbitCamera( out Vector3 position, out Rotation rotation )
	{
		var lookRot = playerController.IsValid()
			? playerController.EyeAngles.ToRotation()
			: WorldRotation;
		var orbit = -lookRot.Forward * RagdollCameraDistance + Vector3.Up * RagdollCameraHeight;
		position = WorldPosition + orbit;
		rotation = lookRot;
	}

	// --- Practice NPC client visuals (scene dummies stay NetworkMode.Snapshot — [Sync] does not replicate) ---

	private static void BroadcastPracticeNpcFreezeForClient( PlayerTackle broadcaster, PlayerTackle victim, Vector3 freezePosition, bool speedBlitzKnockdown )
	{
		if ( !Networking.IsHost || !victim.IsValid() || !victim.GameObject.Tags.Has( PracticeNpcTag ) )
			return;

		ResolvePracticeNpcVisualBroadcaster( broadcaster )?.PracticeNpcClientFreezeRpc( victim.GameObject.Id, freezePosition, speedBlitzKnockdown );
	}

	private static void BroadcastPracticeNpcRagdollForClient( PlayerTackle broadcaster, PlayerTackle victim )
	{
		if ( !Networking.IsHost || !victim.IsValid() || !victim.GameObject.Tags.Has( PracticeNpcTag ) )
			return;

		ResolvePracticeNpcVisualBroadcaster( broadcaster )?.PracticeNpcClientRagdollRpc( victim.GameObject.Id );
	}

	private static void BroadcastPracticeNpcStandUpForClient( PlayerTackle victim )
	{
		if ( !Networking.IsHost || !victim.IsValid() || !victim.GameObject.Tags.Has( PracticeNpcTag ) )
			return;

		ResolvePracticeNpcVisualBroadcaster( null )?.PracticeNpcClientStandUpRpc( victim.GameObject.Id, victim.NetStandUpPosition, victim.NetPracticeNpcStandEyeAngles );
	}

	/// <summary>RPC must originate from a network-spawned player — scene practice dummies are not networked objects.</summary>
	private static PlayerTackle ResolvePracticeNpcVisualBroadcaster( PlayerTackle preferred )
	{
		if ( preferred.IsValid() && preferred.GameObject.Network.Active )
			return preferred;

		var scene = Game.ActiveScene;
		if ( scene is null )
			return null;

		foreach ( var tackle in scene.GetAllComponents<PlayerTackle>() )
		{
			if ( tackle.GameObject.Tags.Has( PracticeNpcTag ) )
				continue;
			if ( !tackle.GameObject.Network.Active )
				continue;

			return tackle;
		}

		return null;
	}

	[Rpc.Broadcast]
	private void PracticeNpcClientFreezeRpc( Guid victimRootId, Vector3 freezePosition, bool speedBlitzKnockdown )
	{
		if ( Networking.IsHost )
			return;

		if ( !TryFindPracticeNpcTackle( victimRootId, out var victim ) )
			return;

		victim.MirrorPracticeNpcFreezeFromHost( freezePosition, speedBlitzKnockdown );
	}

	[Rpc.Broadcast]
	private void PracticeNpcClientRagdollRpc( Guid victimRootId )
	{
		if ( Networking.IsHost )
			return;

		if ( !TryFindPracticeNpcTackle( victimRootId, out var victim ) )
			return;

		victim.MirrorPracticeNpcRagdollFromHost();
	}

	[Rpc.Broadcast]
	private void PracticeNpcClientStandUpRpc( Guid victimRootId, Vector3 standUpPosition, Angles standEyeAngles )
	{
		if ( Networking.IsHost )
			return;

		if ( !TryFindPracticeNpcTackle( victimRootId, out var victim ) )
			return;

		victim.MirrorPracticeNpcStandUpFromHost( standUpPosition, standEyeAngles );
	}

	private static bool TryFindPracticeNpcTackle( Guid victimRootId, out PlayerTackle victim )
	{
		victim = null;
		var scene = Game.ActiveScene;
		if ( scene is null )
			return false;

		foreach ( var tackle in scene.GetAllComponents<PlayerTackle>() )
		{
			if ( tackle.GameObject.Id != victimRootId )
				continue;
			if ( !tackle.GameObject.Tags.Has( PracticeNpcTag ) )
				continue;

			victim = tackle;
			return true;
		}

		return false;
	}

	/// <summary>Client non-host: freeze a scene practice dummy at the current visual contact position (snapshot NPCs can lag behind host).</summary>
	public void BeginPracticeNpcClientContactFreeze( bool speedBlitzKnockdown )
	{
		if ( Networking.IsHost || !GameObject.Tags.Has( PracticeNpcTag ) )
			return;

		netLastKnockdownWasSpeedBlitz = speedBlitzKnockdown;
		netAwaitingRagdollLaunch = true;
		PinPracticeNpcClientContactFreezePosition();
		ApplyKnockdownAwaitingFreezeLocally();
	}

	private void MirrorPracticeNpcFreezeFromHost( Vector3 freezePosition, bool speedBlitzKnockdown )
	{
		NetKnockdownFreezePosition = freezePosition;
		netLastKnockdownWasSpeedBlitz = speedBlitzKnockdown;
		netAwaitingRagdollLaunch = true;
		PinPracticeNpcClientContactFreezePosition();
		ApplyKnockdownAwaitingFreezeLocally();
	}

	private void PinPracticeNpcClientContactFreezePosition()
	{
		if ( Networking.IsHost || !GameObject.Tags.Has( PracticeNpcTag ) )
			return;

		if ( practiceNpcClientContactFreezePinned )
			return;

		practiceNpcClientContactFreezePinned = true;
		practiceNpcClientContactFreezePos = WorldPosition;
	}

	private void ApplyKnockdownFreezeWorldPosition()
	{
		if ( practiceNpcClientContactFreezePinned )
		{
			WorldPosition = practiceNpcClientContactFreezePos;
			return;
		}

		WorldPosition = NetKnockdownFreezePosition;
	}

	private void ClearPracticeNpcClientContactFreezePin()
	{
		practiceNpcClientContactFreezePinned = false;
		practiceNpcClientContactFreezePos = default;
	}

	private void MirrorPracticeNpcRagdollFromHost()
	{
		netAwaitingRagdollLaunch = false;
		ClearPracticeNpcClientContactFreezePin();
		if ( isRagdolled )
			return;

		isRagdolled = true;
		wasRagdolled = true;
		ApplyRagdollLocally();
	}

	private void MirrorPracticeNpcStandUpFromHost( Vector3 standUpPosition, Angles standEyeAngles )
	{
		netAwaitingRagdollLaunch = false;
		ClearPracticeNpcClientContactFreezePin();
		NetStandUpPosition = standUpPosition;
		NetPracticeNpcStandEyeAngles = standEyeAngles;

		if ( !isRagdolled && !knockdownAwaitingFreezeApplied )
			return;

		isRagdolled = false;
		wasRagdolled = false;
		StandUpLocally();
	}

	private static bool TryIsEnemyPlayerTackle( PlayerTackle attacker, PlayerTackle victim )
	{
		if ( !attacker.IsValid() || !victim.IsValid() )
			return false;

		if ( attacker.GameObject.Tags.Has( CitizenAvatarLod.PracticeNpcTag ) )
			return false;

		var attackerTeam = attacker.Components.Get<PlayerTeam>();
		var victimTeam = victim.Components.Get<PlayerTeam>();
		if ( !attackerTeam.IsValid() || !victimTeam.IsValid() )
			return false;

		if ( !MatchTeamIds.IsValid( attackerTeam.TeamId ) || !MatchTeamIds.IsValid( victimTeam.TeamId ) )
			return false;

		return attackerTeam.TeamId != victimTeam.TeamId;
	}
}
