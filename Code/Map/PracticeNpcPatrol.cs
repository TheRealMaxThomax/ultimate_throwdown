using Sandbox;

/// <summary>
/// Host-driven practice dummy: ping-pong between two waypoints at charge tier speed with instant 180° turns.
/// Requires <see cref="CitizenAvatarLod.PracticeNpcTag"/>; pauses while knocked down and resumes after stand-up snap-back.
/// Position is host-teleported each fixed tick; clients mirror pose via <see cref="PracticeNpcPatrolPoseRelay"/>.
/// Run legs use animgraph <c>move_groundspeed</c> — not <see cref="PlayerController"/> (scene NPCs throw if PC is enabled).
/// </summary>
[Order( -50 )]
public sealed class PracticeNpcPatrol : PracticeNpcPatrolHostState, Component.ExecuteInEditor
{
	private const string MoveGroundSpeedParam = "move_groundspeed";
	private const string MoveXParam = "move_x";
	private const string MoveYParam = "move_y";

	[Property] public GameObject PointA { get; set; }
	[Property] public GameObject PointB { get; set; }

	/// <summary> 0 = use <see cref="ClassData.CatchUpMoveSpeed"/> from <see cref="PlayerClass"/> (fallback 320). </summary>
	[Property] public float MoveSpeedOverride { get; set; }

	[Property] public float ArrivalThreshold { get; set; } = 8f;

	/// <summary> Seconds for one full <c>charge_run_cycle</c> scrub while patrolling. </summary>
	[Property] public float ChargeRunCycleSeconds { get; set; } = 0.55f;

	[Property] public SkinnedModelRenderer BodyRenderer { get; set; }

	[Property] public Color PathGizmoColor { get; set; } = new( 0.35f, 0.85f, 1f, 0.9f );

	/// <summary> Fixed tackle charge bonus override. 0 = use class <c>TackleChargeRampRate</c> / <c>MaxTackleChargeBonus</c> (same ramp logic as players); &gt;0 = forced floor regardless of class stats. </summary>
	[Property] public float TackleChargeBonus { get; set; } = 0f;

	public override float PatrolTackleChargeBonus => TackleChargeBonus;

	[Property] public bool EnableDebugLogs { get; set; }

	private bool netIsPatrolling;

	private float syncedChargeRunCycle;

	private Vector3 netPatrolMoveDirection;

	private float netPatrolMoveSpeed;

	public override bool IsPatrollingAtChargeSpeed => netIsPatrolling;

	public override float NetChargeRunCycle => syncedChargeRunCycle;

	private PlayerTackle playerTackle;
	private PlayerClass playerClass;
	private GameObject travelTarget;
	private bool patrolInitialized;

	protected override void OnStart()
	{
		playerTackle = Components.Get<PlayerTackle>();
		playerClass = Components.Get<PlayerClass>();

		if ( !GameObject.Tags.Has( CitizenAvatarLod.PracticeNpcTag ) )
			Log.Warning( $"[PracticeNpcPatrol] {GameObject.Name} is missing tag '{CitizenAvatarLod.PracticeNpcTag}'." );

		Components.Get<PlayerBallHoldAnim>()?.EnsureCustomBodyModel();
		ResolveBodyRenderer();

		if ( Networking.IsHost && Game.IsPlaying )
			TryInitializePatrol();
	}

	protected override void OnUpdate()
	{
		if ( !Game.IsPlaying )
			return;

		if ( netIsPatrolling && !IsMovementBlocked() )
			ApplyPatrolLocomotionAnim( netPatrolMoveSpeed );
		else
			ClearPatrolLocomotionAnim();
	}

	protected override void OnFixedUpdate()
	{
		if ( !Game.IsPlaying || !Networking.IsHost || IsProxy )
			return;

		if ( !HasValidPath() )
		{
			SetPatrolActive( false );
			return;
		}

		RunPatrolStep();
	}

	/// <summary>Host relay: latest authoritative pose for client mirror.</summary>
	public void GetPatrolPoseForClientSync(
		out Vector3 worldPosition,
		out Rotation worldRotation,
		out bool isPatrolling,
		out float moveSpeed,
		out float chargeRunCycle )
	{
		worldPosition = WorldPosition;
		worldRotation = WorldRotation;
		isPatrolling = netIsPatrolling;
		moveSpeed = netPatrolMoveSpeed;
		chargeRunCycle = syncedChargeRunCycle;
	}

