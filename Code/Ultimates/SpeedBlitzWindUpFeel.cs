using Sandbox;
using System;

/// <summary>
/// Speed Blitz slice 2d — phased electric VFX + wind-up/dash SFX on all clients.
/// Driven by synced <see cref="SpeedsterSpeedBlitzUlt"/> phases (no extra RPCs).
/// </summary>
[Order( 10013 )]
public sealed class SpeedBlitzWindUpFeel : Component
{
	private SpeedsterSpeedBlitzUlt ult;

	private GameObject dasherVfxInstance;
	private GameObject victimVfxInstance;

	private SoundHandle electricHandle;
	private SoundHandle windUpRiseHandle;

	private SpeedsterSpeedBlitzUlt.SpeedBlitzPhase previousPhase;
	private bool wasConnectPoseFrozen;
	private bool wasDashing;
	private float missFadeUntil;
	private float missFadeDuration;
	private bool loggedMissingPrefab;
	private bool loggedCloneFailure;
	private bool loggedUpdateFailure;
	private bool loggedMissingSound;

	protected override void OnStart()
	{
		TryResolveUlt();
	}

	protected override void OnUpdate()
	{
		if ( !IsGameObjectAlive( GameObject ) || !TryResolveUlt() )
			return;

		try
		{
			TickFeel();
		}
		catch ( Exception ex )
		{
			if ( !loggedUpdateFailure )
			{
				loggedUpdateFailure = true;
				Log.Warning( $"[SpeedBlitz] SpeedBlitzWindUpFeel Update failed: {ex}" );
			}
		}
	}

	protected override void OnDisabled()
	{
		try
		{
			StopAllFeel( immediate: true );
		}
		catch ( Exception ex )
		{
			Log.Warning( $"[SpeedBlitz] SpeedBlitzWindUpFeel OnDisabled cleanup failed: {ex.Message}" );
		}
	}

	private void TickFeel()
	{
		var phase = ult.SyncedPhase;
		var connectFrozen = ult.IsConnectPoseFrozen;
		var dashing = ult.IsDashing;
		var windUp = ult.IsWindUp;

		if ( windUp && previousPhase != SpeedsterSpeedBlitzUlt.SpeedBlitzPhase.WindUp )
		{
			previousPhase = SpeedsterSpeedBlitzUlt.SpeedBlitzPhase.WindUp;
			BeginWindUpFeel();
		}

		if ( dashing && previousPhase == SpeedsterSpeedBlitzUlt.SpeedBlitzPhase.WindUp )
		{
			previousPhase = SpeedsterSpeedBlitzUlt.SpeedBlitzPhase.Dash;
			BeginDashFeel();
		}

		if ( connectFrozen && !wasConnectPoseFrozen )
			BeginConnectHangFeel();

		if ( wasConnectPoseFrozen && !connectFrozen )
			EndConnectHangFeel();

		if ( wasDashing && !dashing && !connectFrozen && phase == SpeedsterSpeedBlitzUlt.SpeedBlitzPhase.None )
			BeginMissFadeFeel();

		if ( previousPhase != SpeedsterSpeedBlitzUlt.SpeedBlitzPhase.None
			&& phase == SpeedsterSpeedBlitzUlt.SpeedBlitzPhase.None
			&& !connectFrozen
			&& !IsMissFading()
			&& previousPhase == SpeedsterSpeedBlitzUlt.SpeedBlitzPhase.WindUp )
			StopAllFeel( immediate: true );

		UpdateActiveFeel( windUp, connectFrozen, dashing );
		UpdateElectricVolume();

		previousPhase = phase;
		wasConnectPoseFrozen = connectFrozen;
		wasDashing = dashing;
	}

	private bool TryResolveUlt()
	{
		if ( ult is null || !ult.IsValid() )
			ult = Components.Get<SpeedsterSpeedBlitzUlt>();

		return ult is not null && ult.IsValid();
	}

	private void BeginWindUpFeel()
	{
		StopAllFeel( immediate: true );
		PlayElectricLoop();
		PlayWindUpRiseOneShot();
		EnsureDasherVfx();
	}

