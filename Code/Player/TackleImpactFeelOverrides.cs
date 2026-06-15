/// <summary>
/// Per-impact tuning for <see cref="TackleImpactFeel"/> — used by Speed Blitz connect (stronger than normal tackle).
/// </summary>
public readonly struct TackleImpactFeelOverrides
{
	public float HitstopDurationSeconds { get; init; }
	public float ShakeDurationSeconds { get; init; }
	public float ShakePositionAmplitude { get; init; }
	public float ShakeRotationAmplitudeDegrees { get; init; }
	public float AttackerFovPunchDegrees { get; init; }
	public float AttackerCameraOffsetPunchX { get; init; }
	public float AttackerCameraOffsetPunchZ { get; init; }
	public float AttackerPunchDurationSeconds { get; init; }
}
