using Sandbox;
using Sandbox.Diagnostics;

public sealed class BallGrab : Component
{
	[Property] public GameObject MainBall { get; set; }
	[Property] public string MainBallName { get; set; } = "main_ball";
	[Property] public float InteractDistance { get; set; } = 120f;
	[Property] public string InteractAction { get; set; } = "use";
	[Property] public GameObject HoldAnchor { get; set; }
	[Property] public string PromptText { get; set; } = "Pick Up With E";
	[Property] public bool EnableNetDebugLogs { get; set; } = false;

	private GameObject ballObject;
	private GameObject ballOriginalParent;
	private readonly List<Collider> ballCollidersToRestore = new();
	private readonly List<Rigidbody> ballBodiesToRestore = new();
	private bool warnedAboutDuplicateMainBallName;
	private bool isHolding;
	[Sync( SyncFlags.FromHost )] private bool NetIsHolding { get => isHolding; set => isHolding = value; }
	[Sync( SyncFlags.FromHost )] private Vector3 NetHeldBallWorldPosition { get; set; }
	[Sync( SyncFlags.FromHost )] private Rotation NetHeldBallWorldRotation { get; set; }
	private float pickupBlockedUntilTime;
	private bool localDropPending;
	private bool appliedClientHeldProxyState;
	public bool IsHolding => isHolding;
	public GameObject HeldBall => ballObject;
	public Vector3 SyncedBallWorldPosition => NetHeldBallWorldPosition;
	public Rotation SyncedBallWorldRotation => NetHeldBallWorldRotation;

	protected override void OnStart()
	{
		FindMainBall();
	}

	protected override void OnUpdate()
	{
		if ( !isHolding )
		{
			localDropPending = false;
		}

		if ( Networking.IsHost && !localDropPending && isHolding && ballObject.IsValid() )
		{
			KeepHeldBallAttachedToAnchor();
			NetHeldBallWorldPosition = ballObject.WorldPosition;
			NetHeldBallWorldRotation = ballObject.WorldRotation;
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
			RequestDropBallOnHost();
			return;
		}

		if ( !ballObject.IsValid() )
		{
			FindMainBall();
			return;
		}

		if ( Networking.IsHost )
		{
			NetHeldBallWorldPosition = ballObject.WorldPosition;
			NetHeldBallWorldRotation = ballObject.WorldRotation;
		}

		var inRange = Vector3.DistanceBetween( WorldPosition, ballObject.WorldPosition ) <= InteractDistance;

		if ( inRange && !isHolding )
		{
			DebugOverlay.Text( ballObject.WorldPosition + Vector3.Up * 20f, PromptText );
		}

		if ( !inRange )
			return;

		if ( !Network.IsOwner )
			return;

		if ( Input.Pressed( InteractAction ) )
		{
			if ( Time.Now < pickupBlockedUntilTime )
				return;

			RequestPickUpBallOnHost();
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

	private void DropBall()
	{
		ReleaseHeldBall();
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
	private void RequestDropBallOnHost()
	{
		if ( EnableNetDebugLogs )
		{
			Log.Info( $"[NetDebug] Host drop request received. Caller={Rpc.Caller.DisplayName} IsHolding={isHolding}" );
		}

		if ( !isHolding )
			return;

		AssignBallOwner( Connection.Host );
		DropBall();
		localDropPending = false;
	}

	public GameObject ReleaseHeldBall()
	{
		if ( !ballObject.IsValid() || !isHolding )
			return null;

		var dropSource = HoldAnchor.IsValid() ? HoldAnchor : GameObject;
		if ( dropSource.IsValid() )
		{
			// Keep the dropped ball out of player collider overlap to avoid explosive bounce.
			ballObject.WorldPosition = dropSource.WorldPosition + (dropSource.WorldRotation.Forward * 20f) + (Vector3.Up * 4f);
		}

		foreach ( var body in ballBodiesToRestore )
		{
			if ( !body.IsValid() )
				continue;

			body.Velocity = Vector3.Zero;
			body.AngularVelocity = Vector3.Zero;
			body.Enabled = true;
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
		var parentTarget = HoldAnchor.IsValid() ? HoldAnchor : GameObject;
		if ( !parentTarget.IsValid() )
			return;

		ballObject.WorldPosition = parentTarget.WorldPosition;
		ballObject.WorldRotation = parentTarget.WorldRotation;
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
	}
}
