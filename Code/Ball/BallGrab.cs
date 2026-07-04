using System;
using Sandbox;
using Sandbox.Diagnostics;

public sealed class BallGrab : Component
{
	[Property] public GameObject MainBall { get; set; }
	[Property] public string MainBallName { get; set; } = "main_ball";
	[Property] public float InteractDistance { get; set; } = 45f;
	/// <summary> Max |Z| from player root to ball — horizontal reach stays <see cref="InteractDistance"/>. </summary>
	[Property] public float MaxPickupVerticalSeparation { get; set; } = 80f;
	[Property] public string InteractAction { get; set; } = "use";
	[Property] public float PickupDelayAfterDrop { get; set; } = 0.75f;
	[Property] public float DropperNoPushWindow { get; set; } = 0.75f;
	[Property] public float DropperNoPushRadius { get; set; } = 90f;
	[Property] public float DropperMaxHorizontalSpeed { get; set; } = 60f;
	[Property] public GameObject HoldAnchor { get; set; }
	[Property] public SkinnedModelRenderer BodyRenderer { get; set; }
	[Property] public string HoldBoneName { get; set; } = "hold_R";
	[Property] public Vector3 HoldBoneLocalOffset { get; set; }
	[Property] public Angles HoldBoneLocalAngles { get; set; }
	[Property] public float DropSideOffset { get; set; } = 25f;
	[Property, Range( 0f, 2f )] public float DropVelocityScale { get; set; } = 0.5f;
	[Property] public string PromptText { get; set; } = "Pick Up With E";
	[Property] public bool EnableNetDebugLogs { get; set; } = false;

	private GameObject ballObject;
	private GameObject ballOriginalParent;
	private readonly List<Collider> ballCollidersToRestore = new();
	private readonly List<Rigidbody> ballBodiesToRestore = new();
	private bool warnedAboutDuplicateMainBallName;
	private bool isHolding;
	[Sync( SyncFlags.FromHost )] private bool NetIsHolding { get => isHolding; set => isHolding = value; }
	[Sync( SyncFlags.FromHost )] private float NetPickupBlockedRemain { get; set; }
	[Sync( SyncFlags.FromHost )] private Vector3 NetHeldBallWorldPosition { get; set; }
	[Sync( SyncFlags.FromHost )] private Rotation NetHeldBallWorldRotation { get; set; }
	[Sync( SyncFlags.FromHost )] private Vector3 NetHeldBallLinearVelocity { get; set; }
	[Sync( SyncFlags.FromHost )] private Vector3 NetHeldBallAngularVelocity { get; set; }
	private float nextAutoGrabAttemptAt;
	private float dropperNoPushUntilTime;
	private float dropInheritedSpeed;
	private bool localDropPending;
	private bool appliedClientHeldProxyState;
	public bool IsHolding => isHolding;
	public GameObject HeldBall => ballObject;
	public Vector3 SyncedBallWorldPosition => NetHeldBallWorldPosition;
	public Rotation SyncedBallWorldRotation => NetHeldBallWorldRotation;
	public Vector3 SyncedBallLinearVelocity => NetHeldBallLinearVelocity;
	public Vector3 SyncedBallAngularVelocity => NetHeldBallAngularVelocity;

	protected override void OnStart()
	{
		ResolveBodyRenderer();
		FindMainBall();
	}

	protected override void OnUpdate()
	{
		if ( Networking.IsHost && NetPickupBlockedRemain > 0f )
		{
			NetPickupBlockedRemain = MathF.Max( 0f, NetPickupBlockedRemain - Time.Delta );
		}

		if ( !isHolding )
		{
			localDropPending = false;
		}

		if ( Networking.IsHost && !localDropPending && isHolding && ballObject.IsValid() )
		{
			KeepHeldBallAttachedToAnchor();
			UpdateSyncedBallState();
		}
		else if ( Networking.IsHost )
		{
			ApplyDropperNoPushWindow();
		}
		else if ( !Networking.IsHost && ballObject.IsValid() )
		{
			ApplyClientHeldVisualState();
		}

		if ( isHolding && !ballObject.IsValid() )
		{
			ResetHoldingState();
			FindMainBall();
			return;
		}

		if ( isHolding && Input.Pressed( InteractAction ) && IsMatchGameplayInputAllowed() )
		{
			localDropPending = true;
			var localVelocity = (Components.Get<PlayerController>()?.Velocity ?? Vector3.Zero) * DropVelocityScale;
			RequestDropBallOnHost( localVelocity );
			return;
		}

		if ( !ballObject.IsValid() )
		{
			FindMainBall();
			return;
		}

		if ( Networking.IsHost )
		{
			UpdateSyncedBallState();
		}

		var inRange = IsBallInPickupRange( ballObject.WorldPosition );

		if ( !Network.IsOwner )
			return;

		// Auto-grab: entering grab range picks up the ball automatically.
		// Rate-limited to avoid RPC spam; host validates and ignores if already held elsewhere.
		if ( !isHolding && inRange && IsMatchGameplayInputAllowed() && PlayerAllowsBallPickup() && !IsMainBallHeldByAnyone() && NetPickupBlockedRemain <= 0f && Time.Now >= nextAutoGrabAttemptAt )
		{
			RequestPickUpBallOnHost();
			nextAutoGrabAttemptAt = Time.Now + 0.1f;
		}
	}

