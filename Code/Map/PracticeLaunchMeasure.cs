using System.Collections.Generic;
using Sandbox;

/// <summary>
/// Practice arena only: tracks <see cref="CitizenAvatarLod.PracticeNpcTag"/> knockdowns along a straight lane
/// and reports band scores (1, 2, 3…) to <see cref="PracticeLaunchReadout"/>.
/// </summary>
public sealed class PracticeLaunchMeasure : Component
{
	/// <summary> Band width in world units — line (12) + gap (116) = 128 in practice arena layout. </summary>
	[Property] public float BandPitch { get; set; } = 128f;

	/// <summary> Lane forward in this object&apos;s local space. Practice arena uses local Y down the lane → (0, 1, 0). </summary>
	[Property] public Vector3 LocalLaneDirection { get; set; } = new( 0f, 1f, 0f );

	/// <summary> TV / sign object with <see cref="PracticeLaunchReadout"/> — wire <c>LaunchReadoutSign</c> here. </summary>
	[Property] public GameObject ReadoutSign { get; set; }

	[Property] public bool EnableDebugLogs { get; set; }

	private PracticeLaunchReadout readout;
	private MapMatchConfig mapMatchConfig;
	private readonly Dictionary<PlayerTackle, VictimTrack> tracks = new();

	private struct VictimTrack
	{
		public bool WasDown;
		public bool Active;
		public float MaxAlong;
	}

	private bool IsActive =>
		mapMatchConfig.IsValid() && mapMatchConfig.PracticeArenaMode;

	protected override void OnStart()
	{
		mapMatchConfig = MapMatchConfig.FindInScene( Scene );
		ResolveReadout();
	}

	private void ResolveReadout()
	{
		if ( readout.IsValid() )
			return;

		if ( ReadoutSign.IsValid() )
			readout = ReadoutSign.Components.Get<PracticeLaunchReadout>( FindMode.EverythingInSelfAndDescendants );

		if ( !readout.IsValid() )
			readout = Scene.GetAllComponents<PracticeLaunchReadout>().FirstOrDefault();
	}

	protected override void OnFixedUpdate()
	{
		if ( !Networking.IsHost || !IsActive )
			return;

		foreach ( var tackle in Scene.GetAllComponents<PlayerTackle>() )
		{
			if ( !tackle.IsValid() || !tackle.GameObject.Tags.Has( CitizenAvatarLod.PracticeNpcTag ) )
				continue;

			TickVictim( tackle );
		}

		PruneInvalidTracks();
	}

	private void TickVictim( PlayerTackle tackle )
	{
		var down = tackle.IsRagdolled || tackle.IsAwaitingRagdollLaunch;

		if ( !tracks.TryGetValue( tackle, out var track ) )
			track = default;

		if ( down )
		{
			var along = ProjectAlongLane( GetSampleWorldPosition( tackle ) );

			if ( !track.WasDown )
			{
				track.Active = true;
				track.MaxAlong = along;

				if ( EnableDebugLogs )
					Log.Info( $"[PracticeLaunch] Track start {tackle.GameObject.Name} along={along:0.##}" );
			}
			else if ( track.Active )
			{
				track.MaxAlong = MathF.Max( track.MaxAlong, along );
			}
		}
		else if ( track.WasDown && track.Active )
		{
			var score = ScoreFromAlong( track.MaxAlong );

			if ( EnableDebugLogs )
				Log.Info( $"[PracticeLaunch] {tackle.GameObject.Name} maxAlong={track.MaxAlong:0.##} score={score}" );

			ResolveReadout();
			if ( !readout.IsValid() )
			{
				if ( EnableDebugLogs )
					Log.Warning( "[PracticeLaunch] No PracticeLaunchReadout wired — score not shown." );
			}
			else
			{
				readout.ShowScoreOnHost( score );
			}

			track.Active = false;
		}

		track.WasDown = down;
		tracks[tackle] = track;
	}

	private void PruneInvalidTracks()
	{
		List<PlayerTackle> remove = null;

		foreach ( var key in tracks.Keys )
		{
			if ( key.IsValid() )
				continue;

			remove ??= new List<PlayerTackle>();
			remove.Add( key );
		}

		if ( remove is null )
			return;

		foreach ( var key in remove )
			tracks.Remove( key );
	}

	private Vector3 GetLaneForward()
	{
		var local = LocalLaneDirection;
		if ( local.LengthSquared < 0.0001f )
			local = Vector3.Forward;

		var world = (WorldRotation * local).WithZ( 0f );
		if ( world.Length < 0.001f )
			world = WorldRotation.Forward.WithZ( 0f );

		return world.Normal;
	}

	private float ProjectAlongLane( Vector3 worldPosition )
	{
		var delta = worldPosition - WorldPosition;
		return Vector3.Dot( delta.WithZ( 0f ), GetLaneForward() );
	}

	private static Vector3 GetSampleWorldPosition( PlayerTackle tackle )
	{
		if ( tackle.IsRagdolled )
			return tackle.SyncedRagdollPelvisPosition;

		if ( tackle.IsAwaitingRagdollLaunch )
			return tackle.WorldPosition;

		return tackle.WorldPosition;
	}

	private int ScoreFromAlong( float maxAlong )
	{
		if ( maxAlong < 0f || BandPitch <= 0f )
			return 0;

		return (int)(maxAlong / BandPitch) + 1;
	}
}
