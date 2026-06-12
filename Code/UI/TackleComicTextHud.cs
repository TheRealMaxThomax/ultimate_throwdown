using System;
using System.Collections.Generic;
using Sandbox;
using Sandbox.Rendering;

/// <summary>
/// World-anchored comic words (POW!, BAM!, …) drawn on the main camera HUD for every client.
/// Host broadcasts on tackle / knockdown connect; uses Les Flos tier fonts when configured.
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

	private struct ActiveBurst
	{
		public Vector3 WorldPosition;
		public string Text;
		public ComicFontTier Tier;
		public Color FillColor;
		public float StartTime;
		public float Duration;
		public float ShakeSeed;
	}

	private readonly List<ActiveBurst> activeBursts = new();

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

	/// <summary>Flat letters — light hits / traffic knockdowns.</summary>
	[Property] public string FontSage { get; set; } = "Les Flos Sage";
	/// <summary>Letters tilted left/right — medium hits.</summary>
	[Property] public string FontSans { get; set; } = "Les Flos Sans";
	/// <summary>Stronger tilt than Sans — heavy / juggernaut hits.</summary>
	[Property] public string FontChaos { get; set; } = "Les Flos Chaos";

	[Property] public int BaseFontSize { get; set; } = 72;
	[Property] public int ChaosFontSizeBonus { get; set; } = 18;
	[Property] public float WorldHeightOffset { get; set; } = 52f;
	[Property] public float FloatUpSpeed { get; set; } = 42f;
	[Property] public float LifetimeSeconds { get; set; } = 0.95f;
	[Property] public float PopInSeconds { get; set; } = 0.12f;
	[Property] public float ShakeDurationSeconds { get; set; } = 0.14f;
	[Property] public float ShakeAmplitudePixels { get; set; } = 10f;
	[Property] public Vector2 ShadowOffsetPixels { get; set; } = new( 5f, 6f );
	[Property] public Color FillColor { get; set; } = new( 1f, 0.92f, 0.18f );
	[Property] public Color ShadowColor { get; set; } = new( 0.02f, 0.02f, 0.02f );
	/// <summary>Tackle power at or above this uses <see cref="FontSans"/> (tilted). Below uses flat <see cref="FontSage"/>.</summary>
	[Property] public float SansImpactThreshold { get; set; } = 1.12f;
	/// <summary>Tackle power at or above this uses <see cref="FontChaos"/> (max tilt).</summary>
	[Property] public float ChaosImpactThreshold { get; set; } = 1.45f;

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

		hud.BroadcastSpawnRpc( worldPosition, (int)tier, text );
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
	private void BroadcastSpawnRpc( Vector3 worldPosition, int tier, string text )
	{
		if ( !EnableComicText || string.IsNullOrWhiteSpace( text ) )
			return;

		SpawnLocal( worldPosition, (ComicFontTier)tier, text.Trim() );
	}

	void SpawnLocal( Vector3 worldPosition, ComicFontTier tier, string text )
	{
		activeBursts.Add( new ActiveBurst
		{
			WorldPosition = worldPosition + Vector3.Up * WorldHeightOffset,
			Text = text,
			Tier = tier,
			FillColor = FillColor,
			StartTime = Time.Now,
			Duration = LifetimeSeconds,
			ShakeSeed = Game.Random.Float( 0f, 1000f )
		} );
	}

	string ResolveFontFamily( ComicFontTier tier )
	{
		return tier switch
		{
			ComicFontTier.Chaos => FontChaos,
			ComicFontTier.Sage => FontSage,
			_ => FontSans
		};
	}

	protected override void OnUpdate()
	{
		if ( Scene.Camera is null || activeBursts.Count == 0 )
			return;

		var hud = Scene.Camera.Hud;
		var camera = Scene.Camera;
		var now = Time.Now;

		for ( var i = activeBursts.Count - 1; i >= 0; i-- )
		{
			var burst = activeBursts[i];
			var elapsed = now - burst.StartTime;
			if ( elapsed >= burst.Duration )
			{
				activeBursts.RemoveAt( i );
				continue;
			}

			DrawBurst( hud, camera, burst, elapsed );
		}
	}

	void DrawBurst( HudPainter hud, CameraComponent camera, ActiveBurst burst, float elapsed )
	{
		var worldPos = burst.WorldPosition + Vector3.Up * (FloatUpSpeed * elapsed );
		var toPoint = worldPos - camera.WorldPosition;
		if ( Vector3.Dot( camera.WorldRotation.Forward, toPoint ) <= 0f )
			return;

		var screenPos = camera.PointToScreenPixels( worldPos );
		var alpha = ComputeAlpha( elapsed, burst.Duration );
		if ( alpha <= 0.001f )
			return;

		var scale = ComputePopScale( elapsed );
		var shake = ComputeShakeOffset( elapsed, burst.ShakeSeed );
		screenPos += shake;

		var fontSize = (int)MathF.Round( BaseFontSize * scale );
		if ( burst.Tier == ComicFontTier.Chaos )
			fontSize += ChaosFontSizeBonus;

		var fontFamily = ResolveFontFamily( burst.Tier );
		var fillColor = burst.FillColor.WithAlpha( alpha );
		var shadowColor = ShadowColor.WithAlpha( alpha );
		var shadowOffset = ShadowOffsetPixels * scale;

		DrawCenteredComicText( hud, burst.Text, fontFamily, fontSize, screenPos + shadowOffset, shadowColor );
		DrawCenteredComicText( hud, burst.Text, fontFamily, fontSize, screenPos, fillColor );
	}

	static float ComputeAlpha( float elapsed, float duration )
	{
		if ( duration <= 0.0001f )
			return 0f;

		var t = elapsed / duration;
		if ( t < 0.12f )
			return 1f;

		if ( t < 0.55f )
			return 1f;

		var fadeT = MathX.Clamp( (t - 0.55f) / 0.45f, 0f, 1f );
		return 1f - fadeT;
	}

	float ComputePopScale( float elapsed )
	{
		if ( PopInSeconds <= 0.0001f )
			return 1f;

		var t = MathX.Clamp( elapsed / PopInSeconds, 0f, 1f );
		if ( t < 0.55f )
		{
			var popT = t / 0.55f;
			return MathX.Lerp( 0.35f, 1.18f, popT );
		}

		var settleT = (t - 0.55f) / 0.45f;
		return MathX.Lerp( 1.18f, 1f, settleT );
	}

	Vector2 ComputeShakeOffset( float elapsed, float shakeSeed )
	{
		if ( ShakeDurationSeconds <= 0.0001f || elapsed > ShakeDurationSeconds )
			return Vector2.Zero;

		var t = 1f - MathX.Clamp( elapsed / ShakeDurationSeconds, 0f, 1f );
		var amp = ShakeAmplitudePixels * t;
		var wobble = elapsed * 48f + shakeSeed;
		return new Vector2(
			MathF.Sin( wobble ) * amp,
			MathF.Cos( wobble * 1.17f ) * amp * 0.75f );
	}

	static void DrawCenteredComicText( HudPainter hud, string text, string fontFamily, int fontSize, Vector2 screenPos, Color color )
	{
		var scope = string.IsNullOrWhiteSpace( fontFamily )
			? new TextRendering.Scope( text, color, fontSize )
			: new TextRendering.Scope( text, color, fontSize, fontFamily, 400 );

		var measured = scope.Measure();
		var rect = new Rect(
			screenPos.x - measured.x * 0.5f,
			screenPos.y - measured.y * 0.5f,
			measured.x,
			measured.y );

		hud.DrawText( scope, rect, TextFlag.Left );
	}
}
