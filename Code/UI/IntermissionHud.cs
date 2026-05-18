using System;
using Sandbox;
using Sandbox.Rendering;

/// <summary> Intermission countdown during <see cref="MatchPhase.Intermission"/>. </summary>
public sealed class IntermissionHud : Component
{
	[Property] public float PanelWidth { get; set; } = 480f;
	[Property] public float PanelHeight { get; set; } = 56f;
	[Property] public float CenterYOffset { get; set; } = 40f;
	[Property] public int FontSize { get; set; } = 30;

	protected override void OnUpdate()
	{
		if ( Scene.Camera is null )
			return;

		if ( !MatchHudDraw.TryGetHudState( Scene, out var team, out _ ) )
			return;

		if ( team.SyncedMatchPhase != MatchPhase.Intermission )
			return;

		var seconds = MathF.Ceiling( MathF.Max( 0f, team.NetPhaseTimeRemaining ) );
		var line = $"Resuming in {seconds:0}…";

		var hud = Scene.Camera.Hud;
		var panel = BuildPanelRect();
		MatchHudDraw.DrawPanel( hud, panel, new Color( 0f, 0f, 0f, 0.55f ) );

		var textRect = new Rect( panel.Left + 12f, panel.Top, panel.Width - 24f, panel.Height );
		MatchHudDraw.DrawCenteredText( hud, textRect, line, Color.White, FontSize );
	}

	private Rect BuildPanelRect()
	{
		var x = (Screen.Width - PanelWidth) * 0.5f;
		var y = (Screen.Height - PanelHeight) * 0.5f + CenterYOffset;
		return new Rect( x, y, PanelWidth, PanelHeight );
	}
}
