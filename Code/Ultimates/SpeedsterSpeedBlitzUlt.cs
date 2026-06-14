using Sandbox;
using System;

/// <summary>
/// Speedster <b>Speed Blitz</b> ult — slice 2a: tap <c>Ultimate</c> to commit, 3 s wind-up, then a fast dash.
/// <para>
/// Movement is <b>owner-driven through the built-in <see cref="PlayerController"/></b> (we set the player's
/// Rigidbody velocity, just like <see cref="PlayerDodge"/>): the character controller's move mode handles
/// collide-and-slide on walls/props, step-up over small ledges, and stick-to-ground — so the dasher slides
/// along walls instead of dead-stopping, never tunnels through them, and never gets stuck in geometry.
/// The base locomotion keeps the legs running and <see cref="PlayerChargeRunAnim"/> layers the charge pose,
/// so it looks like a normal charge-speed run.
/// </para>
/// <para>
/// The dash is <b>time-based</b> (<c>duration = range / speed</c>). Hitting a wall just means you slide and
/// cover less ground before the timer ends — you are never hard-stopped, and standing next to a wall still
/// dashes (you slide along it).
/// </para>
/// <para>
/// The <b>host</b> validates the commit, spends the charge, owns phase timing, runs enemy hit-detection,
/// applies the knockdown, keeps the dasher tackle-immune while dashing, and blocks all ult-charge gain
/// until the ult is over (so % only climbs again afterwards).
/// </para>
/// See GAMEPLAY_DESIGN.md → Speed Blitz.
/// </summary>
[Order( 10011 )]
public sealed class SpeedsterSpeedBlitzUlt : Component
{
	public enum SpeedBlitzPhase : byte
	{
		None = 0,
		WindUp = 1,
		Dash = 2,
	}

	private const string SpeedsterClassName = "Speedster";

	[Property] public string UltimateAction { get; set; } = "Ultimate";

	[Property, Group( "Wind-up" )] public float WindUpDurationSeconds { get; set; } = 3f;

	/// <summary> Total ground the dash tries to cover (walls reduce actual distance via slide). </summary>
	[Property, Group( "Dash" )] public float DashRange { get; set; } = 1200f;

	/// <summary> Dash horizontal speed. Dash duration = <see cref="DashRange"/> / this. Keep high but not so high it tunnels thin props. </summary>
	[Property, Group( "Dash" )] public float DashSpeed { get; set; } = 2000f;

	/// <summary>
	/// Half-width of the dash hit corridor from the path centerline to the outer body edge (matches
	/// <see cref="SpeedBlitzAimPreview"/> side lines). Host checks victim center lateral distance +
	/// victim <see cref="PlayerController.BodyRadius"/> against this value.
	/// </summary>
	[Property, Group( "Dash" )] public float HitHalfWidth { get; set; } = 42f;

	/// <summary> Fallback body radius when a dash target has no <see cref="PlayerController"/> / class capsule. </summary>
	[Property, Group( "Dash" )] public float DefaultTargetBodyRadius { get; set; } = 16f;

	[Property, Group( "Dash" )] public float KnockdownLaunchSpeed { get; set; } = 950f;
	[Property, Group( "Dash" )] public float KnockdownLaunchArc { get; set; } = 1.2f;

	/// <summary> Multiplier on dash-speed × fixed delta when capping host sweep steps (client RPC can arrive in bursts). </summary>
	[Property, Group( "Dash" )] public float DashSweepStepMultiplier { get; set; } = 2.5f;

	[Property] public bool EnableSpeedBlitzDebugLogs { get; set; }

	[Sync( SyncFlags.FromHost )] private SpeedBlitzPhase NetPhase { get; set; }
	[Sync( SyncFlags.FromHost )] private Vector3 NetCommittedDirection { get; set; }

