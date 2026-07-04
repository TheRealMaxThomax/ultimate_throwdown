using Sandbox.UI;

/// <summary>
/// One shadow + fill text row for <see cref="BallOobDropZoneStackPanel"/> (world signage — no highlight extrusion).
/// </summary>
sealed class BallOobDropZoneComicRow : Panel
{
	private readonly Panel wordStack;
	private readonly Label shadowLabel;
	private readonly Label fillLabel;

	public BallOobDropZoneComicRow()
	{
		Style.Position = PositionMode.Relative;
		Style.Width = Length.Percent( 100 );
		Style.Overflow = OverflowMode.Visible;

		wordStack = AddChild<Panel>();
		wordStack.Style.Position = PositionMode.Relative;
		wordStack.Style.Width = Length.Percent( 100 );
		wordStack.Style.Overflow = OverflowMode.Visible;

		shadowLabel = wordStack.AddChild<Label>();
		fillLabel = wordStack.AddChild<Label>();

		foreach ( var label in new[] { shadowLabel, fillLabel } )
		{
			label.Style.Position = PositionMode.Absolute;
			label.Style.Left = Length.Pixels( 0 );
			label.Style.Top = Length.Pixels( 0 );
			label.Style.TextAlign = TextAlign.Center;
			label.Style.Width = Length.Percent( 100 );
		}
	}

	public void SetText( string text )
	{
		shadowLabel.Text = text;
		fillLabel.Text = text;
	}

	public void ApplyStyle(
		string font,
		Color fillColor,
		Color shadowColor,
		Vector2 shadowOffset,
		int fontSize,
		int fontWeight,
		float marginTop )
	{
		Style.MarginTop = Length.Pixels( marginTop );

		wordStack.Style.MinHeight = Length.Pixels( fontSize * 1.05f );
		wordStack.Style.PaddingRight = Length.Pixels( MathF.Abs( shadowOffset.x ) );
		wordStack.Style.PaddingBottom = Length.Pixels( MathF.Abs( shadowOffset.y ) );

		ApplyLayerStyle( shadowLabel, font, fontSize, fontWeight, shadowColor, shadowOffset );
		ApplyLayerStyle( fillLabel, font, fontSize, fontWeight, fillColor, Vector2.Zero );
	}

	static void ApplyLayerStyle(
		Label label,
		string font,
		int fontSize,
		int fontWeight,
		Color color,
		Vector2 offset )
	{
		label.Style.FontFamily = font;
		label.Style.FontSize = fontSize;
		label.Style.FontWeight = fontWeight;
		label.Style.FontColor = color;
		label.Style.Left = Length.Pixels( offset.x );
		label.Style.Top = Length.Pixels( offset.y );
	}
}
