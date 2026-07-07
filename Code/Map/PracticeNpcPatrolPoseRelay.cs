using Sandbox;

/// <summary>
/// Host broadcasts authoritative <see cref="PracticeNpcPatrol"/> poses to clients each fixed tick.
/// Scene <c>practice_npc</c> dummies are not network-spawned — pose RPCs must originate from a real player pawn.
/// On player prefab + practice patrol runner; host fixed-tick pose broadcast to clients.
/// </summary>
[Order( 100 )]
public sealed class PracticeNpcPatrolPoseRelay : Component
{
	private static PracticeNpcPatrolPoseRelay hostRelay;

	protected override void OnStart()
	{
		if ( !Networking.IsHost || !GameObject.Network.Active || GameObject.Tags.Has( CitizenAvatarLod.PracticeNpcTag ) )
			return;

		hostRelay ??= this;
	}

	protected override void OnDestroy()
	{
		if ( hostRelay == this )
			hostRelay = null;
	}

	protected override void OnFixedUpdate()
	{
		if ( !Networking.IsHost || hostRelay != this || !Game.IsPlaying )
			return;

		var scene = Scene;
		if ( scene is null )
			return;

		foreach ( var patrol in scene.GetAllComponents<PracticeNpcPatrol>() )
		{
			if ( !patrol.IsValid() )
				continue;

			patrol.GetPatrolPoseForClientSync(
				out var position,
				out var rotation,
				out var isPatrolling,
				out var moveSpeed,
				out var chargeRunCycle );

			PracticeNpcPatrolPoseRpc(
				patrol.GameObject.Id,
				position,
				rotation,
				isPatrolling,
				moveSpeed,
				chargeRunCycle );
		}
	}

	[Rpc.Broadcast]
	private void PracticeNpcPatrolPoseRpc(
		Guid npcRootId,
		Vector3 worldPosition,
		Rotation worldRotation,
		bool isPatrolling,
		float moveSpeed,
		float chargeRunCycle )
	{
		if ( Networking.IsHost )
			return;

		if ( !TryFindPracticeNpcPatrol( npcRootId, out var patrol ) )
			return;

		patrol.ApplyHostPatrolPoseFromRelay( worldPosition, worldRotation, isPatrolling, moveSpeed, chargeRunCycle );
	}

	private static bool TryFindPracticeNpcPatrol( Guid npcRootId, out PracticeNpcPatrol patrol )
	{
		patrol = null;
		var scene = Game.ActiveScene;
		if ( scene is null )
			return false;

		foreach ( var candidate in scene.GetAllComponents<PracticeNpcPatrol>() )
		{
			if ( candidate.GameObject.Id != npcRootId )
				continue;

			patrol = candidate;
			return true;
		}

		return false;
	}
}