	private void BeginDashFeel()
	{
		StopWindUpRise();
		StopElectric( 0.05f );
		PlayDashStartOneShot();
		DestroyDasherVfx();
	}

	private void BeginConnectHangFeel()
	{
		StopElectric( 0f );
		EnsureVictimVfx();
	}

	private void EndConnectHangFeel()
	{
		StopAllFeel( immediate: true );
		ult.ClearBlitzConnectVictimOnHost();
	}

	private void BeginMissFadeFeel()
	{
		missFadeDuration = ult.MissVfxFadeSeconds.Clamp( 0.05f, 1.5f );
		missFadeUntil = Time.Now + missFadeDuration;
		StopElectric( missFadeDuration );
		StopWindUpRise();
	}

	private void UpdateActiveFeel( bool windUp, bool connectFrozen, bool dashing )
	{
		if ( IsMissFading() )
		{
			if ( Time.Now >= missFadeUntil )
				DestroyVfxInstances();

			return;
		}

		if ( !windUp && !dashing && !connectFrozen )
		{
			if ( IsGameObjectAlive( dasherVfxInstance ) || IsGameObjectAlive( victimVfxInstance ) )
				DestroyVfxInstances();

			return;
		}

		if ( windUp )
			EnsureDasherVfx();

		if ( connectFrozen )
		{
			EnsureDasherVfx();
			EnsureVictimVfx();
		}
	}

	private void UpdateElectricVolume()
	{
		if ( !electricHandle.IsValid() || ult is null )
			return;

		if ( IsGameObjectAlive( GameObject ) )
			electricHandle.Position = GetFeelSoundWorldPosition();

		electricHandle.Volume = ult.WindUpElectricVolume.Clamp( 0f, 2f );
	}

	private void EnsureDasherVfx()
	{
		if ( IsGameObjectAlive( dasherVfxInstance ) )
			return;

		dasherVfxInstance = CloneWindUpVfx( GameObject );
		if ( !IsGameObjectAlive( dasherVfxInstance ) && !loggedMissingPrefab )
		{
			loggedMissingPrefab = true;
			Log.Warning( "[SpeedBlitz] WindUpVfxPrefab missing — assign on SpeedsterSpeedBlitzUlt or add vfx/speedblitzwindupvfx.prefab." );
		}
	}

	private void EnsureVictimVfx()
	{
		if ( IsGameObjectAlive( victimVfxInstance ) )
			return;

		var victimGo = ult.ResolveConnectVictimGameObject();
		if ( !IsGameObjectAlive( victimGo ) )
			return;

		victimVfxInstance = CloneWindUpVfx( victimGo );
	}

	private GameObject CloneWindUpVfx( GameObject followTarget )
	{
		if ( !IsGameObjectAlive( followTarget ) || ult is null )
			return null;

		try
		{
			var prefabRoot = ult.ResolveWindUpVfxPrefabRoot();
			if ( !IsGameObjectAlive( prefabRoot ) )
				return null;

			var spawnPos = followTarget.WorldPosition + ult.WindUpVfxLocalOffset;
			var instance = prefabRoot.Clone( new CloneConfig
			{
				Transform = new Transform( spawnPos, followTarget.WorldRotation ),
				StartEnabled = true
			} );

			if ( !IsGameObjectAlive( instance ) )
				return null;

			instance.NetworkMode = NetworkMode.Never;
			instance.Name = followTarget == GameObject
				? "SpeedBlitzWindUpVFX_Dasher"
				: "SpeedBlitzWindUpVFX_Victim";

			instance.Parent = followTarget;
			instance.LocalPosition = ult.WindUpVfxLocalOffset;
			instance.LocalRotation = Rotation.Identity;
			ConfigureAttractorsOnInstance( instance );

			return instance;
		}
		catch ( Exception ex )
		{
			if ( !loggedCloneFailure )
			{
				loggedCloneFailure = true;
				Log.Warning( $"[SpeedBlitz] WindUpVfx clone failed: {ex.Message}" );
			}

			return null;
		}
	}

