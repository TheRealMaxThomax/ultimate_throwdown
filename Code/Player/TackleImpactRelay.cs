using Sandbox;
using System;
using System.Collections.Generic;

/// <summary>
/// Tackle + Speed Blitz connect SFX broadcast and owner impact-feel RPC relay — sibling of <see cref="PlayerTackle"/>.
/// </summary>
public sealed class TackleImpactRelay : Component
{
	internal const string DefaultTackleConnectImpactSoundAPath = "sounds/crunch/speed_blitz_connect_crunch_a.sound";
	internal const string DefaultTackleConnectImpactSoundBPath = "sounds/crunch/speed_blitz_connect_crunch_b.sound";

	/// <summary>Body-crunch SFX on player tackle connect — host picks one at random per hit (not Speed Blitz).</summary>
	[Property, Group( "Impact SFX" )] public SoundEvent TackleConnectImpactSoundA { get; set; }
	[Property, Group( "Impact SFX" )] public SoundEvent TackleConnectImpactSoundB { get; set; }
	[Property, Group( "Impact SFX" )] public float TackleConnectImpactSoundVolume { get; set; } = 1f;

	/// <summary>Client-owner tackler already played connect crunch on predict — skip host broadcast duplicate.</summary>
	private bool ownerPredictedTackleConnectSound;

	private PlayerTackle tackle;
	private CombatFeelPredictDedupe combatFeelDedupe;
	private TackleImpactFeel tackleImpactFeel;

	protected override void OnStart()
	{
		tackle = Components.Get<PlayerTackle>();
	}

	/// <summary>Host: random connect crunch from attacker tackle slots, then code defaults.</summary>
	internal string PickTackleConnectImpactSoundResourcePath()
	{
		var options = new List<string>( 2 );

		if ( TackleConnectImpactSoundA.IsValid() )
			options.Add( TackleConnectImpactSoundA.ResourcePath );

		if ( TackleConnectImpactSoundB.IsValid() )
			options.Add( TackleConnectImpactSoundB.ResourcePath );

		if ( options.Count == 0 )
		{
			options.Add( DefaultTackleConnectImpactSoundAPath );
			options.Add( DefaultTackleConnectImpactSoundBPath );
		}

		if ( options.Count == 1 )
			return options[0];

		return options[Game.Random.Int( 0, options.Count - 1 )];
	}

	internal static Vector3 GetTackleConnectImpactSoundPosition( PlayerTackle attacker, PlayerTackle victim )
	{
		if ( !attacker.IsValid() || !victim.IsValid() )
			return Vector3.Zero;

		var attackerPos = attacker.WorldPosition;
		var victimPos = victim.WorldPosition;
		return new Vector3(
			(attackerPos.x + victimPos.x) * 0.5f,
			(attackerPos.y + victimPos.y) * 0.5f,
			(attackerPos.z + victimPos.z) * 0.5f );
	}

	internal void OwnerPlayPredictedTackleConnectImpactSound( PlayerTackle victim )
	{
		if ( !victim.IsValid() || Networking.IsHost || ownerPredictedTackleConnectSound )
			return;

		var sound = ResourceLibrary.Get<SoundEvent>( PickTackleConnectImpactSoundResourcePath() );
		PlayTackleConnectImpactSoundAt(
			GetTackleConnectImpactSoundPosition( tackle, victim ),
			sound,
			TackleConnectImpactSoundVolume.Clamp( 0f, 2f ) );
		ownerPredictedTackleConnectSound = true;
	}

	internal static void BroadcastTackleConnectImpactSoundOnHost( PlayerTackle attacker, PlayerTackle victim )
	{
		if ( !Networking.IsHost || !attacker.IsValid() || !victim.IsValid() )
			return;

		var attackerRelay = ComponentRequire.On<TackleImpactRelay>( attacker, "TackleImpactRelay.BroadcastTackleConnect" );
		var victimRelay = ComponentRequire.On<TackleImpactRelay>( victim, "TackleImpactRelay.BroadcastTackleConnect" );
		if ( !attackerRelay.IsValid() || !victimRelay.IsValid() )
			return;

		victimRelay.BroadcastTackleConnectImpactSound(
			GetTackleConnectImpactSoundPosition( attacker, victim ),
			attackerRelay.PickTackleConnectImpactSoundResourcePath(),
			attackerRelay.TackleConnectImpactSoundVolume.Clamp( 0f, 2f ),
			attacker.GameObject.Id );
	}

	/// <summary>All machines: 3D connect crunch at tackle contact.</summary>
	private static void PlayTackleConnectImpactSoundAt( Vector3 worldPosition, SoundEvent soundEvent, float volume )
	{
		MatchAudioBootstrap.PlayWorldSoundDry( soundEvent, worldPosition, volume );
	}

	internal void BroadcastTackleConnectImpactSound( Vector3 worldPosition, string soundResourcePath, float volume, Guid attackerRootId )
	{
		PlayTackleConnectImpactSoundRpc( worldPosition, soundResourcePath, volume, attackerRootId );
	}

	[Rpc.Broadcast]
	private void PlayTackleConnectImpactSoundRpc( Vector3 worldPosition, string soundResourcePath, float volume, Guid attackerRootId )
	{
		if ( attackerRootId != Guid.Empty
			&& TryConsumeHostTackleConnectSoundDedupeForAttacker( Scene, attackerRootId ) )
			return;

		var sound = ResourceLibrary.Get<SoundEvent>( soundResourcePath );
		PlayTackleConnectImpactSoundAt( worldPosition, sound, volume );
	}

