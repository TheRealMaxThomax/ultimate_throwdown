using System;
using Sandbox;

/// <summary>
/// Owner-only tackle connect juice: brief camera hitstop, optional screen shake, attacker FOV/offset punch.
/// Host triggers via <see cref="PlayerTackle"/> owner RPCs when a tackle or knockdown lands.
/// Does not slow simulation — freezes the local view only.
/// </summary>
[Order( 10050 )]
public sealed class TackleImpactFeel : Component
{
	[Property] public bool EnableHitstop { get; set; } = true;
	[Property] public float HitstopDurationSeconds { get; set; } = 0.055f;

	[Property] public bool ShakeForAttacker { get; set; } = true;
	[Property] public bool ShakeForVictim { get; set; } = true;
	[Property] public float ShakeDurationSeconds { get; set; } = 0.14f;
	[Property] public float ShakePositionAmplitude { get; set; } = 7f;
	[Property] public float ShakeRotationAmplitudeDegrees { get; set; } = 1.2f;

	[Property] public float AttackerFovPunchDegrees { get; set; } = -4f;
	[Property] public float AttackerCameraOffsetPunchX { get; set; } = 22f;
	[Property] public float AttackerCameraOffsetPunchZ { get; set; } = 4f;
	[Property] public float AttackerPunchDurationSeconds { get; set; } = 0.12f;

	private enum ImpactRole
	{
		None,
		Attacker,
		Victim
	}

	private PlayerController playerController;
	private PlayerTackle playerTackle;
	private CameraComponent activeCamera;

	private ImpactRole activeRole = ImpactRole.None;
	private bool impactUseHitstop = true;
	private bool impactIsHazard;
	private float impactStartTime = -1f;

	private Vector3 frozenCameraPosition;
	private Rotation frozenCameraRotation;
	private Vector3 frozenCameraOffset;
	private float frozenFieldOfView = 60f;

	private Vector3 punchBaselineOffset;
	private float punchBaselineFieldOfView = 60f;
	private bool punchBaselineCaptured;

	public bool IsHitstopActive { get; private set; }
	public bool IsShakeActive { get; private set; }
	public bool IsImpactFeelActive => activeRole != ImpactRole.None && impactStartTime >= 0f && Time.Now < GetImpactEndTime();
	/// <summary>True while a traffic/hazard knockdown impact is active (car-specific camera path).</summary>
	public bool IsHazardImpact => activeRole != ImpactRole.None && impactIsHazard;

	float EffectiveHitstopDuration => EnableHitstop && impactUseHitstop ? HitstopDurationSeconds : 0f;

	protected override void OnStart()
	{
		playerController = Components.Get<PlayerController>();
		playerTackle = Components.Get<PlayerTackle>();
		TryFindActiveCamera();
	}

	/// <summary> Owning client: landed a player tackle. </summary>
	public void TriggerAsAttacker()
	{
		BeginImpact( ImpactRole.Attacker, useHitstop: true, isHazard: false );
	}

	/// <summary> Owning client: got tackled by another player. </summary>
	public void TriggerAsVictim()
	{
		BeginImpact( ImpactRole.Victim, useHitstop: true, isHazard: false );
	}

	/// <summary> Owning client: hazard knockdown (traffic, etc.) — shake only, no camera hitstop. </summary>
	public void TriggerAsHazardVictim()
	{
		BeginImpact( ImpactRole.Victim, useHitstop: false, isHazard: true );
	}

	protected override void OnUpdate()
	{
		if ( !Network.IsOwner || activeRole == ImpactRole.None )
			return;

		if ( !TryEnsureReady() )
			return;

		var elapsed = Time.Now - impactStartTime;
		var hitstopDuration = EffectiveHitstopDuration;
		IsHitstopActive = hitstopDuration > 0f && elapsed < hitstopDuration;

		if ( IsHitstopActive )
		{
			ApplyFrozenCameraState();
			IsShakeActive = false;
			return;
		}

		playerTackle ??= Components.Get<PlayerTackle>();
		if ( !impactIsHazard && activeRole == ImpactRole.Victim && playerTackle.IsValid() && playerTackle.IsRagdolled )
		{
			EndImpact( restoreCamera: true );
			return;
		}

		IsShakeActive = ShouldShakeForRole( activeRole ) && elapsed < hitstopDuration + ShakeDurationSeconds;

		if ( activeRole == ImpactRole.Attacker )
			ApplyAttackerPunch( elapsed - hitstopDuration );

		if ( Time.Now >= GetImpactEndTime() )
			EndImpact( restoreCamera: true );
	}

