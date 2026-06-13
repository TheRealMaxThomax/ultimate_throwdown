using System;
using System.Collections.Generic;

/// <summary>Per-letter C# exits during fade window.</summary>
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
			or TackleComicTextHud.ComicExitStyle.LetterPopOffScatter
			or TackleComicTextHud.ComicExitStyle.LetterGlitchMelt
			or TackleComicTextHud.ComicExitStyle.LetterComicStrikeThrough
			or TackleComicTextHud.ComicExitStyle.LetterUnspellDrift;
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
			TackleComicTextHud.ComicExitStyle.LetterGlitchMelt => GlitchMelt( tier, letterIndex, letter, letterCount, exitT, letterJitterSeed ),
			TackleComicTextHud.ComicExitStyle.LetterComicStrikeThrough => ComicStrikeThrough( tier, letterIndex, letterCount, exitT, letterJitterSeed ),
			TackleComicTextHud.ComicExitStyle.LetterUnspellDrift => UnspellDrift( tier, letter, letterCount, exitT, tackleOctant ),
			_ => LetterExitFrame.None
		};
	}

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

	/// <summary>Shuffled melt — horizontal jitter + opacity flicker + sag (chaos heaviest).</summary>
	static LetterExitFrame GlitchMelt( TackleComicTextHud.ComicFontTier tier, int letterIndex, ComicLetterStyle letter, int letterCount, float exitT, int letterJitterSeed )
	{
		var slot = letterCount <= 1 ? 0f : letter.ExitOrderIndex / (float)(letterCount - 1);
		var localT = StaggerLocalTFromSlot( slot, letterCount, exitT, tier, 0.26f, 0.22f, 0.18f );
		if ( localT <= 0.001f )
			return LetterExitFrame.None;

		var melt = EaseIn( localT );
		var glitchPhase = localT * Dist( tier, 24f, 32f, 44f ) + letterIndex * 1.65f + SampleUnit( letterJitterSeed, letterIndex, 4 ) * 2f;
		var mx = MathF.Sin( glitchPhase * 6.2f ) * Dist( tier, 10f, 16f, 26f ) * (1f - melt * 0.35f )
			+ MathF.Cos( glitchPhase * 11.7f ) * Dist( tier, 5f, 8f, 14f );
		var my = Dist( tier, 22f, 30f, 40f ) * melt + MathF.Sin( glitchPhase * 4.4f ) * Dist( tier, 3f, 5f, 9f );

		var shrink = 1f - melt * Dist( tier, 0.78f, 0.86f, 0.94f );
		var flicker = (MathF.Sin( glitchPhase * 13.5f ) + 1f) * 0.5f;
		var opacity = MathX.Lerp( 0.25f, 1f, flicker ) * (1f - EaseIn( MathX.Clamp( (localT - 0.4f) / 0.6f, 0f, 1f ) ));
		return new LetterExitFrame( mx, my, shrink, opacity );
	}

	/// <summary>Diagonal slash sweeps the word — host-synced strike angle via <paramref name="letterJitterSeed"/>.</summary>
	static LetterExitFrame ComicStrikeThrough( TackleComicTextHud.ComicFontTier tier, int letterIndex, int letterCount, float exitT, int letterJitterSeed )
	{
		var slashAngle = (SampleUnit( letterJitterSeed, 0, 23 ) * 0.5f + 0.18f) * MathF.PI;
		var slashX = MathF.Cos( slashAngle );
		var slashY = MathF.Sin( slashAngle );

		var maxProj = 0f;
		for ( var i = 0; i < letterCount; i++ )
		{
			var proj = i * slashX;
			maxProj = MathF.Max( maxProj, proj );
			maxProj = MathF.Max( maxProj, -proj );
		}

		var letterProj = letterIndex * slashX;
		var slot = letterCount <= 1 || maxProj <= 0.001f
			? 0f
			: (letterProj / maxProj + 1f) * 0.5f;

		var localT = StaggerLocalTFromSlot( slot, letterCount, exitT, tier, 0.24f, 0.2f, 0.16f );
		if ( localT <= 0.001f )
			return LetterExitFrame.None;

		var slash = EaseOut( localT );
		var mx = slashX * Dist( tier, 32f, 42f, 52f ) * slash;
		var my = slashY * Dist( tier, 26f, 34f, 42f ) * slash;
		var shrink = 1f - EaseIn( MathX.Clamp( (localT - 0.25f) / 0.75f, 0f, 1f ) ) * 0.92f;
		var opacity = 1f - EaseIn( MathX.Clamp( (localT - 0.35f) / 0.65f, 0f, 1f ) );
		return new LetterExitFrame( mx, my, shrink, opacity );
	}

	/// <summary>Typing-erase order + small nudge along tackle <paramref name="tackleOctant"/>.</summary>
	static LetterExitFrame UnspellDrift( TackleComicTextHud.ComicFontTier tier, ComicLetterStyle letter, int letterCount, float exitT, int tackleOctant )
	{
		var slot = letterCount <= 1 ? 0f : letter.ExitOrderIndex / (float)(letterCount - 1);
		var localT = StaggerLocalTFromSlot( slot, letterCount, exitT, tier, 0.28f, 0.24f, 0.2f );

		var drift = EaseIn( localT );
		var drop = Dist( tier, 40f, 50f, 58f ) * drift;
		OctantToOffset( tackleOctant, Dist( tier, 22f, 32f, 42f ) * drift, out var driftX, out var driftY );

		var shrink = 1f - EaseIn( MathX.Clamp( (localT - 0.55f) / 0.45f, 0f, 1f ) ) * 0.92f;
		var opacity = localT <= 0.001f ? 1f : 1f - EaseIn( MathX.Clamp( (localT - 0.5f) / 0.5f, 0f, 1f ) );
		return new LetterExitFrame( driftX, drop + driftY, shrink, opacity );
	}

	static void OctantToOffset( int octant, float distance, out float marginLeft, out float marginTop )
	{
		octant = ((octant % 8) + 8) % 8;
		var radians = octant * (MathF.PI / 4f);
		marginLeft = MathF.Cos( radians ) * distance;
		marginTop = -MathF.Sin( radians ) * distance;
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
