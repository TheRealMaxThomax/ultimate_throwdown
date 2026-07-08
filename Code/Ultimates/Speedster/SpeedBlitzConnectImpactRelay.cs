using Sandbox;
using System.Collections.Generic;

/// <summary>
/// Speed Blitz connect-crunch SFX — owner predict + host broadcast dedupe. Sibling of <see cref="SpeedsterSpeedBlitzUlt"/>.
/// </summary>
public sealed class SpeedBlitzConnectImpactRelay : Component
{
	internal const string DefaultConnectImpactSoundAPath = "sounds/crunch/speed_blitz_connect_crunch_a.sound";
	internal const string DefaultConnectImpactSoundBPath = "sounds/crunch/speed_blitz_connect_crunch_b.sound";

	/// <summary> Body-crunch SFX when the dash stops on an enemy — host picks one at random each hit. </summary>
	[Property, Group( "Knockdown feel" )] public SoundEvent ConnectImpactSoundA { get; set; }

	[Property, Group( "Knockdown feel" )] public SoundEvent ConnectImpactSoundB { get; set; }

	[Property, Group( "Knockdown feel" )] public float ConnectImpactSoundVolume { get; set; } = 1f;

	/// <summary> Client-owner dasher already played crunch on predict — skip host broadcast duplicate. </summary>
	private bool ownerPredictedConnectSoundThisDash;

	/// <summary> Host: random connect crunch from dasher slots (A/B inspector, then code defaults). </summary>
	internal string PickConnectImpactSoundResourcePath()
	{
		var options = new List<string>( 2 );

		if ( ConnectImpactSoundA.IsValid() )
			options.Add( ConnectImpactSoundA.ResourcePath );

		if ( ConnectImpactSoundB.IsValid() )
			options.Add( ConnectImpactSoundB.ResourcePath );

		if ( options.Count == 0 )
		{
			options.Add( DefaultConnectImpactSoundAPath );
			options.Add( DefaultConnectImpactSoundBPath );
		}

		if ( options.Count == 1 )
			return options[0];

		return options[Game.Random.Int( 0, options.Count - 1 )];
	}

	internal void BroadcastConnectImpactSoundOnHost( PlayerTackle victim, Vector3 dasherStopPosition )
	{
		if ( !Networking.IsHost || !victim.IsValid() )
			return;

		var contactPos = GetBlitzConnectImpactSoundPosition( dasherStopPosition, victim );
		victim.Components.Get<TackleImpactRelay>()?.BroadcastSpeedBlitzConnectImpactSound(
			contactPos,
			PickConnectImpactSoundResourcePath(),
			ConnectImpactSoundVolume.Clamp( 0f, 2f ),
			GameObject.Id );
	}

	internal void OwnerPlayPredictedConnectImpactSound( PlayerTackle victim, Vector3 dasherStopPosition )
	{
		if ( Networking.IsHost || ownerPredictedConnectSoundThisDash )
			return;

		var contactPos = GetBlitzConnectImpactSoundPosition( dasherStopPosition, victim );
		var sound = ResourceLibrary.Get<SoundEvent>( PickConnectImpactSoundResourcePath() );
		SpeedsterSpeedBlitzUlt.PlayConnectImpactSoundAt( contactPos, sound, ConnectImpactSoundVolume.Clamp( 0f, 2f ) );
		ownerPredictedConnectSoundThisDash = true;
	}

	internal bool TryConsumeHostConnectSoundDedupe()
	{
		if ( !ownerPredictedConnectSoundThisDash )
			return false;

		ownerPredictedConnectSoundThisDash = false;
		return true;
	}

	internal void ResetOwnerConnectSoundPredict()
	{
		ownerPredictedConnectSoundThisDash = false;
	}

	private static Vector3 GetBlitzConnectImpactSoundPosition( Vector3 dasherStopPosition, PlayerTackle victim )
	{
		if ( !victim.IsValid() )
			return dasherStopPosition;

		var victimPos = victim.WorldPosition;
		return new Vector3(
			(dasherStopPosition.x + victimPos.x) * 0.5f,
			(dasherStopPosition.y + victimPos.y) * 0.5f,
			(dasherStopPosition.z + victimPos.z) * 0.5f );
	}
}
