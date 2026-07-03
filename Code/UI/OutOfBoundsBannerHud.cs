using System;
using Sandbox;
using Sandbox.Rendering;

/// <summary> Screen callout during ball OOB — white <c>OUT OF BOUNDS!</c> (~3s). </summary>
public sealed class OutOfBoundsBannerHud : Component
{
	[Property] public float PanelWidth { get; set; } = 720f;
	[Property] public float PanelHeight { get; set; } = 80f;
	[Property] public float CenterYOffset { get; set; } = -100f;
	[Property] public int FontSize { get; set; } = 42;
	[Property] public Color TextColor { get; set; } = Color.White;
	[Property] public float BannerSeconds { get; set; } = 3f;

	public static void EnsureOnMainCamera( Scene scene )
	{
		if ( scene is null )
			return;

		foreach ( var camera in scene.GetAllComponents<CameraComponent>() )
		{
			if ( !camera.IsMainCamera )
				continue;

			camera.GameObject.Components.GetOrCreate<OutOfBoundsBannerHud>();
			return;
		}
	}

	protected override void OnUpdate()
	{
		if ( Scene.Camera is null )
			return;

		if ( !MatchHudDraw.TryGetHudState( Scene, out var team, out _ ) )
			return;

		if ( !team.NetBallOobActive )
			return;

		var elapsed = Time.Now - team.NetBallOobSequenceStartTime;
		if ( elapsed < 0f || elapsed > BannerSeconds )
			return;

		var hud = Scene.Camera.Hud;
		var panel = BuildPanelRect();
		MatchHudDraw.DrawPanel( hud, panel, new Color( 0f, 0f, 0f, 0.55f ) );

		var textRect = new Rect( panel.Left + 16f, panel.Top, panel.Width - 32f, panel.Height );
		MatchHudDraw.DrawCenteredText( hud, textRect, "OUT OF BOUNDS!", TextColor, FontSize );
	}

	private Rect BuildPanelRect()
	{
		var x = (Screen.Width - PanelWidth) * 0.5f;
		var y = (Screen.Height - PanelHeight) * 0.5f + CenterYOffset;
		return new Rect( x, y, PanelWidth, PanelHeight );
	}
}
