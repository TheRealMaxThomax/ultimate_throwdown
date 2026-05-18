using Sandbox;

/// <summary> Which match team this player belongs to (host-assigned). </summary>
public sealed class PlayerTeam : Component
{
	public const int TeamCount = 2;

	[Sync( SyncFlags.FromHost )]
	public int TeamId { get; set; }

	public bool IsTeam0 => TeamId == 0;
	public bool IsTeam1 => TeamId == 1;
}
