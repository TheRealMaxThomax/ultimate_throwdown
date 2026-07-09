using Sandbox;

/// <summary>
/// Owner-only third-person pullback + mild FOV widen while <see cref="BallThrow.IsChargingThrow"/>.
/// FOV is applied in <see cref="PlayerController.IEvents.PostCameraSetup"/> because PlayerController
/// resets <see cref="CameraComponent.FieldOfView"/> from preferences every frame after <c>OnUpdate</c>.
/// Runs at <c>[Order(10002)]</c> (right after <see cref="BallThrow"/>) so release blend starts before
/// <see cref="PlayerController"/> camera setup. FOV still applied in <see cref="PlayerController.IEvents.PostCameraSetup"/>.
/// Offset is not touched when idle (same as <see cref="SpeedBlitzDashCamera"/> when ult idle).
/// </summary>
[Order( 10002 )]
public sealed class ThrowChargeCamera : Component, PlayerController.IEvents
{
	[Property] public float ExtraCameraDistanceAtFullCharge { get; set; } = 50f;
	[Property] public float ExtraCameraHeightAtFullCharge { get; set; } = 50f;
	[Property] public float ExtraFieldOfViewAtFullCharge { get; set; } = 7f;
	[Property] public float ReleaseCameraBlendDuration { get; set; } = 0.35f;

	private BallThrow ballThrow;
	private PlayerTackle playerTackle;
	private TackleImpactFeel tackleImpactFeel;
	private SpeedsterSpeedBlitzUlt speedBlitzUlt;
	private JuggernautQuakeSlamUlt quakeSlamUlt;
	private PlayerController playerController;

	private Vector3 baselineCameraOffset;
	private float baselineFieldOfView = 60f;
	private bool baselineCaptured;

	private bool wasChargingThrow;
	private bool wasPendingThrowRelease;
	private float lastChargeCameraLerp;
	private bool wasKnockedDown;
	private float releaseBlendStartTime = -1f;
	private bool releaseBlendAwaitingPostCameraClear;
	private Vector3 releaseBlendFromOffset;
	private float releaseBlendFromFieldOfView;
	private float lastAppliedChargeFieldOfView;
	private bool hasLastAppliedChargeFieldOfView;
	private bool releaseBlendUseZeroT;

	protected override void OnStart()
	{
		ballThrow = Components.Get<BallThrow>();
		playerTackle = Components.Get<PlayerTackle>();
		tackleImpactFeel = Components.Get<TackleImpactFeel>();
		speedBlitzUlt = Components.Get<SpeedsterSpeedBlitzUlt>();
		playerController = Components.Get<PlayerController>();
		TryCaptureBaselineOffset();
	}

	protected override void OnUpdate()
	{
		if ( !Network.IsOwner )
			return;

		playerTackle ??= Components.Get<PlayerTackle>();
		var knockedDown = playerTackle.IsValid() && (playerTackle.IsKnockedDown || playerTackle.IsStandUpCameraBlending);
		if ( knockedDown != wasKnockedDown && TryEnsureReady() )
			RestoreBaselineCamera();
		wasKnockedDown = knockedDown;

		if ( !TryEnsureReady() )
			return;

		if ( releaseBlendAwaitingPostCameraClear )
		{
			releaseBlendStartTime = -1f;
			releaseBlendAwaitingPostCameraClear = false;
		}

		if ( ShouldLeaveCameraAlone() )
		{
			if ( playerTackle.IsValid() && (playerTackle.IsKnockedDown || playerTackle.IsStandUpCameraBlending) )
				RestoreBaselineCamera();
			else
			{
				wasChargingThrow = false;
				wasPendingThrowRelease = false;
				releaseBlendStartTime = -1f;
				releaseBlendAwaitingPostCameraClear = false;
				hasLastAppliedChargeFieldOfView = false;
			}

			return;
		}

		var charging = ballThrow.IsValid() && ballThrow.IsChargingThrow;
		var pendingRelease = ballThrow.IsValid() && ballThrow.IsPendingThrowRelease;

		// Start blend immediately when BallThrow (10001) just cleared charge/pending — before PC camera setup.
		if ( IsAwaitingReleaseBlendStart( charging, pendingRelease ) )
			BeginReleaseOffsetBlend();

		if ( charging )
		{
			if ( !wasChargingThrow )
				baselineCameraOffset = playerController.CameraOffset;

			releaseBlendStartTime = -1f;
			releaseBlendAwaitingPostCameraClear = false;
			lastChargeCameraLerp = ballThrow.GetThrowChargeLerp();
			ApplyChargeCameraOffset( lastChargeCameraLerp );
		}
		else if ( pendingRelease )
		{
			// Hold charge camera while throw wind-up runs (ThrowReleaseDelaySeconds) — blend starts after ball leaves.
			releaseBlendStartTime = -1f;
			releaseBlendAwaitingPostCameraClear = false;
			ApplyChargeCameraOffset( lastChargeCameraLerp );
		}
		else if ( releaseBlendStartTime >= 0f )
		{
			StepReleaseOffsetBlend();
		}
		// Idle: do not touch CameraOffset — PlayerController owns it (same as SpeedBlitzDashCamera when ult idle).

		wasChargingThrow = charging;
		wasPendingThrowRelease = pendingRelease;
	}

