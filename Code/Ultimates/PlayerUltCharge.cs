using Sandbox;
using System;

/// <summary>
/// Host-authoritative ult charge (0–100%). Passive regen during <see cref="MatchPhase.Playing"/> only;
/// goal / assist / enemy-tackle bumps; rematch reset. Max cap comes from the equipped <see cref="IPlayerUlt"/>.
/// See GAMEPLAY_DESIGN.md Ultimates section.
/// </summary>
public sealed class PlayerUltCharge : Component
{
	/// <summary> Fallback when no <see cref="IPlayerUlt"/> is on the player (e.g. class without ult wired yet). </summary>
	public const float DefaultMaxChargePoints = 100f;

	/// <summary> Passive points per second while <see cref="MatchPhase.Playing"/> (default ≈ 1 pt / 5 s). </summary>
	[Property, Group( "Charge" )] public float PassivePointsPerSecond { get; set; } = 0.2f;

	[Property, Group( "Events" )] public float GoalChargePoints { get; set; } = 40f;
	[Property, Group( "Events" )] public float AssistChargePoints { get; set; } = 25f;
	[Property, Group( "Events" )] public int TackleChargePoints { get; set; } = 10;

	[Property] public bool EnableUltChargeDebugLogs { get; set; }

	private float hostChargePoints;

	/// <summary> Host: while true, no charge is gained (passive / goal / tackle). Set during an active ult so % only climbs again afterwards. </summary>
	private bool hostChargeGainBlocked;

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

		if ( hostChargeGainBlocked )
			return;

		if ( !AllowsPassiveRegen() )
			return;

		var maxPoints = ResolveMaxChargePoints();
		if ( PassivePointsPerSecond <= 0f || maxPoints <= 0f )
			return;

		if ( hostChargePoints >= maxPoints )
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

	/// <summary> Host: assist passer bump when a teammate scores within the assist window. </summary>
	public void GrantAssistChargeOnHost()
	{
		if ( !Networking.IsHost )
			return;

		AddChargePointsOnHost( AssistChargePoints, "assist" );
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

	/// <summary> Host: block/allow all charge gain (set true for the duration of an active ult). </summary>
	public void SetHostChargeGainBlocked( bool blocked )
	{
		if ( !Networking.IsHost )
			return;

		hostChargeGainBlocked = blocked;
	}

	/// <summary> Host: spend full charge (ult commit). </summary>
	public bool TrySpendFullChargeOnHost()
	{
		if ( !Networking.IsHost )
			return false;

		var maxPoints = ResolveMaxChargePoints();
		if ( hostChargePoints < maxPoints || maxPoints <= 0f )
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

		hostChargeGainBlocked = false;
		hostChargePoints = 0f;
		SyncPercentFromHostPoints();
	}

	/// <summary>
	/// Host: re-normalize % after equipped ult changes (loadout swap). Raw points carry over; no swap penalty.
	/// </summary>
	public void ResyncFromEquippedUltOnHost()
	{
		if ( !Networking.IsHost )
			return;

		SyncPercentFromHostPoints();
	}

	/// <summary> Host: zero every player ult charge (rematch). </summary>
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
		if ( hostChargeGainBlocked )
			return;

		var maxPoints = ResolveMaxChargePoints();
		if ( points <= 0f || maxPoints <= 0f )
			return;

		var before = hostChargePoints;
		hostChargePoints = MathF.Min( maxPoints, hostChargePoints + points );

		if ( MathF.Abs( hostChargePoints - before ) < 0.0001f )
			return;

		SyncPercentFromHostPoints();

		if ( EnableUltChargeDebugLogs )
			Log.Info( $"[UltCharge] {GameObject.Name}: +{points:F1} ({reason}) → {NetChargePercent:F0}%" );
	}

	private void SyncPercentFromHostPoints()
	{
		var maxPoints = ResolveMaxChargePoints();
		NetChargePercent = maxPoints <= 0f
			? 0f
			: (hostChargePoints / maxPoints * 100f).Clamp( 0f, 100f );
	}

	private float ResolveMaxChargePoints()
	{
		var equipped = ResolveEquippedUlt();
		if ( equipped is null )
			return DefaultMaxChargePoints;

		var max = equipped.MaxChargePoints;
		return max > 0f ? max : DefaultMaxChargePoints;
	}

	/// <summary> Equipped ult from <see cref="PlayerLoadout"/>; scene NPCs without loadout fall back to first enabled <see cref="IPlayerUlt"/>. </summary>
	private IPlayerUlt ResolveEquippedUlt()
	{
		var loadout = Components.Get<PlayerLoadout>();
		if ( loadout.IsValid() )
		{
			var equipped = loadout.ResolveEquippedUlt();
			if ( equipped is not null )
				return equipped;
		}

		foreach ( var component in Components.GetAll<Component>( FindMode.EverythingInSelf ) )
		{
			if ( component is not IPlayerUlt ult )
				continue;

			if ( !component.Enabled )
				continue;

			return ult;
		}

		return null;
	}

	private bool AllowsPassiveRegen()
	{
		var team = Components.Get<PlayerTeam>();
		return team is not null && team.SyncedMatchPhase == MatchPhase.Playing;
	}
}
