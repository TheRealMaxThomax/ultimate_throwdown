using Sandbox;
using System.Collections.Generic;

public sealed class BallClientFeel : Component
{
	[Property] public float FreeBallVisualFollowSharpness { get; set; } = 14f;
	[Property] public float ContactBoostSharpness { get; set; } = 42f;
	[Property] public float ContactBoostDuration { get; set; } = 0.12f;
	[Property] public float InterpolationDelay { get; set; } = 0.06f;
	[Property] public int MaxSnapshots { get; set; } = 24;

	private BallGrab ballGrab;
	private float contactBoostUntilTime;
	private bool appliedHeldProxyState;
	private bool appliedFreeProxyState;
	private readonly List<BallSnapshot> snapshots = new();

	private readonly struct BallSnapshot
	{
		public BallSnapshot( float time, Vector3 position, Rotation rotation, Vector3 linearVelocity )
		{
			Time = time;
			Position = position;
			Rotation = rotation;
			LinearVelocity = linearVelocity;
		}

		public float Time { get; }
		public Vector3 Position { get; }
		public Rotation Rotation { get; }
		public Vector3 LinearVelocity { get; }
	}

	protected override void OnStart()
	{
		ballGrab = Components.Get<BallGrab>();
	}

	protected override void OnUpdate()
	{
		if ( Networking.IsHost )
			return;

		if ( ballGrab is null )
		{
			ballGrab = Components.Get<BallGrab>();
			if ( ballGrab is null )
				return;
		}

		var ball = ballGrab.HeldBall;
		if ( !ball.IsValid() )
			return;

		var isHolding = ballGrab.IsEffectivelyHolding;
		if ( isHolding || appliedHeldProxyState )
		{
			ApplyClientProxyBallState( ball, isHolding );
			appliedHeldProxyState = isHolding;
		}

		if ( !Network.IsOwner )
		{
			if ( appliedFreeProxyState )
			{
				ApplyFreeBallOwnerProxyState( ball, false );
				appliedFreeProxyState = false;
			}
			return;
		}

		if ( isHolding )
		{
			if ( ballGrab.TryGetHoldAnchorWorldTransform( out var position, out var rotation ) )
			{
				// Local visual snap keeps held ball locked to the hand bone instead of floating at the hip.
				ball.WorldPosition = position;
				ball.WorldRotation = rotation;
			}
			else
			{
				ball.WorldPosition = ballGrab.SyncedBallWorldPosition;
				ball.WorldRotation = ballGrab.SyncedBallWorldRotation;
			}
			snapshots.Clear();
			return;
		}

		if ( !appliedFreeProxyState )
		{
			// Use visual-follow only for free ball on owning client to avoid local rigidbody
			// fighting host-sync correction during bounces (jitter/rapid up-down artifacts).
			// Keep colliders enabled so first-contact solidity remains consistent.
			ApplyFreeBallOwnerProxyState( ball, true );
			appliedFreeProxyState = true;
		}

		var syncedPosition = ballGrab.SyncedBallWorldPosition;
		var syncedRotation = ballGrab.SyncedBallWorldRotation;
		var syncedLinearVelocity = ballGrab.SyncedBallLinearVelocity;
		RecordSnapshot( syncedPosition, syncedRotation, syncedLinearVelocity );

		if ( TryGetBufferedTarget( out var bufferedPosition, out var bufferedRotation ) )
		{
			// Buffered interpolation already smooths between host snapshots,
			// so render directly to avoid double-smoothing floaty motion.
			ball.WorldPosition = bufferedPosition;
			ball.WorldRotation = bufferedRotation;
			return;
		}

		TryTriggerContactVisualBoost( ball );

		var visualSharpness = Time.Now < contactBoostUntilTime ? ContactBoostSharpness : FreeBallVisualFollowSharpness;
		var followAlpha = MathX.Clamp( Time.Delta * visualSharpness, 0f, 1f );
		ball.WorldPosition = Vector3.Lerp( ball.WorldPosition, syncedPosition, followAlpha );
		ball.WorldRotation = Rotation.Slerp( ball.WorldRotation, syncedRotation, followAlpha );
	}

