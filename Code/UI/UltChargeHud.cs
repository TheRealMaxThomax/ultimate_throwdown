using Sandbox;
using Sandbox.Rendering;
using System;

/// <summary> Owner-only ult charge readout (0–100%). Placeholder until circular ult UI. </summary>
public sealed class UltChargeHud : Component
{
	[Property] public float MarginRight { get; set; } = 24f;
	[Property] public float MarginBottom { get; set; } = 24f;
	[Property] public float DodgePanelWidth { get; set; } = 120f;
	[Property] public float MovementPanelWidth { get; set; } = 220f;
	[Property] public float GapBetweenPanels { get; set; } = 12f;
	[Property] public float PanelWidth { get; set; } = 148f;
	[Property] public float PanelHeight { get; set; } = 56f;
	[Property] public int PercentFontSize { get; set; } = 22;

	/// <summary> At 100%, stay white this long before switching to ready (blue) highlight. </summary>
	[Property] public float ReadyHighlightDelaySeconds { get; set; } = 0.4f;

	private PlayerUltCharge ultCharge;
	private float fullyChargedHighlightStart = -1f;
	private bool wasFullyCharged;

	protected override void OnStart()
	{
		ultCharge = Components.Get<PlayerUltCharge>();
	}

	protected override void OnUpdate()
	{
		if ( IsProxy || !Network.IsOwner )
			return;

		if ( Scene.Camera is null )
			return;

		ultCharge ??= Components.Get<PlayerUltCharge>();
		if ( ultCharge is null )
			return;

		var ready = ultCharge.IsFullyCharged;
		if ( ready && !wasFullyCharged )
			fullyChargedHighlightStart = Time.Now;
		if ( !ready )
			fullyChargedHighlightStart = -1f;
		wasFullyCharged = ready;

		var hud = Scene.Camera.Hud;
		var panel = BuildPanelRect();
		var displayPercent = (int)MathF.Floor( ultCharge.ChargePercent.Clamp( 0f, 100f ) );

		hud.DrawRect( panel, new Color( 0f, 0f, 0f, 0.45f ) );

		var textRect = new Rect( panel.Left, panel.Top, panel.Width, panel.Height );
		var text = $"{displayPercent}%";

		var showReadyColor = ready
			&& fullyChargedHighlightStart >= 0f
			&& Time.Now - fullyChargedHighlightStart >= ReadyHighlightDelaySeconds;
		var color = showReadyColor ? new Color( 0.55f, 0.85f, 1f ) : Color.White;

		hud.DrawText( new TextRendering.Scope( text, color, PercentFontSize ), textRect, TextFlag.Center );
	}
	private Rect BuildPanelRect()
	{
		var rightStack = MarginRight + DodgePanelWidth + GapBetweenPanels + MovementPanelWidth + GapBetweenPanels;
		var x = Screen.Width - rightStack - PanelWidth;
		var y = Screen.Height - PanelHeight - MarginBottom;
		return new Rect( x, y, PanelWidth, PanelHeight );
	}
}