	// Host-only state.
	private float hostWindUpEndsAt;
	private float hostDashEndsAt;
	private bool hostHasHitTarget;
	private Vector3 hostLastDashCheckPos;
	private Vector3 hostOwnerDashSamplePos;
	private bool hostHasOwnerDashSample;
	private float hostOwnerDashSampleTime;

	// Owner-only state.
	private float nextCommitRequestAt;
	private Angles ownerLockedEyeAngles;
	private bool ownerLookLocked;
	private float ownerCommitPendingUntil;
	private bool ownerControllerInputSuppressed;
	private bool ownerSavedUseInputControls = true;
	private bool ownerWasInDashPhase;
	private bool ownerBlockedAimUntilUltimateRelease;
	/// <summary> Host ended the dash before <see cref="NetPhase"/> sync — blocks <see cref="OwnerDriveDashMovement"/> until inactive. </summary>
	private bool ownerDashMovementBlocked;

	// Owner-only client predict (Tier 0 — see MULTIPLAYER_NETCODE.md).
	private bool ownerHasLocalDashCheckPos;
	private Vector3 ownerLastLocalDashCheckPos;
	private bool ownerPredictedHitThisDash;

	private PlayerUltCharge ultCharge;
	private PlayerClass playerClass;
	private PlayerTeam playerTeam;
	private PlayerTackle playerTackle;
	private PlayerController playerController;
	private BallGrab ballGrab;
	private Rigidbody playerBody;
	private CatchUpSpeedBoost catchUpSpeedBoost;
	private TackleImpactFeel tackleImpactFeel;

	public bool IsActive => NetPhase != SpeedBlitzPhase.None;
	public bool IsWindUp => NetPhase == SpeedBlitzPhase.WindUp;
	public bool IsDashing => NetPhase == SpeedBlitzPhase.Dash;

	/// <summary> Owner-only: holding <see cref="UltimateAction"/> at full charge before commit (slice 2b preview). </summary>
	public bool IsAiming { get; private set; }

	/// <summary> While true, ball pickup is blocked (BallGrab) and dodge is suppressed (PlayerDodge). </summary>
	public bool BlocksBallPickup => IsActive;

	private float DashDurationSeconds => (DashRange / MathF.Max( DashSpeed, 1f )).Clamp( 0.05f, 6f );

	protected override void OnStart()
	{
		ultCharge = Components.Get<PlayerUltCharge>();
		playerClass = Components.Get<PlayerClass>();
		playerTeam = Components.Get<PlayerTeam>();
		playerTackle = Components.Get<PlayerTackle>();
		playerController = Components.Get<PlayerController>();
		ballGrab = Components.Get<BallGrab>();
		playerBody = Components.Get<Rigidbody>();
		catchUpSpeedBoost = Components.Get<CatchUpSpeedBoost>();
		tackleImpactFeel = Components.Get<TackleImpactFeel>();
	}

	protected override void OnUpdate()
	{
		if ( Networking.IsHost )
			HostUpdate();

		if ( Network.IsOwner )
			OwnerUpdate();
	}

	protected override void OnFixedUpdate()
	{
		if ( Networking.IsHost )
			HostFixedUpdate();

		if ( !Network.IsOwner )
			return;

		if ( IsWindUp )
			OwnerFreezeMovement();
		else if ( IsDashing )
		{
			OwnerDriveDashMovement();
			OwnerPredictDashHitCheck();
			ReportDashSamplePositionToHost();
		}
	}

	/// <summary> Owner: re-assert the locked aim last, after other systems integrate look, so the dash holds its committed direction. </summary>
	protected override void OnPreRender()
	{
		if ( !Network.IsOwner || !ownerLookLocked || !IsActive )
			return;

		ApplyOwnerLookLock();
	}

	/// <summary> Safety: never leave the controller with input disabled if this component is turned off mid-ult. </summary>
	protected override void OnDisabled()
	{
		if ( Network.IsOwner )
			RestoreOwnerController();
	}

	// ---------------------------------------------------------------------
	// Host
	// ---------------------------------------------------------------------

