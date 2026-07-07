using System;
using Sandbox;

/// <summary>
/// Host polls whether a ball carrier is inside this zone long enough to score.
/// Valid goal: carrier holds ball, carrier team ≠ <see cref="DefendingTeam"/>, dwell completes while <see cref="MatchDirector"/> is in <see cref="MatchPhase.Playing"/>.
/// Zone is an oriented box centered on this object&apos;s position, sized by <see cref="BoxSize"/>.
/// </summary>
public sealed class GoalZone : Component, Component.ExecuteInEditor
{
	/// <summary> Team that defends this end — scoring team must be the other id. </summary>
	[Property] public int DefendingTeam { get; set; }

	[Property] public float ScoreDwellSeconds { get; set; } = 0.35f;

	/// <summary> Full size of the goal box in local X/Y/Z (not half-extents). Centered on this object. </summary>
	[Property] public Vector3 BoxSize { get; set; } = new( 300f, 300f, 200f );

	[Property] public bool EnableGoalZoneDebugLogs { get; set; } = false;

	[Property] public Color GizmoColor { get; set; } = new( 1f, 0.5f, 0f, 0.5f );

	private MatchDirector matchDirector;
	private GameObject activeCarrier;
	private float dwellRemaining;
	private float nextDiagnosticLogAt;
	private bool warnedInvalidDefendingTeam;

	protected override void OnStart()
	{
		if ( !Networking.IsHost )
			return;

		matchDirector = MatchDirector.FindInScene( Scene );
		if ( EnableGoalZoneDebugLogs )
		{
			if ( !matchDirector.IsValid() )
				Log.Warning( $"[GoalZone] {GameObject.Name}: no MatchDirector in scene." );
			else
				Log.Info( $"[GoalZone] {GameObject.Name}: defending team {DefendingTeam}, center {WorldPosition}, box {BoxSize}." );
		}
	}

	protected override void OnUpdate()
	{
		if ( !Networking.IsHost )
			return;

		if ( !MatchTeamIds.IsValid( DefendingTeam ) )
		{
			if ( !warnedInvalidDefendingTeam )
			{
				warnedInvalidDefendingTeam = true;
				Log.Warning( $"[GoalZone] {GameObject.Name}: DefendingTeam must be 0 or 1." );
			}

			return;
		}

		if ( !matchDirector.IsValid() )
			matchDirector = MatchDirector.FindInScene( Scene );

		if ( EnableGoalZoneDebugLogs )
			MaybeLogDiagnostics();

		if ( !matchDirector.IsValid() || !matchDirector.IsScoringAllowed )
		{
			ResetDwell();
			return;
		}

		if ( !TryFindScoringCarrierInZone( out var carrier, out var scoringTeamId ) )
		{
			ResetDwell();
			return;
		}

		if ( activeCarrier != carrier )
		{
			activeCarrier = carrier;
			dwellRemaining = 0f;

			if ( EnableGoalZoneDebugLogs )
				Log.Info( $"[GoalZone] {GameObject.Name}: carrier {carrier.Name} entered zone (team {scoringTeamId}). Dwell {ScoreDwellSeconds:0.00}s." );
		}

		dwellRemaining += Time.Delta;
		if ( dwellRemaining < ScoreDwellSeconds )
			return;

		carrier.Components.Get<PlayerUltCharge>()?.GrantGoalChargeOnHost();

		var heldBall = carrier.Components.Get<BallGrab>()?.HeldBall;
		if ( heldBall.IsValid() )
			BallPassAssistState.Get( heldBall )?.TryGrantAssistChargeOnHost( carrier );

		matchDirector.RegisterGoal( scoringTeamId );
		ResetDwell();

		if ( EnableGoalZoneDebugLogs )
		{
			var teamName = MapMatchConfig.FindInScene( Scene )?.GetTeamDisplayName( scoringTeamId ) ?? $"Team{scoringTeamId}";
			Log.Info( $"[GoalZone] {GameObject.Name}: goal registered for {teamName}." );
		}
	}

	protected override void DrawGizmos()
	{
		var prevTransform = Gizmo.Transform;
		Gizmo.Transform = WorldTransform.WithScale( 1f );
		Gizmo.Draw.Color = GizmoColor;
		Gizmo.Draw.LineBBox( new BBox( -BoxSize * 0.5f, BoxSize * 0.5f ) );
		Gizmo.Transform = prevTransform;
	}

