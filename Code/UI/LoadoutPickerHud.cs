using System;
using Sandbox;
using Sandbox.Rendering;

/// <summary> Owner loadout picker — class + ult; intermission or practice. Toggle <c>Menu</c> (Q). </summary>
public sealed class LoadoutPickerHud : Component
{
	[Property] public float PanelWidth { get; set; } = 520f;
	[Property] public float PanelHeight { get; set; } = 360f;
	[Property] public float ButtonHeight { get; set; } = 44f;
	[Property] public float ButtonGap { get; set; } = 8f;
	[Property] public int TitleFontSize { get; set; } = 28;
	[Property] public int BodyFontSize { get; set; } = 20;
	[Property] public int HintFontSize { get; set; } = 18;

	private LoadoutClientState clientState;
	private PlayerTeam playerTeam;

	protected override void OnStart()
	{
		clientState = Components.Get<LoadoutClientState>();
		playerTeam = Components.Get<PlayerTeam>();
	}

	protected override void OnUpdate()
	{
		if ( IsProxy || !Network.IsOwner )
			return;

		if ( Scene.Camera is null )
			return;

		clientState ??= Components.Get<LoadoutClientState>();
		playerTeam ??= Components.Get<PlayerTeam>();
		if ( !clientState.IsValid() || !playerTeam.IsValid() )
			return;

		var hud = Scene.Camera.Hud;
		var swapAllowed = LoadoutAuthority.IsLoadoutSwapAllowed( Scene, playerTeam );

		if ( !swapAllowed )
			return;

		if ( !clientState.IsPickerOpen )
		{
			if ( playerTeam.SyncedMatchPhase == MatchPhase.Intermission )
				DrawIntermissionHint( hud );
			return;
		}

		DrawPicker( hud, clientState );
	}

	private void DrawIntermissionHint( HudPainter hud )
	{
		var hint = "Q — Loadout";
		var rect = new Rect( 24f, Screen.Height - 48f, 200f, 32f );
		MatchHudDraw.DrawLeftText( hud, rect, hint, new Color( 0.9f, 0.9f, 0.9f ), HintFontSize );
	}

	private void DrawPicker( HudPainter hud, LoadoutClientState state )
	{
		var panel = BuildPanelRect();
		MatchHudDraw.DrawPanel( hud, panel, new Color( 0f, 0f, 0f, 0.72f ) );

		var inner = panel.Shrink( 16f );
		var y = inner.Top;

		var titleRect = new Rect( inner.Left, y, inner.Width, 36f );
		MatchHudDraw.DrawCenteredText( hud, titleRect, "LOADOUT", Color.White, TitleFontSize );
		y += 40f;

		var classLabel = new Rect( inner.Left, y, inner.Width, 24f );
		MatchHudDraw.DrawLeftText( hud, classLabel, "Class", new Color( 0.75f, 0.75f, 0.75f ), BodyFontSize );
		y += 28f;

		y = DrawClassButtons( hud, state, inner, y );
		y += 12f;

		var ultLabel = new Rect( inner.Left, y, inner.Width, 24f );
		MatchHudDraw.DrawLeftText( hud, ultLabel, "Ultimate", new Color( 0.75f, 0.75f, 0.75f ), BodyFontSize );
		y += 28f;

		y = DrawUltButtons( hud, state, inner, y );
		y += 8f;

		var passiveLine = $"Passive: {FormatPassiveId( state.PendingLoadout.PassiveId )} (auto)";
		var passiveRect = new Rect( inner.Left, y, inner.Width, 24f );
		MatchHudDraw.DrawLeftText( hud, passiveRect, passiveLine, new Color( 0.85f, 0.85f, 0.85f ), BodyFontSize );
		y += 32f;

		var confirmRect = new Rect( inner.Left, inner.Bottom - ButtonHeight, inner.Width, ButtonHeight );
		DrawButton( hud, confirmRect, "Confirm", highlighted: true );
		if ( Input.Pressed( "Attack1" ) && MatchHudDraw.IsMouseOverRect( confirmRect ) )
			state.ConfirmPending();
	}

	private float DrawClassButtons( HudPainter hud, LoadoutClientState state, Rect inner, float y )
	{
		var classIds = LoadoutCatalog.GetAllClassIds();
		var count = classIds.Count;
		if ( count == 0 )
			return y;

		var totalGap = ButtonGap * (count - 1);
		var buttonWidth = (inner.Width - totalGap) / count;

		for ( var i = 0; i < count; i++ )
		{
			var classId = classIds[i];
			var x = inner.Left + i * (buttonWidth + ButtonGap);
			var rect = new Rect( x, y, buttonWidth, ButtonHeight );
			var selected = string.Equals( state.PendingLoadout.ClassId, classId, StringComparison.OrdinalIgnoreCase );
			DrawButton( hud, rect, LoadoutCatalog.GetClassDisplayName( classId ), selected );

			if ( Input.Pressed( "Attack1" ) && MatchHudDraw.IsMouseOverRect( rect ) )
				state.SetPendingClass( classId );
		}

		return y + ButtonHeight;
	}

	private float DrawUltButtons( HudPainter hud, LoadoutClientState state, Rect inner, float y )
	{
		if ( !LoadoutCatalog.TryGetUltIdsForClass( state.PendingLoadout.ClassId, out var ultIds ) || ultIds.Length == 0 )
		{
			var noneRect = new Rect( inner.Left, y, inner.Width, ButtonHeight );
			DrawButton( hud, noneRect, "(none yet)", highlighted: false, enabled: false );
			return y + ButtonHeight;
		}

		var count = ultIds.Length;
		var totalGap = ButtonGap * (count - 1);
		var buttonWidth = (inner.Width - totalGap) / count;

		for ( var i = 0; i < count; i++ )
		{
			var ultId = ultIds[i];
			var x = inner.Left + i * (buttonWidth + ButtonGap);
			var rect = new Rect( x, y, buttonWidth, ButtonHeight );
			var selected = string.Equals( state.PendingLoadout.UltId, ultId, StringComparison.OrdinalIgnoreCase );
			DrawButton( hud, rect, LoadoutCatalog.GetUltDisplayName( ultId ), selected );

			if ( Input.Pressed( "Attack1" ) && MatchHudDraw.IsMouseOverRect( rect ) )
				state.SetPendingUlt( ultId );
		}

		return y + ButtonHeight;
	}

	private static void DrawButton( HudPainter hud, Rect rect, string label, bool highlighted, bool enabled = true )
	{
		var bg = !enabled
			? new Color( 0.15f, 0.15f, 0.15f, 0.6f )
			: highlighted
				? new Color( 0.14f, 0.42f, 0.72f, 0.95f )
				: new Color( 0.2f, 0.2f, 0.2f, 0.9f );

		if ( enabled && MatchHudDraw.IsMouseOverRect( rect ) && !highlighted )
			bg = new Color( 0.28f, 0.28f, 0.28f, 0.95f );

		MatchHudDraw.DrawPanel( hud, rect, bg );
		var textColor = enabled ? Color.White : new Color( 0.55f, 0.55f, 0.55f );
		MatchHudDraw.DrawCenteredText( hud, rect, label, textColor, 18 );
	}

	private static string FormatPassiveId( string passiveId )
	{
		if ( string.IsNullOrWhiteSpace( passiveId ) )
			return "default";

		return passiveId.Replace( '_', ' ' );
	}

	private Rect BuildPanelRect()
	{
		var x = (Screen.Width - PanelWidth) * 0.5f;
		var y = (Screen.Height - PanelHeight) * 0.5f;
		return new Rect( x, y, PanelWidth, PanelHeight );
	}
}
