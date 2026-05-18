using Sandbox;

/// <summary>
/// Red <see cref="HighlightOutline"/> on a tackle ragdoll when the victim is on the enemy team (local viewer).
/// Victim team is host-synced — ragdolls are spawned on the host only.
/// Outline appearance is copied from the victim's <see cref="HighlightOutline"/> on the host before network spawn.
/// </summary>
public sealed class RagdollEnemyOutline : Component
{
	[Sync( SyncFlags.FromHost )]
	public int NetVictimTeamId { get; set; } = MatchDirector.NoTeam;

	private HighlightOutline outline;

	/// <summary> Host: call once after the ragdoll object is created, before network spawn. </summary>
	public void ConfigureFromVictim( PlayerTackle victim )
	{
		if ( !Networking.IsHost || !victim.IsValid() )
			return;

		var team = victim.Components.Get<PlayerTeam>();
		NetVictimTeamId = team.IsValid() ? team.TeamId : MatchDirector.NoTeam;

		outline = Components.GetOrCreate<HighlightOutline>();
		PlayerEnemyOutline.CopyOutlineFromPlayer( victim.GameObject, outline );
		outline.Enabled = false;
	}

	protected override void OnStart()
	{
		outline = Components.Get<HighlightOutline>();
		if ( !outline.IsValid() )
			outline = Components.GetOrCreate<HighlightOutline>();

		// Host sets full style in ConfigureFromVictim; clients keep replicated values — do not reset here.
		if ( outline.IsValid() )
			outline.Enabled = false;
	}

	protected override void OnUpdate()
	{
		if ( !outline.IsValid() )
			return;

		if ( !MatchTeamIds.IsValid( NetVictimTeamId ) )
			return;

		outline.Enabled = PlayerEnemyOutline.ShouldShowOutlineForTeamId( Scene, NetVictimTeamId );
	}
}
