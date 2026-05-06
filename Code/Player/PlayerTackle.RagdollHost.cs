using Sandbox;

public sealed partial class PlayerTackle
{
	// Spawns a host-owned physics ragdoll at the victim's position.
	// NetworkSpawn makes it visible on all clients automatically.
	// Physics runs on the host without client transform ownership conflicts.
	private async void SpawnRagdollObject( PlayerTackle victim, Vector3 tackleDir, float effectiveLaunchSpeed )
	{
		var ragdollGo = new GameObject( true, "PlayerRagdoll" );
		ragdollGo.WorldPosition = victim.WorldPosition + Vector3.Up * 10f;
		ragdollGo.WorldRotation = victim.WorldRotation;

		// Only copy the base body renderer (first one found).
		// Copying clothing renderers too causes them to appear as a T-pose ghost — additional
		// SkinnedModelRenderers on the ragdoll object aren't driven by ModelPhysics.
		var baseVictimRenderer = victim.Components.Get<SkinnedModelRenderer>( FindMode.EverythingInSelfAndDescendants );
		var primaryRenderer = ragdollGo.AddComponent<SkinnedModelRenderer>();
		primaryRenderer.Model = baseVictimRenderer?.Model;

		var ragdollPhysics = ragdollGo.AddComponent<ModelPhysics>();
		ragdollPhysics.Renderer = primaryRenderer;
		ragdollPhysics.MotionEnabled = true;
		ragdollPhysics.IgnoreRoot = false;
		ragdollPhysics.Enabled = true;

		if ( baseVictimRenderer != null )
			ragdollPhysics.CopyBonesFrom( baseVictimRenderer, true );

		// Wait for LOCAL physics to initialise BEFORE networking.
		// NetworkSpawn() previously disconnected the bodies from the host's physics world
		// (PhysicsGroup became null after spawn, so velocity writes had no effect).
		// By waiting first, the local physics world is active and velocity takes hold.
		// Only then do we NetworkSpawn so clients can see the already-moving ragdoll.
		await GameTask.DelaySeconds( 0.05f );
		if ( !ragdollGo.IsValid() )
			return;

		var mp = ragdollGo.Components.Get<ModelPhysics>();
		if ( mp == null || mp.Bodies.Count == 0 )
			return;

		var pb0 = mp.Bodies[0].Component?.PhysicsBody;
		var group = pb0?.PhysicsGroup;

		if ( EnableTackleDebugLogs )
			Log.Info( $"[Tackle] Pre-spawn | Bodies={mp.Bodies.Count} BodyType[0]={pb0?.BodyType} Group={group != null}" );

		var launchDir = (tackleDir + Vector3.Up * TackleLaunchArc).Normal;
		var launchVelocity = launchDir * effectiveLaunchSpeed;

		// Pelvis-only Velocity = launchVelocity killed travel distance: pelvis mass is a small
		// fraction of the whole ragdoll, so linear momentum was tiny and the solver drained it
		// through joints. Applying one impulse M×v at the pelvis (≈ whole-body COM) matches
		// total momentum of "every body at launchVelocity" while joints can still flex limbs
		// relative to the core during flight.
		var totalMass = mp.Mass;
		if ( pb0 != null && totalMass > 0f )
			pb0.ApplyImpulse( launchVelocity * totalMass );

		if ( EnableTackleDebugLogs && pb0 != null )
			Log.Info( $"[Tackle] After velocity | Group={group != null} Vel={pb0.Velocity}" );

		// Tag every GameObject in the ragdoll hierarchy so the floor trace at stand-up time
		// can exclude them. Without this the trace hits the ragdoll's own limbs lying on the
		// floor and reports those as the floor surface — snapping the player to knee/arm height.
		ragdollGo.Tags.Add( "ragdoll" );
		foreach ( var body in mp.Bodies )
			body.Component?.GameObject?.Tags.Add( "ragdoll" );

		// Network the ragdoll now that it already has launch velocity.
		ragdollGo.NetworkSpawn();
		victim.ragdollObject = ragdollGo;
	}

	private async void HandleRagdollRecovery( PlayerTackle victim )
	{
		var classData = victim.playerClass?.CurrentClass;
		var ragdollDuration = classData?.RagdollDuration ?? 2f;
		var invincDuration = classData?.PostTackleInvincibilityDuration ?? 1f;

		await GameTask.DelaySeconds( ragdollDuration );
		if ( !victim.IsValid() )
			return;

		// Trace straight down from the ragdoll's pelvis to find the actual floor.
		// ragdollObject.WorldPosition is the pelvis (IgnoreRoot=false) — waist height above the floor.
		// Without this, the player stands up floating at pelvis height and falls in an idle animation
		// until their controller finds the ground.
		var ragdollPos = victim.ragdollObject.IsValid()
			? victim.ragdollObject.WorldPosition
			: victim.WorldPosition;

		var tr = Scene.Trace
			.Ray( ragdollPos + Vector3.Up * 30f, ragdollPos + Vector3.Down * 200f )
			.WithoutTags( "ragdoll" )
			.Run();

		victim.NetStandUpPosition = tr.Hit ? tr.HitPosition : ragdollPos;

		if ( victim.ragdollObject.IsValid() )
			victim.ragdollObject.Destroy();
		victim.ragdollObject = null;

		victim.NetIsRagdolled = false;

		victim.NetIsTackleImmune = true;
		await GameTask.DelaySeconds( invincDuration );
		if ( !victim.IsValid() )
			return;

		victim.NetIsTackleImmune = false;
	}
}