	private Vector3 GetFeelSoundWorldPosition()
	{
		return GameObject.WorldPosition + ult.WindUpFeelSoundLocalOffset;
	}

	private bool TryResolveFeelSound( Func<SoundEvent> resolve, string label, out SoundEvent sound )
	{
		sound = resolve();
		if ( sound.IsValid() )
			return true;

		if ( !loggedMissingSound )
		{
			loggedMissingSound = true;
			Log.Warning( $"[SpeedBlitz] {label} missing — drag the .sound onto SpeedsterSpeedBlitzUlt (Wind-up feel) or check code fallback paths." );
		}

		return false;
	}

	private void PlayElectricLoop()
	{
		if ( ult is null || !IsGameObjectAlive( GameObject ) )
			return;

		if ( !TryResolveFeelSound( ult.ResolveWindUpElectricSound, "WindUpElectricSound", out var sound ) )
			return;

		electricHandle = SpeedsterSpeedBlitzUlt.PlayFeelSoundAt(
			GetFeelSoundWorldPosition(),
			sound,
			ult.WindUpElectricVolume );
	}

	private void PlayWindUpRiseOneShot()
	{
		if ( ult is null || !IsGameObjectAlive( GameObject ) )
			return;

		if ( !TryResolveFeelSound( ult.ResolveWindUpRiseSound, "WindUpRiseSound", out var sound ) )
			return;

		windUpRiseHandle = SpeedsterSpeedBlitzUlt.PlayFeelSoundAt(
			GetFeelSoundWorldPosition(),
			sound,
			ult.WindUpRiseVolume );
	}

	private void PlayDashStartOneShot()
	{
		if ( ult is null || !IsGameObjectAlive( GameObject ) )
			return;

		if ( !TryResolveFeelSound( ult.ResolveDashStartSound, "DashStartSound", out var sound ) )
			return;

		SpeedsterSpeedBlitzUlt.PlayFeelSoundAt(
			GetFeelSoundWorldPosition(),
			sound,
			ult.DashStartVolume );
	}

	private void StopWindUpRise()
	{
		if ( !windUpRiseHandle.IsValid() )
			return;

		windUpRiseHandle.Stop( 0.03f );
		windUpRiseHandle = default;
	}

	private void StopElectric( float fadeSeconds )
	{
		if ( !electricHandle.IsValid() )
			return;

		electricHandle.Stop( fadeSeconds.Clamp( 0f, 1f ) );
		electricHandle = default;
	}

	private bool IsMissFading() => missFadeUntil > 0f && Time.Now < missFadeUntil;

	private void ConfigureAttractorsOnInstance( GameObject instance )
	{
		foreach ( var attractor in instance.GetComponentsInChildren<ParticleAttractor>( true ) )
		{
			if ( !attractor.IsValid() )
				continue;

			attractor.Target = instance;
		}
	}

	private void StopAllFeel( bool immediate )
	{
		missFadeUntil = 0f;
		missFadeDuration = 0f;

		StopWindUpRise();
		var fadeSeconds = immediate
			? 0f
			: (ult?.MissVfxFadeSeconds ?? 0.25f).Clamp( 0.05f, 1.5f );
		StopElectric( fadeSeconds );
		DestroyVfxInstances();
	}

	private void DestroyDasherVfx()
	{
		if ( IsGameObjectAlive( dasherVfxInstance ) )
			dasherVfxInstance.Destroy();

		dasherVfxInstance = null;
	}

	private void DestroyVfxInstances()
	{
		DestroyDasherVfx();

		if ( IsGameObjectAlive( victimVfxInstance ) )
			victimVfxInstance.Destroy();

		victimVfxInstance = null;
	}

	/// <summary> GameObject refs can be null before <see cref="GameObject.IsValid"/> is safe. </summary>
	private static bool IsGameObjectAlive( GameObject gameObject )
	{
		if ( gameObject is null )
			return false;

		return gameObject.IsValid();
	}
}
