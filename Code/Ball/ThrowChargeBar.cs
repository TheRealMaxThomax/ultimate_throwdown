using Sandbox;
using Sandbox.Rendering;
using System;

/// <summary>
/// Throw wind-up indicator (local owner HUD). Vertical bar on the right screen edge while charging.
/// </summary>
public sealed class ThrowChargeBar : Component
{
	[Property] public float MarginRight { get; set; } = 24f;
	[Property] public float MarginBottom { get; set; } = 24f;
	[Property] public float GapFromDodgePanel { get; set; } = 12f;
	[Property] public float DodgePanelHeight { get; set; } = 56f;
	[Property] public float PanelWidth { get; set; } = 52f;
	[Property] public float PanelPaddingTop { get; set; } = 8f;
	[Property] public float PanelPaddingBottom { get; set; } = 10f;
	[Property] public int BarBlockCount { get; set; } = 14;
	[Property] public float BarRowHeight { get; set; } = 18f;
	[Property] public float BarRowGap { get; set; } = 3f;
	[Property] public int LabelFontSize { get; set; } = 18;
	[Property] public Color BarFillColor { get; set; } = new( 1f, 0.85f, 0.45f );
	[Property] public Color BarEmptyColor { get; set; } = new( 0.35f, 0.35f, 0.35f, 0.85f );

	private bool isVisible;
	private float charge01;

	public void Show()
	{
		isVisible = true;
	}

	public void Hide()
	{
		isVisible = false;
	}

	public void SetCharge( float charge )
	{
		charge01 = charge.Clamp( 0f, 1f );
	}

	protected override void OnUpdate()
	{
		if ( IsProxy || !Network.IsOwner )
			return;

		if ( !isVisible )
			return;

		if ( Scene.Camera is null )
			return;

		var hud = Scene.Camera.Hud;
		var panel = BuildPanelRect();

		hud.DrawRect( panel, new Color( 0f, 0f, 0f, 0.45f ) );

		var labelRect = new Rect( panel.Left, panel.Top + 4f, panel.Width, 22f );
		hud.DrawText( new TextRendering.Scope( "Throw", Color.White, LabelFontSize ), labelRect, TextFlag.Center );

		DrawVerticalBlockBar( hud, BuildBarAreaRect( panel ) );
	}

	private Rect BuildPanelRect()
	{
		var blockCount = BarBlockCount.Clamp( 6, 32 );
		var panelHeight = PanelPaddingTop + 22f + (blockCount * BarRowHeight) + PanelPaddingBottom;
		var x = Screen.Width - PanelWidth - MarginRight;
		var dodgeTop = Screen.Height - MarginBottom - DodgePanelHeight;
		var y = dodgeTop - GapFromDodgePanel - panelHeight;
		return new Rect( x, y, PanelWidth, panelHeight );
	}

	private Rect BuildBarAreaRect( Rect panel )
	{
		var blockCount = BarBlockCount.Clamp( 6, 32 );
		var barHeight = blockCount * BarRowHeight;
		var y = panel.Top + PanelPaddingTop + 22f;
		return new Rect( panel.Left + 6f, y, panel.Width - 12f, barHeight );
	}

	private void DrawVerticalBlockBar( HudPainter hud, Rect barArea )
	{
		var totalBlocks = BarBlockCount.Clamp( 6, 32 );
		var filledBlocks = (int)(charge01 * totalBlocks + 0.5f);
		filledBlocks = filledBlocks.Clamp( 0, totalBlocks );

		var segmentHeight = MathF.Max( 4f, BarRowHeight - BarRowGap );

		for ( var row = 0; row < totalBlocks; row++ )
		{
			var blockIndexFromBottom = totalBlocks - 1 - row;
			var filled = blockIndexFromBottom < filledBlocks;
			var rowRect = new Rect( barArea.Left, barArea.Top + row * BarRowHeight, barArea.Width, segmentHeight );
			var color = filled ? BarFillColor : BarEmptyColor;
			hud.DrawRect( rowRect, color );
		}
	}
}
