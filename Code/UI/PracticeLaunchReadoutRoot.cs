using Sandbox.UI;

/// <summary> Root <see cref="PanelComponent"/> for <see cref="PracticeLaunchReadout"/> + <see cref="WorldPanel"/>. </summary>
public sealed class PracticeLaunchReadoutRoot : PanelComponent
{
	private PracticeLaunchScorePanel scorePanel;

	protected override void OnTreeFirstBuilt()
	{
		base.OnTreeFirstBuilt();

		var readout = Components.Get<PracticeLaunchReadout>();
		scorePanel = new PracticeLaunchScorePanel( readout );
		scorePanel.Parent = Panel;
	}

	public void RefreshAppearance()
	{
		scorePanel?.ApplyAppearance();
	}

	public void SetScore( int score )
	{
		scorePanel?.SetScore( score );
	}

	public void SetIdle()
	{
		scorePanel?.SetIdle();
	}
}
