using Sandbox;
using System;

/// <summary>
/// Host-authoritative ult charge (0–100%). Passive regen during <see cref="MatchPhase.Playing"/> only;
/// goal / enemy-tackle bumps; rematch reset. See GAMEPLAY_DESIGN.md → Ultimates.
/// </summary>
public sealed class PlayerUltCharge : Component
{
	/// <summary> Internal points required for 100% (v1: same for all classes). </summary>
	[Property, Group( "Charge" )] public float MaxChargePoints { get; set; } = 100f;

	/// <summary> Passive points per second while <see cref="MatchPhase.Playing"/> (default ≈ 1 pt / 5 s). </summary>
	[Property, Group( "Charge" )] public float PassivePointsPerSecond { get; set; } = 0.2f;

	[Property, Group( "Events" )] public float GoalChargePoints { get; set; } = 40f;
	[Property, Group( "Events" )] public int TackleChargePoints { get; set; } = 15;

	[Property] public bool EnableUltChargeDebugLogs { get; set; }

	private float hostChargePoints;

	[Sync( SyncFlags.FromHost )]
	public float NetChargePercent { get; private set; }

	/// <summary> True when synced percent is at or above 100. </summary>
	public bool IsFullyCharged => NetChargePercent >= 99.95f;

	public float ChargePercent => NetChargePercent.Clamp( 0f, 100f );

	protected override void OnStart()
	{
		if ( Networking.IsHost )
			SyncPercentFromHostPoints();
	}

	protected override void OnUpdate()
	{
		if ( !Networking.IsHost )
			return;

		if ( !AllowsPassiveRegen() )
			return;

		if ( PassivePointsPerSecond <= 0f || MaxChargePoints <= 0f )
			return;

		if ( hostChargePoints >= MaxChargePoints )
			return;

		AddChargePointsOnHost( PassivePointsPerSecond * Time.Delta, "passive" );
	}

	/// <summary> Host: scorer goal bump. </summary>
	public void GrantGoalChargeOnHost()
	{
		if ( !Networking.IsHost )
			return;

		AddChargePointsOnHost( GoalChargePoints, "goal" );
	}

	/// <summary> Host: attacker bump when tackle lands on an enemy (no friendly-fire credit). </summary>
	public void TryGrantTackleChargeOnHost( PlayerTackle victim )
	{
		if ( !Networking.IsHost || victim is null || !victim.IsValid() )
			return;

		if ( victim.GameObject.Tags.Has( CitizenAvatarLod.PracticeNpcTag ) )
			return;

		var attackerTeam = Components.Get<PlayerTeam>();
		var victimTeam = victim.Components.Get<PlayerTeam>();
		if ( !attackerTeam.IsValid() || !victimTeam.IsValid() )
			return;

		if ( !MatchTeamIds.IsValid( attackerTeam.TeamId ) || !MatchTeamIds.IsValid( victimTeam.TeamId ) )
			return;

		if ( attackerTeam.TeamId == victimTeam.TeamId )
			return;

		AddChargePointsOnHost( TackleChargePoints, "tackle" );
	}

	/// <summary> Host: spend full charge (Speed Blitz commit — slice 2). </summary>
	public bool TrySpendFullChargeOnHost()
	{
		if ( !Networking.IsHost )
			return false;

		if ( hostChargePoints < MaxChargePoints || MaxChargePoints <= 0f )
			return false;

		hostChargePoints = 0f;
		SyncPercentFromHostPoints();

		if ( EnableUltChargeDebugLogs )
			Log.Info( $"[UltCharge] {GameObject.Name}: spent full charge." );

		return true;
	}

	/// <summary> Host: rematch / fresh match — zero charge. </summary>
	public void ResetChargeOnHost()
	{
		if ( !Networking.IsHost )
			return;

		hostChargePoints = 0f;
		SyncPercentFromHostPoints();
	}

	/// <summary> Host: zero every player&apos;s ult charge (rematch). </summary>
	public static void ResetAllPlayersInScene( Scene scene )
	{
		if ( !Networking.IsHost || scene is null )
			return;

		foreach ( var charge in scene.GetAllComponents<PlayerUltCharge>() )
		{
			if ( !charge.IsValid() )
				continue;

			charge.ResetChargeOnHost();
		}
	}

	private void AddChargePointsOnHost( float points, string reason )
	{
		if ( points <= 0f || MaxChargePoints <= 0f )
			return;

		var before = hostChargePoints;
		hostChargePoints = MathF.Min( MaxChargePoints, hostChargePoints + points );

		if ( MathF.Abs( hostChargePoints - before ) < 0.0001f )
			return;

		SyncPercentFromHostPoints();

		if ( EnableUltChargeDebugLogs )
			Log.Info( $"[UltCharge] {GameObject.Name}: +{points:F1} ({reason}) → {NetChargePercent:F0}%" );
	}

	private void SyncPercentFromHostPoints()
	{
		NetChargePercent = MaxChargePoints <= 0f
			? 0f
			: (hostChargePoints / MaxChargePoints * 100f).Clamp( 0f, 100f );
	}

	private bool AllowsPassiveRegen()
	{
		var team = Components.Get<PlayerTeam>();
		return team is not null && team.SyncedMatchPhase == MatchPhase.Playing;
	}
}