	private void FindMainBall()
	{
		if ( MainBall.IsValid() )
		{
			ballObject = MainBall;
			ballObject.Components.GetOrCreate<BallCarrierOutline>();
			return;
		}

		GameObject firstMatch = null;
		var matches = 0;

		foreach ( var go in Scene.GetAllObjects( true ) )
		{
			if ( go.Name != MainBallName )
				continue;

			matches++;
			if ( !firstMatch.IsValid() )
			{
				firstMatch = go;
			}
		}

		if ( matches > 1 && !warnedAboutDuplicateMainBallName )
		{
			Log.Warning( $"BallGrab found {matches} objects named '{MainBallName}'. Using the first match. Set MainBall to avoid ambiguity." );
			warnedAboutDuplicateMainBallName = true;
		}

		ballObject = firstMatch;

		if ( ballObject.IsValid() )
			ballObject.Components.GetOrCreate<BallCarrierOutline>();
	}

	/// <summary>Returns true when any player's BallGrab is holding our main ball (synced/host state).</summary>
	private bool IsMainBallHeldByAnyone()
	{
		if ( !ballObject.IsValid() )
			return false;

		foreach ( var grab in Scene.GetAllComponents<BallGrab>() )
		{
			if ( !grab.IsHolding )
				continue;

			var held = grab.HeldBall;
			if ( held.IsValid() && held == ballObject )
				return true;
		}

		return false;
	}

	private bool PlayerAllowsBallPickup()
	{
		if ( Components.Get<PlayerTackle>() is { IsRagdolled: true } )
			return false;

		// No ball pickup while an ult is running (Speed Blitz wind-up / dash).
		if ( Components.Get<SpeedsterSpeedBlitzUlt>() is { BlocksBallPickup: true } )
			return false;

		return true;
	}

	private void PickUpBall()
	{
		if ( !ballObject.IsValid() || isHolding )
			return;

		ballOriginalParent = ballObject.Parent;

		ApplyHoldAnchorTransformToBall();

		ballBodiesToRestore.Clear();
		foreach ( var body in ballObject.Components.GetAll<Rigidbody>( FindMode.EverythingInSelfAndDescendants ) )
		{
			if ( !body.IsValid() )
				continue;

			body.Enabled = false;
			ballBodiesToRestore.Add( body );
		}

		ballCollidersToRestore.Clear();
		foreach ( var collider in ballObject.Components.GetAll<Collider>( FindMode.EverythingInSelfAndDescendants ) )
		{
			if ( !collider.IsValid() )
				continue;

			collider.Enabled = false;
			ballCollidersToRestore.Add( collider );
		}

		NetIsHolding = true;
	}

	private void DropBall( Vector3 playerVelocity = default )
	{
		ReleaseHeldBall( playerVelocity );
	}

	[Rpc.Host]
	private void RequestPickUpBallOnHost()
	{
		if ( !IsMatchGameplayInputAllowed() )
			return;

		var hostDistanceToBall = ballObject.IsValid()
			? GetPickupHorizontalDistanceToBall( ballObject.WorldPosition )
			: -1f;

		if ( EnableNetDebugLogs )
		{
			Log.Info( $"[NetDebug] Host pickup request received. Caller={Rpc.Caller.DisplayName} HolderObject={GameObject.Name} BallValid={ballObject.IsValid()} IsHolding={isHolding} HostHorizontalToBall={hostDistanceToBall}" );
		}

		if ( !ballObject.IsValid() || isHolding )
			return;

		if ( IsMainBallHeldByAnyone() )
			return;

		if ( !PlayerAllowsBallPickup() )
			return;

		if ( NetPickupBlockedRemain > 0f )
			return;

		if ( !IsBallInPickupRange( ballObject.WorldPosition ) )
			return;

		PickUpBall();
		AssignBallOwner( Connection.Host );
		BallPassAssistState.GetOrCreate( ballObject )?.NotifyPickupOnHost( GameObject );
		if ( EnableNetDebugLogs )
		{
			Log.Info( $"[NetDebug] Host approved pickup. HolderObject={GameObject.Name}" );
		}
	}

