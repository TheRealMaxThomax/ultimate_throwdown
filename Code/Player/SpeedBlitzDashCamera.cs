using Sandbox;

/// <summary>
/// Owner-only Speed Blitz camera — wind-up FOV/pullback buildup, blended transition into dash spike + pullback.
/// On enemy contact, <see cref="BeginHitRecoveryBlend"/> eases back to baseline at freeze start (not victim launch).
/// FOV is applied in <see cref="PlayerController.IEvents.PostCameraSetup"/> because PlayerController
/// resets <see cref="CameraComponent.FieldOfView"/> from preferences every frame after <c>OnUpdate</c>.
/// Runs at <c>[Order(10012)]</c> (right after <see cref="SpeedsterSpeedBlitzUlt"/>) so dash phase + blend start
/// land before <see cref="PlayerController"/> camera setup. Only touches <see cref="PlayerController.CameraOffset"/>
/// during an active ult or hit-recovery blend — never resets offset when idle.
/// </summary>
[Order( 10012 )]
public sealed class SpeedBlitzDashCamera : Component, PlayerController.IEvents
{
	[Property, Group( "Wind-up" )] public float ExtraFieldOfViewAtFullWindUp { get; set; } = 20f;
	[Property, Group( "Wind-up" )] public float ExtraCameraDistanceAtFullWindUp { get; set; } = 0f;
	[Property, Group( "Wind-up" )] public float ExtraCameraHeightAtFullWindUp { get; set; } = 8f;

	[Property, Group( "Dash" )] public float ExtraFieldOfViewDuringDash { get; set; } = 20f;
	[Property, Group( "Dash" )] public float ExtraCameraDistanceDuringDash { get; set; } = 48f;
	[Property, Group( "Dash" )] public float ExtraCameraHeightDuringDash { get; set; } = 16f;
	[Property, Group( "Dash" )] public float WindUpToDashBlendDurationSeconds { get; set; } = 0.15f;
	[Property, Group( "Dash" )] public float DashEndBlendDurationSeconds { get; set; } = 0.05f;

	private SpeedsterSpeedBlitzUlt speedBlitzUlt;
	private PlayerTackle playerTackle;
	private TackleImpactFeel tackleImpactFeel;
	private PlayerController playerController;

	private Vector3 baselineCameraOffset;
	private float baselineFieldOfView = 60f;
	private bool baselineCaptured;

	private bool wasDashing;
	private bool wasWindUp;
	private float lastWindUpCameraLerp = 1f;
	private float lastAppliedWindUpFieldOfView;
	private bool hasLastAppliedWindUpFieldOfView;
	private float lastAppliedDashFieldOfView;
	private bool hasLastAppliedDashFieldOfView;
	private float dashStartBlendStartTime = -1f;
	private bool dashStartBlendUseZeroT;
	private bool dashStartBlendAwaitingPostCameraClear;
	private Vector3 dashStartBlendFromOffset;
	private float dashStartBlendFromFieldOfView;
	private float dashEndBlendStartTime = -1f;
	private bool dashEndBlendFromHit;
	private bool dashEndBlendUseZeroT;
	private bool dashEndBlendAwaitingPostCameraClear;
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

	/// <summary> Owner: dash stopped on enemy contact — ease camera to baseline at impact freeze start. </summary>
	public void BeginHitRecoveryBlend()
	{
		if ( !Network.IsOwner || dashEndBlendStartTime >= 0f )
			return;

		if ( !TryEnsureReady() )
			return;

		BeginDashEndBlend( fromHit: true );
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

		if ( dashStartBlendAwaitingPostCameraClear )
		{
			dashStartBlendStartTime = -1f;
			dashStartBlendAwaitingPostCameraClear = false;
		}

		if ( dashEndBlendAwaitingPostCameraClear )
		{
			dashEndBlendStartTime = -1f;
			dashEndBlendFromHit = false;
			dashEndBlendAwaitingPostCameraClear = false;
		}

		if ( ShouldLeaveCameraAlone() )
		{
			wasDashing = false;
			wasWindUp = false;
			dashStartBlendStartTime = -1f;
			dashStartBlendUseZeroT = false;
			dashStartBlendAwaitingPostCameraClear = false;
			hasLastAppliedWindUpFieldOfView = false;
			hasLastAppliedDashFieldOfView = false;
			if ( !dashEndBlendFromHit )
			{
				dashEndBlendStartTime = -1f;
				dashEndBlendUseZeroT = false;
				dashEndBlendAwaitingPostCameraClear = false;
			}
			return;
		}

		var windUp = speedBlitzUlt.IsWindUp;
		var dashing = speedBlitzUlt.IsDashing;
		var ultActive = speedBlitzUlt.IsActive;

		if ( dashEndBlendStartTime >= 0f )
		{
			StepDashEndOffsetBlend();
		}
		else if ( IsAwaitingDashStartBlend( windUp, dashing ) )
		{
			BeginDashStartBlend();
		}
		else if ( dashing )
		{
			if ( dashStartBlendStartTime >= 0f )
				StepDashStartOffsetBlend();
			else
				ApplyDashCameraOffset();
		}
		else if ( windUp )
		{
			if ( !wasWindUp )
				baselineCameraOffset = playerController.CameraOffset;

			dashStartBlendStartTime = -1f;
			dashStartBlendUseZeroT = false;
			dashStartBlendAwaitingPostCameraClear = false;
			hasLastAppliedDashFieldOfView = false;
			lastWindUpCameraLerp = speedBlitzUlt.GetWindUpLerp();
			ApplyWindUpCameraOffset( lastWindUpCameraLerp );
		}
		else if ( (wasDashing || wasWindUp) && !ultActive )
		{
			BeginDashEndBlend( fromHit: false );
		}

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
			if ( speedBlitzUlt.IsWindUp )
			{
				lastAppliedWindUpFieldOfView = fieldOfView;
				hasLastAppliedWindUpFieldOfView = true;
			}
			else if ( speedBlitzUlt.IsDashing && dashEndBlendStartTime < 0f && dashStartBlendStartTime < 0f )
			{
				lastAppliedDashFieldOfView = fieldOfView;
				hasLastAppliedDashFieldOfView = true;
			}
			else if ( dashStartBlendStartTime >= 0f )
				dashStartBlendUseZeroT = false;
			else if ( dashEndBlendStartTime >= 0f )
				dashEndBlendUseZeroT = false;

			return;
		}

