using System.Collections.Generic;
using Sandbox;

/// <summary>
/// Red <see cref="HighlightOutline"/> on enemy-team players for the local viewer.
/// Disabled while <see cref="PlayerTackle.IsRagdolled"/> — use <see cref="RagdollEnemyOutline"/> on the ragdoll object instead.
/// Requires <see cref="Highlight"/> on the main camera (see <see cref="EnemyOutlineCameraSetup"/>).
/// </summary>
public sealed class PlayerEnemyOutline : Component
{
	[Property] public Color EnemyColor { get; set; } = new Color( 1f, 0.15f, 0.15f );
	[Property] public Color EnemyObscuredColor { get; set; } = new Color( 0.85f, 0.1f, 0.1f, 0.65f );
	[Property] public Color EnemyInsideColor { get; set; } = new Color( 1f, 0.2f, 0.2f, 0.35f );
	[Property] public Color EnemyInsideObscuredColor { get; set; } = new Color( 0.7f, 0.1f, 0.1f, 0.25f );
	[Property] public float OutlineWidth { get; set; } = 2f;

	private HighlightOutline outline;
	private PlayerTeam playerTeam;
	private PlayerTackle playerTackle;

	protected override void OnStart()
	{
		playerTeam = Components.Get<PlayerTeam>();
		playerTackle = Components.Get<PlayerTackle>();

		var hadOutline = Components.Get<HighlightOutline>().IsValid();
		outline = Components.GetOrCreate<HighlightOutline>();

		// Prefab may only have PlayerEnemyOutline — push defaults onto a new HighlightOutline.
		if ( !hadOutline )
			ApplyPropertyDefaultsTo( outline );

		outline.Enabled = false;
	}

	protected override void OnUpdate()
	{
		if ( !outline.IsValid() )
			return;

		outline.Enabled = ShouldShowEnemyOutlineOnPlayer();
	}

	/// <summary> Copy every <see cref="HighlightOutline"/> field from <paramref name="playerRoot"/> onto <paramref name="target"/>. </summary>
	public static void CopyOutlineFromPlayer( GameObject playerRoot, HighlightOutline target )
	{
		if ( !playerRoot.IsValid() || !target.IsValid() )
			return;

		var source = playerRoot.Components.Get<HighlightOutline>();
		if ( source.IsValid() )
		{
			CopyOutlineSettings( source, target );
			return;
		}

		var enemyOutline = playerRoot.Components.Get<PlayerEnemyOutline>();
		if ( enemyOutline.IsValid() )
			enemyOutline.ApplyPropertyDefaultsTo( target );
		else
			ApplyDefaultStyle( target );
	}

	/// <summary> Duplicate all highlight-outline settings (colors, width, material, targets). </summary>
	public static void CopyOutlineSettings( HighlightOutline from, HighlightOutline to )
	{
		if ( !from.IsValid() || !to.IsValid() )
			return;

		to.Color = from.Color;
		to.ObscuredColor = from.ObscuredColor;
		to.InsideColor = from.InsideColor;
		to.InsideObscuredColor = from.InsideObscuredColor;
		to.Width = from.Width;
		to.Material = from.Material;
		to.OverrideTargets = from.OverrideTargets;

		if ( !from.OverrideTargets || from.Targets is null )
			return;

		to.Targets ??= new List<Renderer>();
		to.Targets.Clear();
		foreach ( var entry in from.Targets )
			to.Targets.Add( entry );
	}

	public void ApplyPropertyDefaultsTo( HighlightOutline target )
	{
		if ( !target.IsValid() )
			return;

		target.Color = EnemyColor;
		target.ObscuredColor = EnemyObscuredColor;
		target.InsideColor = EnemyInsideColor;
		target.InsideObscuredColor = EnemyInsideObscuredColor;
		target.Width = OutlineWidth;
	}

	public static void ApplyDefaultStyle( HighlightOutline target )
	{
		new PlayerEnemyOutline().ApplyPropertyDefaultsTo( target );
	}

	private bool ShouldShowEnemyOutlineOnPlayer()
	{
		if ( !GameObject.IsValid() || !GameObject.Enabled )
			return false;

		if ( Network.IsOwner )
			return false;

		playerTackle ??= Components.Get<PlayerTackle>();
		if ( playerTackle.IsValid() && playerTackle.IsRagdolled )
			return false;

		playerTeam ??= Components.Get<PlayerTeam>();
		if ( !playerTeam.IsValid() || !MatchTeamIds.IsValid( playerTeam.TeamId ) )
			return false;

		return ShouldShowOutlineForTeamId( Scene, playerTeam.TeamId );
	}

	public static bool ShouldShowOutlineForTeamId( Scene scene, int teamId )
	{
		if ( !MatchTeamIds.IsValid( teamId ) )
			return false;

		var localTeam = FindLocalViewerTeamId( scene );
		if ( !MatchTeamIds.IsValid( localTeam ) )
			return false;

		return teamId != localTeam;
	}

	/// <summary> Team id of the connection viewing this machine, or <see cref="MatchDirector.NoTeam"/>. </summary>
	public static int FindLocalViewerTeamId( Scene scene )
	{
		var localTeam = FindLocalViewerPlayerTeam( scene );
		return localTeam.IsValid() ? localTeam.TeamId : MatchDirector.NoTeam;
	}

	public static PlayerTeam FindLocalViewerPlayerTeam( Scene scene )
	{
		var local = Connection.Local;
		if ( local is null || scene is null )
			return null;

		foreach ( var team in scene.GetAllComponents<PlayerTeam>() )
		{
			if ( !team.IsValid() || !team.GameObject.Network.Active )
				continue;

			var owner = team.Network.Owner;
			if ( owner is not null && owner.SteamId == local.SteamId )
				return team;
		}

		return null;
	}
}
