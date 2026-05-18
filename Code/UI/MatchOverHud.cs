using System;
using Sandbox;
using Sandbox.Rendering;

/// <summary> Match over: winner celebration (movement allowed), then final score + host rematch. </summary>
public sealed class MatchOverHud : Component
{
	[Property] public float CelebrationPanelWidth { get; set; } = 640f;
	[Property] public float CelebrationPanelHeight { get; set; } = 72f;
	[Property] public float CelebrationCenterYOffset { get; set; } = -80f;
	[Property] public int CelebrationFontSize { get; set; } = 36;

	[Property] public float PanelWidth { get; set; } = 560f;
	[Property] public float PanelHeight { get; set; } = 200f;
	[Property] public float ButtonWidth { get; set; } = 220f;
	[Property] public float ButtonHeight { get; set; } = 48f;
	[Property] public int TitleFontSize { get; set; } = 36;
	[Property] public int ScoreFontSize { get; set; } = 26;
	[Property] public int ActionFontSize { get; set; } = 24;
	/// <summary> Vote slot for rematch (1 = <c>Slot1</c> key). Future map vote uses 2, 3, … </summary>
	[Property] public int RematchVoteSlot { get; set; } = 1;

	protected override void OnUpdate()
	{
		if ( Scene.Camera is null )
			return;

		if ( !MatchHudDraw.TryGetHudState( Scene, out var team, out var config ) )
			return;

		if ( team.SyncedMatchPhase != MatchPhase.MatchOver )
			return;

		if ( !MatchTeamIds.IsValid( team.NetMatchWinnerTeamId ) )
			return;

		var winnerLine = MatchHudDraw.GetMatchWinnerBannerText( config, team.NetMatchWinnerTeamId );
		var hud = Scene.Camera.Hud;

		if ( MatchHudDraw.IsMatchOverCelebrating( team ) )
		{
			DrawCelebrationBanner( hud, winnerLine );
			return;
		}

		DrawRematchPanel( hud, team, config, winnerLine );
	}

	private void DrawCelebrationBanner( HudPainter hud, string winnerLine )
	{
		var panel = BuildCelebrationPanelRect();
		MatchHudDraw.DrawPanel( hud, panel, new Color( 0f, 0f, 0f, 0.55f ) );

		var textRect = new Rect( panel.Left + 16f, panel.Top, panel.Width - 32f, panel.Height );
		MatchHudDraw.DrawCenteredText( hud, textRect, winnerLine, new Color( 1f, 0.92f, 0.35f ), CelebrationFontSize );
	}

	private void DrawRematchPanel( HudPainter hud, PlayerTeam team, MapMatchConfig config, string winnerLine )
	{
		var panel = BuildRematchPanelRect();
		MatchHudDraw.DrawPanel( hud, panel, new Color( 0f, 0f, 0f, 0.65f ) );

		var inner = panel.Shrink( 16f );
		var titleRect = new Rect( inner.Left, inner.Top, inner.Width, 52f );
		var scoreRect = new Rect( inner.Left, inner.Top + 56f, inner.Width, 40f );
		var actionRect = new Rect( inner.Left, inner.Bottom - ButtonHeight, inner.Width, ButtonHeight );

		MatchHudDraw.DrawCenteredText( hud, titleRect, winnerLine, new Color( 1f, 0.92f, 0.35f ), TitleFontSize );

		var scoreLine = MatchHudDraw.FormatFinalRoundScore( config, team.NetTeam0RoundWins, team.NetTeam1RoundWins );
		MatchHudDraw.DrawCenteredText( hud, scoreRect, scoreLine, Color.White, ScoreFontSize );

		if ( Networking.IsHost )
		{
			var voteAction = GetVoteSlotAction( RematchVoteSlot );
			var buttonRect = BuildRematchButtonRect( actionRect, ButtonWidth, ButtonHeight );
			var pressed = Input.Down( voteAction );
			var buttonBg = pressed ? new Color( 0.25f, 0.55f, 0.3f, 0.95f ) : new Color( 0.15f, 0.4f, 0.22f, 0.9f );
			MatchHudDraw.DrawPanel( hud, buttonRect, buttonBg );
			MatchHudDraw.DrawCenteredText( hud, buttonRect, $"{RematchVoteSlot}  Rematch", Color.White, ActionFontSize );

			if ( Input.Pressed( voteAction ) )
			{
				var director = MatchDirector.FindInScene( Scene );
				director?.HostRequestRematch();
			}
		}
		else
		{
			MatchHudDraw.DrawCenteredText( hud, actionRect, "Waiting for host…", new Color( 0.85f, 0.85f, 0.85f ), ActionFontSize );
		}
	}

	private Rect BuildCelebrationPanelRect()
	{
		var x = (Screen.Width - CelebrationPanelWidth) * 0.5f;
		var y = (Screen.Height - CelebrationPanelHeight) * 0.5f + CelebrationCenterYOffset;
		return new Rect( x, y, CelebrationPanelWidth, CelebrationPanelHeight );
	}

	private Rect BuildRematchPanelRect()
	{
		var x = (Screen.Width - PanelWidth) * 0.5f;
		var y = (Screen.Height - PanelHeight) * 0.5f;
		return new Rect( x, y, PanelWidth, PanelHeight );
	}

	private static Rect BuildRematchButtonRect( Rect actionRow, float width, float height )
	{
		var x = actionRow.Left + (actionRow.Width - width) * 0.5f;
		var y = actionRow.Top;
		return new Rect( x, y, width, height );
	}

	/// <summary> <c>Slot1</c>…<c>Slot9</c> from <c>Input.config</c> (keyboard 1–9). </summary>
	private static string GetVoteSlotAction( int slot )
	{
		slot = Math.Clamp( slot, 1, 9 );
		return $"Slot{slot}";
	}
}
