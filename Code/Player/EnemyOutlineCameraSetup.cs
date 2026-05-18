using Sandbox;

/// <summary>
/// Ensures the main camera has <see cref="Highlight"/> post-processing so <see cref="HighlightOutline"/> on players renders.
/// Add to the same GameObject as <see cref="CameraComponent"/> (e.g. Main Camera).
/// </summary>
public sealed class EnemyOutlineCameraSetup : Component
{
	protected override void OnStart()
	{
		var cameraObject = GameObject;
		if ( !cameraObject.Components.Get<CameraComponent>().IsValid() && Scene.Camera.IsValid() )
			cameraObject = Scene.Camera.GameObject;

		cameraObject.Components.GetOrCreate<Highlight>();

		var camera = cameraObject.Components.Get<CameraComponent>();
		if ( camera.IsValid() )
			camera.EnablePostProcessing = true;
	}
}