	private void HostUpdate()
	{
		switch ( NetPhase )
		{
			case SpeedBlitzPhase.WindUp:
				// Vulnerable during wind-up: a knockdown wastes the (already-spent) ult.
				if ( playerTackle.IsValid() && playerTackle.IsKnockedDown )
				{
					EndBlitzOnHost( "windup_interrupted" );
					return;
				}

				if ( Time.Now >= hostWindUpEndsAt )
					BeginDashOnHost();
				return;

		}
	}

	/// <summary> Host dash timer + hit sweep — fixed update so it aligns with owner position reports. </summary>
	private void HostFixedUpdate()
	{
		if ( NetPhase != SpeedBlitzPhase.Dash )
			return;

		playerTackle?.SetHostTackleImmune( true );

		if ( Time.Now >= hostDashEndsAt )
		{
			EndBlitzOnHost( "dash_done" );
			return;
		}

		HostDashHitCheck();
	}

	private void TryCommitOnHost( Vector3 committedDirFromOwner )
	{
		if ( !Networking.IsHost )
			return;

		if ( !PassesCommitPrecheck() || !AllowsUltActivation() )
		{
			LogReject( "host_precheck" );
			return;
		}

		var dir = committedDirFromOwner.WithZ( 0 );
		if ( dir.Length < 0.001f )
		{
			LogReject( "zero_dir" );
			return;
		}

		dir = dir.Normal;

		if ( ultCharge is null || !ultCharge.TrySpendFullChargeOnHost() )
		{
			LogReject( "no_charge" );
			return;
		}

		// No ult-charge gain (passive / tackle / goal) until the ult is over — % only climbs again afterwards.
		ultCharge.SetHostChargeGainBlocked( true );

		NetCommittedDirection = dir;
		hostWindUpEndsAt = Time.Now + WindUpDurationSeconds.Clamp( 0.05f, 30f );
		NetPhase = SpeedBlitzPhase.WindUp;

		if ( EnableSpeedBlitzDebugLogs )
			Log.Info( $"[SpeedBlitz] {GameObject.Name}: commit dir={dir} windUpEnds={hostWindUpEndsAt:F2}" );
	}

	[Rpc.Host]
	private void RequestCommitSpeedBlitzOnHost( Vector3 committedDirFromOwner )
	{
		if ( Network.Owner is null || Rpc.Caller.SteamId != Network.Owner.SteamId )
			return;

		TryCommitOnHost( committedDirFromOwner );
	}

	private void BeginDashOnHost()
	{
		if ( NetCommittedDirection.WithZ( 0 ).Length < 0.001f )
		{
			EndBlitzOnHost( "bad_commit_dir" );
			return;
		}

		hostDashEndsAt = Time.Now + DashDurationSeconds;
		hostHasHitTarget = false;
		hostLastDashCheckPos = GetHostDashCheckCurrentPosition();
		hostHasOwnerDashSample = false;
		hostOwnerDashSampleTime = 0f;
		playerTackle?.SetHostTackleImmune( true );
		NetPhase = SpeedBlitzPhase.Dash;

		if ( EnableSpeedBlitzDebugLogs )
			Log.Info( $"[SpeedBlitz] {GameObject.Name}: dash start dur={DashDurationSeconds:F2}s" );
	}

	/// <summary> Host: swept corridor check from last position to current; first enemy ends the dash. </summary>
	private void HostDashHitCheck()
	{
		var currRaw = GetHostDashCheckCurrentPosition();
		var curr = ClampDashSweepEndPosition( hostLastDashCheckPos, currRaw );

		if ( TryFindBestDashHitInSegment( hostLastDashCheckPos, curr, out var best ) )
			HostApplyDashKnockdown( best );

		hostLastDashCheckPos = curr;
	}

