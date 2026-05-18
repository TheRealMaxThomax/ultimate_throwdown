using Sandbox;
using Sandbox.Rendering;

/// <summary>
/// Placeholder dodge cooldown readout (local owner HUD). Replace with Razor/proper UI later.
/// </summary>
public sealed class DodgeCooldownHud : Component
{
	[Property] public float MarginRight { get; set; } = 24f;
	[Property] public float MarginBottom { get; set; } = 24f;
	[Property] public float PanelWidth { get; set; } = 120f;
	[Property] public float PanelHeight { get; set; } = 56f;
	[Property] public int FontSize { get; set; } = 20;

	private PlayerDodge playerDodge;

	protected override void OnStart()
	{
		playerDodge = Components.Get<PlayerDodge>();
	}

	protected override void OnUpdate()
	{
		if ( IsProxy || !Network.IsOwner )
			return;

		if ( Scene.Camera is null )
			return;

		playerDodge ??= Components.Get<PlayerDodge>();
		if ( playerDodge is null )
			return;

		var hud = Scene.Camera.Hud;
		var panel = BuildPanelRect();

		hud.DrawRect( panel, new Color( 0f, 0f, 0f, 0.45f ) );

		var remaining = playerDodge.DodgeCooldownRemaining;
		var timerLine = remaining > 0f ? $"{remaining:F1}s" : "Ready";
		var timerColor = remaining > 0f ? new Color( 1f, 0.55f, 0.2f ) : new Color( 0.45f, 1f, 0.55f );

		var labelRect = new Rect( panel.Left, panel.Top + 6f, panel.Width - 10f, 22f );
		var timerRect = new Rect( panel.Left, panel.Top + 30f, panel.Width - 10f, 22f );

		hud.DrawText( new TextRendering.Scope( "Dodge", Color.White, FontSize ), labelRect, TextFlag.Right );
		hud.DrawText( new TextRendering.Scope( timerLine, timerColor, FontSize ), timerRect, TextFlag.Right );
	}

	private Rect BuildPanelRect()
	{
		var x = Screen.Width - PanelWidth - MarginRight;
		var y = Screen.Height - PanelHeight - MarginBottom;
		return new Rect( x, y, PanelWidth, PanelHeight );
	}
}
