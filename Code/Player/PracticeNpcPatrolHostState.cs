using Sandbox;

/// <summary>
/// Shared patrol API on practice runner dummies — implemented by <see cref="PracticeNpcPatrol"/> in <c>Code/Map/</c>.
/// Player systems reference this type only (not the map component) so compile order stays stable.
/// </summary>
public abstract class PracticeNpcPatrolHostState : Component
{
	public abstract bool IsPatrollingAtChargeSpeed { get; }

	public abstract float NetChargeRunCycle { get; }

	/// <summary> Host-only tackle path: synthetic velocity + view-forward while patrolling. </summary>
	public abstract bool TryGetHostTackleMove( out Vector3 horizontalVelocity, out Vector3 approachDirection );
}