	/// <summary> Shared corridor sweep — host hit test and owner predict use the same filters/width. </summary>
	private bool TryFindBestDashHitInSegment( Vector3 segStartRaw, Vector3 segEndRaw, out PlayerTackle victim )
	{
		victim = null;

		var segStart = segStartRaw.WithZ( 0 );
		var segEnd = segEndRaw.WithZ( 0 );
		var halfWidth = HitHalfWidth.Clamp( 4f, 200f );

		PlayerTackle best = null;
		var bestAlong = float.MaxValue;

		foreach ( var candidate in Scene.GetAllComponents<PlayerTackle>() )
		{
			if ( !IsValidDashTarget( candidate ) )
				continue;

			var target = candidate.WorldPosition.WithZ( 0 );
			var (lateral, along) = DistanceToSegment2D( segStart, segEnd, target );
			var targetBodyRadius = GetDashTargetBodyRadius( candidate );
			if ( lateral + targetBodyRadius > halfWidth )
				continue;

			if ( along < bestAlong )
			{
				best = candidate;
				bestAlong = along;
			}
		}

		if ( !best.IsValid() )
			return false;

		victim = best;
		return true;
	}

	/// <summary> Host: always launch along committed dash dir (MP-safe); brief pre-launch pause like tackles. </summary>
	private void HostApplyDashKnockdown( PlayerTackle victim )
	{
		var knockDir = NetCommittedDirection.WithZ( 0 );
		if ( knockDir.Length < 0.001f )
			knockDir = Vector3.Forward;
		else
			knockDir = knockDir.Normal;

		playerBody ??= Components.Get<Rigidbody>();
		if ( playerBody.IsValid() )
			playerBody.Velocity = new Vector3( 0f, 0f, playerBody.Velocity.z );

		playerController ??= Components.Get<PlayerController>();
		if ( playerController.IsValid() )
			playerController.WishVelocity = Vector3.Zero;

		var victimBody = victim.Components.Get<Rigidbody>();
		if ( victimBody.IsValid() )
			victimBody.Velocity = Vector3.Zero;

		var victimController = victim.Components.Get<PlayerController>();
		if ( victimController.IsValid() )
			victimController.WishVelocity = Vector3.Zero;

		victim.ApplyKnockdownFromHost( knockDir, KnockdownLaunchSpeed, KnockdownLaunchArc, playerTackle );
		hostHasHitTarget = true;

		if ( EnableSpeedBlitzDebugLogs )
			Log.Info( $"[SpeedBlitz] {GameObject.Name}: dash hit {victim.GameObject.Name}" );

		EndBlitzOnHost( "hit_enemy" );
	}

	/// <summary> Prevents lagged owner samples from sweeping a huge corridor in one host tick. </summary>
	private Vector3 ClampDashSweepEndPosition( Vector3 segStart, Vector3 segEndRaw )
	{
		var flatDelta = (segEndRaw - segStart).WithZ( 0f );
		var maxStep = GetMaxDashSweepStepDistance();
		if ( flatDelta.Length <= maxStep )
			return segEndRaw;

		var clamped = segStart + flatDelta.Normal * maxStep;
		return new Vector3( clamped.x, clamped.y, segEndRaw.z );
	}

	private float GetMaxDashSweepStepDistance()
	{
		var tick = Time.Delta.Clamp( 0.008f, 0.05f );
		return (DashSpeed * tick * DashSweepStepMultiplier.Clamp( 1f, 6f )).Clamp( 16f, 160f );
	}

	/// <summary> Host: owner-reported dash sample for client-owned dashers (local owner uses live transform). </summary>
	private Vector3 GetHostDashCheckCurrentPosition()
	{
		if ( Network.IsOwner || !Networking.IsHost )
			return GameObject.WorldPosition;

		if ( hostHasOwnerDashSample && Time.Now - hostOwnerDashSampleTime <= 0.15f )
			return hostOwnerDashSamplePos;

		return GameObject.WorldPosition;
	}

