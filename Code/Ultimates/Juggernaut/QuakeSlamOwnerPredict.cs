using Sandbox;

/// <summary>
/// Owner-side Quake Slam feel predict — slam punch on caster; dedupe marks for host confirm.
/// </summary>
[Order( 10012 )]
public sealed class QuakeSlamOwnerPredict : Component
{
	private JuggernautQuakeSlamUlt ult;
	private TackleImpactFeel tackleImpactFeel;
	private byte lastSlamPulseIndex;

	protected override void OnStart()
	{
		ult = Components.Get<JuggernautQuakeSlamUlt>();
		tackleImpactFeel = Components.Get<TackleImpactFeel>();
	}

	protected override void OnUpdate()
	{
		if ( !Network.IsOwner || ult is null || !ult.IsValid() )
			return;

		var pulse = ult.SyncedRingPulseIndex;
		if ( pulse <= lastSlamPulseIndex )
			return;

		// First pulse is the slam impact — local punch before ring SFX on other clients.
		if ( pulse == 1 )
			TryPlayOwnerSlamPredictFeel();

		lastSlamPulseIndex = pulse;
	}

	protected override void OnDisabled()
	{
		lastSlamPulseIndex = 0;
	}

	private void TryPlayOwnerSlamPredictFeel()
	{
		ComponentRequire.On<CombatFeelPredictDedupe>( this, "QuakeSlamOwnerPredict" )?.MarkOwnerPredictedAttackerFeel();
		tackleImpactFeel ??= Components.Get<TackleImpactFeel>();
		tackleImpactFeel?.TriggerAsAttacker( ult?.GetSlamImpactFeelOverrides() );
	}

	internal void ResetPredictState()
	{
		lastSlamPulseIndex = 0;
	}
}
