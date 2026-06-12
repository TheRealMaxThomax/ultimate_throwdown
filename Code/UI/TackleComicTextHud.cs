using System;
using System.Collections.Generic;
using Sandbox;
using Sandbox.UI;

/// <summary>
/// Spawns short-lived <see cref="TackleComicBurst"/> world panels on knockdown (all clients via broadcast).
/// Settings live here; bursts are created at runtime — no scene wiring required.
/// </summary>
public sealed class TackleComicTextHud : Component
{
	/// <summary>Les Flos visual intensity: Sage = flat glyphs, Sans = tilted, Chaos = max tilt.</summary>
	public enum ComicFontTier
	{
		Sage = 0,
		Sans = 1,
		Chaos = 2
	}

	/// <summary>Diagonal offset for the black duplicate layer (3D comic depth, not a uniform outline).</summary>
	public enum ComicShadowDirection
	{
		BottomRight = 0,
		BottomLeft = 1,
		TopRight = 2,
		TopLeft = 3
	}

	[Property] public bool EnableComicText { get; set; } = true;

	/// <summary>Random pool — host picks one entry per knockdown and broadcasts the text.</summary>
	[Property] public List<string> ComicWords { get; set; } = new()
	{
		"POW!",
		"BAM!",
		"WHACK!",
		"WHOP!",
		"KAPOW!",
		"BOOM!",
		"SMACK!",
		"ZAP!"
	};

	/// <summary>Flat letters — light player tackles (yellow).</summary>
	[Property] public string FontSage { get; set; } = "Les Flos Sage";
	/// <summary>Letters tilted left/right — medium hits (orange).</summary>
	[Property] public string FontSans { get; set; } = "Les Flos Sans";
	/// <summary>Stronger tilt than Sans — heavy / juggernaut hits (red).</summary>
	[Property] public string FontChaos { get; set; } = "Les Flos Chaos";

	[Property] public Vector2 BurstPanelSize { get; set; } = new( 1280f, 420f );
	[Property] public float BurstPanelWidthPerCharacter { get; set; } = 120f;
	[Property] public float BurstPanelMinWidth { get; set; } = 960f;
	[Property] public float WorldHeightOffset { get; set; } = 52f;
	[Property] public float FloatUpSpeed { get; set; } = 42f;
	[Property] public float LifetimeSeconds { get; set; } = 0.95f;

	/// <summary>Fixed world size — perspective already shrinks with distance. Tune if words feel too big/small up close.</summary>
	[Property] public float RenderScale { get; set; } = 1f;
	[Property] public float ChaosRenderScaleMultiplier { get; set; } = 1.12f;
	[Property] public Vector2 ShadowOffsetPixels { get; set; } = new( 10f, 12f );

	/// <summary>Tackle power at or above this uses <see cref="FontSans"/> (tilted, orange). Below uses flat <see cref="FontSage"/> (yellow).</summary>
	[Property] public float SansImpactThreshold { get; set; } = 1.12f;
	/// <summary>Tackle power at or above this uses <see cref="FontChaos"/> (max tilt, red).</summary>
	[Property] public float ChaosImpactThreshold { get; set; } = 1.45f;

	/// <summary>Max random whole-word rotation (± degrees) — host picks once per burst; Sage stays subtle.</summary>
	[Property] public float WordTiltMaxDegreesSage { get; set; } = 4f;
	[Property] public float WordTiltMaxDegreesSans { get; set; } = 7f;
	[Property] public float WordTiltMaxDegreesChaos { get; set; } = 12f;

	/// <summary>Per-letter font-size multiplier jitter (± fraction around 1.0) — Sage capped for readability.</summary>
	[Property] public float LetterSizeJitterSage { get; set; } = 0.04f;
	[Property] public float LetterSizeJitterSans { get; set; } = 0.10f;
	[Property] public float LetterSizeJitterChaos { get; set; } = 0.18f;

	/// <summary>Per-letter baseline offset (± pixels, margin-top on each label).</summary>
	[Property] public float LetterBaselineJitterSage { get; set; } = 3f;
	[Property] public float LetterBaselineJitterSans { get; set; } = 8f;
	[Property] public float LetterBaselineJitterChaos { get; set; } = 14f;

