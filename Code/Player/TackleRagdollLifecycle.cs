using Sandbox;
using System;
using System.Threading.Tasks;

/// <summary>
/// Host-owned ragdoll spawn, launch impulse, recovery polling, and destroy — sibling of <see cref="PlayerTackle"/>.
/// </summary>
public sealed class TackleRagdollLifecycle : Component
{
	private GameObject ragdollObject;
	private PlayerTackle tackle;

	public GameObject RagdollObject => ragdollObject;

	protected override void OnStart()
	{
		tackle = Components.Get<PlayerTackle>();
	}

	public void DestroyRagdollObjectOnHost()
	{
		if ( ragdollObject.IsValid() )
			ragdollObject.Destroy();
		ragdollObject = null;
	}

	public Vector3 ComputeStandUpPositionFromRagdoll()
	{
		if ( tackle.RagdollLifecycle_TryConsumePracticeNpcStandUpSnap( out var practicePos, out _ ) )
			return practicePos;

		var ragdollPos = ragdollObject.IsValid() ? ragdollObject.WorldPosition : WorldPosition;
		return TraceStandUpPosition( Scene, ragdollPos );
	}

	public void SpawnRagdollObject(
		Vector3 tackleDir,
		float effectiveLaunchSpeed,
		float launchArc,
		bool usePreLaunchPause,
		float preLaunchPauseSeconds,
		bool speedBlitzKnockdown = false,
		PlayerTackle attacker = null,
		float comicTacklePower = 0f,
		float? preLaunchPauseStartedAt = null )
	{
		SpawnRagdollObjectAsync(
			tackleDir,
			effectiveLaunchSpeed,
			launchArc,
			usePreLaunchPause,
			preLaunchPauseSeconds,
			speedBlitzKnockdown,
			attacker,
			comicTacklePower,
			preLaunchPauseStartedAt );
	}

	public void BeginRagdollRecovery()
	{
		HandleRagdollRecoveryAsync();
	}

