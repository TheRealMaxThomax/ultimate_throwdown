/// <summary> Shared team id constants for match flow (0 and 1 only for now). </summary>
public static class MatchTeamIds
{
	public const int Team0 = 0;
	public const int Team1 = 1;
	public const int TeamCount = 2;

	public static bool IsValid( int teamId ) => teamId is Team0 or Team1;
}