	[Rpc.Host]
	private void RequestDropBallOnHost( Vector3 playerVelocity )
	{
		if ( !IsMatchGameplayInputAllowed() )
			return;

		if ( EnableNetDebugLogs )
		{
			Log.Info( $"[NetDebug] Host drop request received. Caller={Rpc.Caller.DisplayName} IsHolding={isHolding}" );
		}

		if ( !isHolding )
			return;

		AssignBallOwner( Connection.Host );
		var releasedBall = ReleaseHeldBall( playerVelocity );
		if ( releasedBall.IsValid() )
			BallLastTouchLedger.GetOrCreate( releasedBall )?.NotifyTouchOnHost( GameObject, GameObject.WorldPosition );

		BlockPickupForSeconds( PickupDelayAfterDrop );
		nextAutoGrabAttemptAt = Time.Now + PickupDelayAfterDrop;
		dropperNoPushUntilTime = Time.Now + DropperNoPushWindow;
		localDropPending = false;
	}

	private void ApplyDropperNoPushWindow()
	{
		if ( Time.Now >= dropperNoPushUntilTime || isHolding || !ballObject.IsValid() )
			return;

		var ballBody = GetPrimaryBallBody();
		if ( !ballBody.IsValid() )
			return;

		var toBall = ballObject.WorldPosition - WorldPosition;
		var horizontalDistance = toBall.WithZ( 0f ).Length;
		if ( horizontalDistance > DropperNoPushRadius )
			return;

		var horizontalVelocity = ballBody.Velocity.WithZ( 0f );
		if ( horizontalVelocity.Length <= DropperMaxHorizontalSpeed )
			return;

		var maxAllowed = MathF.Max( DropperMaxHorizontalSpeed, dropInheritedSpeed );
		var clampedHorizontal = horizontalVelocity.Normal * maxAllowed;
		ballBody.Velocity = new Vector3( clampedHorizontal.x, clampedHorizontal.y, ballBody.Velocity.z );
	}

	/// <summary> Ball pivot on release before <see cref="BallThrow"/> throw offsets — used by throw trajectory preview. </summary>
	public Vector3 GetPredictedThrowReleasePivotPosition()
	{
		if ( isHolding && TryGetHoldAnchorWorldTransform( out var position, out _ ) )
			return position;

		var playerController = Components.Get<PlayerController>();
		var facingRotation = playerController.IsValid()
			? Rotation.FromYaw( playerController.EyeAngles.yaw )
			: GameObject.WorldRotation;
		return GameObject.WorldPosition + (facingRotation.Right * DropSideOffset) + (Vector3.Up * 4f);
	}

	/// <summary> World hold pose — right-hand <see cref="HoldBoneName"/> when available, else <see cref="HoldAnchor"/>. </summary>
	public bool TryGetHoldAnchorWorldTransform( out Vector3 position, out Rotation rotation )
	{
		position = default;
		rotation = Rotation.Identity;

		ResolveBodyRenderer();

		if ( BodyRenderer.IsValid()
			&& !string.IsNullOrWhiteSpace( HoldBoneName )
			&& BodyRenderer.TryGetBoneTransform( HoldBoneName, out var boneTransform ) )
		{
			var localHold = new Transform( HoldBoneLocalOffset, HoldBoneLocalAngles.ToRotation() );
			var worldHold = boneTransform.ToWorld( localHold );
			position = worldHold.Position;
			rotation = worldHold.Rotation;
			return true;
		}

		if ( HoldAnchor.IsValid() )
		{
			position = HoldAnchor.WorldPosition;
			rotation = HoldAnchor.WorldRotation;
			return true;
		}

		position = GameObject.WorldPosition;
		rotation = GameObject.WorldRotation;
		return false;
	}

	void ResolveBodyRenderer()
	{
		if ( BodyRenderer.IsValid() )
			return;

		BodyRenderer = Components.Get<SkinnedModelRenderer>( FindMode.EverythingInDescendants );
	}

	void ApplyHoldAnchorTransformToBall()
	{
		if ( !ballObject.IsValid() )
			return;

		if ( !TryGetHoldAnchorWorldTransform( out var position, out var rotation ) )
			return;

		ballObject.WorldPosition = position;
		ballObject.WorldRotation = rotation;
	}