	void PlayerController.IEvents.PostCameraSetup( CameraComponent cam )
	{
		if ( !Network.IsOwner || !cam.IsValid() )
			return;

		ballThrow ??= Components.Get<BallThrow>();
		speedBlitzUlt ??= Components.Get<SpeedsterSpeedBlitzUlt>();

		if ( !TryEnsureReady() )
			return;

		if ( ShouldLeaveCameraAlone() )
			return;

		if ( speedBlitzUlt.IsValid() && speedBlitzUlt.IsActive )
			return;

		quakeSlamUlt ??= Components.Get<JuggernautQuakeSlamUlt>();
		if ( quakeSlamUlt.IsValid() && quakeSlamUlt.IsWindUp )
			return;

		if ( TryGetOverrideFieldOfView( out var fieldOfView ) )
		{
			cam.FieldOfView = fieldOfView;
			if ( ballThrow?.IsChargingThrow == true || ballThrow?.IsPendingThrowRelease == true )
			{
				lastAppliedChargeFieldOfView = fieldOfView;
				hasLastAppliedChargeFieldOfView = true;
			}
			else if ( releaseBlendStartTime >= 0f )
				releaseBlendUseZeroT = false;

			return;
		}

		if ( !(ballThrow?.IsChargingThrow == true)
			&& !(ballThrow?.IsPendingThrowRelease == true)
			&& releaseBlendStartTime < 0f
			&& !releaseBlendAwaitingPostCameraClear )
			baselineFieldOfView = cam.FieldOfView;
	}

	bool IsAwaitingReleaseBlendStart( bool charging, bool pendingRelease )
	{
		return releaseBlendStartTime < 0f && !charging && !pendingRelease && (wasChargingThrow || wasPendingThrowRelease);
	}

	bool TryGetOverrideFieldOfView( out float fieldOfView )
	{
		fieldOfView = baselineFieldOfView;
		ballThrow ??= Components.Get<BallThrow>();

		if ( ballThrow.IsValid() && ballThrow.IsChargingThrow )
		{
			var chargeLerp = ballThrow.GetThrowChargeLerp().Clamp( 0f, 1f );
			fieldOfView = baselineFieldOfView + (ExtraFieldOfViewAtFullCharge * chargeLerp);
			return true;
		}

		if ( ballThrow.IsValid() && ballThrow.IsPendingThrowRelease )
		{
			fieldOfView = GetHeldChargeFieldOfView();
			return true;
		}

		if ( releaseBlendStartTime >= 0f )
		{
			fieldOfView = MathX.Lerp( releaseBlendFromFieldOfView, baselineFieldOfView, GetReleaseBlendSmoothT() );
			return true;
		}

		var charging = ballThrow.IsValid() && ballThrow.IsChargingThrow;
		var pendingRelease = ballThrow.IsValid() && ballThrow.IsPendingThrowRelease;
		if ( IsAwaitingReleaseBlendStart( charging, pendingRelease ) )
		{
			fieldOfView = GetHeldChargeFieldOfView();
			return true;
		}

		return false;
	}

	float GetHeldChargeFieldOfView()
	{
		if ( hasLastAppliedChargeFieldOfView )
			return lastAppliedChargeFieldOfView;

		return baselineFieldOfView + (ExtraFieldOfViewAtFullCharge * lastChargeCameraLerp.Clamp( 0f, 1f ));
	}

