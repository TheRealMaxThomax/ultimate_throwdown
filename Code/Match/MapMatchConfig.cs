using Sandbox;

/// <summary> Per-map team display names and optional match overrides. Wire on map root or GameNetworkManager. </summary>
public sealed class MapMatchConfig : Component
{
	[Property] public string Team0DisplayName { get; set; } = "Team A";
	[Property] public string Team1DisplayName { get; set; } = "Team B";

	public string GetTeamDisplayName( int teamId )
	{
		return teamId == 0 ? Team0DisplayName : Team1DisplayName;
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
