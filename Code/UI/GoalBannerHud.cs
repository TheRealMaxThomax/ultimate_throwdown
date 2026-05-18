using Sandbox;
using Sandbox.Rendering;

/// <summary> Large goal callout during <see cref="MatchPhase.GoalCelebration"/>. </summary>
public sealed class GoalBannerHud : Component
{
	[Property] public float PanelWidth { get; set; } = 640f;
	[Property] public float PanelHeight { get; set; } = 72f;
	[Property] public float CenterYOffset { get; set; } = -80f;
	[Property] public int FontSize { get; set; } = 36;

	protected override void OnUpdate()
	{
		if ( Scene.Camera is null )
			return;

		if ( !MatchHudDraw.TryGetHudState( Scene, out var team, out var config ) )
			return;

		if ( team.SyncedMatchPhase != MatchPhase.GoalCelebration )
			return;

		if ( !MatchTeamIds.IsValid( team.NetLastGoalScoringTeamId ) )
			return;

		var hud = Scene.Camera.Hud;
		var panel = BuildPanelRect();
		MatchHudDraw.DrawPanel( hud, panel, new Color( 0f, 0f, 0f, 0.55f ) );

		var banner = MatchHudDraw.GetScoringTeamBannerText( config, team.NetLastGoalScoringTeamId );
		var textRect = new Rect( panel.Left + 16f, panel.Top, panel.Width - 32f, panel.Height );
		MatchHudDraw.DrawCenteredText( hud, textRect, banner, new Color( 1f, 0.92f, 0.35f ), FontSize );
	}

	private Rect BuildPanelRect()
	{
		var x = (Screen.Width - PanelWidth) * 0.5f;
		var y = (Screen.Height - PanelHeight) * 0.5f + CenterYOffset;
		return new Rect( x, y, PanelWidth, PanelHeight );
	}
}
