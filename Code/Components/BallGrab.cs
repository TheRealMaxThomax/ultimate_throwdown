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

	private GameObject ballObject;
	private Rigidbody ballBody;
	private GameObject ballOriginalParent;
	private readonly List<Collider> ballCollidersToRestore = new();
	private bool warnedAboutDuplicateMainBallName;
	private bool isHolding;
	private float pickupBlockedUntilTime;
	public bool IsHolding => isHolding;
	public GameObject HeldBall => ballObject;

	protected override void OnStart()
	{
		FindMainBall();
	}

	protected override void OnUpdate()
	{
		if ( isHolding && !ballObject.IsValid() )
		{
			ResetHoldingState();
			FindMainBall();
			return;
		}

		if ( isHolding && Input.Pressed( InteractAction ) )
		{
			DropBall();
			return;
		}

		if ( !ballObject.IsValid() )
		{
			FindMainBall();
			return;
		}

		var inRange = Vector3.DistanceBetween( Transform.Position, ballObject.Transform.Position ) <= InteractDistance;

		if ( inRange && !isHolding )
		{
			DebugOverlay.Text( ballObject.Transform.Position + Vector3.Up * 20f, PromptText );
		}

		if ( !inRange )
			return;

		if ( Input.Pressed( InteractAction ) )
		{
			if ( Time.Now < pickupBlockedUntilTime )
				return;

			PickUpBall();
		}
	}

	private void FindMainBall()
	{
		if ( MainBall.IsValid() )
		{
			ballObject = MainBall;
			ballBody = ballObject.Components.Get<Rigidbody>();
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
		ballBody = ballObject.IsValid() ? ballObject.Components.Get<Rigidbody>() : null;
	}

	private void PickUpBall()
	{
		if ( !ballObject.IsValid() || isHolding )
			return;

		ballOriginalParent = ballObject.Parent;

		var parentTarget = HoldAnchor.IsValid() ? HoldAnchor : GameObject;
		ballObject.SetParent( parentTarget, true );
		ballObject.Transform.Position = parentTarget.Transform.Position;
		ballObject.Transform.Rotation = parentTarget.Transform.Rotation;

		if ( ballBody.IsValid() )
		{
			ballBody.Enabled = false;
		}

		ballCollidersToRestore.Clear();
		foreach ( var collider in ballObject.Components.GetAll<Collider>() )
		{
			if ( !collider.IsValid() )
				continue;

			collider.Enabled = false;
			ballCollidersToRestore.Add( collider );
		}

		isHolding = true;
	}

	private void DropBall()
	{
		ReleaseHeldBall();
	}

	public GameObject ReleaseHeldBall()
	{
		if ( !ballObject.IsValid() || !isHolding )
			return null;

		ballObject.SetParent( ballOriginalParent, true );

		if ( ballBody.IsValid() )
		{
			ballBody.Enabled = true;
		}

		foreach ( var collider in ballCollidersToRestore )
		{
			if ( collider.IsValid() )
			{
				collider.Enabled = true;
			}
		}
		ballCollidersToRestore.Clear();

		isHolding = false;
		return ballObject;
	}

	public void BlockPickupForSeconds( float seconds )
	{
		pickupBlockedUntilTime = Time.Now + seconds;
	}

	private void ResetHoldingState()
	{
		ballCollidersToRestore.Clear();
		ballBody = null;
		ballOriginalParent = null;
		isHolding = false;
	}
}
