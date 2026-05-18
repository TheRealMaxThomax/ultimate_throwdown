using Sandbox;

/// <summary> Which match team this player belongs to (host-assigned). Also carries match phase + round-reset pose replicated to every peer (including the owning client). </summary>
public sealed class PlayerTeam : Component
{
	public const int TeamCount = 2;

	[Sync( SyncFlags.FromHost )]
	public int TeamId { get; set; }

	/// <summary> Mirrors <see cref="MatchDirector.NetPhase"/> — pushed from host so clients respect intermission freeze. </summary>
	[Sync( SyncFlags.FromHost )]
	public int NetMatchPhase { get; set; }

	/// <summary> Host bumps after each round reset; all machines apply <see cref="NetRoundResetPosition"/> when this changes. </summary>
	[Sync( SyncFlags.FromHost )]
	public int NetRoundResetSequence { get; set; }

	[Sync( SyncFlags.FromHost )]
	public Vector3 NetRoundResetPosition { get; set; }

	[Sync( SyncFlags.FromHost )]
	public Rotation NetRoundResetRotation { get; set; }

	/// <summary> HUD mirrors of <see cref="MatchDirector"/> — pushed from host (director on local camera does not replicate). </summary>
	[Sync( SyncFlags.FromHost )]
	public int NetTeam0RoundWins { get; set; }

	[Sync( SyncFlags.FromHost )]
	public int NetTeam1RoundWins { get; set; }

	[Sync( SyncFlags.FromHost )]
	public float NetMatchTimeRemaining { get; set; }

	[Sync( SyncFlags.FromHost )]
	public float NetPhaseTimeRemaining { get; set; }

	[Sync( SyncFlags.FromHost )]
	public int NetLastGoalScoringTeamId { get; set; } = MatchDirector.NoTeam;

	[Sync( SyncFlags.FromHost )]
	public bool NetIsOvertime { get; set; }

	public bool IsTeam0 => TeamId == 0;
	public bool IsTeam1 => TeamId == 1;

	public MatchPhase SyncedMatchPhase => (MatchPhase)NetMatchPhase;

	/// <summary> Movement / ball / tackle allowed when Playing or celebrating a goal. </summary>
	public bool IsMatchGameplayInputAllowed =>
		SyncedMatchPhase is MatchPhase.Playing or MatchPhase.GoalCelebration;

	private int lastAppliedRoundResetSequence;

	protected override void OnUpdate()
	{
		if ( NetRoundResetSequence == lastAppliedRoundResetSequence )
			return;

		lastAppliedRoundResetSequence = NetRoundResetSequence;
		if ( NetRoundResetSequence <= 0 )
			return;

		ApplyRoundResetTransform();
	}

	/// <summary> Snap to synced reset pose (runs on host + every client, including owner). </summary>
	public void ApplyRoundResetTransform()
	{
		GameObject.WorldTransform = new Transform( NetRoundResetPosition, NetRoundResetRotation );

		var body = Components.Get<Rigidbody>();
		if ( body.IsValid() )
			body.Velocity = Vector3.Zero;
	}
}