	/// <summary> Host-only: latest owner-reported dash position for this pawn (client-owned dashers). </summary>
	internal bool TryGetHostReportedDashPosition( out Vector3 position )
	{
		if ( !Networking.IsHost || !hostHasOwnerDashSample || Time.Now - hostOwnerDashSampleTime > 0.15f )
		{
			position = default;
			return false;
		}

		position = hostOwnerDashSamplePos;
		return true;
	}

	[Rpc.Host]
	private void ReportDashSamplePositionOnHostRpc( Vector3 samplePos )
	{
		if ( Network.Owner is null || Rpc.Caller.SteamId != Network.Owner.SteamId )
			return;

		if ( NetPhase != SpeedBlitzPhase.Dash )
			return;

		hostOwnerDashSamplePos = samplePos;
		hostHasOwnerDashSample = true;
		hostOwnerDashSampleTime = Time.Now;
	}

	/// <summary> Returns (lateral distance from point to segment, distance along segment from start to the closest point). </summary>
	private static (float lateral, float along) DistanceToSegment2D( Vector3 a, Vector3 b, Vector3 p )
	{
		var ab = b - a;
		var abLen = ab.Length;
		if ( abLen < 0.0001f )
			return ((p - a).Length, 0f);

		var dir = ab / abLen;
		var t = Vector3.Dot( p - a, dir ).Clamp( 0f, abLen );
		var closest = a + dir * t;
		return ((p - closest).Length, t);
	}

	private void EndBlitzOnHost( string reason )
	{
		if ( !Networking.IsHost || NetPhase == SpeedBlitzPhase.None )
			return;

		var wasDashing = NetPhase == SpeedBlitzPhase.Dash;
		var applyWalkRampPenalty = wasDashing && (reason == "dash_done" || reason == "hit_enemy" );
		var notifyOwnerToStop = wasDashing && Network.Owner is not null && !Network.IsOwner;

		if ( EnableSpeedBlitzDebugLogs )
			Log.Info( $"[SpeedBlitz] {GameObject.Name}: end ({reason})" );

		NetPhase = SpeedBlitzPhase.None;
		hostWindUpEndsAt = 0f;
		hostDashEndsAt = 0f;
		hostHasHitTarget = false;
		hostHasOwnerDashSample = false;
		hostOwnerDashSampleTime = 0f;
		playerTackle?.SetHostTackleImmune( false );
		ultCharge?.SetHostChargeGainBlocked( false );

		if ( applyWalkRampPenalty )
		{
			catchUpSpeedBoost ??= Components.Get<CatchUpSpeedBoost>();
			catchUpSpeedBoost?.TriggerForceWalkRampOnHost();

			if ( EnableSpeedBlitzDebugLogs )
				Log.Info( $"[SpeedBlitz] {GameObject.Name}: dash ended — forced to walk ramp" );
		}

		if ( notifyOwnerToStop )
			NotifyOwnerDashEndedRpc();
	}

	[Rpc.Owner]
	private void NotifyOwnerDashEndedRpc()
	{
		ownerDashMovementBlocked = true;
		OwnerZeroHorizontalVelocity();
	}

	/// <summary> Host: abort wind-up or dash (e.g. round reset). Charge is not refunded. </summary>
	public void CancelBlitzOnHost()
	{
		EndBlitzOnHost( "cancelled" );
	}

	/// <summary> Host: cancel every active Speed Blitz (goal reset / rematch). </summary>
	public static void CancelAllInScene( Scene scene )
	{
		if ( !Networking.IsHost || scene is null )
			return;

		foreach ( var ult in scene.GetAllComponents<SpeedsterSpeedBlitzUlt>() )
		{
			if ( ult.IsValid() )
				ult.CancelBlitzOnHost();
		}
	}

	// ---------------------------------------------------------------------
	// Owner
	// ---------------------------------------------------------------------

