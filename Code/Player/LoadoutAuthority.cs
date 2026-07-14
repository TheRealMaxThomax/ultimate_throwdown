using Sandbox;

/// <summary> Host-side loadout validation — v1 allows all catalog options; progression slice gates unlocks here. </summary>
public static class LoadoutAuthority
{
	/// <summary> Normalize + catalog + unlock checks. </summary>
	public static bool TryValidateCommittedLoadout( long steamId, SavedLoadoutData data, out SavedLoadoutData normalized )
	{
		normalized = null;
		if ( data is null )
			return false;

		normalized = LoadoutCatalog.Normalize( data );
		return IsLoadoutAllowedForPlayer( steamId, normalized );
	}

	/// <summary> v1: always true. Progression slice filters class/ult/passive to server-trusted unlocks. </summary>
	public static bool IsLoadoutAllowedForPlayer( long steamId, SavedLoadoutData normalized )
	{
		_ = steamId;
		_ = normalized;
		return true;
	}

	/// <summary> Turf Wars: <see cref="MatchPhase.MatchSetup"/> + <see cref="MatchPhase.Intermission"/>. Practice: anytime. </summary>
	public static bool IsLoadoutSwapAllowed( Scene scene, PlayerTeam team )
	{
		if ( team is null )
			return false;

		var config = MapMatchConfig.FindInScene( scene );
		if ( config.IsValid() && config.PracticeArenaMode )
			return true;

		return team.SyncedMatchPhase is MatchPhase.MatchSetup or MatchPhase.Intermission;
	}
}
