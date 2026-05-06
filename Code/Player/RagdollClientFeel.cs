using Sandbox;
using System.Collections.Generic;

// Owning client: smooths synced ragdoll pelvis into WorldPosition while down (same idea as BallClientFeel).
public sealed class RagdollClientFeel : Component
{
	[Property] public float InterpolationDelay { get; set; } = 0.06f;
	[Property] public int MaxSnapshots { get; set; } = 24;
	[Property] public float FollowSharpness { get; set; } = 18f;

	private PlayerTackle tackle;
	private bool wasRagdolled;
	private readonly List<Snapshot> snapshots = new();

	private readonly struct Snapshot
	{
		public Snapshot( float time, Vector3 position )
		{
			Time = time;
			Position = position;
		}

		public float Time { get; }
		public Vector3 Position { get; }
	}

	protected override void OnStart()
	{
		tackle = Components.Get<PlayerTackle>();
	}

	protected override void OnUpdate()
	{
		if ( Networking.IsHost )
			return;
		if ( IsProxy )
			return;

		if ( tackle is null || !tackle.IsValid() )
			tackle = Components.Get<PlayerTackle>();
		if ( tackle is null )
			return;

		var ragdolled = tackle.IsRagdolled;
		if ( ragdolled != wasRagdolled )
		{
			snapshots.Clear();
			wasRagdolled = ragdolled;
		}

		if ( !ragdolled )
			return;

		var synced = tackle.SyncedRagdollPelvisPosition;
		RecordSnapshot( synced );

		if ( TryGetBufferedPosition( out var smoothed ) )
		{
			WorldPosition = smoothed;
			return;
		}

		var followAlpha = MathX.Clamp( Time.Delta * FollowSharpness, 0f, 1f );
		WorldPosition = Vector3.Lerp( WorldPosition, synced, followAlpha );
	}

	private void RecordSnapshot( Vector3 position )
	{
		if ( snapshots.Count > 0 )
		{
			var last = snapshots[^1];
			if ( last.Position == position )
				return;
		}

		snapshots.Add( new Snapshot( Time.Now, position ) );
		if ( snapshots.Count > MaxSnapshots )
			snapshots.RemoveAt( 0 );
	}

	private bool TryGetBufferedPosition( out Vector3 position )
	{
		position = default;
		if ( snapshots.Count == 0 )
			return false;

		var delay = InterpolationDelay < 0f ? 0f : InterpolationDelay;
		var targetTime = Time.Now - delay;
		if ( snapshots.Count == 1 || targetTime <= snapshots[0].Time )
		{
			position = snapshots[0].Position;
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
			position = Vector3.Lerp( older.Position, newer.Position, t );
			return true;
		}

		position = snapshots[^1].Position;
		return true;
	}
}
