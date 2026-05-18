using Sandbox;
using Sandbox.Rendering;

/// <summary> Top bar: team names + round wins (reads synced state from any active <see cref="PlayerTeam"/>). </summary>
public sealed class MatchScoreHud : Component
{
	[Property] public float MarginTop { get; set; } = 20f;
	[Property] public float PanelWidth { get; set; } = 520f;
	[Property] public float PanelHeight { get; set; } = 48f;
	[Property] public int FontSize { get; set; } = 26;

	protected override void OnUpdate()
	{
		if ( Scene.Camera is null )
			return;

		if ( !MatchHudDraw.TryGetHudState( Scene, out var team, out var config ) )
			return;

		if ( team.SyncedMatchPhase == MatchPhase.MatchOver )
			return;

		var hud = Scene.Camera.Hud;
		var panel = BuildPanelRect();
		MatchHudDraw.DrawPanel( hud, panel, new Color( 0f, 0f, 0f, 0.45f ) );

		var team0Name = config.IsValid() ? config.Team0DisplayName : "Team A";
		var team1Name = config.IsValid() ? config.Team1DisplayName : "Team B";
		var line = $"{team0Name}  {team.NetTeam0RoundWins} — {team.NetTeam1RoundWins}  {team1Name}";

		var textRect = new Rect( panel.Left + 12f, panel.Top, panel.Width - 24f, panel.Height );
		MatchHudDraw.DrawCenteredText( hud, textRect, line, Color.White, FontSize );
	}

	private Rect BuildPanelRect()
	{
		var x = (Screen.Width - PanelWidth) * 0.5f;
		var y = MarginTop;
		return new Rect( x, y, PanelWidth, PanelHeight );
	}
}