		if ( !speedBlitzUlt.IsActive
			&& dashEndBlendStartTime < 0f
			&& dashStartBlendStartTime < 0f
			&& !dashStartBlendAwaitingPostCameraClear
			&& !dashEndBlendAwaitingPostCameraClear )
			baselineFieldOfView = cam.FieldOfView;
	}

	bool IsAwaitingDashStartBlend( bool windUp, bool dashing )
	{
		return dashStartBlendStartTime < 0f && wasWindUp && dashing && !windUp;
	}

	bool TryGetOverrideFieldOfView( out float fieldOfView )
	{
		fieldOfView = baselineFieldOfView;
		speedBlitzUlt ??= Components.Get<SpeedsterSpeedBlitzUlt>();
		if ( !speedBlitzUlt.IsValid() )
			return false;

		if ( dashEndBlendStartTime >= 0f )
		{
			fieldOfView = MathX.Lerp( dashEndBlendFromFieldOfView, baselineFieldOfView, GetDashEndBlendSmoothT() );
			return true;
		}

		if ( dashStartBlendStartTime >= 0f )
		{
			fieldOfView = MathX.Lerp( dashStartBlendFromFieldOfView, GetDashPeakFieldOfView(), GetDashStartBlendSmoothT() );
			return true;
		}

		var windUp = speedBlitzUlt.IsWindUp;
		var dashing = speedBlitzUlt.IsDashing;

		if ( IsAwaitingDashStartBlend( windUp, dashing ) )
		{
			fieldOfView = GetHeldWindUpFieldOfView();
			return true;
		}

		if ( dashing )
		{
			fieldOfView = GetDashPeakFieldOfView();
			return true;
		}

		if ( windUp )
		{
			var windUpLerp = speedBlitzUlt.GetWindUpLerp();
			fieldOfView = baselineFieldOfView + (ExtraFieldOfViewAtFullWindUp * windUpLerp);
			return true;
		}

		return false;
	}

	float GetHeldWindUpFieldOfView()
	{
		if ( hasLastAppliedWindUpFieldOfView )
			return lastAppliedWindUpFieldOfView;

		return baselineFieldOfView + (ExtraFieldOfViewAtFullWindUp * lastWindUpCameraLerp.Clamp( 0f, 1f ));
	}

	float GetHeldDashFieldOfView()
	{
		if ( hasLastAppliedDashFieldOfView )
			return lastAppliedDashFieldOfView;

		return GetDashPeakFieldOfView();
	}

	float GetDashStartBlendSmoothT()
	{
		if ( dashStartBlendUseZeroT )
			return 0f;

		var duration = WindUpToDashBlendDurationSeconds <= 0.0001f ? 0.0001f : WindUpToDashBlendDurationSeconds;
		var tLinear = MathX.Clamp( (Time.Now - dashStartBlendStartTime) / duration, 0f, 1f );
		return tLinear * tLinear * (3f - 2f * tLinear );
	}

	float GetDashEndBlendSmoothT()
	{
		if ( dashEndBlendUseZeroT )
			return 0f;

		var duration = DashEndBlendDurationSeconds <= 0.0001f ? 0.0001f : DashEndBlendDurationSeconds;
		var tLinear = MathX.Clamp( (Time.Now - dashEndBlendStartTime) / duration, 0f, 1f );
		return tLinear * tLinear * (3f - 2f * tLinear );
	}

	float GetDashPeakFieldOfView() => baselineFieldOfView + ExtraFieldOfViewAtFullWindUp + ExtraFieldOfViewDuringDash;

	Vector3 GetWindUpCameraOffset( float windUpLerp )
	{
		windUpLerp = windUpLerp.Clamp( 0f, 1f );
		return baselineCameraOffset + new Vector3(
			ExtraCameraDistanceAtFullWindUp * windUpLerp,
			0f,
			ExtraCameraHeightAtFullWindUp * windUpLerp );
	}

	Vector3 GetDashPeakCameraOffset()
	{
		return baselineCameraOffset + new Vector3(
			ExtraCameraDistanceAtFullWindUp + ExtraCameraDistanceDuringDash,
			0f,
			ExtraCameraHeightAtFullWindUp + ExtraCameraHeightDuringDash );
	}

	bool ShouldLeaveCameraAlone()
	{
		if ( !playerTackle.IsValid() )
			playerTackle = Components.Get<PlayerTackle>();

		if ( playerTackle.IsValid() && (playerTackle.IsKnockedDown || playerTackle.IsStandUpCameraBlending) )
			return true;

		// Keep easing to baseline through blitz connect hitstop/punch — do not yield to frozen dash FOV.
		if ( dashEndBlendFromHit && dashEndBlendStartTime >= 0f )
			return false;

		tackleImpactFeel ??= Components.Get<TackleImpactFeel>();
		if ( tackleImpactFeel?.IsImpactFeelActive == true )
			return true;

		return false;
	}

	void RefreshBaselineFromController()
	{
		if ( !playerController.IsValid() )
			return;

		baselineCameraOffset = playerController.CameraOffset;
		baselineCaptured = true;
		dashStartBlendStartTime = -1f;
		dashStartBlendUseZeroT = false;
		dashStartBlendAwaitingPostCameraClear = false;
		hasLastAppliedWindUpFieldOfView = false;
		hasLastAppliedDashFieldOfView = false;
		dashEndBlendStartTime = -1f;
		dashEndBlendFromHit = false;
		dashEndBlendUseZeroT = false;
		dashEndBlendAwaitingPostCameraClear = false;
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
		playerController.CameraOffset = GetWindUpCameraOffset( windUpLerp );
	}

	void ApplyDashCameraOffset()
	{
		playerController.CameraOffset = GetDashPeakCameraOffset();
	}

	void BeginDashStartBlend()
	{
		var windUpLerp = lastWindUpCameraLerp.Clamp( 0f, 1f );
		dashStartBlendFromOffset = GetWindUpCameraOffset( windUpLerp );
		dashStartBlendFromFieldOfView = GetHeldWindUpFieldOfView();
		hasLastAppliedWindUpFieldOfView = false;
		dashStartBlendStartTime = Time.Now;
		dashStartBlendUseZeroT = true;
		dashStartBlendAwaitingPostCameraClear = false;
		playerController.CameraOffset = dashStartBlendFromOffset;
		StepDashStartOffsetBlend();
	}

	void StepDashStartOffsetBlend()
	{
		var t = GetDashStartBlendSmoothT();
		var duration = WindUpToDashBlendDurationSeconds <= 0.0001f ? 0.0001f : WindUpToDashBlendDurationSeconds;
		var tLinear = dashStartBlendUseZeroT
			? 0f
			: MathX.Clamp( (Time.Now - dashStartBlendStartTime) / duration, 0f, 1f );
		var dashOffset = GetDashPeakCameraOffset();

		playerController.CameraOffset = tLinear >= 1f
			? dashOffset
			: Vector3.Lerp( dashStartBlendFromOffset, dashOffset, t );

		if ( tLinear >= 1f )
			dashStartBlendAwaitingPostCameraClear = true;
	}

	void BeginDashEndBlend( bool fromHit )
	{
		if ( dashEndBlendStartTime >= 0f )
			return;

		dashEndBlendFromHit = fromHit;
		dashStartBlendStartTime = -1f;
		dashStartBlendUseZeroT = false;
		dashStartBlendAwaitingPostCameraClear = false;
		hasLastAppliedDashFieldOfView = false;

		dashEndBlendFromOffset = fromHit
			? GetDashPeakCameraOffset()
			: playerController.CameraOffset;
		dashEndBlendFromFieldOfView = fromHit
			? GetHeldDashFieldOfView()
			: wasDashing
				? GetDashPeakFieldOfView()
				: GetHeldWindUpFieldOfView();

		dashEndBlendStartTime = Time.Now;
		dashEndBlendUseZeroT = true;
		dashEndBlendAwaitingPostCameraClear = false;
		playerController.CameraOffset = dashEndBlendFromOffset;
		StepDashEndOffsetBlend();
	}

	void StepDashEndOffsetBlend()
	{
		var t = GetDashEndBlendSmoothT();
		var duration = DashEndBlendDurationSeconds <= 0.0001f ? 0.0001f : DashEndBlendDurationSeconds;
		var tLinear = dashEndBlendUseZeroT
			? 0f
			: MathX.Clamp( (Time.Now - dashEndBlendStartTime) / duration, 0f, 1f );

		playerController.CameraOffset = tLinear >= 1f
			? baselineCameraOffset
			: Vector3.Lerp( dashEndBlendFromOffset, baselineCameraOffset, t );

		if ( tLinear >= 1f )
			dashEndBlendAwaitingPostCameraClear = true;
	}

	void ApplyBaselineOffset()
	{
		playerController.CameraOffset = baselineCameraOffset;
	}
}