	private async void SpawnRagdollObjectAsync(
		Vector3 tackleDir,
		float effectiveLaunchSpeed,
		float launchArc,
		bool usePreLaunchPause,
		float preLaunchPauseSeconds,
		bool speedBlitzKnockdown,
		PlayerTackle attacker,
		float comicTacklePower,
		float? preLaunchPauseStartedAt )
	{
		var pauseStartedAt = usePreLaunchPause
			? (preLaunchPauseStartedAt ?? Time.Now)
			: 0f;

		var ragdollGo = new GameObject( true, "PlayerRagdoll" );
		var spawnPos = usePreLaunchPause ? tackle.GetKnockdownFreezePosition() : WorldPosition + Vector3.Up * 10f;
		ragdollGo.WorldPosition = spawnPos;
		ragdollGo.WorldRotation = WorldRotation;

		var modelScale = tackle.GetTackleClassData()?.ModelScale ?? 1f;
		if ( modelScale <= 0f )
			modelScale = 1f;
		ragdollGo.LocalScale = Vector3.One * modelScale;

		var dresser = Components.Get<Dresser>( FindMode.EverythingInSelfAndDescendants );
		var baseVictimRenderer = dresser.IsValid && dresser.BodyTarget.IsValid()
			? dresser.BodyTarget
			: Components.Get<SkinnedModelRenderer>( FindMode.EverythingInSelfAndDescendants );

		var primaryRenderer = ragdollGo.AddComponent<SkinnedModelRenderer>();
		if ( baseVictimRenderer.IsValid )
		{
			primaryRenderer.CopyFrom( baseVictimRenderer );
			primaryRenderer.UseAnimGraph = false;
			primaryRenderer.CreateBoneObjects = false;
			primaryRenderer.LodOverride = 0;
		}
		else
		{
			primaryRenderer.Model = null;
		}

		var ragdollPhysics = ragdollGo.AddComponent<ModelPhysics>();
		ragdollPhysics.Renderer = primaryRenderer;
		ragdollPhysics.MotionEnabled = false;
		ragdollPhysics.IgnoreRoot = false;
		ragdollPhysics.Enabled = true;

		if ( baseVictimRenderer != null )
			ragdollPhysics.CopyBonesFrom( baseVictimRenderer, true );

		if ( baseVictimRenderer != null )
			AddVictimClothingToRagdoll( ragdollGo, primaryRenderer, baseVictimRenderer );

		if ( usePreLaunchPause )
			SetRagdollRenderersEnabled( ragdollGo, false );

		ragdollGo.Tags.Add( "ragdoll" );
		ragdollObject = ragdollGo;
		ragdollGo.Components.GetOrCreate<RagdollEnemyOutline>().ConfigureFromVictim( tackle );

		ragdollPhysics.MotionEnabled = true;

		var waitForBodies = tackle.RagdollPhysicsInitDelay.Clamp( 0.01f, 0.25f );
		var bodiesReady = await WaitForRagdollBodiesAsync( ragdollGo, waitForBodies );
		if ( !ragdollGo.IsValid() )
			return;

		var pause = preLaunchPauseSeconds.Clamp( 0f, 1.5f );
		if ( usePreLaunchPause && pause > 0.0001f )
		{
			if ( baseVictimRenderer != null )
				ragdollPhysics.CopyBonesFrom( baseVictimRenderer, true );

			FreezeRagdollPhysics( ragdollPhysics );

			var elapsedPause = Time.Now - pauseStartedAt;
			var remainingPause = Math.Max( 0f, pause - elapsedPause );

			if ( tackle.EnableTackleDebugLogs )
				Log.Info( $"[Tackle] Pre-launch pause | bodies={bodiesReady} pause={pause * 1000f:F0}ms remaining={remainingPause * 1000f:F0}ms victim-visible" );

			if ( remainingPause > 0.0001f )
				await GameTask.DelaySeconds( remainingPause );

			if ( !ragdollGo.IsValid() || !tackle.IsValid )
				return;

			SetRagdollRenderersEnabled( ragdollGo, true );
			ragdollPhysics.MotionEnabled = true;
			var launched = TryApplyRagdollLaunchImpulse( ragdollGo, tackleDir, effectiveLaunchSpeed, launchArc, out var bodyCount );

			if ( speedBlitzKnockdown && launched && Networking.IsHost )
			{
				attacker?.Components.Get<SpeedsterSpeedBlitzUlt>()?.EndConnectHangOnHostAtLaunch();

				tackle.Components.Get<TackleImpactRelay>()?.BroadcastSpeedBlitzLaunchSound(
					spawnPos,
					SpeedsterSpeedBlitzUlt.ResolveLaunchSoundResourcePath( attacker ),
					SpeedsterSpeedBlitzUlt.ResolveLaunchSoundVolume( attacker ) );

				try
				{
					TackleComicTextHud.NotifyHostKnockdown(
						Scene,
						ragdollGo.WorldPosition,
						comicTacklePower,
						tackleDir,
						TackleComicTextHud.ComicBurstPalette.Ult );
				}
				catch ( Exception ex )
				{
					Log.Warning( $"[Tackle] Speed Blitz comic text spawn failed: {ex.Message}" );
				}
			}

			ragdollGo.NetworkSpawn();

			tackle.RagdollLifecycle_NotifyLaunchedAfterPreLaunchPause( ragdollGo.WorldPosition, attacker );

			if ( tackle.EnableTackleDebugLogs )
				Log.Info( $"[Tackle] Post-pause impulse + spawn | launched={launched} bodies={bodyCount}" );
		}
		else
		{
			var launched = bodiesReady && TryApplyRagdollLaunchImpulse( ragdollGo, tackleDir, effectiveLaunchSpeed, launchArc, out _ );
			if ( !ragdollGo.IsValid() )
				return;

			if ( tackle.EnableTackleDebugLogs )
				Log.Info( $"[Tackle] NetworkSpawn after impulse | launched={launched}" );

			ragdollGo.NetworkSpawn();
		}
	}

	private async Task<bool> WaitForRagdollBodiesAsync( GameObject ragdollGo, float maxWaitSeconds )
	{
		const float pollStepSeconds = 0.008f;
		var started = Time.Now;

		while ( ragdollGo.IsValid() && Time.Now - started < maxWaitSeconds )
		{
			if ( HasRagdollLaunchBodies( ragdollGo ) )
			{
				if ( tackle.EnableTackleDebugLogs )
					Log.Info( $"[Tackle] Bodies ready in {(Time.Now - started) * 1000f:F0}ms" );
				return true;
			}

			await GameTask.DelaySeconds( pollStepSeconds );
		}

		return ragdollGo.IsValid() && HasRagdollLaunchBodies( ragdollGo );
	}

	private static bool HasRagdollLaunchBodies( GameObject ragdollGo )
	{
		var mp = ragdollGo.Components.Get<ModelPhysics>();
		if ( mp == null || mp.Bodies.Count == 0 )
			return false;

		return mp.Bodies[0].Component?.PhysicsBody != null;
	}

	private static void SetRagdollRenderersEnabled( GameObject ragdollGo, bool enabled )
	{
		foreach ( var renderer in ragdollGo.Components.GetAll<SkinnedModelRenderer>( FindMode.EverythingInSelfAndDescendants ) )
		{
			if ( renderer.IsValid )
				renderer.Enabled = enabled;
		}
	}

