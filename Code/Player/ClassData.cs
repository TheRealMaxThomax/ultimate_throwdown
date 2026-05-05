using Sandbox;

[GameResource( "Class Data", "cdata", "Player class statistics for Ultimate Throwdown" )]
public class ClassData : GameResource
{
	[Property, Group( "Identity" )]
	public string ClassName { get; set; } = "Unnamed";

	[Property, Group( "Physics" )]
	public float Mass { get; set; } = 80f;

	[Property, Group( "Capsule" )]
	public float CapsuleHeight { get; set; } = 72f;

	[Property, Group( "Capsule" )]
	public float CapsuleRadius { get; set; } = 16f;

	[Property, Group( "Capsule" )]
	public float ModelScale { get; set; } = 1f;

	[Property, Group( "Movement" )]
	public float StartMoveSpeed { get; set; } = 140f;

	[Property, Group( "Movement" )]
	public float SprintMoveSpeed { get; set; } = 220f;

	[Property, Group( "Movement" )]
	public float CatchUpMoveSpeed { get; set; } = 320f;

	[Property, Group( "Movement" )]
	public float TimeToSprintSpeed { get; set; } = 2f;

	[Property, Group( "Movement" )]
	public float TimeToCatchUpSpeed { get; set; } = 4f;

	[Property, Group( "Movement" )]
	public float WalkTurnSpeed { get; set; } = 10f;

	[Property, Group( "Movement" )]
	public float RunTurnSpeed { get; set; } = 8f;

	[Property, Group( "Movement" )]
	public float ChargeTurnSpeed { get; set; } = 5f;

	[Property, Group( "Movement" )]
	public float MomentumMultiplier { get; set; } = 1f;

	[Property, Group( "Throw" )]
	public float ThrowPower { get; set; } = 1f;

	[Property, Group( "Dodge" )]
	public float DodgeCooldown { get; set; } = 3f;

	[Property, Group( "Dodge" )]
	public float DodgeDistance { get; set; } = 200f;

	[Property, Group( "Dodge" )]
	public float DodgeInvincibilityWindow { get; set; } = 0.3f;

	[Property, Group( "Tackle" )]
	public float TriggerSphereRadius { get; set; } = 40f;

	[Property, Group( "Tackle" )]
	public float RagdollDuration { get; set; } = 2f;

	[Property, Group( "Tackle" )]
	public float PostTackleInvincibilityDuration { get; set; } = 1f;

	[Property, Group( "Tackle" )]
	public float BallLaunchForceOnTackle { get; set; } = 500f;

	[Property, Group( "Tackle" )]
	public float BallPickupLockoutAfterTackle { get; set; } = 1.5f;

	[Property, Group( "Tackle" )]
	public float TackleChargeRampRate { get; set; } = 0f;

	[Property, Group( "Tackle" )]
	public float MaxTackleChargeBonus { get; set; } = 0f;

	[Property, Group( "Weapons" )]
	public bool IgnoreWeaponSpeedPenalty { get; set; } = false;

	[Property, Group( "Weapons" )]
	public float WeaponSwingSpeedPenaltyDuration { get; set; } = 1f;
}