	protected override void OnPreRender()
	{
		if ( !Network.IsOwner || activeRole == ImpactRole.None || !activeCamera.IsValid() )
			return;

		if ( IsHitstopActive )
		{
			activeCamera.WorldPosition = frozenCameraPosition;
			activeCamera.WorldRotation = frozenCameraRotation;
			return;
		}

		if ( !IsShakeActive )
			return;

		var afterHitstop = Time.Now - impactStartTime - EffectiveHitstopDuration;
		var duration = ShakeDurationSeconds <= 0.0001f ? 0.0001f : ShakeDurationSeconds;
		var t = MathX.Clamp( afterHitstop / duration, 0f, 1f );
		var falloff = 1f - t;
		var amp = ShakePositionAmplitude * falloff;
		var rotAmp = ShakeRotationAmplitudeDegrees * falloff;
		var wobble = afterHitstop;

		var pitch = MathF.Sin( wobble * 61f ) * rotAmp;
		var yaw = MathF.Cos( wobble * 57f ) * rotAmp * 0.65f;
		var wobbleRot = Rotation.From( pitch, yaw, 0f );

		if ( activeRole == ImpactRole.Attacker )
		{
			var right = activeCamera.WorldRotation.Right;
			var up = activeCamera.WorldRotation.Up;
			var forward = activeCamera.WorldRotation.Forward;
			var offset =
				right * (MathF.Sin( wobble * 47f ) * amp)
				+ up * (MathF.Cos( wobble * 53f ) * amp * 0.55f)
				+ forward * (MathF.Sin( wobble * 39f ) * amp * 0.25f );

			activeCamera.WorldPosition += offset;
			activeCamera.WorldRotation *= wobbleRot;
			return;
		}

		TryGetShakeBaseline( out var basePosition, out var baseRotation );

		var shakeRight = baseRotation.Right;
		var shakeUp = baseRotation.Up;
		var shakeForward = baseRotation.Forward;
		var victimOffset =
			shakeRight * (MathF.Sin( wobble * 47f ) * amp)
			+ shakeUp * (MathF.Cos( wobble * 53f ) * amp * 0.55f)
			+ shakeForward * (MathF.Sin( wobble * 39f ) * amp * 0.25f );

		activeCamera.WorldPosition = basePosition + victimOffset;
		activeCamera.WorldRotation = baseRotation * wobbleRot;
	}

	void BeginImpact( ImpactRole role, bool useHitstop, bool isHazard )
	{
		if ( !Network.IsOwner || role == ImpactRole.None )
			return;

		if ( !TryEnsureReady() )
			return;

		impactUseHitstop = useHitstop;
		impactIsHazard = isHazard;
		CaptureFrozenCameraState();
		punchBaselineCaptured = false;

		activeRole = role;
		impactStartTime = Time.Now;
		IsHitstopActive = EffectiveHitstopDuration > 0f;
		IsShakeActive = false;
	}

	void EndImpact( bool restoreCamera = false )
	{
		playerTackle ??= Components.Get<PlayerTackle>();
		var skipRestore = restoreCamera
			&& impactIsHazard
			&& activeRole == ImpactRole.Victim
			&& playerTackle.IsValid()
			&& playerTackle.IsRagdolled;

		if ( restoreCamera && !skipRestore )
			RestoreCapturedCameraState();

		activeRole = ImpactRole.None;
		impactUseHitstop = true;
		impactIsHazard = false;
		impactStartTime = -1f;
		IsHitstopActive = false;
		IsShakeActive = false;
		punchBaselineCaptured = false;
	}

