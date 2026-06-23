using Sandbox;

/// <summary>
/// Host-driven practice dummy: ping-pong between two waypoints at charge tier speed with instant 180° turns.
/// Requires <see cref="CitizenAvatarLod.PracticeNpcTag"/>; pauses while knocked down and resumes after stand-up snap-back.
/// Position is host-teleported (RB stays locked). Run legs use animgraph <c>move_groundspeed</c> — not <see cref="PlayerController"/> (scene NPCs throw if PC is enabled).
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

	[Property] public bool EnableDebugLogs { get; set; }

	[Sync( SyncFlags.FromHost )] private bool NetIsPatrolling { get; set; }

	[Sync( SyncFlags.FromHost )] private float SyncedChargeRunCycle { get; set; }

	[Sync( SyncFlags.FromHost )] private Vector3 NetPatrolMoveDirection { get; set; }

	[Sync( SyncFlags.FromHost )] private float NetPatrolMoveSpeed { get; set; }

	public override bool IsPatrollingAtChargeSpeed => NetIsPatrolling;

	public override float NetChargeRunCycle => SyncedChargeRunCycle;

	private PlayerTackle playerTackle;
	private PlayerClass playerClass;
	private GameObject hostTravelTarget;
	private bool hostPatrolInitialized;

	protected override void OnStart()
	{
		playerTackle = Components.Get<PlayerTackle>();
		playerClass = Components.Get<PlayerClass>();

		if ( !GameObject.Tags.Has( CitizenAvatarLod.PracticeNpcTag ) )
			Log.Warning( $"[PracticeNpcPatrol] {GameObject.Name} is missing tag '{CitizenAvatarLod.PracticeNpcTag}'." );

		Components.Get<PlayerBallHoldAnim>()?.EnsureCustomBodyModel();
		ResolveBodyRenderer();

		if ( Networking.IsHost && Game.IsPlaying )
			TryInitializeHostPatrol();
	}

	protected override void OnUpdate()
	{
		if ( !Game.IsPlaying )
			return;

		if ( NetIsPatrolling && !IsMovementBlocked() )
			ApplyPatrolLocomotionAnim( NetPatrolMoveSpeed );
		else
			ClearPatrolLocomotionAnim();
	}

	protected override void OnFixedUpdate()
	{
		if ( IsProxy || !Networking.IsHost || !Game.IsPlaying )
			return;

		if ( !HasValidPath() )
		{
			SetPatrolActive( false );
			return;
		}

		TryInitializeHostPatrol();

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

		if ( !Networking.IsHost || !NetIsPatrolling )
			return false;

		var dir = NetPatrolMoveDirection.WithZ( 0f );
		if ( dir.Length < 0.001f )
			return false;

		approachDirection = dir.Normal;
		horizontalVelocity = approachDirection * NetPatrolMoveSpeed;
		return horizontalVelocity.Length >= 1f;
	}

	private void TryInitializeHostPatrol()
	{
		if ( hostPatrolInitialized || !HasValidPath() )
			return;

		hostTravelTarget = PickInitialTravelTarget();
		FaceToward( GetHorizontalDirectionTo( hostTravelTarget ) );
		var initDir = GetHorizontalDirectionTo( hostTravelTarget );
		if ( initDir.LengthSquared >= 0.0001f )
			SyncPatrolMoveState( initDir.Normal, ResolveMoveSpeed() );
		hostPatrolInitialized = true;

		if ( EnableDebugLogs )
			Log.Info( $"[PracticeNpcPatrol] {GameObject.Name} init → {hostTravelTarget.Name}" );
	}

	private bool HasValidPath()
	{
		if ( !PointA.IsValid() || !PointB.IsValid() )
			return false;

		return GetHorizontalDistance( PointA.WorldPosition, PointB.WorldPosition ) > 1f;
	}

	private bool IsMovementBlocked()
	{
		return playerTackle is { IsKnockedDown: true };
	}

	private void SetPatrolActive( bool active )
	{
		NetIsPatrolling = active;
		if ( !active )
		{
			NetPatrolMoveDirection = Vector3.Zero;
			NetPatrolMoveSpeed = 0f;
		}
	}

	private void StepPatrol( float delta )
	{
		if ( !hostTravelTarget.IsValid() )
		{
			hostTravelTarget = PickInitialTravelTarget();
			if ( !hostTravelTarget.IsValid() )
				return;
		}

		var speed = ResolveMoveSpeed();
		if ( speed <= 0f )
			return;

		AdvanceChargeRunCycle( speed, delta );

		var pos = WorldPosition;
		var targetPos = hostTravelTarget.WorldPosition;
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
		hostTravelTarget = GetOppositeEndpoint( hostTravelTarget );
		ApplyInstantTurnAround();

		var dir = GetHorizontalDirectionTo( hostTravelTarget );
		if ( dir.LengthSquared >= 0.0001f )
			SyncPatrolMoveState( dir.Normal, ResolveMoveSpeed() );
		else
			SyncPatrolMoveState( WorldRotation.Forward.WithZ( 0f ).Normal, ResolveMoveSpeed() );

		if ( EnableDebugLogs )
			Log.Info( $"[PracticeNpcPatrol] {GameObject.Name} turn → {hostTravelTarget.Name}" );
	}

	private void SyncPatrolMoveState( Vector3 horizontalDirection, float speed )
	{
		var dir = horizontalDirection.WithZ( 0f );
		NetPatrolMoveDirection = dir.Length >= 0.001f ? dir.Normal : Vector3.Zero;
		NetPatrolMoveSpeed = speed;
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
		SyncedChargeRunCycle = (SyncedChargeRunCycle + cycleDelta) % 1f;
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
