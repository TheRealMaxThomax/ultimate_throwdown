using Sandbox;

/// <summary>
/// Owner-only third-person pullback + mild FOV widen while <see cref="BallThrow.IsChargingThrow"/>.
/// Scales with <see cref="BallThrow.GetThrowChargeLerp"/>; eases <see cref="PlayerController.CameraOffset"/> and FOV back on release/cancel.
/// </summary>
[Order( 10003 )]
public sealed class ThrowChargeCamera : Component
{
	[Property] public float ExtraCameraDistanceAtFullCharge { get; set; } = 50f;
	[Property] public float ExtraCameraHeightAtFullCharge { get; set; } = 20f;
	[Property] public float ExtraFieldOfViewAtFullCharge { get; set; } = 7f;
	[Property] public float ReleaseCameraBlendDuration { get; set; } = 0.35f;

	private BallThrow ballThrow;
	private PlayerTackle playerTackle;
	private TackleImpactFeel tackleImpactFeel;
	private PlayerController playerController;
	private CameraComponent activeCamera;

	private Vector3 baselineCameraOffset;
	private float baselineFieldOfView = 60f;
	private bool baselineCaptured;

	private bool wasChargingThrow;
	private bool wasKnockedDown;
	private float releaseBlendStartTime = -1f;
	private Vector3 releaseBlendFromOffset;
	private float releaseBlendFromFieldOfView;

	protected override void OnStart()
	{
		ballThrow = Components.Get<BallThrow>();
		playerTackle = Components.Get<PlayerTackle>();
		tackleImpactFeel = Components.Get<TackleImpactFeel>();
		playerController = Components.Get<PlayerController>();
		TryCaptureBaseline();
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
			releaseBlendStartTime = -1f;
			return;
		}

		var charging = ballThrow.IsValid() && ballThrow.IsChargingThrow;

		if ( charging )
		{
			releaseBlendStartTime = -1f;
			ApplyChargeCamera( ballThrow.GetThrowChargeLerp() );
		}
		else if ( wasChargingThrow )
		{
			BeginReleaseBlend();
		}
		else if ( releaseBlendStartTime >= 0f )
		{
			StepReleaseBlend();
		}
		else
		{
			ApplyBaseline();
		}

		wasChargingThrow = charging;
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

		TryFindActiveCamera();
		if ( activeCamera.IsValid() )
			baselineFieldOfView = activeCamera.FieldOfView;

		releaseBlendStartTime = -1f;
		ApplyBaseline();
	}

	void TryCaptureBaseline()
	{
		if ( baselineCaptured )
			return;

		playerController ??= Components.Get<PlayerController>();
		ballThrow ??= Components.Get<BallThrow>();
		playerTackle ??= Components.Get<PlayerTackle>();

		if ( !playerController.IsValid() )
			return;

		if ( ballThrow?.IsChargingThrow == true )
			return;

		if ( playerTackle?.IsRagdolled == true )
			return;

		baselineCameraOffset = playerController.CameraOffset;
		baselineCaptured = true;

		TryFindActiveCamera();
		if ( activeCamera.IsValid() )
			baselineFieldOfView = activeCamera.FieldOfView;
	}

	bool TryEnsureReady()
	{
		if ( !baselineCaptured )
			TryCaptureBaseline();

		playerController ??= Components.Get<PlayerController>();
		ballThrow ??= Components.Get<BallThrow>();
		playerTackle ??= Components.Get<PlayerTackle>();

		if ( !playerController.IsValid() || !baselineCaptured )
			return false;

		TryFindActiveCamera();
		return true;
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

	void ApplyChargeCamera( float chargeLerp )
	{
		chargeLerp = chargeLerp.Clamp( 0f, 1f );
		var extraOffset = new Vector3(
			ExtraCameraDistanceAtFullCharge * chargeLerp,
			0f,
			ExtraCameraHeightAtFullCharge * chargeLerp );

		playerController.CameraOffset = baselineCameraOffset + extraOffset;

		if ( activeCamera.IsValid() )
			activeCamera.FieldOfView = baselineFieldOfView + (ExtraFieldOfViewAtFullCharge * chargeLerp);
	}

	void BeginReleaseBlend()
	{
		releaseBlendFromOffset = playerController.CameraOffset;
		releaseBlendFromFieldOfView = activeCamera.IsValid() ? activeCamera.FieldOfView : baselineFieldOfView;
		releaseBlendStartTime = Time.Now;
		StepReleaseBlend();
	}

	void StepReleaseBlend()
	{
		var duration = ReleaseCameraBlendDuration <= 0.0001f ? 0.0001f : ReleaseCameraBlendDuration;
		var tLinear = MathX.Clamp( (Time.Now - releaseBlendStartTime) / duration, 0f, 1f );
		var t = tLinear * tLinear * (3f - 2f * tLinear );

		playerController.CameraOffset = Vector3.Lerp( releaseBlendFromOffset, baselineCameraOffset, t );

		if ( activeCamera.IsValid() )
			activeCamera.FieldOfView = MathX.Lerp( releaseBlendFromFieldOfView, baselineFieldOfView, t );

		if ( tLinear >= 1f )
			releaseBlendStartTime = -1f;
	}

	void ApplyBaseline()
	{
		playerController.CameraOffset = baselineCameraOffset;

		if ( activeCamera.IsValid() )
			activeCamera.FieldOfView = baselineFieldOfView;
	}
}
