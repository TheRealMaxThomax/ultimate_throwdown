using Sandbox;
using Sandbox.Diagnostics;
using System;

public sealed class BallGrab : Component
{
	[Property] public GameObject MainBall { get; set; }
	[Property] public string MainBallName { get; set; } = "main_ball";
	[Property] public float InteractDistance { get; set; } = 120f;
	[Property] public string InteractAction { get; set; } = "use";
	[Property] public float PickupDelayAfterDrop { get; set; } = 0.25f;
	[Property] public float DropForwardOffset { get; set; } = 32f;
	[Property] public float DropUpOffset { get; set; } = 8f;
	[Property] public float DropCarryMultiplier { get; set; } = 0.45f;
	[Property] public float DropMaxCarrySpeed { get; set; } = 180f;
	[Property] public float DropCarryUpVelocity { get; set; } = 10f;
	[Property] public float DropperNoPushWindow { get; set; } = 0.75f;
	[Property] public float DropperNoPushRadius { get; set; } = 90f;
	[Property] public float DropperMaxHorizontalSpeed { get; set; } = 60f;
	[Property] public GameObject HoldAnchor { get; set; }
	[Property] public string PromptText { get; set; } = "Pick Up With E";
	[Property] public bool EnableNetDebugLogs { get; set; } = false;

