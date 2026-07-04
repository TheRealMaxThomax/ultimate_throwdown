using Sandbox.UI;

/// <summary> Root <see cref="PanelComponent"/> for <see cref="BallOobDropZoneMarker"/>. </summary>
public sealed class BallOobDropZonePanelRoot : PanelComponent
{
	private BallOobDropZoneStackPanel stackPanel;

	protected override void OnTreeFirstBuilt()
	{
		base.OnTreeFirstBuilt();

		var hud = BallOobDropZoneHud.FindInScene( Scene );
		stackPanel = new BallOobDropZoneStackPanel( hud );
		stackPanel.Parent = Panel;
	}

	public void RefreshAppearance()
	{
		var hud = BallOobDropZoneHud.FindInScene( Scene );
		stackPanel?.ApplyAppearance( hud );
	}

	public void SetCountdownSeconds( int seconds )
	{
		stackPanel?.SetCountdownSeconds( seconds );
	}

	public void UpdateMotion( BallOobDropZoneHud settings, float timeNow )
	{
		stackPanel?.UpdateMotion( settings, timeNow );
	}
}
