using System;
using System.Collections.Generic;

/// <summary>Per-letter exit for vortex and typing-erase — C# during fade window.</summary>
public static class ComicLetterExitMotion
{
	public readonly struct LetterExitFrame
	{
		public LetterExitFrame( float marginLeft, float marginTop, float fontSizeMul, float opacity, float spacingAfterMul = 1f )
		{
			MarginLeft = marginLeft;
			MarginTop = marginTop;
			FontSizeMul = fontSizeMul;
			Opacity = opacity;
			SpacingAfterMul = spacingAfterMul;
		}

		public float MarginLeft { get; }
		public float MarginTop { get; }
		public float FontSizeMul { get; }
		public float Opacity { get; }
		public float SpacingAfterMul { get; }

		public static LetterExitFrame None => new( 0f, 0f, 1f, 1f );
	}

	public static bool UsesLetterMotion( TackleComicTextHud.ComicExitStyle style )
	{
		return style is TackleComicTextHud.ComicExitStyle.LetterSuckInVortex
			or TackleComicTextHud.ComicExitStyle.LetterTypingErase;
	}

	public static LetterExitFrame Evaluate(
		TackleComicTextHud.ComicExitStyle style,
		TackleComicTextHud.ComicFontTier tier,
		int letterIndex,
		IReadOnlyList<ComicLetterStyle> letters,
		float exitT,
		int tackleOctant,
		int letterJitterSeed )
	{
		exitT = MathX.Clamp( exitT, 0f, 1f );
		if ( exitT <= 0.0001f )
			return LetterExitFrame.None;

		var letterCount = letters?.Count ?? 0;
		if ( letterCount <= 0 )
			return LetterExitFrame.None;

		var letter = letters[letterIndex];

		return style switch
		{
			TackleComicTextHud.ComicExitStyle.LetterSuckInVortex => SuckInVortex( tier, letterIndex, letterCount, letter, exitT ),
			TackleComicTextHud.ComicExitStyle.LetterTypingErase => TypingErase( tier, letter, letterCount, exitT ),
			_ => LetterExitFrame.None
		};
	}

	static LetterExitFrame SuckInVortex( TackleComicTextHud.ComicFontTier tier, int letterIndex, int letterCount, ComicLetterStyle letter, float exitT )
	{
		var pull = EaseOut( exitT );
		var wobbleAmp = Dist( tier, 3f, 5f, 7f ) * MathF.Sin( exitT * MathF.PI );
		var angle = letter.OrbitStartRadians + exitT * 1.25f * MathF.PI * 2f;
		var wobbleX = MathF.Cos( angle ) * wobbleAmp;
		var wobbleY = -MathF.Sin( angle ) * wobbleAmp * 0.55f;

		var centerIndex = (letterCount - 1) * 0.5f;
		var fromCenter = letterIndex - centerIndex;
		var pullX = -fromCenter * Dist( tier, 10f, 14f, 18f ) * pull;
		var pullY = Dist( tier, 8f, 12f, 16f ) * pull;

		var mx = pullX + wobbleX;
		var my = pullY + wobbleY;
		var shrink = 1f - pull * 0.65f;
		return new LetterExitFrame( mx, my, shrink, 1f - EaseIn( exitT ) );
	}

	static LetterExitFrame TypingErase( TackleComicTextHud.ComicFontTier tier, ComicLetterStyle letter, int letterCount, float exitT )
	{
		var slot = letterCount <= 1 ? 0f : letter.ExitOrderIndex / (float)(letterCount - 1);
		var window = Dist( tier, 0.24f, 0.2f, 0.16f );
		var localT = MathX.Clamp( (exitT - slot * (1f - window)) / window, 0f, 1f );
		var shrink = 1f - EaseIn( localT ) * 0.9f;
		var opacity = localT <= 0.001f ? 1f : 1f - EaseIn( localT );
		return new LetterExitFrame( 0f, localT * 4f, shrink, opacity );
	}

	static float Dist( TackleComicTextHud.ComicFontTier tier, float sage, float sans, float chaos )
	{
		return tier switch
		{
			TackleComicTextHud.ComicFontTier.Chaos => chaos,
			TackleComicTextHud.ComicFontTier.Sans => sans,
			_ => sage
		};
	}

	static float EaseIn( float t ) => t * t;
	static float EaseOut( float t ) => 1f - (1f - t) * (1f - t );
}
