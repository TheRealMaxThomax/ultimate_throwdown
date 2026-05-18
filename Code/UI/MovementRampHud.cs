using Sandbox;
using Sandbox.Rendering;

/// <summary>
/// Placeholder walk / sprint / charge ramp HUD (local owner). Replace with proper UI later.
/// </summary>
public sealed class MovementRampHud : Component
{
	[Property] public float MarginRight { get; set; } = 24f;
	[Property] public float MarginBottom { get; set; } = 24f;
	[Property] public float DodgePanelWidth { get; set; } = 120f;
	[Property] public float GapFromDodgePanel { get; set; } = 12f;
	[Property] public float PanelWidth { get; set; } = 128f;
	[Property] public float PanelHeight { get; set; } = 56f;
	[Property] public int BarBlockCount { get; set; } = 14;
	[Property] public int LabelFontSize { get; set; } = 20;
	[Property] public int BarFontSize { get; set; } = 14;

	private CatchUpSpeedBoost speedBoost;

	protected override void OnStart()
	{
		speedBoost = Components.Get<CatchUpSpeedBoost>();
	}

	protected override void OnUpdate()
	{
		if ( IsProxy || !Network.IsOwner )
			return;

		if ( Scene.Camera is null )
			return;

		speedBoost ??= Components.Get<CatchUpSpeedBoost>();
		if ( speedBoost is null )
			return;

		speedBoost.GetMovementRampDisplay( out var tier, out var progress01 );

		var hud = Scene.Camera.Hud;
		var panel = BuildPanelRect();

		hud.DrawRect( panel, new Color( 0f, 0f, 0f, 0.45f ) );

		var labelRect = new Rect( panel.Left + 8f, panel.Top + 6f, panel.Width - 16f, 22f );
		var barRect = new Rect( panel.Left + 8f, panel.Top + 30f, panel.Width - 16f, 22f );

		hud.DrawText( new TextRendering.Scope( TierLabel( tier ), Color.White, LabelFontSize ), labelRect, TextFlag.Left );
		hud.DrawText( new TextRendering.Scope( BuildBlockBar( progress01, BarBlockCount ), new Color( 0.55f, 0.85f, 1f ), BarFontSize ), barRect, TextFlag.Left );
	}

	private Rect BuildPanelRect()
	{
		var dodgeLeft = Screen.Width - DodgePanelWidth - MarginRight;
		var x = dodgeLeft - GapFromDodgePanel - PanelWidth;
		var y = Screen.Height - PanelHeight - MarginBottom;
		return new Rect( x, y, PanelWidth, PanelHeight );
	}

	private static string TierLabel( MovementRampTier tier )
	{
		return tier switch
		{
			MovementRampTier.Walk => "Walk",
			MovementRampTier.Sprint => "Sprint",
			MovementRampTier.Charge => "Charge",
			_ => "Walk",
		};
	}

	private static string BuildBlockBar( float progress01, int totalBlocks )
	{
		totalBlocks = totalBlocks.Clamp( 6, 32 );
		var filledBlocks = (int)(progress01.Clamp( 0f, 1f ) * totalBlocks + 0.5f);
		filledBlocks = filledBlocks.Clamp( 0, totalBlocks );

		var bar = "[";
		for ( var i = 0; i < totalBlocks; i++ )
			bar += i < filledBlocks ? "█" : "░";
		bar += "]";

		return bar;
	}
}
