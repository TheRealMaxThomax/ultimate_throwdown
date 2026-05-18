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

	public static string GetMatchWinnerBannerText( MapMatchConfig config, int winnerTeamId )
	{
		var teamName = config.IsValid()
			? config.GetTeamDisplayName( winnerTeamId )
			: $"Team{winnerTeamId}";

		return $"{teamName.ToUpperInvariant()} WINS!";
	}

	public static string FormatFinalRoundScore( MapMatchConfig config, int team0Wins, int team1Wins )
	{
		var team0Name = config.IsValid() ? config.Team0DisplayName : "Team A";
		var team1Name = config.IsValid() ? config.Team1DisplayName : "Team B";
		return $"{team0Name}  {team0Wins} — {team1Wins}  {team1Name}";
	}

	public static bool IsMouseOverRect( Rect rect )
	{
		return rect.IsInside( Mouse.Position );
	}

	public static bool IsMatchOverCelebrating( PlayerTeam team )
	{
		return team is not null
			&& team.SyncedMatchPhase == MatchPhase.MatchOver
			&& team.NetPhaseTimeRemaining > 0f;
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
