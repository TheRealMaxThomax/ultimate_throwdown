/// <summary> High-level match flow phase (host-driven, synced on <see cref="MatchDirector"/>). </summary>
public enum MatchPhase
{
	Playing = 0,
	GoalCelebration = 1,
	Intermission = 2,
	MatchOver = 3,
	/// <summary> Pre-round / rematch loadout window — frozen like intermission. </summary>
	MatchSetup = 4,
}
