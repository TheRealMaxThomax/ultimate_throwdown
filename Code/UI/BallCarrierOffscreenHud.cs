using System;
using Sandbox;
using Sandbox.Rendering;

/// <summary>
/// Local viewer HUD: edge arrow toward a teammate who is carrying the ball when they are off-screen.
/// </summary>
public sealed class BallCarrierOffscreenHud : Component
{
	[Property] public float EdgeMargin { get; set; } = 56f;
	[Property] public float OnScreenInset { get; set; } = 0.04f;
	[Property] public float ArrowLength { get; set; } = 22f;
	[Property] public float ArrowWidth { get; set; } = 4f;
	[Property] public float WorldTargetHeight { get; set; } = 36f;
	[Property] public float MinBehindCameraDot { get; set; } = 0.08f;

	private PlayerTeam playerTeam;

	protected override void OnStart()
	{
		playerTeam = Components.Get<PlayerTeam>();
	}

	protected override void OnUpdate()
	{
		if ( IsProxy || !Network.IsOwner )
			return;

		var camera = Scene.Camera;
		if ( camera is null )
			return;

		playerTeam ??= Components.Get<PlayerTeam>();
		if ( playerTeam is null || !MatchTeamIds.IsValid( playerTeam.TeamId ) )
			return;

		if ( playerTeam.SyncedMatchPhase is not MatchPhase.Playing and not MatchPhase.GoalCelebration )
			return;

		if ( !TryFindTeammateBallCarrier( Scene, playerTeam.TeamId, out var carrier, out var ball ) )
			return;

		var targetWorld = ball.IsValid() ? ball.WorldPosition : carrier.WorldPosition;
		targetWorld += Vector3.Up * WorldTargetHeight;

		if ( IsTargetOnScreen( camera, targetWorld ) )
			return;

		if ( !TryGetEdgeArrow( camera, targetWorld, out var tip, out var tail ) )
			return;

		var hud = camera.Hud;
		var alpha = 0.85f + (0.15f * MathF.Sin( Time.Now * 4f ));
		var color = new Color( 1f, 1f, 1f, alpha );
		hud.DrawLine( tip, tail, ArrowWidth, color );
		hud.DrawLine( tip, tail + GetArrowWingOffset( tip, tail, ArrowLength, 0.55f ), ArrowWidth * 0.85f, color );
		hud.DrawLine( tip, tail + GetArrowWingOffset( tip, tail, ArrowLength, -0.55f ), ArrowWidth * 0.85f, color );
	}

	static bool TryFindTeammateBallCarrier( Scene scene, int localTeamId, out GameObject carrier, out GameObject ball )
	{
		carrier = null;
		ball = null;

		if ( scene is null || !MatchTeamIds.IsValid( localTeamId ) )
			return false;

		var local = Connection.Local;

		foreach ( var grab in scene.GetAllComponents<BallGrab>() )
		{
			if ( !grab.IsValid() || !grab.IsHolding )
				continue;

			var holder = grab.GameObject;
			if ( !holder.IsValid() || !holder.Network.Active )
				continue;

			var holderTeam = grab.Components.Get<PlayerTeam>();
			if ( !holderTeam.IsValid() || holderTeam.TeamId != localTeamId )
				continue;

			if ( local is not null )
			{
				var owner = holder.Network.Owner;
				if ( owner is not null && owner.SteamId == local.SteamId )
					continue;
			}

			var heldBall = grab.HeldBall;
			if ( !heldBall.IsValid() )
				continue;

			carrier = holder;
			ball = heldBall;
			return true;
		}

		return false;
	}

	bool IsTargetOnScreen( CameraComponent camera, Vector3 worldPosition )
	{
		var toTarget = worldPosition - camera.WorldPosition;
		if ( Vector3.Dot( camera.WorldRotation.Forward, toTarget.Normal ) < MinBehindCameraDot )
			return false;

		var screenNormal = camera.PointToScreenNormal( worldPosition );
		var inset = OnScreenInset;
		return screenNormal.x >= inset
			&& screenNormal.x <= 1f - inset
			&& screenNormal.y >= inset
			&& screenNormal.y <= 1f - inset;
	}

	bool TryGetEdgeArrow( CameraComponent camera, Vector3 worldPosition, out Vector2 tip, out Vector2 tail )
	{
		tip = default;
		tail = default;

		var center = new Vector2( Screen.Width * 0.5f, Screen.Height * 0.5f );
		var toTarget = worldPosition - camera.WorldPosition;
		var localDir = camera.WorldRotation.Inverse * toTarget;

		var direction = new Vector2( localDir.x, -localDir.y );
		if ( direction.LengthSquared < 0.0001f )
			direction = new Vector2( 0f, 1f );
		else
			direction = direction.Normal;

		if ( !TryGetEdgePoint( center, direction, EdgeMargin, out tip ) )
			return false;

		tail = tip - (direction * ArrowLength);
		return true;
	}

	static bool TryGetEdgePoint( Vector2 center, Vector2 direction, float margin, out Vector2 edgePoint )
	{
		edgePoint = default;

		if ( direction.LengthSquared < 0.0001f )
			return false;

		direction = direction.Normal;

		var maxT = float.MaxValue;
		if ( direction.x > 0.0001f )
			maxT = MathF.Min( maxT, (Screen.Width - margin - center.x) / direction.x );
		if ( direction.x < -0.0001f )
			maxT = MathF.Min( maxT, (margin - center.x) / direction.x );
		if ( direction.y > 0.0001f )
			maxT = MathF.Min( maxT, (Screen.Height - margin - center.y) / direction.y );
		if ( direction.y < -0.0001f )
			maxT = MathF.Min( maxT, (margin - center.y) / direction.y );

		if ( maxT <= 0f || maxT == float.MaxValue )
			return false;

		edgePoint = center + (direction * maxT);
		return true;
	}

	static Vector2 GetArrowWingOffset( Vector2 tip, Vector2 tail, float arrowLength, float side )
	{
		var forward = (tip - tail).Normal;
		var perpendicular = new Vector2( -forward.y, forward.x );
		return perpendicular * (arrowLength * 0.45f * side);
	}
}
