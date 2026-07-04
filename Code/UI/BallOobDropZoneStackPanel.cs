using Sandbox.UI;

/// <summary> Vertical world stack for <see cref="BallOobDropZoneMarker"/> — DROP ZONE, countdown, ▼. </summary>
public sealed class BallOobDropZoneStackPanel : Panel
{
	private readonly BallOobDropZoneComicRow dropZoneRow;
	private readonly BallOobDropZoneComicRow countdownRow;
	private readonly BallOobDropZoneComicRow arrowRow;

	public BallOobDropZoneStackPanel( BallOobDropZoneHud settings )
	{
		Style.Width = Length.Percent( 100 );
		Style.Height = Length.Percent( 100 );
		Style.FlexDirection = FlexDirection.Column;
		Style.JustifyContent = Justify.Center;
		Style.AlignItems = Align.Center;
		Style.BackgroundColor = Color.Transparent;
		Style.Overflow = OverflowMode.Visible;

		dropZoneRow = AddChild<BallOobDropZoneComicRow>();
		countdownRow = AddChild<BallOobDropZoneComicRow>();
		arrowRow = AddChild<BallOobDropZoneComicRow>();

		dropZoneRow.SetText( "DROP ZONE" );
		arrowRow.SetText( "▼" );

		ApplyAppearance( settings );
	}

	public void ApplyAppearance( BallOobDropZoneHud settings )
	{
		if ( !settings.IsValid() )
			return;

		var font = string.IsNullOrWhiteSpace( settings.StackFontFamily ) ? "Les Flos Sage" : settings.StackFontFamily;
		var fillColor = settings.StackTextColor;
		var padding = MathF.Max( 0f, settings.StackPanelPadding );
		var rowGap = MathF.Max( 0f, settings.StackRowGap );
		var shadowOffset = settings.StackShadowOffsetPixels;

		Style.PaddingLeft = Length.Pixels( padding );
		Style.PaddingRight = Length.Pixels( padding );
		Style.PaddingTop = Length.Pixels( padding );
		Style.PaddingBottom = Length.Pixels( padding );

		dropZoneRow.ApplyStyle(
			font,
			fillColor,
			settings.StackShadowColor,
			shadowOffset,
			settings.DropZoneFontSize,
			settings.DropZoneFontWeight,
			0f );

		countdownRow.ApplyStyle(
			font,
			fillColor,
			settings.StackShadowColor,
			shadowOffset,
			settings.CountdownFontSize,
			settings.CountdownFontWeight,
			rowGap );

		arrowRow.ApplyStyle(
			font,
			fillColor,
			settings.StackShadowColor,
			shadowOffset,
			settings.ArrowFontSize,
			settings.ArrowFontWeight,
			rowGap );
	}

	public void SetCountdownSeconds( int seconds )
	{
		countdownRow.SetText( Math.Max( 0, seconds ).ToString() );
	}
}
