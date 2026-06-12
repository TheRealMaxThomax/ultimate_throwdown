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
		hud.BroadcastSpawnRpc( worldPosition, (int)tier, text, shadowDir, wordTiltDegrees );
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

		return valid[Game.Random.Int( 0, valid.Count - 1 )];
	}

	[Rpc.Broadcast]
	private void BroadcastSpawnRpc( Vector3 worldPosition, int tier, string text, int shadowDirection, float wordTiltDegrees )
	{
		if ( !EnableComicText || string.IsNullOrWhiteSpace( text ) )
			return;

		var dir = (ComicShadowDirection)MathX.Clamp( shadowDirection, 0, 3 );
		SpawnBurst( worldPosition, (ComicFontTier)tier, text.Trim(), dir, wordTiltDegrees );
	}

	void SpawnBurst( Vector3 worldPosition, ComicFontTier tier, string text, ComicShadowDirection shadowDirection, float wordTiltDegrees )
	{
		var burstGo = new GameObject( true, "TackleComicBurst" );
		burstGo.WorldPosition = worldPosition + Vector3.Up * WorldHeightOffset;

		var worldPanel = burstGo.Components.Create<Sandbox.WorldPanel>();
		worldPanel.PanelSize = ResolveBurstPanelSize( text, tier, wordTiltDegrees );
		worldPanel.LookAtCamera = true;

		var burst = burstGo.Components.Create<TackleComicBurst>();
		burst.Configure( this, tier, text, shadowDirection, wordTiltDegrees );
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

	Vector2 ResolveBurstPanelSize( string text, ComicFontTier tier, float wordTiltDegrees )
	{
		const float baseFontSize = 112f;
		const float chaosFontSize = 128f;
		const float popPeakScale = 1.16f;

		var charCount = MathF.Max( 1, text?.Length ?? 1 );
		var fontScale = tier == ComicFontTier.Chaos ? chaosFontSize / baseFontSize : 1f;
		var tierRenderScale = tier == ComicFontTier.Chaos ? ChaosRenderScaleMultiplier : 1f;
		var layoutScale = fontScale * tierRenderScale * popPeakScale;

		var width = MathF.Max( BurstPanelMinWidth, charCount * BurstPanelWidthPerCharacter ) * layoutScale;

		// Rotated glyphs + pop overshoot need extra room than flat width estimate.
		var textHeight = baseFontSize * layoutScale;
		var tiltRadians = MathF.Abs( wordTiltDegrees ) * MathF.PI / 180f;
		width += textHeight * MathF.Sin( tiltRadians ) * 2f;

		var shadowPad = MathF.Max( MathF.Abs( ShadowOffsetPixels.x ), MathF.Abs( ShadowOffsetPixels.y ) ) * 2f;
		width += shadowPad;

		var height = BurstPanelSize.y * layoutScale + shadowPad;

		return new Vector2( width, height );
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