	/// <summary>Extra horizontal gap after each glyph (± pixels on top of <see cref="LetterSpacingBasePixels"/>).</summary>
	[Property] public float LetterSpacingJitterSage { get; set; } = 2f;
	[Property] public float LetterSpacingJitterSans { get; set; } = 6f;
	[Property] public float LetterSpacingJitterChaos { get; set; } = 12f;

	[Property] public float LetterSpacingBasePixels { get; set; } = 4f;

	[Property] public bool EnableComicDebugLogs { get; set; }

	public static TackleComicTextHud FindInScene( Scene scene )
	{
		if ( scene is null )
			return null;

		foreach ( var hud in scene.GetAllComponents<TackleComicTextHud>() )
		{
			if ( hud.IsValid() )
				return hud;
		}

		return null;
	}

	public static void EnsureOnMainCamera( Scene scene )
	{
		if ( scene is null )
			return;

		foreach ( var camera in scene.GetAllComponents<CameraComponent>() )
		{
			if ( !camera.IsMainCamera )
				continue;

			camera.GameObject.Components.GetOrCreate<TackleComicTextHud>();
			return;
		}
	}

	/// <summary>Host: pick word + font tier from tackle power and broadcast to all clients.</summary>
	public static void NotifyHostKnockdown( Scene scene, Vector3 worldPosition, float tacklePower )
	{
		if ( !Networking.IsHost )
			return;

		var hud = FindInScene( scene );
		if ( !hud.IsValid() || !hud.EnableComicText )
			return;

		var tier = ResolveTier( tacklePower, hud.SansImpactThreshold, hud.ChaosImpactThreshold );
		var text = hud.PickRandomWord();
		if ( string.IsNullOrWhiteSpace( text ) )
			return;

		var shadowDir = Game.Random.Int( 0, 3 );
		var wordTiltDegrees = hud.PickRandomWordTiltDegrees( tier );
		// Stay below int.MaxValue — inclusive Random.Int(max+1) overflows and throws.
		var letterJitterSeed = Game.Random.Int( 1, 2_000_000_000 );
		hud.BroadcastSpawnRpc( worldPosition, (int)tier, text, shadowDir, wordTiltDegrees, letterJitterSeed );
	}

	public static void ResolveShadowOffset( ComicShadowDirection direction, Vector2 magnitude, out float offsetX, out float offsetY )
	{
		var magX = MathF.Abs( magnitude.x );
		var magY = MathF.Abs( magnitude.y );

		switch ( direction )
		{
			case ComicShadowDirection.BottomLeft:
				offsetX = -magX;
				offsetY = magY;
				return;
			case ComicShadowDirection.TopRight:
				offsetX = magX;
				offsetY = -magY;
				return;
			case ComicShadowDirection.TopLeft:
				offsetX = -magX;
				offsetY = -magY;
				return;
			default:
				offsetX = magX;
				offsetY = magY;
				return;
		}
	}

	static ComicFontTier ResolveTier( float tacklePower, float sansThreshold, float chaosThreshold )
	{
		if ( tacklePower >= chaosThreshold )
			return ComicFontTier.Chaos;

		if ( tacklePower >= sansThreshold )
			return ComicFontTier.Sans;

		return ComicFontTier.Sage;
	}

	string PickRandomWord()
	{
		if ( ComicWords is null || ComicWords.Count == 0 )
			return null;

		var valid = new List<string>();
		foreach ( var word in ComicWords )
		{
			if ( !string.IsNullOrWhiteSpace( word ) )
				valid.Add( word.Trim() );
		}

		if ( valid.Count == 0 )
			return null;

		if ( valid.Count == 1 )
			return valid[0];

		return valid[Game.Random.Int( 0, valid.Count - 1 )];
	}

	[Rpc.Broadcast]
	private void BroadcastSpawnRpc( Vector3 worldPosition, int tier, string text, int shadowDirection, float wordTiltDegrees, int letterJitterSeed )
	{
		if ( !EnableComicText || string.IsNullOrWhiteSpace( text ) )
			return;

		var dir = (ComicShadowDirection)MathX.Clamp( shadowDirection, 0, 3 );
		SpawnBurst( worldPosition, (ComicFontTier)tier, text.Trim(), dir, wordTiltDegrees, letterJitterSeed );
	}