	/// <summary>Client: snap to host patrol pose (skipped while knocked down — knockdown RPCs own transform).</summary>
	public void ApplyHostPatrolPoseFromRelay(
		Vector3 worldPosition,
		Rotation worldRotation,
		bool isPatrolling,
		float moveSpeed,
		float chargeRunCycle )
	{
		if ( Networking.IsHost )
			return;

		netIsPatrolling = isPatrolling;
		netPatrolMoveSpeed = moveSpeed;
		syncedChargeRunCycle = chargeRunCycle;

		if ( IsMovementBlocked() )
			return;

		WorldPosition = worldPosition;
		WorldRotation = worldRotation;
	}

	private void RunPatrolStep()
	{
		TryInitializePatrol();

		if ( IsMovementBlocked() )
		{
			SetPatrolActive( false );
			return;
		}

		SetPatrolActive( true );
		StepPatrol( Time.Delta );
	}

	public override bool TryGetHostTackleMove( out Vector3 horizontalVelocity, out Vector3 approachDirection )
	{
		horizontalVelocity = default;
		approachDirection = default;

		if ( !Networking.IsHost || !netIsPatrolling )
			return false;

		var dir = netPatrolMoveDirection.WithZ( 0f );
		if ( dir.Length < 0.001f )
			return false;

		approachDirection = dir.Normal;
		horizontalVelocity = approachDirection * netPatrolMoveSpeed;
		return horizontalVelocity.Length >= 1f;
	}

	private void TryInitializePatrol()
	{
		if ( patrolInitialized || !HasValidPath() )
			return;

		travelTarget = PickInitialTravelTarget();
		FaceToward( GetHorizontalDirectionTo( travelTarget ) );
		var initDir = GetHorizontalDirectionTo( travelTarget );
		if ( initDir.LengthSquared >= 0.0001f )
			SyncPatrolMoveState( initDir.Normal, ResolveMoveSpeed() );
		patrolInitialized = true;

		if ( EnableDebugLogs )
			Log.Info( $"[PracticeNpcPatrol] {GameObject.Name} init → {travelTarget.Name}" );
	}

	private bool HasValidPath()
	{
		if ( !PointA.IsValid() || !PointB.IsValid() )
			return false;

		return GetHorizontalDistance( PointA.WorldPosition, PointB.WorldPosition ) > 1f;
	}

	private bool IsMovementBlocked()
	{
		if ( playerTackle is { IsKnockedDown: true } )
			return true;

		// After landing a tackle, freeze in place during the cooldown window so the victim
		// gets a clean launch impulse instead of being continuously shoved by a charging NPC.
		return playerTackle is { IsInTackleCooldown: true };
	}

	private void SetPatrolActive( bool active )
	{
		netIsPatrolling = active;
		if ( !active )
		{
			netPatrolMoveDirection = Vector3.Zero;
			netPatrolMoveSpeed = 0f;
		}
	}

	private void StepPatrol( float delta )
	{
		if ( !travelTarget.IsValid() )
		{
			travelTarget = PickInitialTravelTarget();
			if ( !travelTarget.IsValid() )
				return;
		}

		var speed = ResolveMoveSpeed();
		if ( speed <= 0f )
			return;

		AdvanceChargeRunCycle( speed, delta );

		var pos = WorldPosition;
		var targetPos = travelTarget.WorldPosition;
		var toTarget = targetPos - pos;
		var horizontal = toTarget.WithZ( 0f );
		var dist = horizontal.Length;

		if ( dist <= ArrivalThreshold )
		{
			ArriveAtEndpoint( targetPos );
			return;
		}

		var dir = horizontal / dist;
		var step = speed * delta;

		if ( step >= dist )
		{
			ArriveAtEndpoint( targetPos );
			return;
		}

		WorldPosition = pos + dir * step;
		FaceToward( dir );
		SyncPatrolMoveState( dir, speed );
	}

	private void ArriveAtEndpoint( Vector3 endpointPos )
	{
		WorldPosition = endpointPos;
		travelTarget = GetOppositeEndpoint( travelTarget );
		ApplyInstantTurnAround();

		var dir = GetHorizontalDirectionTo( travelTarget );
		if ( dir.LengthSquared >= 0.0001f )
			SyncPatrolMoveState( dir.Normal, ResolveMoveSpeed() );
		else
			SyncPatrolMoveState( WorldRotation.Forward.WithZ( 0f ).Normal, ResolveMoveSpeed() );

		if ( EnableDebugLogs )
			Log.Info( $"[PracticeNpcPatrol] {GameObject.Name} turn → {travelTarget.Name}" );
	}

