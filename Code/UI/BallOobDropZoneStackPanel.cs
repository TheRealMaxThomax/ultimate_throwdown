using Sandbox.UI;

/// <summary> Vertical world stack for <see cref="BallOobDropZoneMarker"/> — DROP ZONE, countdown, ▼. </summary>
public sealed class BallOobDropZoneStackPanel : Panel
{
	private readonly Label dropZoneLabel;
	private readonly Label countdownLabel;
	private readonly Label arrowLabel;

	public BallOobDropZoneStackPanel( BallOobDropZoneHud settings )
	{
		Style.Width = Length.Percent( 100 );
		Style.Height = Length.Percent( 100 );
		Style.FlexDirection = FlexDirection.Column;
		Style.JustifyContent = Justify.Center;
		Style.AlignItems = Align.Center;
		Style.BackgroundColor = Color.Transparent;

		dropZoneLabel = AddChild<Label>();
		countdownLabel = AddChild<Label>();
		arrowLabel = AddChild<Label>();

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
		var padding = MathF.Max( 0f, settings.StackPanelPadding );
		var rowGap = MathF.Max( 0f, settings.StackRowGap );

		Style.PaddingLeft = Length.Pixels( padding );
		Style.PaddingRight = Length.Pixels( padding );
		Style.PaddingTop = Length.Pixels( padding );
		Style.PaddingBottom = Length.Pixels( padding );

		ApplyLabelStyle( dropZoneLabel, font, color, settings.DropZoneFontSize, settings.DropZoneFontWeight, 0f );
		ApplyLabelStyle( countdownLabel, font, color, settings.CountdownFontSize, settings.CountdownFontWeight, rowGap );
		ApplyLabelStyle( arrowLabel, font, color, settings.ArrowFontSize, settings.ArrowFontWeight, rowGap );
		dropZoneLabel.Style.Width = Length.Percent( 100 );
		dropZoneLabel.Style.TextAlign = TextAlign.Center;
	}

	static void ApplyLabelStyle( Label label, string font, Color color, int fontSize, int fontWeight, float marginTop )
	{
		label.Style.FontFamily = font;
		label.Style.FontSize = fontSize;
		label.Style.FontWeight = fontWeight;
		label.Style.FontColor = color;
		label.Style.MarginTop = Length.Pixels( marginTop );
	}

	public void SetCountdownSeconds( int seconds )
	{
		countdownLabel.Text = Math.Max( 0, seconds ).ToString();
	}
}
