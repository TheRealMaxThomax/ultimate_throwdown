using Sandbox;

/// <summary> Per-map team display names and optional match overrides. Wire on map root or GameNetworkManager. </summary>
public sealed class MapMatchConfig : Component
{
	[Property] public string Team0DisplayName { get; set; } = "Gassy Moe's";
	[Property] public string Team1DisplayName { get; set; } = "MorgsFuel";

	/// <summary>
	/// Training / practice maps only — enable on that scene&apos;s <see cref="MapMatchConfig"/>; leave off on Turf Wars and other competitive maps.
	/// Unlimited clock, no match-over from timer or round-win cap, all joiners on <see cref="PracticeSpawnTeamId"/> using team-0 spawn list.
	/// </summary>
	[Property] public bool PracticeArenaMode { get; set; }

	/// <summary> When <see cref="PracticeArenaMode"/> is on, every player spawns on this team (default 0). </summary>
	[Property] public int PracticeSpawnTeamId { get; set; } = MatchTeamIds.Team0;

	public string GetTeamDisplayName( int teamId )
	{
		return teamId == 0 ? Team0DisplayName : Team1DisplayName;
	}

	public int ResolveSpawnTeamId( int balancedTeamId )
	{
		if ( !PracticeArenaMode )
			return balancedTeamId;

		return MatchTeamIds.IsValid( PracticeSpawnTeamId ) ? PracticeSpawnTeamId : MatchTeamIds.Team0;
	}

	public static MapMatchConfig FindInScene( Scene scene )
	{
		if ( scene is null )
			return null;

		foreach ( var go in scene.GetAllObjects( true ) )
		{
			var config = go.Components.Get<MapMatchConfig>();
			if ( config.IsValid() )
				return config;
		}

		return null;
	}
}