	private void SyncPatrolMoveState( Vector3 horizontalDirection, float speed )
	{
		var dir = horizontalDirection.WithZ( 0f );
		netPatrolMoveDirection = dir.Length >= 0.001f ? dir.Normal : Vector3.Zero;
		netPatrolMoveSpeed = speed;
	}

	private void ApplyInstantTurnAround()
	{
		var yaw = WorldRotation.Angles();
		yaw.yaw += 180f;
		WorldRotation = yaw.ToRotation();
	}

	private void FaceToward( Vector3 horizontalDirection )
	{
		if ( horizontalDirection.LengthSquared < 0.0001f )
			return;

		WorldRotation = Rotation.LookAt( horizontalDirection.Normal, Vector3.Up );
	}

	/// <summary>
	/// Citizen locomotion blend. Requires all three velocity params — the animgraph locomotion
	/// layer uses <c>move_x</c> (local forward velocity) to leave idle; <c>move_groundspeed</c>
	/// alone is not enough. NPC always moves forward in local space so move_x = speed, move_y = 0.
	/// </summary>
	private void ApplyPatrolLocomotionAnim( float speed )
	{
		if ( !TryGetBodyRenderer( out var renderer ) || speed <= 0f )
			return;

		renderer.Set( MoveGroundSpeedParam, speed );
		renderer.Set( MoveXParam, speed );
		renderer.Set( MoveYParam, 0f );
	}

	private void ClearPatrolLocomotionAnim()
	{
		if ( !TryGetBodyRenderer( out var renderer ) )
			return;

		renderer.Set( MoveGroundSpeedParam, 0f );
		renderer.Set( MoveYParam, 0f );
		renderer.Set( MoveXParam, 0f );
	}

	private void ResolveBodyRenderer()
	{
		if ( BodyRenderer.IsValid() )
			return;

		BodyRenderer = Components.Get<SkinnedModelRenderer>( FindMode.EverythingInDescendants );
	}

	private bool TryGetBodyRenderer( out SkinnedModelRenderer renderer )
	{
		ResolveBodyRenderer();
		renderer = BodyRenderer;
		return renderer.IsValid() && renderer.UseAnimGraph;
	}

	private GameObject PickInitialTravelTarget()
	{
		var distA = GetHorizontalDistance( WorldPosition, PointA.WorldPosition );
		var distB = GetHorizontalDistance( WorldPosition, PointB.WorldPosition );
		return distA >= distB ? PointB : PointA;
	}

	private GameObject GetOppositeEndpoint( GameObject current )
	{
		if ( current == PointA )
			return PointB;

		if ( current == PointB )
			return PointA;

		return PickInitialTravelTarget();
	}

	private Vector3 GetHorizontalDirectionTo( GameObject target )
	{
		if ( !target.IsValid() )
			return Vector3.Zero;

		return (target.WorldPosition - WorldPosition).WithZ( 0f );
	}

	private float ResolveMoveSpeed()
	{
		if ( MoveSpeedOverride > 0f )
			return MoveSpeedOverride;

		var catchUp = playerClass?.CurrentClass?.CatchUpMoveSpeed;
		return catchUp is > 0f ? catchUp.Value : 320f;
	}

	private void AdvanceChargeRunCycle( float speed, float delta )
	{
		var cycleSeconds = ChargeRunCycleSeconds.Clamp( 0.05f, 4f );
		var cycleDelta = delta / cycleSeconds;
		cycleDelta *= speed / 320f;
		syncedChargeRunCycle = (syncedChargeRunCycle + cycleDelta) % 1f;
	}

	private static float GetHorizontalDistance( Vector3 a, Vector3 b )
	{
		return (b - a).WithZ( 0f ).Length;
	}

	protected override void DrawGizmos()
	{
		if ( !PointA.IsValid() || !PointB.IsValid() )
			return;

		var a = PointA.WorldPosition;
		var b = PointB.WorldPosition;
		Gizmo.Draw.Color = PathGizmoColor;
		Gizmo.Draw.Line( a, b );
		Gizmo.Draw.Color = PathGizmoColor.WithAlpha( 1f );
		Gizmo.Draw.SolidSphere( a, 6f );
		Gizmo.Draw.SolidSphere( b, 6f );
	}
}