	void SpawnBurst( Vector3 worldPosition, ComicFontTier tier, string text, ComicShadowDirection shadowDirection, float wordTiltDegrees, int letterJitterSeed )
	{
		var letterStyles = BuildLetterStyles( text, tier, letterJitterSeed );

		var burstGo = new GameObject( true, "TackleComicBurst" );
		burstGo.WorldPosition = worldPosition + Vector3.Up * WorldHeightOffset;

		var worldPanel = burstGo.Components.Create<Sandbox.WorldPanel>();
		worldPanel.PanelSize = ResolveBurstPanelSize( text, tier, wordTiltDegrees, letterStyles );
		worldPanel.LookAtCamera = true;

		var spawnData = new ComicBurstSpawnData
		{
			Hud = this,
			Tier = tier,
			Text = text,
			ShadowDirection = shadowDirection,
			WordTiltDegrees = wordTiltDegrees,
			LetterJitterSeed = letterJitterSeed,
			LetterStyles = letterStyles
		};

		var burst = burstGo.Components.Create<TackleComicBurst>();
		burst.ApplySpawnData( spawnData );

		if ( EnableComicDebugLogs )
			Log.Info( $"[Comic] burst \"{text}\" tier={tier} letters={letterStyles.Count} pos={worldPosition}" );
	}

	float PickRandomWordTiltDegrees( ComicFontTier tier )
	{
		var max = tier switch
		{
			ComicFontTier.Chaos => WordTiltMaxDegreesChaos,
			ComicFontTier.Sans => WordTiltMaxDegreesSans,
			_ => WordTiltMaxDegreesSage
		};

		if ( max <= 0f )
			return 0f;

		return Game.Random.Float( -max, max );
	}

	Vector2 ResolveBurstPanelSize( string text, ComicFontTier tier, float wordTiltDegrees, IReadOnlyList<ComicLetterStyle> letterStyles )
	{
		const float baseFontSize = 112f;
		const float popPeakScale = 1.16f;

		var charCount = MathF.Max( 1, text?.Length ?? 1 );
		var tierRenderScale = tier == ComicFontTier.Chaos ? ChaosRenderScaleMultiplier : 1f;
		var layoutScale = tierRenderScale * popPeakScale;

		var width = MathF.Max( BurstPanelMinWidth, charCount * BurstPanelWidthPerCharacter ) * layoutScale;

		if ( letterStyles is not null && letterStyles.Count > 0 )
		{
			var letterWidth = 0f;
			var letterHeight = 0f;

			foreach ( var letter in letterStyles )
			{
				letterWidth += letter.FontSizePx * 0.62f + letter.SpacingAfterPx;
				letterHeight = MathF.Max( letterHeight, letter.FontSizePx + MathF.Abs( letter.BaselineOffsetPx ) );
			}

			width = MathF.Max( width, letterWidth * layoutScale );
			width = MathF.Max( width, BurstPanelMinWidth * layoutScale * 0.5f );
		}

		// Rotated glyphs + pop overshoot need extra room than flat width estimate.
		var textHeight = baseFontSize * layoutScale;
		if ( letterStyles is not null && letterStyles.Count > 0 )
		{
			foreach ( var letter in letterStyles )
				textHeight = MathF.Max( textHeight, letter.FontSizePx * layoutScale );
		}

		var tiltRadians = MathF.Abs( wordTiltDegrees ) * MathF.PI / 180f;
		width += textHeight * MathF.Sin( tiltRadians ) * 2f;

		var shadowPad = MathF.Max( MathF.Abs( ShadowOffsetPixels.x ), MathF.Abs( ShadowOffsetPixels.y ) ) * 2f;
		width += shadowPad;

		var height = MathF.Max( BurstPanelSize.y * layoutScale, textHeight + shadowPad );
		if ( letterStyles is not null && letterStyles.Count > 0 )
		{
			var maxBaseline = 0f;
			foreach ( var letter in letterStyles )
				maxBaseline = MathF.Max( maxBaseline, MathF.Abs( letter.BaselineOffsetPx ) );

			height = MathF.Max( height, (textHeight + maxBaseline * 2f) * layoutScale + shadowPad );
		}

		return new Vector2( width, height );
	}

