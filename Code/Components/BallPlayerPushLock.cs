using Sandbox;
using System;

public sealed class BallPlayerPushLock : Component
{
	[Property] public float PlayerLockRadius { get; set; } = 90f;
	[Property] public float MaxAllowedBodyPushSpeed { get; set; } = 220f;
	[Property] public float ThrowExemptSeconds { get; set; } = 0.75f;
	[Property] public bool EnableDebugLogs { get; set; } = false;

	private Rigidbody ballBody;
	private float exemptUntilTime;

	protected override void OnStart()
	{
		ballBody = Components.Get<Rigidbody>( FindMode.EverythingInSelfAndDescendants );
	}

	protected override void OnFixedUpdate()
	{
		if ( !Networking.IsHost )
			return;

		if ( Time.Now < exemptUntilTime )
			return;

		if ( !ballBody.IsValid() )
		{
			ballBody = Components.Get<Rigidbody>( FindMode.EverythingInSelfAndDescendants );
			return;
		}

		if ( !IsAnyPlayerClose() )
			return;

		var speed = ballBody.Velocity.Length;
		if ( speed > MaxAllowedBodyPushSpeed )
			return;

		// Ignore low-speed body-push impulses from player collision.
		ballBody.Velocity = Vector3.Zero;
		ballBody.AngularVelocity = Vector3.Zero;
		if ( EnableDebugLogs && speed > 0.001f )
		{
			Log.Info( $"[BallPlayerPushLock] Cleared low-speed body push ({speed:0.0})." );
		}
	}

	public void SuppressForSeconds( float seconds )
	{
		exemptUntilTime = MathF.Max( exemptUntilTime, Time.Now + seconds );
	}

	private bool IsAnyPlayerClose()
	{
		foreach ( var go in Scene.GetAllObjects( true ) )
		{
			if ( !go.Components.Get<PlayerController>().IsValid() )
				continue;

			if ( Vector3.DistanceBetween( go.WorldPosition, WorldPosition ) <= PlayerLockRadius )
				return true;
		}

		return false;
	}

}