	private void MaybeLogDiagnostics()
	{
		if ( Time.Now < nextDiagnosticLogAt )
			return;

		nextDiagnosticLogAt = Time.Now + 1f;

		var scoringAllowed = matchDirector.IsValid() && matchDirector.IsScoringAllowed;
		var foundAnyHolder = false;

		foreach ( var ballGrab in Scene.GetAllComponents<BallGrab>() )
		{
			if ( !ballGrab.IsValid() || !ballGrab.IsHolding )
				continue;

			foundAnyHolder = true;
			var player = ballGrab.GameObject;
			var sample = GetCarrierSamplePosition( ballGrab );
			var local = WorldTransform.PointToLocal( sample );
			var half = BoxSize * 0.5f;
			var inZone = IsInsideHalfExtents( local, half );

			var playerTeam = player.Components.Get<PlayerTeam>();
			var teamId = playerTeam.IsValid() ? playerTeam.TeamId : -1;
			var isPracticeNpc = player.Tags.Has( CitizenAvatarLod.PracticeNpcTag );
			var isOwnGoal = teamId == DefendingTeam;
			var canScoreHere = !isPracticeNpc && MatchTeamIds.IsValid( teamId ) && !isOwnGoal && inZone;

			Log.Info( $"[GoalZone] {GameObject.Name} diag: holder={player.Name} team={teamId} defending={DefendingTeam} ballAt={sample} local={local} half={half} inZone={inZone} ownGoal={isOwnGoal} canScore={canScoreHere} phaseOk={scoringAllowed}" );
		}

		if ( !foundAnyHolder && scoringAllowed )
			Log.Info( $"[GoalZone] {GameObject.Name} diag: no ball carrier in scene (host sees IsHolding=false on everyone)." );
	}

	private bool TryFindScoringCarrierInZone( out GameObject carrier, out int scoringTeamId )
	{
		carrier = null;
		scoringTeamId = MatchTeamIds.Team0;

		var halfExtents = BoxSize * 0.5f;
		if ( halfExtents.LengthSquared <= 0.001f )
			return false;

		var zoneTransform = WorldTransform;

		foreach ( var ballGrab in Scene.GetAllComponents<BallGrab>() )
		{
			if ( !ballGrab.IsValid() || !ballGrab.IsHolding )
				continue;

			var player = ballGrab.GameObject;
			if ( !player.Enabled )
				continue;

			if ( player.Tags.Has( CitizenAvatarLod.PracticeNpcTag ) )
				continue;

			var playerTeam = player.Components.Get<PlayerTeam>();
			if ( !playerTeam.IsValid() || !MatchTeamIds.IsValid( playerTeam.TeamId ) )
				continue;

			if ( playerTeam.TeamId == DefendingTeam )
				continue;

			var sample = GetCarrierSamplePosition( ballGrab );
			if ( !IsPointInOrientedBox( sample, zoneTransform, halfExtents ) )
				continue;

			carrier = player;
			scoringTeamId = playerTeam.TeamId;
			return true;
		}

		return false;
	}

	private static Vector3 GetCarrierSamplePosition( BallGrab ballGrab )
	{
		if ( ballGrab.IsHolding && ballGrab.HeldBall.IsValid() )
			return ballGrab.HeldBall.WorldPosition;

		return ballGrab.GameObject.WorldPosition;
	}

	private static bool IsPointInOrientedBox( Vector3 worldPoint, Transform boxTransform, Vector3 halfExtents )
	{
		var localPoint = boxTransform.PointToLocal( worldPoint );
		return IsInsideHalfExtents( localPoint, halfExtents );
	}

	private static bool IsInsideHalfExtents( Vector3 localPoint, Vector3 halfExtents )
	{
		return Math.Abs( localPoint.x ) <= halfExtents.x
			&& Math.Abs( localPoint.y ) <= halfExtents.y
			&& Math.Abs( localPoint.z ) <= halfExtents.z;
	}

	private void ResetDwell()
	{
		activeCarrier = null;
		dwellRemaining = 0f;
	}
}
