using Sandbox;
using Sandbox.Audio;

/// <summary>
/// Global audio policy at play start — every machine (host + join clients).
/// Mixer Reverb alone does not dry engine physics / surface sounds; disable room simulation too.
/// </summary>
public sealed class MatchAudioBootstrap : Component
{
	/// <summary>
	/// Whitelist tag for audio sim traces — no map geometry uses this, so room reverb/occlusion sim sees no walls/ceilings.
	/// Uncheck for indoor/tunnel maps later.
	/// </summary>
	internal const string RoomSimulationWhitelistTag = "audio_room_sim";

	/// <summary>Kills dynamic room reverb (footsteps, ragdoll impacts, surface hits). Default on for outdoor Turf Wars.</summary>
	[Property] public bool DisableRoomSimulation { get; set; } = true;

	[Property] public float MasterReverb { get; set; } = 0f;

	public static void EnsureOnMainCamera( Scene scene )
	{
		if ( scene is null )
			return;

		foreach ( var camera in scene.GetAllComponents<CameraComponent>() )
		{
			if ( !camera.IsMainCamera )
				continue;

			camera.GameObject.Components.GetOrCreate<MatchAudioBootstrap>();
			return;
		}
	}

	protected override void OnStart()
	{
		ApplyGlobalAudioPolicy( DisableRoomSimulation, MasterReverb );
	}

	public static void ApplyGlobalAudioPolicy( bool disableRoomSimulation, float masterReverb )
	{
		var master = Mixer.Master;
		if ( master is null )
			return;

		ApplyReverbToMixerTree( master, masterReverb );
		ConfigureRoomSimulationBlockingTags( master, disableRoomSimulation );
	}

	private static void ConfigureRoomSimulationBlockingTags( Mixer master, bool disableRoomSimulation )
	{
		var blocking = master.BlockingTags;
		if ( blocking is null )
			return;

		blocking.RemoveAll();

		if ( disableRoomSimulation )
			blocking.Add( RoomSimulationWhitelistTag );
	}

	private static void ApplyReverbToMixerTree( Mixer mixer, float reverb )
	{
		if ( mixer is null )
			return;

		mixer.Reverb = reverb.Clamp( 0f, 1f );

		foreach ( var child in mixer.GetChildren() )
			ApplyReverbToMixerTree( child, reverb );
	}

	/// <summary>3D one-shot with per-handle room simulation disabled.</summary>
	public static void PlayWorldSoundDry( SoundEvent soundEvent, Vector3 worldPosition, float volume = 1f )
	{
		if ( !soundEvent.IsValid() )
			return;

		var handle = Sound.Play( soundEvent, worldPosition );
		if ( !handle.IsPlaying )
			return;

		if ( Math.Abs( volume - 1f ) >= 0.001f )
			handle.Volume = volume.Clamp( 0f, 2f );

		ApplyDryHandle( handle );
	}

	public static void ApplyDryHandle( SoundHandle handle )
	{
		if ( !handle.IsValid )
			return;

		handle.ReverbEnabled = false;
		handle.Reverb = 0f;
	}
}
