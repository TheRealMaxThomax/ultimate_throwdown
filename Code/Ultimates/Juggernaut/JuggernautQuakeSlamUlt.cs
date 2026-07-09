using Sandbox;
using System;
using System.Collections.Generic;
using static QuakeSlamRadiusMath;

/// <summary>
/// Juggernaut <b>Quake Slam</b> ult — hold/release <c>Ultimate</c>, wind-up, then three expanding ring knockdowns.
/// Host owns phase timing, ring hits, and knockdowns; owner gets aim preview + planted wind-up.
/// See GAMEPLAY_DESIGN.md → Quake Slam.
/// </summary>
[Order( 10011 )]
public sealed class JuggernautQuakeSlamUlt : Component, IPlayerUlt
{
	public enum QuakeSlamPhase : byte
	{
		None = 0,
		WindUp = 1,
		Rings = 2,
	}

	private const string JuggernautClassName = "Juggernaut";

	[Property, Group( "Charge" )] public float MaxChargePoints { get; set; } = 100f;
	[Property] public string UltimateAction { get; set; } = "Ultimate";

	[Property, Group( "Wind-up" )] public float WindUpDurationSeconds { get; set; } = 1.5f;

	[Property, Group( "Rings" )] public float InnerRadius { get; set; } = 70f;
	[Property, Group( "Rings" )] public float MidRadius { get; set; } = 135f;
	[Property, Group( "Rings" )] public float OuterRadius { get; set; } = 200f;
	[Property, Group( "Rings" )] public float RingPhaseDelaySeconds { get; set; } = 0.5f;
	[Property, Group( "Rings" )] public float MaxHitVerticalSeparation { get; set; } = 56f;
	[Property, Group( "Rings" )] public float MaxGroundCheckDistance { get; set; } = 12f;

	[Property, Group( "Knockdown" )] public float KnockdownLaunchSpeed { get; set; } = 620f;
	[Property, Group( "Knockdown" )] public float KnockdownLaunchArc { get; set; } = 1.35f;
	[Property, Group( "Knockdown" )] public float OutwardKnockFraction { get; set; } = 0.22f;
	[Property, Group( "Knockdown" )] public float InnerRingPowerMultiplier { get; set; } = 1f;
	[Property, Group( "Knockdown" )] public float MidRingPowerMultiplier { get; set; } = 0.72f;
	[Property, Group( "Knockdown" )] public float OuterRingPowerMultiplier { get; set; } = 0.48f;

	[Property, Group( "Knockdown feel" )] public float ImpactHitstopDurationSeconds { get; set; } = 0.12f;
	[Property, Group( "Knockdown feel" )] public float ImpactShakeDurationSeconds { get; set; } = 0.18f;
	[Property, Group( "Knockdown feel" )] public float ImpactShakePositionAmplitude { get; set; } = 9f;
	[Property, Group( "Knockdown feel" )] public float ImpactShakeRotationAmplitudeDegrees { get; set; } = 1.4f;
	[Property, Group( "Knockdown feel" )] public float ImpactAttackerFovPunchDegrees { get; set; } = -5f;
	[Property, Group( "Knockdown feel" )] public float ImpactAttackerCameraOffsetPunchX { get; set; } = 22f;
	[Property, Group( "Knockdown feel" )] public float ImpactAttackerCameraOffsetPunchZ { get; set; } = 4f;
	[Property, Group( "Knockdown feel" )] public float ImpactAttackerPunchDurationSeconds { get; set; } = 0.12f;

	[Property] public bool EnableQuakeSlamDebugLogs { get; set; }

	[Sync( SyncFlags.FromHost )] private QuakeSlamPhase NetPhase { get; set; }
	[Sync( SyncFlags.FromHost )] private Vector3 NetSlamOrigin { get; set; }
	[Sync( SyncFlags.FromHost )] private float NetWindUpEndsAt { get; set; }
	[Sync( SyncFlags.FromHost )] private byte NetRingPulseIndex { get; set; }

	private float hostWindUpEndsAt;
	private float hostSlamAt;
	private bool hostFiredInnerRing;
	private bool hostFiredMidRing;
	private bool hostFiredOuterRing;
	private readonly HashSet<Guid> hostHitVictimIds = new();

	private float nextCommitRequestAt;
	private float ownerCommitPendingUntil;
	private bool ownerControllerInputSuppressed;
	private bool ownerSavedUseInputControls = true;
	private bool ownerBlockedAimUntilUltimateRelease;

