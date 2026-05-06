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

		// Body = Dresser target when present (matches cosmetics); otherwise first skinned mesh.
		var dresser = victim.Components.Get<Dresser>( FindMode.EverythingInSelfAndDescendants );
		var baseVictimRenderer = dresser.IsValid() && dresser.BodyTarget.IsValid()
			? dresser.BodyTarget
			: victim.Components.Get<SkinnedModelRenderer>( FindMode.EverythingInSelfAndDescendants );

		var primaryRenderer = ragdollGo.AddComponent<SkinnedModelRenderer>();
		primaryRenderer.Model = baseVictimRenderer?.Model;

		var ragdollPhysics = ragdollGo.AddComponent<ModelPhysics>();
		ragdollPhysics.Renderer = primaryRenderer;
		ragdollPhysics.MotionEnabled = true;
		ragdollPhysics.IgnoreRoot = false;
		ragdollPhysics.Enabled = true;

		if ( baseVictimRenderer != null )
			ragdollPhysics.CopyBonesFrom( baseVictimRenderer, true );

		if ( baseVictimRenderer != null )
			AddVictimClothingToRagdoll( victim, ragdollGo, primaryRenderer, baseVictimRenderer );

		// Network as soon as the mesh exists so we don't sit in a hole where the player is hidden
		// (NetIsRagdolled) but the ragdoll object hasn't replicated yet. Impulse is applied after
		// a short host delay so physics bodies exist (see RagdollPhysicsInitDelay).
		ragdollGo.Tags.Add( "ragdoll" );
		victim.ragdollObject = ragdollGo;
		ragdollGo.NetworkSpawn();

		var initDelay = RagdollPhysicsInitDelay.Clamp( 0.01f, 0.25f );
		await GameTask.DelaySeconds( initDelay );
		if ( !ragdollGo.IsValid() )
			return;

		var mp = ragdollGo.Components.Get<ModelPhysics>();
		if ( mp == null || mp.Bodies.Count == 0 )
			return;

		var pb0 = mp.Bodies[0].Component?.PhysicsBody;
		var group = pb0?.PhysicsGroup;

		if ( EnableTackleDebugLogs )
			Log.Info( $"[Tackle] Post-network impulse | Bodies={mp.Bodies.Count} BodyType[0]={pb0?.BodyType} Group={group != null}" );

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

		// Tag every physics body for stand-up floor traces.
		foreach ( var body in mp.Bodies )
			body.Component?.GameObject?.Tags.Add( "ragdoll" );
	}

	/// <summary>
	/// Replicates the victim's extra skinned meshes (cosmetics) on the ragdoll by merging skinning to the physics body.
	/// </summary>
	private static void AddVictimClothingToRagdoll(
		PlayerTackle victim,
		GameObject ragdollRoot,
		SkinnedModelRenderer ragdollBody,
		SkinnedModelRenderer victimBody )
	{
		foreach ( var src in victim.Components.GetAll<SkinnedModelRenderer>( FindMode.EverythingInSelfAndDescendants ) )
		{
			if ( !src.IsValid() || src == victimBody || src.Model is null )
				continue;

			var pieceGo = new GameObject( true, src.GameObject.Name );
			pieceGo.Parent = ragdollRoot;
			pieceGo.LocalPosition = Vector3.Zero;
			pieceGo.LocalRotation = Rotation.Identity;
			pieceGo.LocalScale = 1f;
			pieceGo.Tags.Add( "ragdoll" );

			var dst = pieceGo.AddComponent<SkinnedModelRenderer>();
			dst.CopyFrom( src );
			dst.BoneMergeTarget = ragdollBody;
			dst.UseAnimGraph = false;
			dst.CreateBoneObjects = false;
			dst.LodOverride = 0;
			dst.Enabled = src.Enabled;
		}
	}

	private async void HandleRagdollRecovery( PlayerTackle victim )
	{
		var classData = victim.playerClass?.CurrentClass;
		var downTimeAfterGrounded = classData?.RagdollDuration ?? 2f;
		var maxTotalRagdoll = classData?.RagdollMaxDuration ?? 8f;
		var groundSpeedMax = classData?.RagdollGroundSpeedMax ?? 160f;
		var groundTraceDown = classData?.RagdollGroundTraceDown ?? 120f;
		var groundTraceUp = classData?.RagdollGroundTraceUp ?? 24f;
		var invincDuration = classData?.PostTackleInvincibilityDuration ?? 1f;

		var scene = victim.Scene;
		var started = Time.Now;
		var groundedAccum = 0f;
		const float pollSeconds = 0.05f;

		// SpawnRagdollObject is async; wait briefly so ragdollObject exists on the host.
		while ( victim.IsValid() && !victim.ragdollObject.IsValid() && Time.Now - started < 2f )
			await GameTask.DelaySeconds( pollSeconds );

		while ( victim.IsValid() )
		{
			if ( Time.Now - started >= maxTotalRagdoll )
				break;

			var ragdoll = victim.ragdollObject;
			if ( !ragdoll.IsValid() )
				break;

			if ( IsRagdollGroundedAndSettled( ragdoll, scene, groundTraceUp, groundTraceDown, groundSpeedMax ) )
				groundedAccum += pollSeconds;
			else
				groundedAccum = 0f;

			if ( groundedAccum >= downTimeAfterGrounded )
				break;

			await GameTask.DelaySeconds( pollSeconds );
		}

		if ( !victim.IsValid() )
			return;

		// Trace straight down from the ragdoll's pelvis to find the actual floor.
		// ragdollObject.WorldPosition is the pelvis (IgnoreRoot=false) — waist height above the floor.
		// Without this, the player stands up floating at pelvis height and falls in an idle animation
		// until their controller finds the ground.
		var ragdollPos = victim.ragdollObject.IsValid()
			? victim.ragdollObject.WorldPosition
			: victim.WorldPosition;

		var tr = scene.Trace
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

	/// <summary>Pelvis near floor (trace) and not still moving fast from flight or bounce.</summary>
	private static bool IsRagdollGroundedAndSettled(
		GameObject ragdollRoot,
		Scene scene,
		float traceUp,
		float traceDown,
		float maxPelvisSpeed )
	{
		if ( !ragdollRoot.IsValid() )
			return false;

		var pos = ragdollRoot.WorldPosition;
		var tr = scene.Trace
			.Ray( pos + Vector3.Up * traceUp, pos + Vector3.Down * traceDown )
			.WithoutTags( "ragdoll" )
			.Run();
		if ( !tr.Hit )
			return false;

		var mp = ragdollRoot.Components.Get<ModelPhysics>();
		if ( mp == null || mp.Bodies.Count == 0 )
			return false;

		var pelvisBody = mp.Bodies[0].Component?.PhysicsBody;
		if ( pelvisBody == null )
			return false;

		return pelvisBody.Velocity.Length <= maxPelvisSpeed;
	}
}