	float GetReleaseBlendSmoothT()
	{
		if ( releaseBlendUseZeroT )
			return 0f;

		var duration = ReleaseCameraBlendDuration <= 0.0001f ? 0.0001f : ReleaseCameraBlendDuration;
		var tLinear = MathX.Clamp( (Time.Now - releaseBlendStartTime) / duration, 0f, 1f );
		return tLinear * tLinear * (3f - 2f * tLinear );
	}

	bool ShouldLeaveCameraAlone()
	{
		tackleImpactFeel ??= Components.Get<TackleImpactFeel>();
		if ( tackleImpactFeel?.IsImpactFeelActive == true )
			return true;

		if ( !playerTackle.IsValid() )
			return false;

		return playerTackle.IsKnockedDown || playerTackle.IsStandUpCameraBlending;
	}

	void RestoreBaselineCamera()
	{
		if ( !playerController.IsValid() )
			return;

		playerController.CameraOffset = baselineCameraOffset;
		wasChargingThrow = false;
		wasPendingThrowRelease = false;
		lastChargeCameraLerp = 0f;
		releaseBlendStartTime = -1f;
		releaseBlendAwaitingPostCameraClear = false;
		releaseBlendUseZeroT = false;
		hasLastAppliedChargeFieldOfView = false;
	}

	void TryCaptureBaselineOffset()
	{
		if ( baselineCaptured )
			return;

		playerController ??= Components.Get<PlayerController>();
		playerTackle ??= Components.Get<PlayerTackle>();
		ballThrow ??= Components.Get<BallThrow>();
		speedBlitzUlt ??= Components.Get<SpeedsterSpeedBlitzUlt>();

		if ( !playerController.IsValid() )
			return;

		if ( ballThrow?.IsChargingThrow == true )
			return;

		if ( playerTackle?.IsRagdolled == true )
			return;

		if ( speedBlitzUlt?.IsActive == true )
			return;

		baselineCameraOffset = playerController.CameraOffset;
		baselineCaptured = true;
	}

	bool TryEnsureReady()
	{
		if ( !baselineCaptured )
			TryCaptureBaselineOffset();

		playerController ??= Components.Get<PlayerController>();
		ballThrow ??= Components.Get<BallThrow>();
		playerTackle ??= Components.Get<PlayerTackle>();

		return playerController.IsValid() && baselineCaptured;
	}

	void ApplyChargeCameraOffset( float chargeLerp )
	{
		playerController.CameraOffset = GetChargeCameraOffset( chargeLerp );
	}

	Vector3 GetChargeCameraOffset( float chargeLerp )
	{
		chargeLerp = chargeLerp.Clamp( 0f, 1f );
		return baselineCameraOffset + new Vector3(
			ExtraCameraDistanceAtFullCharge * chargeLerp,
			0f,
			ExtraCameraHeightAtFullCharge * chargeLerp );
	}

	void BeginReleaseOffsetBlend()
	{
		var chargeLerp = lastChargeCameraLerp.Clamp( 0f, 1f );
		releaseBlendFromOffset = GetChargeCameraOffset( chargeLerp );
		releaseBlendFromFieldOfView = GetHeldChargeFieldOfView();
		hasLastAppliedChargeFieldOfView = false;
		releaseBlendAwaitingPostCameraClear = false;
		releaseBlendStartTime = Time.Now;
		releaseBlendUseZeroT = true;
		playerController.CameraOffset = releaseBlendFromOffset;
		StepReleaseOffsetBlend();
	}

	void StepReleaseOffsetBlend()
	{
		var t = GetReleaseBlendSmoothT();
		var duration = ReleaseCameraBlendDuration <= 0.0001f ? 0.0001f : ReleaseCameraBlendDuration;
		var tLinear = releaseBlendUseZeroT
			? 0f
			: MathX.Clamp( (Time.Now - releaseBlendStartTime) / duration, 0f, 1f );

		playerController.CameraOffset = tLinear >= 1f
			? baselineCameraOffset
			: Vector3.Lerp( releaseBlendFromOffset, baselineCameraOffset, t );

		if ( tLinear >= 1f )
			releaseBlendAwaitingPostCameraClear = true;
	}
}