	private PlayerUltCharge ultCharge;
	private PlayerClass playerClass;
	private PlayerTeam playerTeam;
	private PlayerTackle playerTackle;
	private PlayerController playerController;
	private BallGrab ballGrab;
	private Rigidbody playerBody;
	private QuakeSlamOwnerPredict ownerPredict;

	public bool IsActive => NetPhase != QuakeSlamPhase.None;
	public bool IsWindUp => NetPhase == QuakeSlamPhase.WindUp;
	public bool IsRingPhase => NetPhase == QuakeSlamPhase.Rings;
	public QuakeSlamPhase SyncedPhase => NetPhase;
	public byte SyncedRingPulseIndex => NetRingPulseIndex;
	public bool BlocksBallPickup => IsActive;

	/// <summary> Owner-only: holding <see cref="UltimateAction"/> at full charge before commit. </summary>
	public bool IsAiming { get; private set; }

	public float GetWindUpLerp()
	{
		if ( !IsWindUp || NetWindUpEndsAt <= 0f )
			return 0f;

		var duration = WindUpDurationSeconds.Clamp( 0.05f, 30f );
		var remaining = MathX.Clamp( NetWindUpEndsAt - Time.Now, 0f, duration );
		var tLinear = 1f - (remaining / duration);
		return tLinear * tLinear * (3f - 2f * tLinear);
	}

	public TackleImpactFeelOverrides GetSlamImpactFeelOverrides() => new()
	{
		HitstopDurationSeconds = ImpactHitstopDurationSeconds,
		ShakeDurationSeconds = ImpactShakeDurationSeconds,
		ShakePositionAmplitude = ImpactShakePositionAmplitude,
		ShakeRotationAmplitudeDegrees = ImpactShakeRotationAmplitudeDegrees,
		AttackerFovPunchDegrees = ImpactAttackerFovPunchDegrees,
		AttackerCameraOffsetPunchX = ImpactAttackerCameraOffsetPunchX,
		AttackerCameraOffsetPunchZ = ImpactAttackerCameraOffsetPunchZ,
		AttackerPunchDurationSeconds = ImpactAttackerPunchDurationSeconds,
	};

	public Vector3 GetSlamOriginWorld() => NetSlamOrigin;

	public void GetAimPreviewParams( out Vector3 origin, out float innerRadius, out float midRadius, out float outerRadius )
	{
		origin = GameObject.WorldPosition;
		innerRadius = InnerRadius;
		midRadius = MidRadius;
		outerRadius = OuterRadius;
	}

	protected override void OnStart()
	{
		ultCharge = Components.Get<PlayerUltCharge>();
		playerClass = Components.Get<PlayerClass>();
		playerTeam = Components.Get<PlayerTeam>();
		playerTackle = Components.Get<PlayerTackle>();
		playerController = Components.Get<PlayerController>();
		ballGrab = Components.Get<BallGrab>();
		playerBody = Components.Get<Rigidbody>();
		ownerPredict = ComponentRequire.On<QuakeSlamOwnerPredict>( this, "JuggernautQuakeSlamUlt" );
		ComponentRequire.WarnIfMissing<QuakeSlamAimPreview>( this, "JuggernautQuakeSlamUlt" );
		ComponentRequire.WarnIfMissing<QuakeSlamFeel>( this, "JuggernautQuakeSlamUlt" );
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
		if ( IsWindUp )
			ApplyPlantedHorizontalFreeze();
	}

	protected override void OnDisabled()
	{
		if ( Network.IsOwner )
			RestoreOwnerController();
	}

	private void HostUpdate()
	{
		switch ( NetPhase )
		{
			case QuakeSlamPhase.WindUp:
				if ( playerTackle.IsValid() && playerTackle.IsKnockedDown )
				{
					EndSlamOnHost( "windup_interrupted" );
					return;
				}

				if ( Time.Now >= hostWindUpEndsAt )
					BeginSlamOnHost();
				return;

			case QuakeSlamPhase.Rings:
				HostUpdateRingSchedule();
				return;
		}
	}

	private void HostUpdateRingSchedule()
	{
		var delay = RingPhaseDelaySeconds.Clamp( 0.05f, 3f );
		var elapsed = Time.Now - hostSlamAt;

		if ( !hostFiredInnerRing && elapsed >= 0f )
			FireRingOnHost( QuakeSlamRing.Inner );

		if ( !hostFiredMidRing && elapsed >= delay )
			FireRingOnHost( QuakeSlamRing.Mid );

		if ( !hostFiredOuterRing && elapsed >= delay * 2f )
		{
			FireRingOnHost( QuakeSlamRing.Outer );
			EndSlamOnHost( "rings_complete" );
		}
	}

