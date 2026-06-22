using Sandbox.UI;

/// <summary> World-panel child for <see cref="PracticeLaunchReadoutRoot"/> — big band score digit. </summary>
public sealed class PracticeLaunchScorePanel : Panel
{
	private readonly Label scoreLabel;
	private readonly PracticeLaunchReadout settings;

	public PracticeLaunchScorePanel( PracticeLaunchReadout readoutSettings )
	{
		settings = readoutSettings;

		Style.Width = Length.Percent( 100 );
		Style.Height = Length.Percent( 100 );
		Style.JustifyContent = Justify.Center;
		Style.AlignItems = Align.Center;

		scoreLabel = AddChild<Label>();
		scoreLabel.Text = "—";

		ApplyAppearance();
	}

	public void ApplyAppearance()
	{
		if ( !settings.IsValid() )
			return;

		Style.BackgroundColor = settings.PanelBackgroundColor;

		scoreLabel.Style.FontSize = settings.ScoreFontSize;
		scoreLabel.Style.FontWeight = settings.ScoreFontWeight;
		scoreLabel.Style.FontColor = settings.ScoreColor;

		if ( !string.IsNullOrWhiteSpace( settings.ScoreFontFamily ) )
			scoreLabel.Style.FontFamily = settings.ScoreFontFamily;
	}

	public void SetScore( int score )
	{
		scoreLabel.Text = score <= 0 ? "0" : score.ToString();
	}

	public void SetIdle()
	{
		scoreLabel.Text = "—";
	}
}
