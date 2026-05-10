using Sandbox;

public sealed class PlayerClass : Component
{
	[Property] public ClassData CurrentClass { get; set; }
}

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
	public float MomentumMultiplier { get; set; } = 1f;

	[Property, Group( "Throw" )]
	public float ThrowPower { get; set; } = 1f;

	[Property, Group( "Dodge" )]
	public float DodgeCooldown { get; set; } = 3f;

	[Property, Group( "Dodge" )]
	public float DodgeDistance { get; set; } = 260f;

	/// <summary>Tackle-only invulnerability after dodge (seconds). Keep small — dodge is mostly the shove; SESSION_NOTES band ~0.12–0.16.</summary>
	[Property, Group( "Dodge" )]
	public float DodgeInvincibilityWindow { get; set; } = 0.14f;

	[Property, Group( "Tackle" )]
	public float TriggerSphereRadius { get; set; } = 40f;

	/// <summary>After the ragdoll is grounded and settled, seconds to stay down before standing up.</summary>
	[Property, Group( "Tackle" )]
	public float RagdollDuration { get; set; } = 2f;

	/// <summary>Max seconds from tackle until forced stand-up (flying ragdoll, stuck, etc.).</summary>
	[Property, Group( "Tackle" )]
	public float RagdollMaxDuration { get; set; } = 8f;

	/// <summary>Pelvis speed (units/s) at or below this counts as settled enough for grounded time.</summary>
	[Property, Group( "Tackle" )]
	public float RagdollGroundSpeedMax { get; set; } = 160f;

	/// <summary>Downward trace length from pelvis for floor detection.</summary>
	[Property, Group( "Tackle" )]
	public float RagdollGroundTraceDown { get; set; } = 120f;

	/// <summary>Start ray this far above pelvis when testing for floor.</summary>
	[Property, Group( "Tackle" )]
	public float RagdollGroundTraceUp { get; set; } = 24f;

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