	/// <summary>Deterministic per-glyph layout from host-synced <paramref name="letterJitterSeed"/>.</summary>
	public List<ComicLetterStyle> BuildLetterStyles( string text, ComicFontTier tier, int letterJitterSeed )
	{
		var letters = new List<ComicLetterStyle>();
		if ( string.IsNullOrEmpty( text ) )
			return letters;

		var baseFontSize = ResolveBaseFontSizePx( tier );
		ResolveLetterJitterCaps( tier, out var sizeJitter, out var baselineJitter, out var spacingJitter );

		for ( var i = 0; i < text.Length; i++ )
		{
			var ch = text[i];
			var isLast = i >= text.Length - 1;
			var sizeMul = 1f + SampleSignedJitter( letterJitterSeed, i, 0, sizeJitter );
			var baseline = SampleSignedJitter( letterJitterSeed, i, 1, baselineJitter );
			var spacingAfter = isLast
				? 0f
				: LetterSpacingBasePixels + SampleSignedJitter( letterJitterSeed, i, 2, spacingJitter );

			letters.Add( new ComicLetterStyle
			{
				Character = ch,
				FontSizePx = baseFontSize * sizeMul,
				BaselineOffsetPx = baseline,
				SpacingAfterPx = spacingAfter
			} );
		}

		return letters;
	}

	static float ResolveBaseFontSizePx( ComicFontTier tier )
		=> tier == ComicFontTier.Chaos ? 128f : 112f;

	void ResolveLetterJitterCaps( ComicFontTier tier, out float sizeJitter, out float baselineJitter, out float spacingJitter )
	{
		switch ( tier )
		{
			case ComicFontTier.Chaos:
				sizeJitter = LetterSizeJitterChaos;
				baselineJitter = LetterBaselineJitterChaos;
				spacingJitter = LetterSpacingJitterChaos;
				return;
			case ComicFontTier.Sans:
				sizeJitter = LetterSizeJitterSans;
				baselineJitter = LetterBaselineJitterSans;
				spacingJitter = LetterSpacingJitterSans;
				return;
			default:
				sizeJitter = LetterSizeJitterSage;
				baselineJitter = LetterBaselineJitterSage;
				spacingJitter = LetterSpacingJitterSage;
				return;
		}
	}

	static float SampleSignedJitter( int seed, int index, int channel, float magnitude )
	{
		if ( magnitude <= 0f )
			return 0f;

		var t = SampleUnit( seed, index, channel );
		return MathX.Lerp( -magnitude, magnitude, t );
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

	public string ResolveFontFamily( ComicFontTier tier )
	{
		return tier switch
		{
			ComicFontTier.Chaos => FontChaos,
			ComicFontTier.Sans => FontSans,
			_ => FontSage
		};
	}
}

/// <summary>Payload for runtime <see cref="TackleComicBurst"/> spawn — passed to <c>ApplySpawnData</c> immediately after create.</summary>
public sealed class ComicBurstSpawnData
{
	public TackleComicTextHud Hud { get; init; }
	public TackleComicTextHud.ComicFontTier Tier { get; init; }
	public string Text { get; init; }
	public TackleComicTextHud.ComicShadowDirection ShadowDirection { get; init; }
	public float WordTiltDegrees { get; init; }
	public int LetterJitterSeed { get; init; }
	public IReadOnlyList<ComicLetterStyle> LetterStyles { get; init; }
}

/// <summary>One glyph in a <see cref="TackleComicBurst"/> — size, baseline, and trailing gap.</summary>
public sealed class ComicLetterStyle
{
	public char Character { get; init; }
	public float FontSizePx { get; init; }
	public float BaselineOffsetPx { get; init; }
	public float SpacingAfterPx { get; init; }

	public string ContainerStyle =>
		$"font-size: {FontSizePx:0.#}px; margin-top: {BaselineOffsetPx:0.#}px; margin-right: {SpacingAfterPx:0.#}px;";
}