	private void TryCommitOnHost()
	{
		if ( !Networking.IsHost )
			return;

		if ( !PassesCommitPrecheck() || !AllowsUltActivation() )
		{
			LogReject( "host_precheck" );
			return;
		}

		if ( ultCharge is null || !ultCharge.TrySpendFullChargeOnHost() )
		{
			LogReject( "no_charge" );
			return;
		}

		ultCharge.SetHostChargeGainBlocked( true );
		hostWindUpEndsAt = Time.Now + WindUpDurationSeconds.Clamp( 0.05f, 30f );
		NetWindUpEndsAt = hostWindUpEndsAt;
		NetPhase = QuakeSlamPhase.WindUp;
		ResetHostRingState();

		if ( EnableQuakeSlamDebugLogs )
			Log.Info( $"[QuakeSlam] {GameObject.Name}: commit windUpEnds={hostWindUpEndsAt:F2}" );
	}

	[Rpc.Host]
	private void RequestCommitQuakeSlamOnHost()
	{
		if ( Network.Owner is null || Rpc.Caller.SteamId != Network.Owner.SteamId )
			return;

		TryCommitOnHost();
	}

	private void BeginSlamOnHost()
	{
		if ( !Networking.IsHost )
			return;

		NetSlamOrigin = GameObject.WorldPosition;
		hostSlamAt = Time.Now;
		NetPhase = QuakeSlamPhase.Rings;
		NetRingPulseIndex = 0;
		FireRingOnHost( QuakeSlamRing.Inner );

		if ( EnableQuakeSlamDebugLogs )
			Log.Info( $"[QuakeSlam] {GameObject.Name}: slam at {NetSlamOrigin}" );
	}

	private void FireRingOnHost( QuakeSlamRing ring )
	{
		if ( !Networking.IsHost )
			return;

		switch ( ring )
		{
			case QuakeSlamRing.Inner:
				if ( hostFiredInnerRing )
					return;
				hostFiredInnerRing = true;
				NetRingPulseIndex = 1;
				break;
			case QuakeSlamRing.Mid:
				if ( hostFiredMidRing )
					return;
				hostFiredMidRing = true;
				NetRingPulseIndex = 2;
				break;
			case QuakeSlamRing.Outer:
				if ( hostFiredOuterRing )
					return;
				hostFiredOuterRing = true;
				NetRingPulseIndex = 3;
				break;
		}

		var power = GetRingPowerMultiplier( ring, InnerRingPowerMultiplier, MidRingPowerMultiplier, OuterRingPowerMultiplier );
		var launchSpeed = KnockdownLaunchSpeed * power.Clamp( 0.05f, 3f );

		foreach ( var victim in Scene.GetAllComponents<PlayerTackle>() )
		{
			if ( !IsValidRingTarget( victim, ring ) )
				continue;

			if ( hostHitVictimIds.Contains( victim.GameObject.Id ) )
				continue;

			hostHitVictimIds.Add( victim.GameObject.Id );
			ApplyRingKnockdownOnHost( victim, launchSpeed );
		}

		if ( EnableQuakeSlamDebugLogs )
			Log.Info( $"[QuakeSlam] {GameObject.Name}: ring {ring} fired" );
	}

	private bool IsValidRingTarget( PlayerTackle victim, QuakeSlamRing ring )
	{
		if ( !victim.IsValid() || victim == playerTackle )
			return false;

		if ( victim.IsKnockedDown || victim.IsTackleImmune )
			return false;

		if ( victim.Components.Get<PlayerDodge>() is { IsImmuneToTackle: true } )
			return false;

		if ( !IsEnemyVictim( victim ) )
			return false;

		return IsInRing(
			NetSlamOrigin,
			victim.WorldPosition,
			ring,
			InnerRadius,
			MidRadius,
			OuterRadius,
			MaxHitVerticalSeparation );
	}

	private void ApplyRingKnockdownOnHost( PlayerTackle victim, float launchSpeed )
	{
		var launchDir = BuildLaunchDirection( NetSlamOrigin, victim.WorldPosition, OutwardKnockFraction );
		victim.ApplyKnockdownFromHost(
			launchDir,
			launchSpeed,
			KnockdownLaunchArc,
			playerTackle );
	}