	private void OwnerUpdate()
	{
		if ( !IsActive )
		{
			ownerDashMovementBlocked = false;
			ResetOwnerDashPredictState();
		}

		if ( IsDashing )
			ownerWasInDashPhase = true;

		// "Pending" covers the brief window between pressing X and the host confirming the phase
		// (client owner) so the player can't keep moving before the lock kicks in.
		var suppress = IsActive || Time.Now < ownerCommitPendingUntil;

		if ( IsActive )
			ownerCommitPendingUntil = 0f;

		if ( !suppress )
		{
			if ( ownerWasInDashPhase )
			{
				ownerWasInDashPhase = false;
				OwnerZeroHorizontalVelocity();
			}

			ownerLookLocked = false;
			RestoreOwnerController();
			OwnerUpdateAimAndCommit();
			return;
		}

		IsAiming = false;

		// Take over the controller so its own input can't move/strafe the player during the ult.
		SuppressOwnerController();
		Input.AnalogMove = Vector3.Zero;
		ApplyOwnerLookLock();
	}

	private void OwnerUpdateAimAndCommit()
	{
		if ( ownerBlockedAimUntilUltimateRelease )
		{
			if ( Input.Released( UltimateAction ) )
				ownerBlockedAimUntilUltimateRelease = false;

			IsAiming = false;
			return;
		}

		var canAim = PassesCommitPrecheck() && AllowsUltActivation();
		IsAiming = canAim && Input.Down( UltimateAction );

		if ( IsAiming && Input.Down( "Attack2" ) )
		{
			ownerBlockedAimUntilUltimateRelease = true;
			IsAiming = false;
			return;
		}

		if ( !Input.Released( UltimateAction ) )
			return;

		if ( Time.Now < nextCommitRequestAt )
			return;

		if ( !canAim )
		{
			if ( EnableSpeedBlitzDebugLogs )
				Log.Info( $"[SpeedBlitz] {GameObject.Name}: X released but commit blocked (speedster={IsSpeedsterClass()} full={ultCharge?.IsFullyCharged} holdingBall={ballGrab?.IsHolding} phaseOk={AllowsUltActivation()})" );
			return;
		}

		var dir = GetHorizontalCommitDirection();
		if ( dir.Length < 0.001f )
			return;

		playerController ??= Components.Get<PlayerController>();
		// Lock aim on release (before wind-up) so committed direction matches frozen look during channel + dash.
		ownerLockedEyeAngles = playerController.IsValid()
			? LockEyeAnglesToHorizontalDirection( playerController.EyeAngles, dir )
			: default;
		ownerLookLocked = true;
		ownerCommitPendingUntil = Time.Now + 0.5f;
		nextCommitRequestAt = Time.Now + 0.25f;

		if ( Networking.IsHost )
			TryCommitOnHost( dir );
		else
			RequestCommitSpeedBlitzOnHost( dir );

		// Lock movement immediately so there's no walk window before the phase syncs back.
		SuppressOwnerController();
	}

	/// <summary> Owner preview: horizontal aim from eye forward, dash tuning from ult properties. </summary>
	public void GetAimPreviewParams(
		out Vector3 origin,
		out Vector3 direction,
		out float dashRange,
		out float hitHalfWidth )
	{
		origin = GameObject.WorldPosition;
		direction = GetHorizontalCommitDirection();
		dashRange = DashRange;
		hitHalfWidth = HitHalfWidth;
	}

	/// <summary> Owner: disable the controller's own input handling so only the ult drives movement. </summary>
	private void SuppressOwnerController()
	{
		playerController ??= Components.Get<PlayerController>();
		if ( !playerController.IsValid() )
			return;

		if ( !ownerControllerInputSuppressed )
		{
			ownerSavedUseInputControls = playerController.UseInputControls;
			ownerControllerInputSuppressed = true;
		}

		playerController.UseInputControls = false;
	}

	/// <summary> Owner: hand input back to the controller once the ult is over. </summary>
	private void RestoreOwnerController()
	{
		if ( !ownerControllerInputSuppressed )
			return;

		playerController ??= Components.Get<PlayerController>();
		if ( playerController.IsValid() )
			playerController.UseInputControls = ownerSavedUseInputControls;

		ownerControllerInputSuppressed = false;
	}