	private void TryTriggerContactVisualBoost( GameObject ball )
	{
		var toBall = (ball.WorldPosition - WorldPosition).WithZ( 0f );
		var distance = toBall.Length;
		if ( distance > 42f )
			return;

		var moveInput = Input.AnalogMove.WithZ( 0f );
		if ( moveInput.Length < 0.15f )
			return;

		var moveDirection = moveInput.Normal;
		var approachDot = moveDirection.Dot( toBall.Normal );
		if ( approachDot < 0.25f )
			return;

		contactBoostUntilTime = Time.Now + ContactBoostDuration;
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

	private static void ApplyFreeBallOwnerProxyState( GameObject ball, bool useVisualProxy )
	{
		foreach ( var body in ball.Components.GetAll<Rigidbody>( FindMode.EverythingInSelfAndDescendants ) )
		{
			if ( !body.IsValid() )
				continue;

			body.Enabled = !useVisualProxy;
			if ( useVisualProxy )
			{
				body.Velocity = Vector3.Zero;
				body.AngularVelocity = Vector3.Zero;
			}
		}

		// Preserve colliders while proxying free-ball visuals so the client still gets
		// immediate solid contact before any pickup/drop cycle.
		foreach ( var collider in ball.Components.GetAll<Collider>( FindMode.EverythingInSelfAndDescendants ) )
		{
			if ( !collider.IsValid() )
				continue;

			collider.Enabled = true;
		}
	}

	private void RecordSnapshot( Vector3 position, Rotation rotation, Vector3 linearVelocity )
	{
		if ( snapshots.Count > 0 )
		{
			var last = snapshots[snapshots.Count - 1];
			if ( last.Position == position && last.Rotation == rotation && last.LinearVelocity == linearVelocity )
				return;
		}

		snapshots.Add( new BallSnapshot( Time.Now, position, rotation, linearVelocity ) );
		if ( snapshots.Count > MaxSnapshots )
		{
			snapshots.RemoveAt( 0 );
		}
	}

	private bool TryGetBufferedTarget( out Vector3 position, out Rotation rotation )
	{
		position = default;
		rotation = default;

		if ( snapshots.Count == 0 )
			return false;

		var targetTime = Time.Now - InterpolationDelay;
		if ( snapshots.Count == 1 || targetTime <= snapshots[0].Time )
		{
			position = snapshots[0].Position;
			rotation = snapshots[0].Rotation;
			return true;
		}

		for ( var i = 1; i < snapshots.Count; i++ )
		{
			var newer = snapshots[i];
			if ( newer.Time < targetTime )
				continue;

			var older = snapshots[i - 1];
			var span = newer.Time - older.Time;
			var t = span > 0.0001f ? MathX.Clamp( (targetTime - older.Time) / span, 0f, 1f ) : 1f;
			position = HermitePosition( older, newer, t, span );
			rotation = Rotation.Slerp( older.Rotation, newer.Rotation, t );
			return true;
		}

		var last = snapshots[snapshots.Count - 1];
		position = last.Position;
		rotation = last.Rotation;
		return true;
	}

	private static Vector3 HermitePosition( BallSnapshot older, BallSnapshot newer, float t, float span )
	{
		var t2 = t * t;
		var t3 = t2 * t;
		var h00 = (2f * t3) - (3f * t2) + 1f;
		var h10 = t3 - (2f * t2) + t;
		var h01 = (-2f * t3) + (3f * t2);
		var h11 = t3 - t2;

		var m0 = older.LinearVelocity * span;
		var m1 = newer.LinearVelocity * span;
		return (h00 * older.Position) + (h10 * m0) + (h01 * newer.Position) + (h11 * m1);
	}
}
