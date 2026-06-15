using Sandbox;

/// <summary>
/// Owner-only third-person pullback + mild FOV widen while <see cref="BallThrow.IsChargingThrow"/>.
/// FOV is applied in <see cref="PlayerController.IEvents.PostCameraSetup"/> because PlayerController
/// resets <see cref="CameraComponent.FieldOfView"/> from preferences every frame after <c>OnUpdate</c>.
/// Runs after <see cref="SpeedBlitzDashCamera"/> so throw offset is not overwritten when ult is idle.
/// </summary>
[Order( 10006 )]
public sealed class ThrowChargeCamera : Component, PlayerController.IEvents
{
	[Property] public float ExtraCameraDistanceAtFullCharge { get; set; } = 50f;
	[Property] public float ExtraCameraHeightAtFullCharge { get; set; } = 20f;
	[Property] public float ExtraFieldOfViewAtFullCharge { get; set; } = 7f;
	[Property] public float ReleaseCameraBlendDuration { get; set; } = 0.35f;

	private BallThrow ballThrow;
	private PlayerTackle playerTackle;
	private TackleImpactFeel tackleImpactFeel;
	private SpeedsterSpeedBlitzUlt speedBlitzUlt;
	private PlayerController playerController;

	private Vector3 baselineCameraOffset;
	private float baselineFieldOfView = 60f;
	private bool baselineCaptured;

	private bool wasChargingThrow;
	private bool wasPendingThrowRelease;
	private float lastChargeCameraLerp;
	private bool wasKnockedDown;
	private float releaseBlendStartTime = -1f;
	private Vector3 releaseBlendFromOffset;
	private float releaseBlendFromFieldOfView;

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
		if ( wasKnockedDown && !knockedDown && TryEnsureReady() )
			RefreshBaselineFromController();
		wasKnockedDown = knockedDown;

		if ( !TryEnsureReady() )
			return;

		if ( ShouldLeaveCameraAlone() )
		{
			wasChargingThrow = false;
			wasPendingThrowRelease = false;
			releaseBlendStartTime = -1f;
			return;
		}

		var charging = ballThrow.IsValid() && ballThrow.IsChargingThrow;
		var pendingRelease = ballThrow.IsValid() && ballThrow.IsPendingThrowRelease;

		if ( charging )
		{
			releaseBlendStartTime = -1f;
			lastChargeCameraLerp = ballThrow.GetThrowChargeLerp();
			ApplyChargeCameraOffset( lastChargeCameraLerp );
		}
		else if ( pendingRelease )
		{
			// Hold charge camera while throw wind-up runs (ThrowReleaseDelaySeconds) — blend starts after ball leaves.
			releaseBlendStartTime = -1f;
			ApplyChargeCameraOffset( lastChargeCameraLerp );
		}
		else if ( wasChargingThrow || wasPendingThrowRelease )
		{
			BeginReleaseOffsetBlend();
		}
		else if ( releaseBlendStartTime >= 0f )
		{
			StepReleaseOffsetBlend();
		}
		else
		{
			ApplyBaselineOffset();
		}

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

		if ( TryGetOverrideFieldOfView( out var fieldOfView ) )
		{
			cam.FieldOfView = fieldOfView;
			return;
		}

		if ( !(ballThrow?.IsChargingThrow == true)
			&& !(ballThrow?.IsPendingThrowRelease == true)
			&& releaseBlendStartTime < 0f )
			baselineFieldOfView = cam.FieldOfView;
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
			fieldOfView = baselineFieldOfView + (ExtraFieldOfViewAtFullCharge * lastChargeCameraLerp.Clamp( 0f, 1f ));
			return true;
		}

		if ( releaseBlendStartTime >= 0f )
		{
			var duration = ReleaseCameraBlendDuration <= 0.0001f ? 0.0001f : ReleaseCameraBlendDuration;
			var tLinear = MathX.Clamp( (Time.Now - releaseBlendStartTime) / duration, 0f, 1f );
			var t = tLinear * tLinear * (3f - 2f * tLinear );
			fieldOfView = MathX.Lerp( releaseBlendFromFieldOfView, baselineFieldOfView, t );
			return true;
		}

		return false;
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

	void RefreshBaselineFromController()
	{
		if ( !playerController.IsValid() )
			return;

		baselineCameraOffset = playerController.CameraOffset;
		baselineCaptured = true;
		releaseBlendStartTime = -1f;
		ApplyBaselineOffset();
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
		chargeLerp = chargeLerp.Clamp( 0f, 1f );
		var extraOffset = new Vector3(
			ExtraCameraDistanceAtFullCharge * chargeLerp,
			0f,
			ExtraCameraHeightAtFullCharge * chargeLerp );

		playerController.CameraOffset = baselineCameraOffset + extraOffset;
	}

	void BeginReleaseOffsetBlend()
	{
		releaseBlendFromOffset = playerController.CameraOffset;
		releaseBlendFromFieldOfView = baselineFieldOfView + (ExtraFieldOfViewAtFullCharge * lastChargeCameraLerp.Clamp( 0f, 1f ));
		releaseBlendStartTime = Time.Now;
		StepReleaseOffsetBlend();
	}

	void StepReleaseOffsetBlend()
	{
		var duration = ReleaseCameraBlendDuration <= 0.0001f ? 0.0001f : ReleaseCameraBlendDuration;
		var tLinear = MathX.Clamp( (Time.Now - releaseBlendStartTime) / duration, 0f, 1f );
		var t = tLinear * tLinear * (3f - 2f * tLinear );

		playerController.CameraOffset = Vector3.Lerp( releaseBlendFromOffset, baselineCameraOffset, t );

		if ( tLinear >= 1f )
			releaseBlendStartTime = -1f;
	}

	void ApplyBaselineOffset()
	{
		playerController.CameraOffset = baselineCameraOffset;
	}
}
