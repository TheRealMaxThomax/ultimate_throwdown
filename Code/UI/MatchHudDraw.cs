using System;
using Sandbox;
using Sandbox.Rendering;

/// <summary> Shared read + draw helpers for match HUD components on a scene UI root. </summary>
public static class MatchHudDraw
{
	public static bool TryGetHudState( Scene scene, out PlayerTeam team, out MapMatchConfig config )
	{
		team = null;
		config = MapMatchConfig.FindInScene( scene );

		if ( scene is null )
			return false;

		foreach ( var playerTeam in scene.GetAllComponents<PlayerTeam>() )
		{
			if ( !playerTeam.GameObject.Network.Active )
				continue;

			team = playerTeam;
			return true;
		}

		return false;
	}

	/// <summary> Minutes + zero-padded seconds, e.g. 10.00 → 9.59 → 9.58. </summary>
	public static string FormatMatchClock( float secondsRemaining, bool isOvertime )
	{
		if ( isOvertime )
			return "OVERTIME";

		var total = MathF.Max( 0f, secondsRemaining );
		var minutes = (int)(total / 60f);
		var seconds = (int)(total % 60f);
		return $"{minutes}.{seconds:00}";
	}

	public static string GetScoringTeamBannerText( MapMatchConfig config, int scoringTeamId )
	{
		var teamName = config.IsValid()
			? config.GetTeamDisplayName( scoringTeamId )
			: $"Team{scoringTeamId}";

		return $"{teamName.ToUpperInvariant()} SCORED!";
	}

	public static void DrawPanel( HudPainter hud, Rect panel, Color background )
	{
		hud.DrawRect( panel, background );
	}

	public static void DrawCenteredText( HudPainter hud, Rect rect, string text, Color color, int fontSize )
	{
		hud.DrawText( new TextRendering.Scope( text, color, fontSize ), rect, TextFlag.Center );
	}

	public static void DrawLeftText( HudPainter hud, Rect rect, string text, Color color, int fontSize )
	{
		hud.DrawText( new TextRendering.Scope( text, color, fontSize ), rect, TextFlag.Left );
	}
}