	private void EndSlamOnHost( string reason )
	{
		if ( !Networking.IsHost || NetPhase == QuakeSlamPhase.None )
			return;

		if ( EnableQuakeSlamDebugLogs )
			Log.Info( $"[QuakeSlam] {GameObject.Name}: end ({reason})" );

		NetPhase = QuakeSlamPhase.None;
		NetWindUpEndsAt = 0f;
		NetRingPulseIndex = 0;
		hostWindUpEndsAt = 0f;
		hostSlamAt = 0f;
		ResetHostRingState();
		ultCharge?.SetHostChargeGainBlocked( false );
	}

	private void ResetHostRingState()
	{
		hostFiredInnerRing = false;
		hostFiredMidRing = false;
		hostFiredOuterRing = false;
		hostHitVictimIds.Clear();
	}

	public void CancelQuakeSlamOnHost()
	{
		EndSlamOnHost( "cancelled" );
	}

	public static void CancelAllInScene( Scene scene )
	{
		if ( !Networking.IsHost || scene is null )
			return;

		foreach ( var ult in scene.GetAllComponents<JuggernautQuakeSlamUlt>() )
		{
			if ( ult.IsValid() )
				ult.CancelQuakeSlamOnHost();
		}
	}

	private void OwnerUpdate()
	{
		if ( !IsActive )
		{
			ownerPredict?.ResetPredictState();
			RestoreOwnerController();
			OwnerUpdateAimAndCommit();
			return;
		}

		var suppress = IsWindUp || Time.Now < ownerCommitPendingUntil;
		if ( !suppress )
		{
			RestoreOwnerController();
			return;
		}

		IsAiming = false;
		SuppressOwnerController();
		Input.AnalogMove = Vector3.Zero;
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
			return;

		ownerCommitPendingUntil = Time.Now + 0.5f;
		nextCommitRequestAt = Time.Now + 0.25f;

		if ( Networking.IsHost )
			TryCommitOnHost();
		else
			RequestCommitQuakeSlamOnHost();

		SuppressOwnerController();
	}

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

	private void RestoreOwnerController()
	{
		if ( !ownerControllerInputSuppressed )
			return;

		playerController ??= Components.Get<PlayerController>();
		if ( playerController.IsValid() )
			playerController.UseInputControls = ownerSavedUseInputControls;

		ownerControllerInputSuppressed = false;
	}

	private void ApplyPlantedHorizontalFreeze()
	{
		playerController ??= Components.Get<PlayerController>();
		playerBody ??= Components.Get<Rigidbody>();

		if ( playerController.IsValid() )
			playerController.WishVelocity = Vector3.Zero;

		if ( playerBody.IsValid() )
			playerBody.Velocity = new Vector3( 0f, 0f, playerBody.Velocity.z );
	}

	private bool PassesCommitPrecheck()
	{
		if ( IsActive )
			return false;

		if ( !IsJuggernautClass() )
			return false;

		if ( ultCharge is null || !ultCharge.IsFullyCharged )
			return false;

		if ( ballGrab?.IsHolding == true )
			return false;

		if ( playerTackle is { IsKnockedDown: true } )
			return false;

		if ( !IsGrounded( Scene, GameObject, GameObject.WorldPosition, MaxGroundCheckDistance ) )
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

	private bool IsJuggernautClass()
	{
		return string.Equals( playerClass?.CurrentClass?.ClassName, JuggernautClassName, StringComparison.Ordinal );
	}

	private bool IsEnemyVictim( PlayerTackle victim )
	{
		if ( victim.GameObject.Tags.Has( CitizenAvatarLod.PracticeNpcTag ) )
			return true;

		var attackerTeam = playerTeam ?? Components.Get<PlayerTeam>();
		var victimTeam = victim.Components.Get<PlayerTeam>();
		if ( !attackerTeam.IsValid() || !victimTeam.IsValid() )
			return false;

		if ( !MatchTeamIds.IsValid( attackerTeam.TeamId ) || !MatchTeamIds.IsValid( victimTeam.TeamId ) )
			return false;

		return attackerTeam.TeamId != victimTeam.TeamId;
	}

	private void LogReject( string reason )
	{
		if ( !EnableQuakeSlamDebugLogs )
			return;

		Log.Info( $"[QuakeSlam] {GameObject.Name}: reject {reason}" );
	}
}
