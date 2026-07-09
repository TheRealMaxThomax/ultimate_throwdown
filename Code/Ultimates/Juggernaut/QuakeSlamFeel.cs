using Sandbox;

/// <summary>
/// Quake Slam presentation — wind-up / slam / ring pulse SFX. Reads synced state only.
/// </summary>
[Order( 10013 )]
public sealed class QuakeSlamFeel : Component
{
	[Property, Group( "Wind-up" )] public SoundEvent WindUpRiseSound { get; set; }
	[Property, Group( "Wind-up" )] public float WindUpRiseVolume { get; set; } = 0.55f;

	/// <summary> Plays at slam origin when each ring phase ticks (inner / mid / outer). </summary>
	[Property, Group( "Rings" )] public SoundEvent RingPulseSound { get; set; }
	[Property, Group( "Rings" )] public float RingPulseVolume { get; set; } = 0.85f;

	internal const string DefaultWindUpRiseSoundPath = "sounds/rising pitch/speedblitz_windup.sound";
	internal const string DefaultRingPulseSoundPath = "sounds/explosions/quake_slam_impact.sound";

	private JuggernautQuakeSlamUlt ult;
	private JuggernautQuakeSlamUlt.QuakeSlamPhase previousPhase = JuggernautQuakeSlamUlt.QuakeSlamPhase.None;
	private byte previousRingPulseIndex;

	protected override void OnStart()
	{
		ult = Components.Get<JuggernautQuakeSlamUlt>();
	}

	protected override void OnUpdate()
	{
		ult ??= Components.Get<JuggernautQuakeSlamUlt>();
		if ( ult is null || !ult.IsValid() )
			return;

		var phase = ult.SyncedPhase;
		if ( phase == JuggernautQuakeSlamUlt.QuakeSlamPhase.WindUp && previousPhase != JuggernautQuakeSlamUlt.QuakeSlamPhase.WindUp )
			PlayWindUpRiseOneShot();

		var pulse = ult.SyncedRingPulseIndex;
		if ( pulse > previousRingPulseIndex && pulse > 0 )
			PlayRingPulseSound();

		previousPhase = phase;
		previousRingPulseIndex = pulse;
	}

	protected override void OnDisabled()
	{
		previousPhase = JuggernautQuakeSlamUlt.QuakeSlamPhase.None;
		previousRingPulseIndex = 0;
	}

	private void PlayWindUpRiseOneShot()
	{
		if ( !TryResolveSound( WindUpRiseSound, DefaultWindUpRiseSoundPath, out var sound ) )
			return;

		Sound.Play( sound, GameObject.WorldPosition ).Volume = WindUpRiseVolume.Clamp( 0f, 2f );
	}

	private void PlayRingPulseSound()
	{
		if ( !TryResolveSound( RingPulseSound, DefaultRingPulseSoundPath, out var sound ) )
			return;

		Sound.Play( sound, ult.GetSlamOriginWorld() ).Volume = RingPulseVolume.Clamp( 0f, 2f );
	}

	private static bool TryResolveSound( SoundEvent assigned, string fallbackPath, out SoundEvent resolved )
	{
		if ( assigned is not null )
		{
			resolved = assigned;
			return true;
		}

		resolved = ResourceLibrary.Get<SoundEvent>( fallbackPath );
		return resolved is not null;
	}
}