	private GameObject ballObject;
	private GameObject ballOriginalParent;
	private readonly List<Collider> ballCollidersToRestore = new();
	private readonly List<Rigidbody> ballBodiesToRestore = new();
	private Rigidbody playerBody;
	private Vector3 previousWorldPosition;
	private Vector3 estimatedWorldVelocity;
	private bool hasPreviousWorldPosition;
	private Vector3 previousHeldAnchorPosition;
	private Vector3 estimatedHeldAnchorVelocity;
	private bool hasPreviousHeldAnchorPosition;
	private bool warnedAboutDuplicateMainBallName;
	private bool isHolding;
	[Sync( SyncFlags.FromHost )] private bool NetIsHolding { get => isHolding; set => isHolding = value; }
	[Sync( SyncFlags.FromHost )] private Vector3 NetHeldBallWorldPosition { get; set; }
	[Sync( SyncFlags.FromHost )] private Rotation NetHeldBallWorldRotation { get; set; }
	[Sync( SyncFlags.FromHost )] private Vector3 NetHeldBallLinearVelocity { get; set; }
	[Sync( SyncFlags.FromHost )] private Vector3 NetHeldBallAngularVelocity { get; set; }
	private float pickupBlockedUntilTime;
	private float nextAutoGrabAttemptAt;
	private float dropperNoPushUntilTime;
	private float noPushPreservedHorizontalSpeed;
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
		playerBody = Components.Get<Rigidbody>();
		previousWorldPosition = WorldPosition;
		hasPreviousWorldPosition = true;
		var startAnchor = HoldAnchor.IsValid() ? HoldAnchor : GameObject;
		previousHeldAnchorPosition = startAnchor.IsValid() ? startAnchor.WorldPosition : WorldPosition;
		hasPreviousHeldAnchorPosition = true;
		FindMainBall();
	}

	protected override void OnUpdate()
	{
		UpdateEstimatedWorldVelocity();

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

		if ( isHolding && Input.Pressed( InteractAction ) )
		{
			localDropPending = true;
			var requestedDropCarryVelocity = GetRequestedDropCarryVelocity();
			RequestDropBallOnHost( requestedDropCarryVelocity );
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

		var inRange = Vector3.DistanceBetween( WorldPosition, ballObject.WorldPosition ) <= InteractDistance;

		if ( !Network.IsOwner )
			return;

		// Auto-grab: entering grab range picks up the ball automatically.
		// Rate-limited to avoid RPC spam; host validates and ignores if already held.
		if ( !isHolding && inRange && Time.Now >= pickupBlockedUntilTime && Time.Now >= nextAutoGrabAttemptAt )
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
	}

	private void PickUpBall()
	{
		if ( !ballObject.IsValid() || isHolding )
			return;

		ballOriginalParent = ballObject.Parent;

		var parentTarget = HoldAnchor.IsValid() ? HoldAnchor : GameObject;
		ballObject.WorldPosition = parentTarget.WorldPosition;
		ballObject.WorldRotation = parentTarget.WorldRotation;

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

	private void DropBall( Vector3 dropCarryVelocity )
	{
		ReleaseHeldBall( dropCarryVelocity );
	}

	[Rpc.Host]
	private void RequestPickUpBallOnHost()
	{
		var hostDistanceToBall = ballObject.IsValid()
			? Vector3.DistanceBetween( WorldPosition, ballObject.WorldPosition )
			: -1f;

		if ( EnableNetDebugLogs )
		{
			Log.Info( $"[NetDebug] Host pickup request received. Caller={Rpc.Caller.DisplayName} HolderObject={GameObject.Name} BallValid={ballObject.IsValid()} IsHolding={isHolding} HostDistanceToBall={hostDistanceToBall}" );
		}

		if ( !ballObject.IsValid() || isHolding )
			return;

		if ( Time.Now < pickupBlockedUntilTime )
			return;

		PickUpBall();
		AssignBallOwner( Connection.Host );
		if ( EnableNetDebugLogs )
		{
			Log.Info( $"[NetDebug] Host approved pickup. HolderObject={GameObject.Name}" );
		}
	}

	[Rpc.Host]
	private void RequestDropBallOnHost( Vector3 requestedDropCarryVelocity )
	{
		if ( EnableNetDebugLogs )
		{
			Log.Info( $"[NetDebug] Host drop request received. Caller={Rpc.Caller.DisplayName} IsHolding={isHolding}" );
		}

		if ( !isHolding )
			return;

		var validatedDropCarryVelocity = ValidateDropCarryVelocity( requestedDropCarryVelocity );
		AssignBallOwner( Connection.Host );
		DropBall( validatedDropCarryVelocity );
		BlockPickupForSeconds( PickupDelayAfterDrop );
		nextAutoGrabAttemptAt = Time.Now + PickupDelayAfterDrop;
		dropperNoPushUntilTime = Time.Now + DropperNoPushWindow;
		localDropPending = false;
	}

	private void ApplyDropperNoPushWindow()
	{
		if ( Time.Now >= dropperNoPushUntilTime )
		{
			noPushPreservedHorizontalSpeed = 0f;
			return;
		}

		if ( isHolding || !ballObject.IsValid() )
			return;

		var ballBody = GetPrimaryBallBody();
		if ( !ballBody.IsValid() )
			return;

		var toBall = ballObject.WorldPosition - WorldPosition;
		var horizontalDistance = toBall.WithZ( 0f ).Length;
		if ( horizontalDistance > DropperNoPushRadius )
			return;

		var horizontalVelocity = ballBody.Velocity.WithZ( 0f );
		// Do not clamp intentional drop carry below DropperMaxHorizontalSpeed; that erased carry velocity every frame.
		var horizontalCap = MathF.Max( DropperMaxHorizontalSpeed, noPushPreservedHorizontalSpeed );
		if ( horizontalVelocity.Length <= horizontalCap )
			return;

		var clampedHorizontal = horizontalVelocity.Normal * horizontalCap;
		ballBody.Velocity = new Vector3( clampedHorizontal.x, clampedHorizontal.y, ballBody.Velocity.z );
	}

	public GameObject ReleaseHeldBall( Vector3? overrideDropCarryVelocity = null )
	{
		if ( !ballObject.IsValid() || !isHolding )
			return null;

		noPushPreservedHorizontalSpeed = overrideDropCarryVelocity.HasValue
			? overrideDropCarryVelocity.Value.WithZ( 0f ).Length
			: 0f;

		var dropSource = HoldAnchor.IsValid() ? HoldAnchor : GameObject;
		if ( dropSource.IsValid() )
		{
			var dropForward = dropSource.WorldRotation.Forward.WithZ( 0f );
			if ( dropForward.Length < 0.001f )
			{
				dropForward = GameObject.WorldRotation.Forward.WithZ( 0f );
			}

			dropForward = dropForward.Length > 0.001f ? dropForward.Normal : Vector3.Forward;
			ballObject.WorldPosition = dropSource.WorldPosition + (dropForward * DropForwardOffset) + (Vector3.Up * DropUpOffset);
		}

		var dropCarryVelocity = overrideDropCarryVelocity ?? GetDropCarryVelocity();
		foreach ( var body in ballBodiesToRestore )
		{
			if ( !body.IsValid() )
				continue;

			body.Enabled = true;
			body.Velocity = dropCarryVelocity;
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

	private Vector3 GetDropCarryVelocity()
	{
		if ( !playerBody.IsValid() )
		{
			playerBody = Components.Get<Rigidbody>();
		}

		// Prefer held-anchor velocity because it directly tracks the held ball movement path.
		var sourceVelocity = estimatedHeldAnchorVelocity;
		if ( sourceVelocity.WithZ( 0f ).Length < 1f && playerBody.IsValid() )
		{
			sourceVelocity = playerBody.Velocity;
		}

		if ( sourceVelocity.WithZ( 0f ).Length < 1f )
		{
			// Host-side proxy rigidbody velocity can be zero for remote players; use transform-derived estimate.
			sourceVelocity = estimatedWorldVelocity;
		}

		var horizontalCarry = sourceVelocity.WithZ( 0f ) * DropCarryMultiplier;
		if ( horizontalCarry.Length > DropMaxCarrySpeed )
		{
			horizontalCarry = horizontalCarry.Normal * DropMaxCarrySpeed;
		}

		return horizontalCarry + (Vector3.Up * DropCarryUpVelocity);
	}

	private Vector3 GetRequestedDropCarryVelocity()
	{
		var sourceVelocity = playerBody.IsValid() ? playerBody.Velocity : Vector3.Zero;
		if ( sourceVelocity.WithZ( 0f ).Length < 1f )
		{
			sourceVelocity = estimatedHeldAnchorVelocity;
		}

		if ( sourceVelocity.WithZ( 0f ).Length < 1f )
		{
			sourceVelocity = estimatedWorldVelocity;
		}

		var horizontalCarry = sourceVelocity.WithZ( 0f ) * DropCarryMultiplier;
		if ( horizontalCarry.Length < 1f )
		{
			var inputMove = Input.AnalogMove.WithZ( 0f );
			if ( inputMove.Length > 0.1f )
			{
				horizontalCarry = inputMove.Normal * (DropMaxCarrySpeed * 0.5f);
			}
		}

		if ( horizontalCarry.Length > DropMaxCarrySpeed )
		{
			horizontalCarry = horizontalCarry.Normal * DropMaxCarrySpeed;
		}

		return horizontalCarry + (Vector3.Up * DropCarryUpVelocity);
	}

	private Vector3 ValidateDropCarryVelocity( Vector3 requestedDropCarryVelocity )
	{
		var horizontal = requestedDropCarryVelocity.WithZ( 0f );
		if ( horizontal.Length > DropMaxCarrySpeed )
		{
			horizontal = horizontal.Normal * DropMaxCarrySpeed;
		}

		var up = requestedDropCarryVelocity.z.Clamp( -30f, DropCarryUpVelocity + 40f );
		return new Vector3( horizontal.x, horizontal.y, up );
	}

	private void UpdateEstimatedWorldVelocity()
	{
		if ( !hasPreviousWorldPosition )
		{
			previousWorldPosition = WorldPosition;
			hasPreviousWorldPosition = true;
			estimatedWorldVelocity = Vector3.Zero;
			return;
		}

		if ( Time.Delta <= 0.0001f )
			return;

		estimatedWorldVelocity = (WorldPosition - previousWorldPosition) / Time.Delta;
		previousWorldPosition = WorldPosition;
	}

	public void TransferBallOwnershipToHost()
	{
		AssignBallOwner( Connection.Host );
	}

	private void KeepHeldBallAttachedToAnchor()
	{
		var parentTarget = HoldAnchor.IsValid() ? HoldAnchor : GameObject;
		if ( !parentTarget.IsValid() )
			return;

		UpdateHeldAnchorVelocityEstimate( parentTarget.WorldPosition );
		ballObject.WorldPosition = parentTarget.WorldPosition;
		ballObject.WorldRotation = parentTarget.WorldRotation;
	}

	private void UpdateHeldAnchorVelocityEstimate( Vector3 anchorWorldPosition )
	{
		if ( !hasPreviousHeldAnchorPosition )
		{
			previousHeldAnchorPosition = anchorWorldPosition;
			hasPreviousHeldAnchorPosition = true;
			estimatedHeldAnchorVelocity = Vector3.Zero;
			return;
		}

		if ( Time.Delta <= 0.0001f )
			return;

		estimatedHeldAnchorVelocity = (anchorWorldPosition - previousHeldAnchorPosition) / Time.Delta;
		previousHeldAnchorPosition = anchorWorldPosition;
	}

	private void ApplyClientHeldVisualState()
	{
		if ( isHolding )
		{
			var parentTarget = HoldAnchor.IsValid() ? HoldAnchor : GameObject;
			if ( parentTarget.IsValid() )
			{
				ballObject.WorldPosition = parentTarget.WorldPosition;
				ballObject.WorldRotation = parentTarget.WorldRotation;
			}
			else
			{
				ballObject.WorldPosition = NetHeldBallWorldPosition;
				ballObject.WorldRotation = NetHeldBallWorldRotation;
			}

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
		pickupBlockedUntilTime = Time.Now + seconds;
	}

	private void ResetHoldingState()
	{
		ballCollidersToRestore.Clear();
		ballOriginalParent = null;
		NetIsHolding = false;
		localDropPending = false;
		appliedClientHeldProxyState = false;
		noPushPreservedHorizontalSpeed = 0f;
	}
}
