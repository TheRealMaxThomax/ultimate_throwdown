using Sandbox;

/// <summary>
/// Owner-only super-speed camera during <see cref="SpeedsterSpeedBlitzUlt.IsDashing"/> —
/// FOV widen + third-person pullback. Yields to impact feel and ragdoll cameras.
/// Auto-added by <see cref="SpeedsterSpeedBlitzUlt"/> on start when present on the same pawn.
/// </summary>
[Order( 10004 )]
public sealed class SpeedBlitzDashCamera : Component
{
	[Property] public float DashBlendInDurationSeconds { get; set; } = 0.08f;
	[Property] public float DashEndBlendDurationSeconds { get; set; } = 0.15f;
	[Property] public float ExtraFieldOfViewDuringDash { get; set; } = 12f;
	[Property] public float ExtraCameraDistanceDuringDash { get; set; } = 40f;
	[Property] public float ExtraCameraHeightDuringDash { get; set; } = 12f;

	private SpeedsterSpeedBlitzUlt speedBlitzUlt;
	private PlayerTackle playerTackle;
	private TackleImpactFeel tackleImpactFeel;
	private PlayerController playerController;
	private CameraComponent activeCamera;

	private Vector3 baselineCameraOffset;
	private float baselineFieldOfView = 60f;
	private bool baselineCaptured;

	private bool wasDashing;
	private float dashBlendStartTime = -1f;
	private float dashEndBlendStartTime = -1f;
	private Vector3 dashEndBlendFromOffset;
	private float dashEndBlendFromFieldOfView;

	protected override void OnStart()
	{
		speedBlitzUlt = Components.Get<SpeedsterSpeedBlitzUlt>();
		playerTackle = Components.Get<PlayerTackle>();
		tackleImpactFeel = Components.Get<TackleImpactFeel>();
		playerController = Components.Get<PlayerController>();
		TryCaptureBaseline();
	}

	protected override void OnUpdate()
	{
		if ( !Network.IsOwner )
			return;

		speedBlitzUlt ??= Components.Get<SpeedsterSpeedBlitzUlt>();
		if ( !speedBlitzUlt.IsValid() )
			return;

		playerTackle ??= Components.Get<PlayerTackle>();
		var knockedDown = playerTackle.IsValid() && (playerTackle.IsKnockedDown || playerTackle.IsStandUpCameraBlending);
		if ( knockedDown && TryEnsureReady() )
			RefreshBaselineFromController();

		if ( !TryEnsureReady() )
			return;

		if ( ShouldLeaveCameraAlone() )
		{
			wasDashing = false;
			dashBlendStartTime = -1f;
			dashEndBlendStartTime = -1f;
			return;
		}

		var dashing = speedBlitzUlt.IsDashing;

		if ( dashing )
		{
			dashEndBlendStartTime = -1f;
			if ( !wasDashing )
				dashBlendStartTime = Time.Now;

			ApplyDashCamera( GetDashBlendT() );
		}
		else if ( wasDashing )
		{
			BeginDashEndBlend();
		}
		else if ( dashEndBlendStartTime >= 0f )
		{
			StepDashEndBlend();
		}
		else
		{
			dashBlendStartTime = -1f;
			ApplyBaseline();
		}

		wasDashing = dashing;
	}

	float GetDashBlendT()
	{
		if ( dashBlendStartTime < 0f )
			return 1f;

		var duration = DashBlendInDurationSeconds <= 0.0001f ? 0.0001f : DashBlendInDurationSeconds;
		var tLinear = MathX.Clamp( (Time.Now - dashBlendStartTime) / duration, 0f, 1f );
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

	void RefreshBaselineFromController()
	{
		if ( !playerController.IsValid() )
			return;

		baselineCameraOffset = playerController.CameraOffset;
		baselineCaptured = true;

		TryFindActiveCamera();
		if ( activeCamera.IsValid() )
			baselineFieldOfView = activeCamera.FieldOfView;

		dashBlendStartTime = -1f;
		dashEndBlendStartTime = -1f;
		ApplyBaseline();
	}

	void TryCaptureBaseline()
	{
		if ( baselineCaptured )
			return;

		playerController ??= Components.Get<PlayerController>();
		playerTackle ??= Components.Get<PlayerTackle>();

		if ( !playerController.IsValid() )
			return;

		if ( playerTackle?.IsRagdolled == true )
			return;

		if ( speedBlitzUlt?.IsDashing == true )
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

	void ApplyDashCamera( float dashLerp )
	{
		dashLerp = dashLerp.Clamp( 0f, 1f );
		var extraOffset = new Vector3(
			ExtraCameraDistanceDuringDash * dashLerp,
			0f,
			ExtraCameraHeightDuringDash * dashLerp );

		playerController.CameraOffset = baselineCameraOffset + extraOffset;

		if ( activeCamera.IsValid() )
			activeCamera.FieldOfView = baselineFieldOfView + (ExtraFieldOfViewDuringDash * dashLerp);
	}

	void BeginDashEndBlend()
	{
		dashEndBlendFromOffset = playerController.CameraOffset;
		dashEndBlendFromFieldOfView = activeCamera.IsValid() ? activeCamera.FieldOfView : baselineFieldOfView;
		dashEndBlendStartTime = Time.Now;
		dashBlendStartTime = -1f;
		StepDashEndBlend();
	}

	void StepDashEndBlend()
	{
		var duration = DashEndBlendDurationSeconds <= 0.0001f ? 0.0001f : DashEndBlendDurationSeconds;
		var tLinear = MathX.Clamp( (Time.Now - dashEndBlendStartTime) / duration, 0f, 1f );
		var t = tLinear * tLinear * (3f - 2f * tLinear );

		playerController.CameraOffset = Vector3.Lerp( dashEndBlendFromOffset, baselineCameraOffset, t );

		if ( activeCamera.IsValid() )
			activeCamera.FieldOfView = MathX.Lerp( dashEndBlendFromFieldOfView, baselineFieldOfView, t );

		if ( tLinear >= 1f )
			dashEndBlendStartTime = -1f;
	}

	void ApplyBaseline()
	{
		playerController.CameraOffset = baselineCameraOffset;

		if ( activeCamera.IsValid() )
			activeCamera.FieldOfView = baselineFieldOfView;
	}
}
