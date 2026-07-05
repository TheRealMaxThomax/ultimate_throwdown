using Sandbox;
using Sandbox.Rendering;

/// <summary> Match timer in M.SS form (10.00, 9.59, …). Pauses during celebration / intermission; shows OT when applicable. </summary>
public sealed class MatchClockHud : Component
{
	[Property] public float MarginTop { get; set; } = 72f;
	[Property] public float PanelWidth { get; set; } = 120f;
	[Property] public float PanelHeight { get; set; } = 40f;
	[Property] public int FontSize { get; set; } = 28;

	protected override void OnUpdate()
	{
		if ( Scene.Camera is null )
			return;

		if ( !MatchHudDraw.TryGetHudState( Scene, out var team, out var config ) )
			return;

		if ( !MatchHudDraw.ShowTopMatchHud( config ) )
			return;

		if ( team.SyncedMatchPhase == MatchPhase.MatchOver )
			return;

		var hud = Scene.Camera.Hud;
		var panel = BuildPanelRect();
		MatchHudDraw.DrawPanel( hud, panel, new Color( 0f, 0f, 0f, 0.45f ) );

		var clock = MatchHudDraw.FormatMatchClock( team.NetMatchTimeRemaining, team.NetIsOvertime );
		var color = team.NetIsOvertime ? new Color( 1f, 0.75f, 0.25f ) : Color.White;

		var textRect = new Rect( panel.Left, panel.Top, panel.Width, panel.Height );
		MatchHudDraw.DrawCenteredText( hud, textRect, clock, color, FontSize );
	}

	private Rect BuildPanelRect()
	{
		var x = (Screen.Width - PanelWidth) * 0.5f;
		var y = MarginTop;
		return new Rect( x, y, PanelWidth, PanelHeight );
	}
}
