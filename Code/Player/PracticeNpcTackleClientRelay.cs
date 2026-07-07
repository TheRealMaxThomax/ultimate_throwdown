using Sandbox;
using System;

/// <summary>
/// Practice dummy client-visual mirroring — scene <c>practice_npc</c> dummies use NetworkMode.Snapshot so
/// <c>[Sync]</c> knockdown state does not replicate; host broadcasts freeze/ragdoll/stand-up via RPC on a networked player.
/// Sibling of <see cref="PlayerTackle"/> on player prefabs (RPC origin) and practice dummy prefabs (mirror target).
/// </summary>
public sealed class PracticeNpcTackleClientRelay : Component
{
	private const string PracticeNpcTag = "practice_npc";

	/// <summary>Client: scene dummy stays at contact position — snapshot lag must not rewind to host freeze pos.</summary>
	private bool practiceNpcClientContactFreezePinned;
	private Vector3 practiceNpcClientContactFreezePos;

	private PlayerTackle tackle;

	protected override void OnStart()
	{
		tackle = Components.Get<PlayerTackle>();
	}

	/// <summary>Client non-host: freeze a scene practice dummy at the current visual contact position (snapshot NPCs can lag behind host).</summary>
	public void BeginPracticeNpcClientContactFreeze( bool speedBlitzKnockdown )
	{
		if ( !tackle.IsValid() )
			return;

		if ( Networking.IsHost || !GameObject.Tags.Has( PracticeNpcTag ) )
			return;

		tackle.PracticeNpc_ApplyAwaitingFreezeLocally( speedBlitzKnockdown );
		PinPracticeNpcClientContactFreezePosition();
	}

	internal static void BroadcastPracticeNpcFreezeForClient( PlayerTackle broadcaster, PlayerTackle victim, Vector3 freezePosition, bool speedBlitzKnockdown )
	{
		if ( !Networking.IsHost || !victim.IsValid() || !victim.GameObject.Tags.Has( PracticeNpcTag ) )
			return;

		ResolvePracticeNpcVisualBroadcaster( broadcaster )?.PracticeNpcClientFreezeRpc( victim.GameObject.Id, freezePosition, speedBlitzKnockdown );
	}

	internal static void BroadcastPracticeNpcRagdollForClient( PlayerTackle broadcaster, PlayerTackle victim )
	{
		if ( !Networking.IsHost || !victim.IsValid() || !victim.GameObject.Tags.Has( PracticeNpcTag ) )
			return;

		ResolvePracticeNpcVisualBroadcaster( broadcaster )?.PracticeNpcClientRagdollRpc( victim.GameObject.Id );
	}

	internal static void BroadcastPracticeNpcStandUpForClient( PlayerTackle victim )
	{
		if ( !Networking.IsHost || !victim.IsValid() || !victim.GameObject.Tags.Has( PracticeNpcTag ) )
			return;

		ResolvePracticeNpcVisualBroadcaster( null )?.PracticeNpcClientStandUpRpc(
			victim.GameObject.Id,
			victim.PracticeNpcBroadcast_GetStandUpPosition(),
			victim.PracticeNpcBroadcast_GetStandEyeAngles() );
	}

	/// <summary>Called from <see cref="PlayerTackle"/> while awaiting pre-launch freeze.</summary>
	internal void ApplyKnockdownFreezeWorldPosition()
	{
		if ( !tackle.IsValid() )
			return;

		if ( practiceNpcClientContactFreezePinned )
		{
			tackle.WorldPosition = practiceNpcClientContactFreezePos;
			return;
		}

		tackle.WorldPosition = tackle.GetKnockdownFreezePosition();
	}

	/// <summary>RPC must originate from a network-spawned player — scene practice dummies are not networked objects.</summary>
	private static PracticeNpcTackleClientRelay ResolvePracticeNpcVisualBroadcaster( PlayerTackle preferred )
	{
		if ( preferred.IsValid() && preferred.GameObject.Network.Active )
		{
			var relay = preferred.Components.Get<PracticeNpcTackleClientRelay>();
			if ( relay.IsValid() )
				return relay;
		}

		var scene = Game.ActiveScene;
		if ( scene is null )
			return null;

		foreach ( var playerTackle in scene.GetAllComponents<PlayerTackle>() )
		{
			if ( playerTackle.GameObject.Tags.Has( PracticeNpcTag ) )
				continue;
			if ( !playerTackle.GameObject.Network.Active )
				continue;

			var relay = playerTackle.Components.Get<PracticeNpcTackleClientRelay>();
			if ( relay.IsValid() )
				return relay;
		}

		return null;
	}

