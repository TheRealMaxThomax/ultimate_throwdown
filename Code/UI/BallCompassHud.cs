using System;
using Sandbox;
using Sandbox.Rendering;

/// <summary>
/// Local viewer HUD: bottom-left compass toward the match ball (held or loose).
/// Bearing is player position + <see cref="PlayerController.EyeAngles"/> yaw (not camera).
/// Needle hidden while the local player carries; panel, ring, and label stay visible.
/// </summary>
public sealed class BallCompassHud : Component
{
	enum BallCompassState
	{
		Loose,
		TeammateCarries,
		EnemyCarries,
		LocalCarries
	}

	[Property] public string LabelText { get; set; } = "BALL";
	[Property] public float MarginLeft { get; set; } = 24f;
	[Property] public float MarginBottom { get; set; } = 24f;
	[Property] public float CompassSize { get; set; } = 56f;
	[Property] public int LabelFontSize { get; set; } = 11;
	[Property] public float MarkerOrbitRadiusFraction { get; set; } = 1f;
	[Property] public float MarkerTipLength { get; set; } = 5f;
	[Property] public float MarkerBaseLength { get; set; } = 2.5f;
	[Property] public float MarkerHalfWidth { get; set; } = 3.5f;
	[Property] public int MarkerFillSteps { get; set; } = 8;
	[Property] public float MarkerEdgeWidth { get; set; } = 1.25f;
	[Property] public float RingWidth { get; set; } = 2f;
	[Property] public int RingSegments { get; set; } = 28;
	[Property] public float WorldTargetHeight { get; set; } = 36f;
	[Property] public Color LabelColor { get; set; } = Color.White;
	[Property] public Color LooseColor { get; set; } = new Color( 0.92f, 0.92f, 0.92f, 0.9f );
	[Property] public Color FriendlyColor { get; set; } = new Color( 0.25f, 0.95f, 0.4f, 0.95f );
	[Property] public Color EnemyColor { get; set; } = new Color( 1f, 0.15f, 0.15f, 0.95f );
	[Property] public Color LocalCarryRingColor { get; set; } = new Color( 0.75f, 0.75f, 0.75f, 0.45f );

	private PlayerTeam playerTeam;
	private PlayerController playerController;

	protected override void OnStart()
	{
		playerTeam = Components.Get<PlayerTeam>();
		playerController = Components.Get<PlayerController>();
	}

	protected override void OnUpdate()
	{
		if ( IsProxy || !Network.IsOwner )
			return;

		var camera = Scene.Camera;
		if ( camera is null )
			return;

		playerTeam ??= Components.Get<PlayerTeam>();
		playerController ??= Components.Get<PlayerController>();
		if ( playerTeam is null || !MatchTeamIds.IsValid( playerTeam.TeamId ) )
			return;

		if ( playerTeam.SyncedMatchPhase is not MatchPhase.Playing and not MatchPhase.GoalCelebration )
			return;

		if ( !TryResolveBallCompass( Scene, playerTeam.TeamId, out var targetWorld, out var state ) )
			return;

		targetWorld += Vector3.Up * WorldTargetHeight;

		var showNeedle = state != BallCompassState.LocalCarries;
		Vector2 bearing = default;

		if ( showNeedle && !TryGetPlayerBearing( playerController, targetWorld, out bearing ) )
			return;

		DrawCompass( camera.Hud, bearing, state, showNeedle );
	}

	void DrawCompass( HudPainter hud, Vector2 bearing, BallCompassState state, bool showNeedle )
	{
		var panel = BuildCompassPanelRect();
		var center = new Vector2( panel.Left + panel.Width * 0.5f, panel.Top + panel.Height * 0.5f );
		var radius = panel.Width * 0.5f - RingWidth - 2f;
		var accentColor = GetAccentColor( state );
		var ringColor = state == BallCompassState.LocalCarries
			? LocalCarryRingColor
			: accentColor.WithAlpha( MathF.Max( accentColor.a * 0.55f, 0.45f ) );

		hud.DrawRect( panel, new Color( 0f, 0f, 0f, 0.45f ) );
		DrawCircle( hud, center, radius, RingWidth, ringColor );
		DrawCenterLabel( hud, center, radius );

		if ( !showNeedle )
			return;

		var angle = MathF.Atan2( bearing.x, bearing.y );
		var markerDir = new Vector2( MathF.Sin( angle ), -MathF.Cos( angle ) );
		var orbitRadius = radius * MarkerOrbitRadiusFraction.Clamp( 0.5f, 1.15f );
		DrawRingMarkerTriangle( hud, center, orbitRadius, markerDir, accentColor );
	}

	void DrawCenterLabel( HudPainter hud, Vector2 center, float innerRadius )
	{
		if ( string.IsNullOrWhiteSpace( LabelText ) )
			return;

		var hubSize = innerRadius * 1.35f;
		var labelRect = new Rect( center.x - hubSize * 0.5f, center.y - hubSize * 0.5f, hubSize, hubSize );
		hud.DrawText( new TextRendering.Scope( LabelText.Trim(), LabelColor, LabelFontSize ), labelRect, TextFlag.Center );
	}