	private void ApplyOwnerLookLock()
	{
		if ( !ownerLookLocked )
			return;

		playerController ??= Components.Get<PlayerController>();
		if ( !playerController.IsValid() )
			return;

		Input.AnalogLook = default;
		playerController.EyeAngles = ownerLockedEyeAngles;
	}

	/// <summary> Owner wind-up: plant in place (zero horizontal velocity, keep gravity). </summary>
	private void OwnerFreezeMovement()
	{
		OwnerZeroHorizontalVelocity();
	}

	private void OwnerZeroHorizontalVelocity()
	{
		playerController ??= Components.Get<PlayerController>();
		playerBody ??= Components.Get<Rigidbody>();

		if ( playerController.IsValid() )
			playerController.WishVelocity = Vector3.Zero;

		// Zero horizontal velocity, keep vertical so gravity / ground-stick still works.
		if ( playerBody.IsValid() )
			playerBody.Velocity = new Vector3( 0f, 0f, playerBody.Velocity.z );
	}

	/// <summary>
	/// Owner dash: drive horizontal velocity through the controller so its move mode resolves
	/// wall slide, step-up and ground stick, and the locomotion keeps the legs running.
	/// </summary>
	private void OwnerDriveDashMovement()
	{
		if ( ownerDashMovementBlocked )
		{
			OwnerZeroHorizontalVelocity();
			return;
		}

		var dir = NetCommittedDirection.WithZ( 0 );
		if ( dir.Length < 0.001f )
			return;

		dir = dir.Normal;
		var horizontal = dir * DashSpeed;

		playerController ??= Components.Get<PlayerController>();
		playerBody ??= Components.Get<Rigidbody>();

		// WishVelocity drives the run animation and prevents the controller's brake friction.
		if ( playerController.IsValid() )
			playerController.WishVelocity = horizontal;

		// Set the actual velocity (preserve vertical so gravity / stick-to-ground still works).
		if ( playerBody.IsValid() )
			playerBody.Velocity = horizontal.WithZ( playerBody.Velocity.z );
	}

	private void ReportDashSamplePositionToHost()
	{
		if ( Networking.IsHost )
			return;

		ReportDashSamplePositionOnHostRpc( GameObject.WorldPosition );
	}

	/// <summary>
	/// Client owner only: local corridor sweep during dash — stop + attacker feel on first overlap.
	/// Host-as-owner already gets instant host hit detection; false-positive v1 stays stopped until host ends.
	/// </summary>
	private void OwnerPredictDashHitCheck()
	{
		if ( Networking.IsHost || ownerPredictedHitThisDash || ownerDashMovementBlocked )
			return;

		var curr = GameObject.WorldPosition;

		if ( !ownerHasLocalDashCheckPos )
		{
			ownerLastLocalDashCheckPos = curr;
			ownerHasLocalDashCheckPos = true;
			return;
		}

		if ( !TryFindBestDashHitInSegment( ownerLastLocalDashCheckPos, curr, out var victim ) )
		{
			ownerLastLocalDashCheckPos = curr;
			return;
		}

		ownerLastLocalDashCheckPos = curr;
		OwnerApplyPredictedDashHit( victim );
	}

	private void OwnerApplyPredictedDashHit( PlayerTackle victim )
	{
		ownerPredictedHitThisDash = true;
		ownerDashMovementBlocked = true;
		OwnerZeroHorizontalVelocity();

		Components.GetOrCreate<CombatFeelPredictDedupe>().MarkOwnerPredictedAttackerFeel();
		tackleImpactFeel ??= Components.Get<TackleImpactFeel>();
		tackleImpactFeel?.TriggerAsAttacker();

		if ( EnableSpeedBlitzDebugLogs )
			Log.Info( $"[SpeedBlitz] {GameObject.Name}: owner predict hit {victim.GameObject.Name}" );
	}

