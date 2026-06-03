using Sandbox;
using System.Collections.Generic;

public sealed class PlayerClass : Component
{
	[Property] public ClassData CurrentClass { get; set; }

	/// <summary> Re-apply <see cref="ClassData.ModelScale"/> for a few seconds so clothing added after start still picks up scale. </summary>
	[Property] public float ModelScaleRetrySeconds { get; set; } = 3f;

	/// <summary> Shader / clothing height when menu avatar height is disabled — matches engine dresser "standard" body. </summary>
	public const float NeutralMenuHeight = 1f;

	private float modelScaleRetryUntil;

	protected override void OnStart()
	{
		DisableDresserMenuHeight();
		modelScaleRetryUntil = Time.Now + ModelScaleRetrySeconds;
		ApplyClassAppearance();
	}

	/// <summary> Re-applies capsule + model scale (call after cosmetics or ragdoll stand-up). </summary>
	public void ApplyClassAppearance()
	{
		ApplyClassCapsule();
		ApplyNeutralMenuHeight();
		ApplyClassModelScale();
	}

	/// <summary> Call on cloned players before <c>Enabled = true</c> so the prefab <see cref="Dresser"/> cannot apply menu height first. </summary>
	public static void PrepareDresserBeforeSpawn( GameObject playerRoot )
	{
		if ( !playerRoot.IsValid() )
			return;

		var dresser = playerRoot.Components.Get<Dresser>( FindMode.EverythingInSelfAndDescendants );
		if ( !dresser.IsValid() )
			return;

		dresser.ApplyHeightScale = false;
		dresser.Enabled = false;
	}

	/// <summary> Menu avatar height is a morph (<c>scale_height</c>) — class size uses <see cref="ClassData.ModelScale"/> on mesh roots. </summary>
	public static void ApplyNeutralMenuHeight( GameObject playerRoot )
	{
		if ( !playerRoot.IsValid() )
			return;

		foreach ( var smr in playerRoot.Components.GetAll<SkinnedModelRenderer>( FindMode.EverythingInSelfAndDescendants ) )
		{
			if ( !smr.IsValid() )
				continue;

			smr.Set( "scale_height", NeutralMenuHeight );
		}
	}

	private void ApplyNeutralMenuHeight() => ApplyNeutralMenuHeight( GameObject );

	/// <summary> Class <see cref="ClassData.ModelScale"/> is authoritative — menu avatar height must not stack on top. </summary>
	private void DisableDresserMenuHeight()
	{
		var dresser = Components.Get<Dresser>( FindMode.EverythingInSelfAndDescendants );
		if ( !dresser.IsValid() )
			return;

		dresser.ApplyHeightScale = false;
	}

	protected override void OnUpdate()
	{
		if ( CurrentClass is null )
			return;

		if ( Time.Now <= modelScaleRetryUntil )
			ApplyClassModelScale();
	}

	/// <summary> Cosmetics can set <c>scale_height</c> after our async callback — enforce class size right before draw. </summary>
	protected override void OnPreRender()
	{
		if ( CurrentClass is null )
			return;

		ApplyNeutralMenuHeight();
		ApplyClassModelScale();
	}

	/// <summary> Pushes <see cref="ClassData.CapsuleHeight"/> / <see cref="ClassData.CapsuleRadius"/> onto <see cref="PlayerController.BodyHeight"/> / <see cref="PlayerController.BodyRadius"/>. </summary>
	private void ApplyClassCapsule()
	{
		var data = CurrentClass;
		if ( data is null )
			return;

		var pc = Components.Get<PlayerController>();
		if ( !pc.IsValid() )
			return;

		pc.BodyHeight = data.CapsuleHeight;
		pc.BodyRadius = data.CapsuleRadius;
	}

	/// <summary> Uniform scale on each independent skinned-mesh root under this player (body + separate clothing roots). </summary>
	private void ApplyClassModelScale()
	{
		var data = CurrentClass;
		if ( data is null )
			return;

		var s = data.ModelScale;
		if ( s <= 0f )
			s = 1f;

		var uniformScale = Vector3.One * s;
		var roots = new HashSet<GameObject>();

		foreach ( var smr in Components.GetAll<SkinnedModelRenderer>( FindMode.EverythingInSelfAndDescendants ) )
		{
			if ( !smr.IsValid() )
				continue;

			var rootGo = FindSkinnedHierarchyRoot( smr );
			if ( rootGo.IsValid() )
				roots.Add( rootGo );
		}

		foreach ( var go in roots )
			go.LocalScale = uniformScale;
	}

