using Sandbox;

/// <summary>
/// Owner-only Speed Blitz camera — wind-up FOV/pullback buildup, instant dash FOV spike + strong pullback.
/// FOV is applied in <see cref="PlayerController.IEvents.PostCameraSetup"/> because PlayerController
/// resets <see cref="CameraComponent.FieldOfView"/> from preferences every frame after <c>OnUpdate</c>.
/// Only touches <see cref="PlayerController.CameraOffset"/> during an active ult — never resets offset when idle
/// ( <see cref="ThrowChargeCamera"/> owns offset the rest of the time ).
/// </summary>
[Order( 10004 )]
public sealed class SpeedBlitzDashCamera : Component, PlayerController.IEvents
{
	[Property, Group( "Wind-up" )] public float ExtraFieldOfViewAtFullWindUp { get; set; } = 10f;
	[Property, Group( "Wind-up" )] public float ExtraCameraDistanceAtFullWindUp { get; set; } = 22f;
	[Property, Group( "Wind-up" )] public float ExtraCameraHeightAtFullWindUp { get; set; } = 8f;

	[Property, Group( "Dash" )] public float ExtraFieldOfViewDuringDash { get; set; } = 24f;
	[Property, Group( "Dash" )] public float ExtraCameraDistanceDuringDash { get; set; } = 48f;
	[Property, Group( "Dash" )] public float ExtraCameraHeightDuringDash { get; set; } = 16f;
	[Property, Group( "Dash" )] public float DashEndBlendDurationSeconds { get; set; } = 0.18f;

	private SpeedsterSpeedBlitzUlt speedBlitzUlt;
	private PlayerTackle playerTackle;
	private TackleImpactFeel tackleImpactFeel;
	private PlayerController playerController;

	private Vector3 baselineCameraOffset;
	private float baselineFieldOfView = 60f;
	private bool baselineCaptured;

	private bool wasDashing;
	private bool wasWindUp;
	private float dashEndBlendStartTime = -1f;
	private Vector3 dashEndBlendFromOffset;
	private float dashEndBlendFromFieldOfView;

	protected override void OnStart()
	{
		speedBlitzUlt = Components.Get<SpeedsterSpeedBlitzUlt>();
		playerTackle = Components.Get<PlayerTackle>();
		tackleImpactFeel = Components.Get<TackleImpactFeel>();
		playerController = Components.Get<PlayerController>();
		TryCaptureBaselineOffset();
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
			wasWindUp = false;
			dashEndBlendStartTime = -1f;
			return;
		}

		var windUp = speedBlitzUlt.IsWindUp;
		var dashing = speedBlitzUlt.IsDashing;
		var ultActive = speedBlitzUlt.IsActive;

		if ( dashing )
		{
			dashEndBlendStartTime = -1f;
			ApplyDashCameraOffset();
		}
		else if ( windUp )
		{
			dashEndBlendStartTime = -1f;
			ApplyWindUpCameraOffset( speedBlitzUlt.GetWindUpLerp() );
		}
		else if ( dashEndBlendStartTime >= 0f )
		{
			StepDashEndOffsetBlend();
		}
		else if ( (wasDashing || wasWindUp) && !ultActive )
		{
			BeginDashEndBlend();
		}
		// When idle: do not touch CameraOffset — ThrowChargeCamera / PlayerController own it.

