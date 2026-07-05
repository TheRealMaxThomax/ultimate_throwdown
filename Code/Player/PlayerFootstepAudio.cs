using Sandbox;
using Sandbox.Audio;

/// <summary>
/// Owner: route built-in <see cref="PlayerController"/> footsteps through Master so prefab mixer GUID
/// mismatches on join clients cannot fall back to wet defaults.
/// </summary>
public sealed class PlayerFootstepAudio : Component
{
	protected override void OnStart()
	{
		if ( !Network.IsOwner )
			return;

		var controller = Components.Get<PlayerController>();
		var master = Mixer.Master;
		if ( !controller.IsValid() || master is null )
			return;

		controller.FootstepMixer = master;
	}
}