	/// <summary> Highest <see cref="SkinnedModelRenderer"/> in the parent chain before the player root, so child clothing does not get its own uniform (avoids double scale). </summary>
	private GameObject FindSkinnedHierarchyRoot( SkinnedModelRenderer smr )
	{
		var top = smr;
		var go = smr.GameObject;

		while ( go.Parent is { } p && p != GameObject )
		{
			var parentSmr = p.Components.Get<SkinnedModelRenderer>();
			if ( parentSmr.IsValid() )
				top = parentSmr;

			go = p;
		}

		return top.GameObject;
	}
}

[GameResource( "Class Data", "cdata", "Player class statistics for Ultimate Throwdown" )]
public class ClassData : GameResource
{
	[Property, Group( "Identity" )]
	public string ClassName { get; set; } = "Unnamed";

	[Property, Group( "Physics" )]
	public float Mass { get; set; } = 80f;

	/// <summary>Applied to <see cref="PlayerController.BodyHeight"/> via <see cref="PlayerClass"/> on start.</summary>
	[Property, Group( "Capsule" )]
	public float CapsuleHeight { get; set; } = 72f;

	/// <summary>Applied to <see cref="PlayerController.BodyRadius"/> via <see cref="PlayerClass"/> on start.</summary>
	[Property, Group( "Capsule" )]
	public float CapsuleRadius { get; set; } = 16f;

	/// <summary>Uniform local scale on avatar <see cref="SkinnedModelRenderer"/> roots via <see cref="PlayerClass"/> (retried briefly for late cosmetics).</summary>
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

	/// <summary>After ragdoll stand-up, for <see cref="PlayerTackle.PostRagdollCatchUpRampDuration"/> seconds replace <see cref="TimeToCatchUpSpeed"/> for sprint→charge ramp (usually ≥ TimeToSprintSpeed). 0 disables.</summary>
	[Property, Group( "Movement" )]
	public float TimeToCatchUpSpeedAfterRagdoll { get; set; } = 0f;

	/// <summary>After this class <b>lands</b> a tackle, for <see cref="PlayerTackle.PostAttackCatchUpRampDuration"/> seconds replace <see cref="TimeToCatchUpSpeed"/> for sprint→charge ramp on the attacker. 0 disables.</summary>
	[Property, Group( "Movement" )]
	public float TimeToCatchUpSpeedAfterAttack { get; set; } = 0f;

	/// <summary>Scales <see cref="PlayerController.AccelerationTime"/> / <see cref="PlayerController.DeaccelerationTime"/> (from prefab snapshot) plus move-cap easing: <b>1</b> baseline; <b>&gt;1</b> heavier; <b>&lt;1</b> snappier.</summary>
	[Property, Group( "Movement" )]
	public float MomentumMultiplier { get; set; } = 1f;

	[Property, Group( "Throw" )]
	public float ThrowPower { get; set; } = 1f;

	/// <summary>
	/// Multiplier on real-time throw charge progress (&gt;1 = reaches full charge sooner). Base window still comes from prefab <see cref="BallThrow"/>.
	/// </summary>
	[Property, Group( "Throw" )]
	public float ThrowChargeSpeedScale { get; set; } = 1f;

	[Property, Group( "Dodge" )]
	public float DodgeCooldown { get; set; } = 3f;

	[Property, Group( "Dodge" )]
	public float DodgeDistance { get; set; } = 260f;

	/// <summary>Multiplies <see cref="DodgeDistance"/> when dodging during throw wind-up (<see cref="BallThrow.IsChargingThrow"/>). Intended for Sniper (others keep <b>1</b>; non-Snipers cannot dodge while charging).</summary>
	[Property, Group( "Dodge" )]
	public float ThrowChargeDodgeDistanceMultiplier { get; set; } = 1f;

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