	void DrawRingMarkerTriangle( HudPainter hud, Vector2 center, float orbitRadius, Vector2 direction, Color color )
	{
		var anchor = center + (direction * orbitRadius );
		var tip = anchor + (direction * MarkerTipLength );
		var baseCenter = anchor - (direction * MarkerBaseLength );
		var perpendicular = new Vector2( -direction.y, direction.x );
		var left = baseCenter + (perpendicular * MarkerHalfWidth );
		var right = baseCenter - (perpendicular * MarkerHalfWidth );

		var fillSteps = MarkerFillSteps.Clamp( 4, 24 );
		for ( var i = 0; i <= fillSteps; i++ )
		{
			var t = i / (float)fillSteps;
			var leftPoint = Vector2.Lerp( tip, left, t, true );
			var rightPoint = Vector2.Lerp( tip, right, t, true );
			hud.DrawLine( leftPoint, rightPoint, 1.25f, color );
		}

		var edgeWidth = MarkerEdgeWidth;
		hud.DrawLine( left, tip, edgeWidth, color );
		hud.DrawLine( right, tip, edgeWidth, color );
		hud.DrawLine( left, right, edgeWidth, color );
	}

	Color GetAccentColor( BallCompassState state )
	{
		return state switch
		{
			BallCompassState.TeammateCarries => FriendlyColor,
			BallCompassState.EnemyCarries => EnemyColor,
			BallCompassState.LocalCarries => LocalCarryRingColor,
			_ => LooseColor
		};
	}

	Rect BuildCompassPanelRect()
	{
		var size = CompassSize;
		var x = MarginLeft;
		var y = Screen.Height - size - MarginBottom;
		return new Rect( x, y, size, size );
	}

	void DrawCircle( HudPainter hud, Vector2 center, float radius, float width, Color color )
	{
		var segments = RingSegments.Clamp( 12, 64 );
		var step = MathF.PI * 2f / segments;
		var prev = center + new Vector2( 0f, -radius );

		for ( var i = 1; i <= segments; i++ )
		{
			var a = step * i;
			var next = center + new Vector2( MathF.Sin( a ) * radius, -MathF.Cos( a ) * radius );
			hud.DrawLine( prev, next, width, color );
			prev = next;
		}
	}

	static bool TryResolveBallCompass( Scene scene, int localTeamId, out Vector3 targetWorld, out BallCompassState state )
	{
		targetWorld = default;
		state = BallCompassState.Loose;

		// OOB countdown — ball is hidden at the foul spot; aim at the synced drop anchor instead.
		if ( MatchHudDraw.TryGetHudState( scene, out var hudTeam, out _ )
			&& hudTeam.NetBallOobActive )
		{
			targetWorld = hudTeam.NetBallOobDropAnchor;
			return true;
		}

		var ball = FindMainBall( scene );
		if ( !ball.IsValid() )
			return false;

		targetWorld = ball.WorldPosition;

		var carrier = BallCarrierOutline.FindCarrierGrab( scene, ball );
		if ( !carrier.IsValid() || !carrier.IsHolding )
		{
			state = BallCompassState.Loose;
			return true;
		}

		var local = Connection.Local;
		if ( local is not null )
		{
			var owner = carrier.Network.Owner;
			if ( owner is not null && owner.SteamId == local.SteamId )
			{
				state = BallCompassState.LocalCarries;
				return true;
			}
		}

		var holderTeam = carrier.Components.Get<PlayerTeam>();
		if ( holderTeam.IsValid()
			&& MatchTeamIds.IsValid( holderTeam.TeamId )
			&& MatchTeamIds.IsValid( localTeamId ) )
		{
			state = holderTeam.TeamId == localTeamId
				? BallCompassState.TeammateCarries
				: BallCompassState.EnemyCarries;
		}

		return true;
	}

	static GameObject FindMainBall( Scene scene )
	{
		if ( scene is null )
			return null;

		foreach ( var grab in scene.GetAllComponents<BallGrab>() )
		{
			if ( grab.MainBall.IsValid() )
				return grab.MainBall;
		}

		foreach ( var go in scene.GetAllObjects( true ) )
		{
			if ( go.Name == "main_ball" )
				return go;
		}

		return null;
	}

	static bool TryGetPlayerBearing( PlayerController playerController, Vector3 worldPosition, out Vector2 bearing )
	{
		bearing = default;

		if ( !playerController.IsValid() )
			return false;

		var lookRotation = playerController.EyeAngles.ToRotation();
		var lookForward = lookRotation.Forward.WithZ( 0 );
		var lookRight = lookRotation.Right.WithZ( 0 );
		if ( lookForward.LengthSquared < 0.0001f || lookRight.LengthSquared < 0.0001f )
			return false;

		lookForward = lookForward.Normal;
		lookRight = lookRight.Normal;

		var planar = (worldPosition - playerController.WorldPosition).WithZ( 0 );
		if ( planar.LengthSquared < 0.0001f )
			return false;

		planar = planar.Normal;

		bearing = new Vector2(
			Vector3.Dot( planar, lookRight ),
			Vector3.Dot( planar, lookForward ) );

		if ( bearing.LengthSquared < 0.0001f )
			return false;

		bearing = bearing.Normal;
		return true;
	}
}
