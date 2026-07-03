using Sandbox.UI;

/// <summary> Vertical world stack for <see cref="BallOobDropZoneMarker"/> — countdown, arrow, label. </summary>
public sealed class BallOobDropZoneStackPanel : Panel
{
	private readonly Label countdownLabel;
	private readonly Label arrowLabel;
	private readonly Label dropZoneLabel;

	public BallOobDropZoneStackPanel( BallOobDropZoneHud settings )
	{
		Style.Width = Length.Percent( 100 );
		Style.Height = Length.Percent( 100 );
		Style.FlexDirection = FlexDirection.Column;
		Style.JustifyContent = Justify.Center;
		Style.AlignItems = Align.Center;

		countdownLabel = AddChild<Label>();
		arrowLabel = AddChild<Label>();
		dropZoneLabel = AddChild<Label>();

		arrowLabel.Text = "▼";
		dropZoneLabel.Text = "DROP ZONE";

		ApplyAppearance( settings );
	}

	public void ApplyAppearance( BallOobDropZoneHud settings )
	{
		if ( !settings.IsValid() )
			return;

		var font = string.IsNullOrWhiteSpace( settings.StackFontFamily ) ? "Les Flos Sage" : settings.StackFontFamily;
		var color = settings.StackTextColor;

		countdownLabel.Style.FontFamily = font;
		countdownLabel.Style.FontSize = settings.CountdownFontSize;
		countdownLabel.Style.FontWeight = settings.CountdownFontWeight;
		countdownLabel.Style.FontColor = color;

		arrowLabel.Style.FontFamily = font;
		arrowLabel.Style.FontSize = settings.ArrowFontSize;
		arrowLabel.Style.FontWeight = settings.ArrowFontWeight;
		arrowLabel.Style.FontColor = color;

		dropZoneLabel.Style.FontFamily = font;
		dropZoneLabel.Style.FontSize = settings.DropZoneFontSize;
		dropZoneLabel.Style.FontWeight = settings.DropZoneFontWeight;
		dropZoneLabel.Style.FontColor = color;
	}

	public void SetCountdownSeconds( int seconds )
	{
		countdownLabel.Text = Math.Max( 0, seconds ).ToString();
	}
}
