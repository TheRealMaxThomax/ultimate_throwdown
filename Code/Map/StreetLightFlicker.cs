using Sandbox;

/// <summary>
/// Random street-lamp flicker for a single pole cluster (parent empty + lamp model + spot child).
/// Drives <see cref="SpotLight"/> and optionally the bulb material slot on <see cref="ModelRenderer"/>.
/// </summary>
public sealed class StreetLightFlicker : Component
{
	[Property] public SpotLight Spot { get; set; }
	[Property] public ModelRenderer LampModel { get; set; }

	/// <summary>Material for the bulb slot while the lamp is off (no emissive). Same as broken lamp bulb if unset in inspector.</summary>
	[Property] public Material BulbOffMaterial { get; set; }

	/// <summary>Material slot index for the bulb. <c>-1</c> = auto (slot whose original material is the emissive bulb / <c>light.vmat</c> remap).</summary>
	[Property] public int BulbMaterialIndex { get; set; } = -1;

	[Property] public bool SyncBulbEmissive { get; set; } = true;

	[Property, Group( "Timing" )] public float MinSteadyOnSeconds { get; set; } = 4f;
	[Property, Group( "Timing" )] public float MaxSteadyOnSeconds { get; set; } = 14f;
	[Property, Group( "Timing" )] public float MinBriefOffSeconds { get; set; } = 0.04f;
	[Property, Group( "Timing" )] public float MaxBriefOffSeconds { get; set; } = 0.18f;
	[Property, Group( "Timing" )] public float LongOffChance { get; set; } = 0.1f;
	[Property, Group( "Timing" )] public float MinLongOffSeconds { get; set; } = 0.35f;
	[Property, Group( "Timing" )] public float MaxLongOffSeconds { get; set; } = 1.1f;
	[Property, Group( "Timing" )] public float DoubleFlickerChance { get; set; } = 0.35f;

	private bool isLit = true;
	private float nextChangeAt;
	private int flickerBurstRemaining;
	private int resolvedBulbMaterialIndex = -1;

	protected override void OnStart()
	{
		ResolveReferences();
		SetLit( true );
		ScheduleWhileLit();
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

	private void ResolveReferences()
	{
		if ( !Spot.IsValid() )
			Spot = Components.Get<SpotLight>( FindMode.EverythingInSelfAndDescendants );

		if ( !LampModel.IsValid() )
			LampModel = Components.Get<ModelRenderer>( FindMode.EverythingInSelfAndDescendants );

		if ( !BulbOffMaterial.IsValid() )
			BulbOffMaterial = Material.Load( "materials/turfwarspoly/goldenearth_streetlight_off.vmat" );

		resolvedBulbMaterialIndex = ResolveBulbMaterialIndex();
	}

	private void SetLit( bool lit )
	{
		isLit = lit;

		if ( Spot.IsValid() )
			Spot.Enabled = lit;

		if ( !SyncBulbEmissive || !LampModel.IsValid() || !BulbOffMaterial.IsValid() )
			return;

		if ( resolvedBulbMaterialIndex < 0 )
			return;

		LampModel.Materials.SetOverride( resolvedBulbMaterialIndex, lit ? null : BulbOffMaterial );
	}

	/// <summary>Find the bulb slot (<c>light.vmat</c> → emissive), not the pole (<c>body.vmat</c> → dimgrey).</summary>
	private int ResolveBulbMaterialIndex()
	{
		if ( BulbMaterialIndex >= 0 )
			return BulbMaterialIndex;

		if ( !LampModel.IsValid() )
			return -1;

		var count = LampModel.Materials.Count;
		for ( var i = 0; i < count; i++ )
		{
			var original = LampModel.Materials.GetOriginal( i );
			if ( !original.IsValid() )
				continue;

			if ( IsBulbMaterial( original ) )
				return i;
		}

		return count > 0 ? 0 : -1;
	}

	private static bool IsBulbMaterial( Material material )
	{
		var path = (material.ResourcePath ?? material.ResourceName ?? string.Empty).Replace( '\\', '/' ).ToLowerInvariant();

		if ( string.IsNullOrWhiteSpace( path ) )
			return false;

		if ( path.Contains( "goldenearth_streetlight" ) || path.Contains( "streetlight_off" ) )
			return true;

		if ( path.Contains( "dimgrey" ) && !path.Contains( "streetlight" ) )
			return false;

		return path.Contains( "/light." ) || path.Contains( "light.vmat" );
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