	[Rpc.Broadcast]
	private void PracticeNpcClientFreezeRpc( Guid victimRootId, Vector3 freezePosition, bool speedBlitzKnockdown )
	{
		if ( Networking.IsHost )
			return;

		if ( !TryFindPracticeNpcRelay( victimRootId, out var victimRelay ) )
			return;

		victimRelay.MirrorPracticeNpcFreezeFromHost( freezePosition, speedBlitzKnockdown );
	}

	[Rpc.Broadcast]
	private void PracticeNpcClientRagdollRpc( Guid victimRootId )
	{
		if ( Networking.IsHost )
			return;

		if ( !TryFindPracticeNpcRelay( victimRootId, out var victimRelay ) )
			return;

		victimRelay.MirrorPracticeNpcRagdollFromHost();
	}

	[Rpc.Broadcast]
	private void PracticeNpcClientStandUpRpc( Guid victimRootId, Vector3 standUpPosition, Angles standEyeAngles )
	{
		if ( Networking.IsHost )
			return;

		if ( !TryFindPracticeNpcRelay( victimRootId, out var victimRelay ) )
			return;

		victimRelay.MirrorPracticeNpcStandUpFromHost( standUpPosition, standEyeAngles );
	}

	private static bool TryFindPracticeNpcRelay( Guid victimRootId, out PracticeNpcTackleClientRelay victimRelay )
	{
		victimRelay = null;
		var scene = Game.ActiveScene;
		if ( scene is null )
			return false;

		foreach ( var tackle in scene.GetAllComponents<PlayerTackle>() )
		{
			if ( tackle.GameObject.Id != victimRootId )
				continue;
			if ( !tackle.GameObject.Tags.Has( PracticeNpcTag ) )
				continue;

			victimRelay = tackle.Components.Get<PracticeNpcTackleClientRelay>();
			return victimRelay.IsValid();
		}

		return false;
	}

	private void MirrorPracticeNpcFreezeFromHost( Vector3 freezePosition, bool speedBlitzKnockdown )
	{
		if ( !tackle.IsValid() )
			return;

		tackle.PracticeNpc_ApplyAwaitingFreezeLocally( speedBlitzKnockdown, freezePosition );
		PinPracticeNpcClientContactFreezePosition();
	}

	private void PinPracticeNpcClientContactFreezePosition()
	{
		if ( Networking.IsHost || !GameObject.Tags.Has( PracticeNpcTag ) )
			return;

		if ( practiceNpcClientContactFreezePinned )
			return;

		practiceNpcClientContactFreezePinned = true;
		practiceNpcClientContactFreezePos = WorldPosition;
	}

	private void ClearPracticeNpcClientContactFreezePin()
	{
		practiceNpcClientContactFreezePinned = false;
		practiceNpcClientContactFreezePos = default;
	}

	private void MirrorPracticeNpcRagdollFromHost()
	{
		if ( !tackle.IsValid() )
			return;

		tackle.PracticeNpc_ClearAwaitingRagdollLaunch();
		ClearPracticeNpcClientContactFreezePin();
		tackle.PracticeNpc_ForceLocalRagdollState( ragdolled: true );
	}

	private void MirrorPracticeNpcStandUpFromHost( Vector3 standUpPosition, Angles standEyeAngles )
	{
		if ( !tackle.IsValid() )
			return;

		tackle.PracticeNpc_ClearAwaitingRagdollLaunch();
		ClearPracticeNpcClientContactFreezePin();
		tackle.PracticeNpc_SetStandUpSync( standUpPosition, standEyeAngles );
		tackle.PracticeNpc_ForceLocalRagdollState( ragdolled: false );
	}
}
