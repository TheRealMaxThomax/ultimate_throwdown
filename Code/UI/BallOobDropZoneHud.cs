using Sandbox;

/// <summary>
/// Spawns the white OOB drop-zone marker (ground ring + world stack) from synced <see cref="PlayerTeam"/> OOB fields.
/// Settings live on Main Camera — no scene wiring per drop.
/// </summary>
public sealed class BallOobDropZoneHud : Component
{
	private BallOobDropZoneMarker activeMarker;
	private int lastSpawnedSequenceId;

	public static BallOobDropZoneHud FindInScene( Scene scene )
	{
		if ( scene is null )
			return null;

		foreach ( var camera in scene.GetAllComponents<CameraComponent>() )
		{
			if ( !camera.IsMainCamera )
				continue;

			return camera.GameObject.Components.Get<BallOobDropZoneHud>();
		}

		return null;
	}

	public static void EnsureOnMainCamera( Scene scene )
	{
		if ( scene is null )
			return;

		foreach ( var camera in scene.GetAllComponents<CameraComponent>() )
		{
			if ( !camera.IsMainCamera )
				continue;

			camera.GameObject.Components.GetOrCreate<BallOobDropZoneHud>();
			return;
		}
	}

	[Property, Group( "World stack" )] public Vector2 StackPanelSize { get; set; } = new( 420f, 360f );
	[Property, Group( "World stack" )] public float StackHeightAboveGround { get; set; } = 160f;
	[Property, Group( "World stack" )] public string StackFontFamily { get; set; } = "Les Flos Sage";
	[Property, Group( "World stack" )] public Color StackTextColor { get; set; } = Color.White;
	[Property, Group( "World stack" )] public int CountdownFontSize { get; set; } = 96;
	[Property, Group( "World stack" )] public int CountdownFontWeight { get; set; } = 800;
	[Property, Group( "World stack" )] public int ArrowFontSize { get; set; } = 72;
	[Property, Group( "World stack" )] public int ArrowFontWeight { get; set; } = 800;
	[Property, Group( "World stack" )] public int DropZoneFontSize { get; set; } = 48;
	[Property, Group( "World stack" )] public int DropZoneFontWeight { get; set; } = 700;

	[Property, Group( "Ground ring" )] public string RingModelPath { get; set; } = "models/dev/plane.vmdl";
	[Property, Group( "Ground ring" )] public string RingMaterialPath { get; set; } = "materials/turfwarspoly/speed_blitz_preview.vmat";
	[Property, Group( "Ground ring" )] public float RingPlaneBaseSize { get; set; } = 100f;
	[Property, Group( "Ground ring" )] public float RingDiameter { get; set; } = 180f;
	[Property, Group( "Ground ring" )] public float RingGroundLift { get; set; } = 1.5f;
	[Property, Group( "Ground ring" )] public Color RingTint { get; set; } = Color.White;
	[Property, Group( "Ground ring" )] public float RingBaseAlpha { get; set; } = 0.55f;
	[Property, Group( "Ground ring" )] public float RingAlphaMin { get; set; } = 0.35f;
	[Property, Group( "Ground ring" )] public float RingAlphaMax { get; set; } = 0.85f;
	[Property, Group( "Ground ring" )] public float RingPulseSpeed { get; set; } = 4f;

	protected override void OnUpdate()
	{
		if ( !MatchHudDraw.TryGetHudState( Scene, out var team, out _ ) )
		{
			ClearMarker();
			return;
		}

		if ( !team.NetBallOobActive )
		{
			ClearMarker();
			return;
		}

		if ( team.NetBallOobSequenceId == lastSpawnedSequenceId && activeMarker.IsValid() )
			return;

		SpawnMarker( team );
	}

	void SpawnMarker( PlayerTeam team )
	{
		ClearMarker();

		var markerGo = new GameObject( true, "BallOobDropZoneMarker" );
		activeMarker = markerGo.Components.Create<BallOobDropZoneMarker>();
		activeMarker.Configure( this, team.NetBallOobDropAnchor, team.NetBallOobDropAt );
		lastSpawnedSequenceId = team.NetBallOobSequenceId;
	}

	void ClearMarker()
	{
		if ( activeMarker.IsValid() )
			activeMarker.GameObject.Destroy();

		activeMarker = null;
		lastSpawnedSequenceId = 0;
	}
}