	void RestoreCapturedCameraState()
	{
		if ( playerController.IsValid() )
			playerController.CameraOffset = frozenCameraOffset;

		if ( activeCamera.IsValid() )
			activeCamera.FieldOfView = frozenFieldOfView;
	}

	float GetImpactEndTime()
	{
		if ( impactStartTime < 0f )
			return 0f;

		var afterHitstopEnd = impactStartTime + EffectiveHitstopDuration;
		var shakeEnd = afterHitstopEnd + ShakeDurationSeconds;
		if ( activeRole == ImpactRole.Attacker )
			return MathF.Max( shakeEnd, afterHitstopEnd + AttackerPunchDurationSeconds );

		return shakeEnd;
	}

	bool ShouldShakeForRole( ImpactRole role )
	{
		return role switch
		{
			ImpactRole.Attacker => ShakeForAttacker,
			ImpactRole.Victim => ShakeForVictim,
			_ => false
		};
	}

	void TryGetShakeBaseline( out Vector3 basePosition, out Rotation baseRotation )
	{
		playerTackle ??= Components.Get<PlayerTackle>();
		if ( impactIsHazard && activeRole == ImpactRole.Victim && playerTackle.IsValid() && playerTackle.TryGetRagdollOrbitCamera( out basePosition, out baseRotation ) )
			return;

		basePosition = frozenCameraPosition;
		baseRotation = frozenCameraRotation;
	}

	void CaptureFrozenCameraState()
	{
		TryFindActiveCamera();

		if ( activeCamera.IsValid() )
		{
			frozenCameraPosition = activeCamera.WorldPosition;
			frozenCameraRotation = activeCamera.WorldRotation;
			frozenFieldOfView = activeCamera.FieldOfView;
		}

		if ( playerController.IsValid() )
			frozenCameraOffset = playerController.CameraOffset;
	}

	void ApplyFrozenCameraState()
	{
		if ( playerController.IsValid() )
			playerController.CameraOffset = frozenCameraOffset;

		if ( activeCamera.IsValid() )
			activeCamera.FieldOfView = frozenFieldOfView;
	}

	void ApplyAttackerPunch( float afterHitstopSeconds )
	{
		if ( afterHitstopSeconds < 0f || AttackerPunchDurationSeconds <= 0.0001f )
			return;

		if ( !punchBaselineCaptured )
		{
			punchBaselineOffset = playerController.CameraOffset;
			punchBaselineFieldOfView = activeCamera.IsValid() ? activeCamera.FieldOfView : frozenFieldOfView;
			punchBaselineCaptured = true;
		}

		var duration = AttackerPunchDurationSeconds;
		var tLinear = MathX.Clamp( afterHitstopSeconds / duration, 0f, 1f );
		var t = tLinear * tLinear * (3f - 2f * tLinear );

		var punchOffset = new Vector3( AttackerCameraOffsetPunchX, 0f, AttackerCameraOffsetPunchZ );
		var targetOffset = punchBaselineOffset + punchOffset;
		playerController.CameraOffset = Vector3.Lerp( targetOffset, punchBaselineOffset, t );

		if ( activeCamera.IsValid() )
		{
			var punchFov = punchBaselineFieldOfView + AttackerFovPunchDegrees;
			activeCamera.FieldOfView = MathX.Lerp( punchFov, punchBaselineFieldOfView, t );
		}
	}

	bool TryEnsureReady()
	{
		playerController ??= Components.Get<PlayerController>();
		playerTackle ??= Components.Get<PlayerTackle>();
		TryFindActiveCamera();
		return playerController.IsValid();
	}

	void TryFindActiveCamera()
	{
		if ( activeCamera.IsValid() )
			return;

		foreach ( var cam in Scene.GetAllComponents<CameraComponent>() )
		{
			if ( !cam.IsMainCamera )
				continue;

			activeCamera = cam;
			break;
		}
	}
}