	private void ResetOwnerDashPredictState()
	{
		ownerHasLocalDashCheckPos = false;
		ownerPredictedHitThisDash = false;
	}

	// ---------------------------------------------------------------------
	// Shared checks
	// ---------------------------------------------------------------------

	private bool PassesCommitPrecheck()
	{
		if ( IsActive )
			return false;

		if ( !IsSpeedsterClass() )
			return false;

		if ( ultCharge is null || !ultCharge.IsFullyCharged )
			return false;

		if ( ballGrab?.IsHolding == true )
			return false;

		if ( playerTackle is { IsKnockedDown: true } )
			return false;

		return true;
	}

	private bool AllowsUltActivation()
	{
		if ( playerTeam is null )
			return true;

		var phase = playerTeam.SyncedMatchPhase;
		if ( phase == MatchPhase.Playing )
			return true;

		return phase == MatchPhase.MatchOver && playerTeam.NetPhaseTimeRemaining > 0f;
	}

	private bool IsSpeedsterClass()
	{
		return string.Equals( playerClass?.CurrentClass?.ClassName, SpeedsterClassName, StringComparison.Ordinal );
	}

	private Vector3 GetHorizontalCommitDirection()
	{
		playerController ??= Components.Get<PlayerController>();
		if ( !playerController.IsValid() )
			return default;

		var fwd = playerController.EyeAngles.ToRotation().Forward.WithZ( 0f );
		return fwd.Length >= 0.001f ? fwd.Normal : default;
	}

	/// <summary> Keep pitch/roll from release; snap yaw so horizontal forward matches the committed dash direction. </summary>
	private static Angles LockEyeAnglesToHorizontalDirection( Angles eyeAngles, Vector3 horizontalDir )
	{
		var flat = horizontalDir.WithZ( 0f );
		if ( flat.Length < 0.001f )
			return eyeAngles;

		var yaw = MathF.Atan2( flat.y, flat.x ) * (180f / MathF.PI);
		return new Angles( eyeAngles.pitch, yaw, eyeAngles.roll );
	}

	private float GetDashTargetBodyRadius( PlayerTackle candidate )
	{
		if ( !candidate.IsValid() )
			return DefaultTargetBodyRadius.Clamp( 1f, 64f );

		var controller = candidate.Components.Get<PlayerController>();
		if ( controller.IsValid() && controller.BodyRadius > 0f )
			return controller.BodyRadius;

		var classData = candidate.Components.Get<PlayerClass>()?.CurrentClass;
		if ( classData is not null && classData.CapsuleRadius > 0f )
			return classData.CapsuleRadius;

		return DefaultTargetBodyRadius.Clamp( 1f, 64f );
	}

	private bool IsValidDashTarget( PlayerTackle candidate )
	{
		if ( !candidate.IsValid() || candidate == playerTackle || candidate.GameObject == GameObject )
			return false;

		if ( candidate.IsTackleImmune || candidate.IsKnockedDown )
			return false;

		if ( candidate.Components.Get<PlayerDodge>() is { IsImmuneToTackle: true } )
			return false;

		if ( candidate.GameObject.Tags.Has( CitizenAvatarLod.PracticeNpcTag ) )
			return true;

		var victimTeam = candidate.Components.Get<PlayerTeam>();
		if ( playerTeam is null || !playerTeam.IsValid() || victimTeam is null || !victimTeam.IsValid() )
			return false;

		if ( !MatchTeamIds.IsValid( playerTeam.TeamId ) || !MatchTeamIds.IsValid( victimTeam.TeamId ) )
			return false;

		return playerTeam.TeamId != victimTeam.TeamId;
	}

	private void LogReject( string reason )
	{
		if ( EnableSpeedBlitzDebugLogs )
			Log.Info( $"[SpeedBlitz] {GameObject.Name}: commit rejected ({reason})" );
	}
}
