using Sandbox;

/// <summary>
/// Flicker controller for petrol-station lights.
/// Supports a <see cref="SpotLight"/> plus an optional mesh visual (for mesh blocks) that tints on/off with the light.
/// </summary>
public sealed class StationLightFlicker : Component
{
	[Property] public SpotLight Spot { get; set; }
	[Property] public GameObject LightVisual { get; set; }
	[Property] public MeshComponent VisualMesh { get; set; }
	[Property] public bool SyncVisualTint { get; set; } = true;
	[Property] public Color VisualOnColor { get; set; } = Color.White;
	[Property] public Color VisualOffColor { get; set; } = new Color( 0.25f, 0.25f, 0.25f, 1f );
	[Property] public bool StartLit { get; set; } = true;

	[Property, Group( "Timing" )] public float MinSteadyOnSeconds { get; set; } = 1.5f;
	[Property, Group( "Timing" )] public float MaxSteadyOnSeconds { get; set; } = 6f;
	[Property, Group( "Timing" )] public float MinBriefOffSeconds { get; set; } = 0.05f;
	[Property, Group( "Timing" )] public float MaxBriefOffSeconds { get; set; } = 0.2f;
	[Property, Group( "Timing" )] public float LongOffChance { get; set; } = 0.25f;
	[Property, Group( "Timing" )] public float MinLongOffSeconds { get; set; } = 0.5f;
	[Property, Group( "Timing" )] public float MaxLongOffSeconds { get; set; } = 2.2f;
	[Property, Group( "Timing" )] public float DoubleFlickerChance { get; set; } = 0.45f;

	private bool isLit;
	private float nextChangeAt;
	private int flickerBurstRemaining;

	protected override void OnStart()
	{
		if ( !Spot.IsValid() )
			Spot = Components.Get<SpotLight>( FindMode.EverythingInSelfAndDescendants );

		ResolveVisualMesh();

		SetLit( StartLit );
		if ( isLit )
			ScheduleWhileLit();
		else
			ScheduleWhileOff();
	}

	protected override void OnUpdate()
	{
		if ( Time.Now < nextChangeAt )
			return;

		if ( isLit )
		{
			SetLit( false );
			ScheduleWhileOff();
			return;
		}

		SetLit( true );

		if ( flickerBurstRemaining > 0 )
		{
			flickerBurstRemaining--;
			nextChangeAt = Time.Now + Game.Random.Float( MinBriefOffSeconds, MaxBriefOffSeconds );
			return;
		}

		ScheduleWhileLit();
	}

	private void SetLit( bool lit )
	{
		isLit = lit;

		if ( Spot.IsValid() )
			Spot.Enabled = lit;

		if ( SyncVisualTint && VisualMesh.IsValid() )
			VisualMesh.Color = lit ? VisualOnColor : VisualOffColor;
	}

	private void ResolveVisualMesh()
	{
		if ( VisualMesh.IsValid() )
			return;

		if ( LightVisual.IsValid() )
		{
			VisualMesh = LightVisual.Components.Get<MeshComponent>( FindMode.EverythingInSelfAndDescendants );
			if ( VisualMesh.IsValid() )
				return;
		}

		VisualMesh = Components.Get<MeshComponent>( FindMode.EverythingInSelfAndDescendants );
	}

	private void ScheduleWhileLit()
	{
		nextChangeAt = Time.Now + Game.Random.Float( MinSteadyOnSeconds, MaxSteadyOnSeconds );
	}

	private void ScheduleWhileOff()
	{
		var duration = Game.Random.Float( 0f, 1f ) < LongOffChance
			? Game.Random.Float( MinLongOffSeconds, MaxLongOffSeconds )
			: Game.Random.Float( MinBriefOffSeconds, MaxBriefOffSeconds );

		nextChangeAt = Time.Now + duration;

		if ( Game.Random.Float( 0f, 1f ) < DoubleFlickerChance )
			flickerBurstRemaining = Game.Random.Int( 1, 2 );
	}
}