	public GameObject ReleaseHeldBall( Vector3 playerVelocity = default )
	{
		if ( !ballObject.IsValid() || !isHolding )
			return null;

		dropInheritedSpeed = playerVelocity.WithZ( 0f ).Length;
		ballObject.WorldPosition = GetPredictedThrowReleasePivotPosition();

		foreach ( var body in ballBodiesToRestore )
		{
			if ( !body.IsValid() )
				continue;

			body.Enabled = true;
			body.Velocity = playerVelocity;
			body.AngularVelocity = Vector3.Zero;
		}
		ballBodiesToRestore.Clear();

		foreach ( var collider in ballCollidersToRestore )
		{
			if ( collider.IsValid() )
			{
				collider.Enabled = true;
			}
		}
		ballCollidersToRestore.Clear();

		NetIsHolding = false;
		localDropPending = false;
		return ballObject;
	}

	public void TransferBallOwnershipToHost()
	{
		AssignBallOwner( Connection.Host );
	}

	private void KeepHeldBallAttachedToAnchor()
	{
		ApplyHoldAnchorTransformToBall();
	}

	private void ApplyClientHeldVisualState()
	{
		if ( isHolding )
		{
			if ( !TryGetHoldAnchorWorldTransform( out var position, out var rotation ) )
			{
				position = NetHeldBallWorldPosition;
				rotation = NetHeldBallWorldRotation;
			}

			ballObject.WorldPosition = position;
			ballObject.WorldRotation = rotation;

			ApplyClientProxyBallState( ballObject, true );
			appliedClientHeldProxyState = true;
			return;
		}

		if ( !appliedClientHeldProxyState )
			return;

		ApplyClientProxyBallState( ballObject, false );
		appliedClientHeldProxyState = false;
	}

	private static void ApplyClientProxyBallState( GameObject ball, bool holding )
	{
		foreach ( var body in ball.Components.GetAll<Rigidbody>( FindMode.EverythingInSelfAndDescendants ) )
		{
			if ( !body.IsValid() )
				continue;

			body.Enabled = !holding;
			if ( holding )
			{
				body.Velocity = Vector3.Zero;
				body.AngularVelocity = Vector3.Zero;
			}
		}

		foreach ( var collider in ball.Components.GetAll<Collider>( FindMode.EverythingInSelfAndDescendants ) )
		{
			if ( !collider.IsValid() )
				continue;

			collider.Enabled = !holding;
		}
	}

	private void AssignBallOwner( Connection connection )
	{
		if ( !ballObject.IsValid() || connection is null )
			return;

		if ( !ballObject.Network.Active )
			return;

		ballObject.Network.AssignOwnership( connection );
	}

	private void UpdateSyncedBallState()
	{
		NetHeldBallWorldPosition = ballObject.WorldPosition;
		NetHeldBallWorldRotation = ballObject.WorldRotation;

		var body = GetPrimaryBallBody();
		if ( body.IsValid() )
		{
			NetHeldBallLinearVelocity = body.Velocity;
			NetHeldBallAngularVelocity = body.AngularVelocity;
			return;
		}

		NetHeldBallLinearVelocity = Vector3.Zero;
		NetHeldBallAngularVelocity = Vector3.Zero;
	}

	private Rigidbody GetPrimaryBallBody()
	{
		return ballObject.Components.Get<Rigidbody>( FindMode.EverythingInSelfAndDescendants );
	}

	public void BlockPickupForSeconds( float seconds )
	{
		if ( seconds <= 0f )
			return;

		NetPickupBlockedRemain = MathF.Max( NetPickupBlockedRemain, seconds );
	}

	bool IsBallInPickupRange( Vector3 ballWorldPosition )
	{
		return GetPickupHorizontalDistanceToBall( ballWorldPosition ) <= InteractDistance
			&& GetPickupVerticalSeparationToBall( ballWorldPosition ) <= MaxPickupVerticalSeparation;
	}

	float GetPickupHorizontalDistanceToBall( Vector3 ballWorldPosition )
	{
		return (ballWorldPosition - WorldPosition).WithZ( 0f ).Length;
	}

	float GetPickupVerticalSeparationToBall( Vector3 ballWorldPosition )
	{
		return MathF.Abs( ballWorldPosition.z - WorldPosition.z );
	}

	private bool IsMatchGameplayInputAllowed()
	{
		var team = Components.Get<PlayerTeam>();
		return team is null || team.IsMatchGameplayInputAllowed;
	}

	private void ResetHoldingState()
	{
		ballCollidersToRestore.Clear();
		ballOriginalParent = null;
		NetIsHolding = false;
		localDropPending = false;
		appliedClientHeldProxyState = false;
	}
}