	internal static bool TryConsumeHostTackleConnectSoundDedupeForAttacker( Scene scene, Guid attackerRootId )
	{
		if ( scene is null || attackerRootId == Guid.Empty )
			return false;

		foreach ( var relay in scene.GetAllComponents<TackleImpactRelay>() )
		{
			if ( !relay.IsValid() || relay.GameObject.Id != attackerRootId || !relay.Network.IsOwner )
				continue;

			return relay.TryConsumeHostTackleConnectSoundDedupe();
		}

		return false;
	}

	private bool TryConsumeHostTackleConnectSoundDedupe()
	{
		if ( !ownerPredictedTackleConnectSound )
			return false;

		ownerPredictedTackleConnectSound = false;
		return true;
	}

	internal void BroadcastSpeedBlitzLaunchSound( Vector3 worldPosition, string soundResourcePath, float volume )
	{
		PlaySpeedBlitzLaunchSoundRpc( worldPosition, soundResourcePath, volume );
	}

	[Rpc.Broadcast]
	private void PlaySpeedBlitzLaunchSoundRpc( Vector3 worldPosition, string soundResourcePath, float volume )
	{
		var sound = ResourceLibrary.Get<SoundEvent>( soundResourcePath );
		SpeedsterSpeedBlitzUlt.PlayLaunchSoundAt( worldPosition, sound, volume );
	}

	internal void BroadcastSpeedBlitzConnectImpactSound( Vector3 worldPosition, string soundResourcePath, float volume, Guid dasherRootId )
	{
		PlaySpeedBlitzConnectImpactSoundRpc( worldPosition, soundResourcePath, volume, dasherRootId );
	}

	[Rpc.Broadcast]
	private void PlaySpeedBlitzConnectImpactSoundRpc( Vector3 worldPosition, string soundResourcePath, float volume, Guid dasherRootId )
	{
		if ( dasherRootId != Guid.Empty
			&& SpeedsterSpeedBlitzUlt.TryConsumeHostConnectSoundDedupeForDasher( Scene, dasherRootId ) )
			return;

		var sound = ResourceLibrary.Get<SoundEvent>( soundResourcePath );
		SpeedsterSpeedBlitzUlt.PlayConnectImpactSoundAt( worldPosition, sound, volume );
	}

	/// <summary>Host: owner-only hitstop / shake / punch on attacker and victim clients.</summary>
	internal static void NotifyTackleImpactFeel( PlayerTackle attacker, PlayerTackle victim, bool speedBlitzKnockdown = false )
	{
		if ( !Networking.IsHost )
			return;

		if ( attacker.IsValid() && attacker != victim )
		{
			var attackerDedupe = ComponentRequire.On<CombatFeelPredictDedupe>( attacker, "TackleImpactRelay.NotifyFeel" );
			if ( attackerDedupe.IsValid() )
			{
				var applyId = attackerDedupe.AllocateCombatFeelApplyIdOnHost();
				var attackerRelay = ComponentRequire.On<TackleImpactRelay>( attacker, "TackleImpactRelay.NotifyFeel" );
				attackerRelay?.TriggerTackleImpactFeelAsAttackerRpc( applyId, speedBlitzKnockdown );
			}
		}

		if ( victim.IsValid() )
		{
			var victimDedupe = ComponentRequire.On<CombatFeelPredictDedupe>( victim, "TackleImpactRelay.NotifyFeel" );
			if ( !victimDedupe.IsValid() )
				return;

			var applyId = victimDedupe.AllocateCombatFeelApplyIdOnHost();
			var victimRelay = ComponentRequire.On<TackleImpactRelay>( victim, "TackleImpactRelay.NotifyFeel" );
			victimRelay?.TriggerTackleImpactFeelAsVictimRpc( applyId, hazardKnockdown: !attacker.IsValid(), speedBlitzKnockdown );
		}
	}

	[Rpc.Owner]
	private void TriggerTackleImpactFeelAsAttackerRpc( int combatFeelApplyId, bool speedBlitzKnockdown = false )
	{
		combatFeelDedupe ??= Components.Get<CombatFeelPredictDedupe>();
		if ( combatFeelDedupe.IsValid() && combatFeelDedupe.TryConsumeHostAttackerFeelDedupe( combatFeelApplyId ) )
			return;

		tackleImpactFeel ??= Components.Get<TackleImpactFeel>();
		if ( !tackleImpactFeel.IsValid() )
			return;

		if ( speedBlitzKnockdown )
			tackleImpactFeel.TriggerAsAttacker( ResolveSpeedBlitzImpactFeelOverrides() );
		else
			tackleImpactFeel.TriggerAsAttacker();
	}

	[Rpc.Owner]
	private void TriggerTackleImpactFeelAsVictimRpc( int combatFeelApplyId, bool hazardKnockdown, bool speedBlitzKnockdown = false )
	{
		combatFeelDedupe ??= Components.Get<CombatFeelPredictDedupe>();
		if ( combatFeelDedupe.IsValid() && combatFeelDedupe.TryConsumeHostVictimFeelDedupe( combatFeelApplyId ) )
			return;

		tackleImpactFeel ??= Components.Get<TackleImpactFeel>();
		if ( !tackleImpactFeel.IsValid() )
			return;

		if ( hazardKnockdown )
			tackleImpactFeel.TriggerAsHazardVictim();
		else if ( speedBlitzKnockdown )
			tackleImpactFeel.TriggerAsVictim( SpeedsterSpeedBlitzUlt.DefaultKnockdownImpactFeelOverrides );
		else
			tackleImpactFeel.TriggerAsVictim();
	}

	private TackleImpactFeelOverrides ResolveSpeedBlitzImpactFeelOverrides()
	{
		var ult = Components.Get<SpeedsterSpeedBlitzUlt>();
		return ult.IsValid() ? ult.GetKnockdownImpactFeelOverrides() : SpeedsterSpeedBlitzUlt.DefaultKnockdownImpactFeelOverrides;
	}
}