	private static void FreezeRagdollPhysics( ModelPhysics ragdollPhysics )
	{
		if ( ragdollPhysics == null )
			return;

		ragdollPhysics.MotionEnabled = false;

		foreach ( var body in ragdollPhysics.Bodies )
		{
			var pb = body.Component?.PhysicsBody;
			if ( pb == null )
				continue;

			pb.Velocity = Vector3.Zero;
			pb.AngularVelocity = Vector3.Zero;
		}
	}

	private bool TryApplyRagdollLaunchImpulse(
		GameObject ragdollGo,
		Vector3 tackleDir,
		float effectiveLaunchSpeed,
		float launchArc,
		out int bodyCount )
	{
		bodyCount = 0;
		var mp = ragdollGo.Components.Get<ModelPhysics>();
		if ( mp == null || mp.Bodies.Count == 0 )
			return false;

		bodyCount = mp.Bodies.Count;
		var pb0 = mp.Bodies[0].Component?.PhysicsBody;
		if ( pb0 == null )
			return false;

		var launchDir = (tackleDir + Vector3.Up * launchArc).Normal;
		var launchVelocity = launchDir * effectiveLaunchSpeed;
		var totalMass = mp.Mass;
		if ( totalMass <= 0f )
			return false;

		pb0.ApplyImpulse( launchVelocity * totalMass );

		foreach ( var body in mp.Bodies )
			body.Component?.GameObject?.Tags.Add( "ragdoll" );

		if ( tackle.EnableTackleDebugLogs )
			Log.Info( $"[Tackle] ApplyImpulse | Bodies={bodyCount} Vel={pb0.Velocity}" );

		return true;
	}

	private void AddVictimClothingToRagdoll(
		GameObject ragdollRoot,
		SkinnedModelRenderer ragdollBody,
		SkinnedModelRenderer victimBody )
	{
		foreach ( var src in Components.GetAll<SkinnedModelRenderer>( FindMode.EverythingInSelfAndDescendants ) )
		{
			if ( !src.IsValid || src == victimBody || src.Model is null )
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

	private async void HandleRagdollRecoveryAsync()
	{
		var classData = tackle.GetTackleClassData();
		var downTimeAfterGrounded = classData?.RagdollDuration ?? 2f;
		var maxTotalRagdoll = classData?.RagdollMaxDuration ?? 8f;
		var groundSpeedMax = classData?.RagdollGroundSpeedMax ?? 160f;
		var groundTraceDown = classData?.RagdollGroundTraceDown ?? 120f;
		var groundTraceUp = classData?.RagdollGroundTraceUp ?? 24f;
		var invincDuration = classData?.PostTackleInvincibilityDuration ?? 1f;

		var scene = Scene;
		var started = Time.Now;
		var groundedAccum = 0f;
		const float pollSeconds = 0.05f;

		while ( tackle.IsValid && tackle.IsAwaitingRagdollLaunch && !tackle.IsRagdolled && Time.Now - started < 3f )
			await GameTask.DelaySeconds( pollSeconds );

		while ( tackle.IsValid && tackle.IsRagdolled && !ragdollObject.IsValid() && Time.Now - started < 2f )
			await GameTask.DelaySeconds( pollSeconds );

		while ( tackle.IsValid && tackle.IsRagdolled )
		{
			if ( Time.Now - started >= maxTotalRagdoll )
				break;

			var ragdoll = ragdollObject;
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

		if ( !tackle.IsValid || !tackle.IsRagdolled )
			return;

		var ragdollPos = ragdollObject.IsValid()
			? ragdollObject.WorldPosition
			: WorldPosition;

		Vector3 standUpPosition;
		Angles practiceNpcEyeAngles = default;
		var assignPracticeNpcEyeAngles = false;

		if ( tackle.RagdollLifecycle_TryConsumePracticeNpcStandUpSnap( out var practicePos, out practiceNpcEyeAngles ) )
		{
			standUpPosition = practicePos;
			assignPracticeNpcEyeAngles = true;
		}
		else
		{
			standUpPosition = TraceStandUpPosition( scene, ragdollPos );
		}

		DestroyRagdollObjectOnHost();
		tackle.RagdollLifecycle_CompleteStandUp( standUpPosition, practiceNpcEyeAngles, assignPracticeNpcEyeAngles );
		tackle.RagdollLifecycle_BeginPostStandUpInvincibility( invincDuration );
	}

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

	private static Vector3 TraceStandUpPosition( Scene scene, Vector3 ragdollPos )
	{
		var tr = scene.Trace
			.Ray( ragdollPos + Vector3.Up * 30f, ragdollPos + Vector3.Down * 200f )
			.WithoutTags( "ragdoll" )
			.Run();

		return tr.Hit ? tr.HitPosition : ragdollPos;
	}
}
