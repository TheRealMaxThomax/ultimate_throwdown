using System;
using System.Collections.Generic;

/// <summary>Per-letter C# exits during fade window — vortex, typing-erase, domino, pop-scatter.</summary>
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
			or TackleComicTextHud.ComicExitStyle.LetterTypingErase
			or TackleComicTextHud.ComicExitStyle.LetterDominoTip
			or TackleComicTextHud.ComicExitStyle.LetterPopOffScatter;
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
			TackleComicTextHud.ComicExitStyle.LetterDominoTip => DominoTip( tier, letterIndex, letterCount, exitT, letterJitterSeed ),
			TackleComicTextHud.ComicExitStyle.LetterPopOffScatter => PopOffScatter( tier, letterIndex, letterCount, letter, exitT ),
			_ => LetterExitFrame.None
		};
	}

	/// <summary>Outer letters spiral in first; strong inward curl toward word center.</summary>
	static LetterExitFrame SuckInVortex( TackleComicTextHud.ComicFontTier tier, int letterIndex, int letterCount, ComicLetterStyle letter, float exitT )
	{
		var centerIndex = (letterCount - 1) * 0.5f;
		var fromCenter = letterIndex - centerIndex;
		var maxFromCenter = MathF.Max( (letterCount - 1) * 0.5f, 0.5f );
		var outerFirstSlot = letterCount <= 1 ? 0f : 1f - MathF.Abs( fromCenter ) / maxFromCenter;

		var localT = StaggerLocalTFromSlot( outerFirstSlot, letterCount, exitT, tier, 0.26f, 0.22f, 0.18f );
		if ( localT <= 0.001f )
			return LetterExitFrame.None;

		var pull = EaseIn( localT );
		var spin = letter.OrbitStartRadians + localT * MathF.PI * 3f;
		var spiralRadius = Dist( tier, 42f, 52f, 62f ) * (1f - pull );
		var spiralX = MathF.Cos( spin ) * spiralRadius;
		var spiralY = MathF.Sin( spin ) * spiralRadius * 0.65f;

		var inwardX = -fromCenter * Dist( tier, 32f, 40f, 48f ) * pull;
		var inwardY = Dist( tier, 14f, 18f, 22f ) * pull;

		var mx = inwardX + spiralX;
		var my = inwardY + spiralY;
		var shrink = 1f - EaseIn( MathX.Clamp( (localT - 0.35f) / 0.65f, 0f, 1f ) ) * 0.75f;
		var opacity = 1f - EaseIn( MathX.Clamp( (localT - 0.45f) / 0.55f, 0f, 1f ) );
		return new LetterExitFrame( mx, my, shrink, opacity );
	}

	/// <summary>Shuffled vanish order; straight drop — no horizontal motion.</summary>
	static LetterExitFrame TypingErase( TackleComicTextHud.ComicFontTier tier, ComicLetterStyle letter, int letterCount, float exitT )
	{
		var slot = letterCount <= 1 ? 0f : letter.ExitOrderIndex / (float)(letterCount - 1);
		var localT = StaggerLocalTFromSlot( slot, letterCount, exitT, tier, 0.28f, 0.24f, 0.2f );

		var drop = Dist( tier, 48f, 58f, 68f ) * EaseIn( localT );
		var shrink = 1f - EaseIn( MathX.Clamp( (localT - 0.55f) / 0.45f, 0f, 1f ) ) * 0.95f;
		var opacity = localT <= 0.001f ? 1f : 1f - EaseIn( MathX.Clamp( (localT - 0.5f) / 0.5f, 0f, 1f ) );
		return new LetterExitFrame( 0f, drop, shrink, opacity );
	}

	static LetterExitFrame DominoTip( TackleComicTextHud.ComicFontTier tier, int letterIndex, int letterCount, float exitT, int letterJitterSeed )
	{
		var leftToRight = SampleUnit( letterJitterSeed, 0, 22 ) >= 0.5f;
		var spatialRank = leftToRight ? letterIndex : letterCount - 1 - letterIndex;
		var slot = letterCount <= 1 ? 0f : spatialRank / (float)(letterCount - 1);
		var localT = StaggerLocalTFromSlot( slot, letterCount, exitT, tier, 0.22f, 0.19f, 0.16f );
		if ( localT <= 0.001f )
			return LetterExitFrame.None;

		var tipDir = leftToRight ? 1f : -1f;
		var mx = tipDir * Dist( tier, 28f, 36f, 44f ) * EaseOut( localT );
		var my = Dist( tier, 22f, 28f, 34f ) * EaseIn( localT );
		var shrink = 1f - EaseIn( localT ) * 0.85f;
		return new LetterExitFrame( mx, my, shrink, 1f - EaseIn( localT ) );
	}

	/// <summary>Left-to-right wave; each letter pops outward along its seeded angle.</summary>
	static LetterExitFrame PopOffScatter( TackleComicTextHud.ComicFontTier tier, int letterIndex, int letterCount, ComicLetterStyle letter, float exitT )
	{
		var slot = letterCount <= 1 ? 0f : letterIndex / (float)(letterCount - 1);
		var localT = StaggerLocalTFromSlot( slot, letterCount, exitT, tier, 0.24f, 0.2f, 0.17f );
		if ( localT <= 0.001f )
			return LetterExitFrame.None;

		var blast = EaseOut( localT );
		blast *= blast;
		var dist = Dist( tier, 95f, 125f, 155f ) * blast;
		var mx = MathF.Cos( letter.OrbitStartRadians ) * dist;
		var my = -MathF.Sin( letter.OrbitStartRadians ) * dist;

		var popPhase = MathX.Clamp( localT / 0.28f, 0f, 1f );
		var shrink = localT < 0.35f
			? 1f + 0.22f * EaseOut( popPhase )
			: 1f - EaseIn( MathX.Clamp( (localT - 0.35f) / 0.65f, 0f, 1f ) ) * 0.85f;
		var opacity = 1f - EaseIn( MathX.Clamp( (localT - 0.55f) / 0.45f, 0f, 1f ) );
		return new LetterExitFrame( mx, my, shrink, opacity );
	}

	static float StaggerLocalTFromSlot( float slot, int letterCount, float exitT, TackleComicTextHud.ComicFontTier tier, float windowSage, float windowSans, float windowChaos )
	{
		var window = Dist( tier, windowSage, windowSans, windowChaos );
		return MathX.Clamp( (exitT - slot * (1f - window)) / window, 0f, 1f );
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

	static float SampleUnit( int seed, int index, int channel )
	{
		unchecked
		{
			var hash = (uint)seed;
			hash ^= (uint)index * 0x9E3779B9u;
			hash ^= (uint)channel * 0x85EBCA6Bu;
			hash ^= hash >> 16;
			hash *= 0x7FEB352Du;
			hash ^= hash >> 15;
			hash *= 0x846CA68Bu;
			hash ^= hash >> 16;
			return (hash & 0xFFFFFF) / (float)0xFFFFFF;
		}
	}

	static float EaseIn( float t ) => t * t;
	static float EaseOut( float t ) => 1f - (1f - t) * (1f - t );
}