		wasDashing = dashing;
		wasWindUp = windUp;
	}

	void PlayerController.IEvents.PostCameraSetup( CameraComponent cam )
	{
		if ( !Network.IsOwner || !cam.IsValid() )
			return;

		speedBlitzUlt ??= Components.Get<SpeedsterSpeedBlitzUlt>();
		playerController ??= Components.Get<PlayerController>();

		if ( !TryEnsureReady() )
			return;

		if ( ShouldLeaveCameraAlone() )
			return;

		if ( TryGetOverrideFieldOfView( out var fieldOfView ) )
		{
			cam.FieldOfView = fieldOfView;
			return;
		}

		if ( !speedBlitzUlt.IsActive && dashEndBlendStartTime < 0f )
			baselineFieldOfView = cam.FieldOfView;
	}

	bool TryGetOverrideFieldOfView( out float fieldOfView )
	{
		fieldOfView = baselineFieldOfView;
		speedBlitzUlt ??= Components.Get<SpeedsterSpeedBlitzUlt>();
		if ( !speedBlitzUlt.IsValid() )
			return false;

		if ( speedBlitzUlt.IsDashing )
		{
			fieldOfView = baselineFieldOfView + ExtraFieldOfViewAtFullWindUp + ExtraFieldOfViewDuringDash;
			return true;
		}

		if ( speedBlitzUlt.IsWindUp )
		{
			var windUpLerp = speedBlitzUlt.GetWindUpLerp();
			fieldOfView = baselineFieldOfView + (ExtraFieldOfViewAtFullWindUp * windUpLerp);
			return true;
		}

		if ( dashEndBlendStartTime >= 0f )
		{
			var duration = DashEndBlendDurationSeconds <= 0.0001f ? 0.0001f : DashEndBlendDurationSeconds;
			var tLinear = MathX.Clamp( (Time.Now - dashEndBlendStartTime) / duration, 0f, 1f );
			var t = tLinear * tLinear * (3f - 2f * tLinear );
			fieldOfView = MathX.Lerp( dashEndBlendFromFieldOfView, baselineFieldOfView, t );
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
		dashEndBlendStartTime = -1f;
		ApplyBaselineOffset();
	}

	void TryCaptureBaselineOffset()
	{
		if ( baselineCaptured )
			return;

		playerController ??= Components.Get<PlayerController>();
		playerTackle ??= Components.Get<PlayerTackle>();
		speedBlitzUlt ??= Components.Get<SpeedsterSpeedBlitzUlt>();

		if ( !playerController.IsValid() )
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
		return playerController.IsValid() && baselineCaptured;
	}

	void ApplyWindUpCameraOffset( float windUpLerp )
	{
		windUpLerp = windUpLerp.Clamp( 0f, 1f );
		var extraOffset = new Vector3(
			ExtraCameraDistanceAtFullWindUp * windUpLerp,
			0f,
			ExtraCameraHeightAtFullWindUp * windUpLerp );

		playerController.CameraOffset = baselineCameraOffset + extraOffset;
	}

	void ApplyDashCameraOffset()
	{
		var extraOffset = new Vector3(
			ExtraCameraDistanceAtFullWindUp + ExtraCameraDistanceDuringDash,
			0f,
			ExtraCameraHeightAtFullWindUp + ExtraCameraHeightDuringDash );

		playerController.CameraOffset = baselineCameraOffset + extraOffset;
	}

	void BeginDashEndBlend()
	{
		dashEndBlendFromOffset = playerController.CameraOffset;
		dashEndBlendFromFieldOfView = wasDashing
			? baselineFieldOfView + ExtraFieldOfViewAtFullWindUp + ExtraFieldOfViewDuringDash
			: baselineFieldOfView + ExtraFieldOfViewAtFullWindUp;
		dashEndBlendStartTime = Time.Now;
		StepDashEndOffsetBlend();
	}

	void StepDashEndOffsetBlend()
	{
		var duration = DashEndBlendDurationSeconds <= 0.0001f ? 0.0001f : DashEndBlendDurationSeconds;
		var tLinear = MathX.Clamp( (Time.Now - dashEndBlendStartTime) / duration, 0f, 1f );
		var t = tLinear * tLinear * (3f - 2f * tLinear );

		playerController.CameraOffset = Vector3.Lerp( dashEndBlendFromOffset, baselineCameraOffset, t );

		if ( tLinear >= 1f )
			dashEndBlendStartTime = -1f;
	}

	void ApplyBaselineOffset()
	{
		playerController.CameraOffset = baselineCameraOffset;
	}
}
