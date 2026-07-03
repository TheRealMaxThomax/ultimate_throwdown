using System;
using System.Collections.Generic;
using Sandbox;

/// <summary>
/// Passive oriented box volume — host ball OOB watcher tests ball position against all zones in the scene.
/// Mirror <see cref="GoalZone"/> placement: empty per strip, tune <see cref="BoxSize"/> + rotation.
/// </summary>
public sealed class OutOfBoundsZone : Component, Component.ExecuteInEditor
{
	private static readonly List<OutOfBoundsZone> ActiveZones = new();

	/// <summary> Full size of the box in local X/Y/Z (not half-extents). Centered on this object. </summary>
	[Property] public Vector3 BoxSize { get; set; } = new( 300f, 300f, 200f );

	[Property] public Color GizmoColor { get; set; } = new( 1f, 1f, 1f, 0.35f );

	protected override void OnEnabled()
	{
		if ( !ActiveZones.Contains( this ) )
			ActiveZones.Add( this );
	}

	protected override void OnDisabled()
	{
		ActiveZones.Remove( this );
	}

	protected override void DrawGizmos()
	{
		var prevTransform = Gizmo.Transform;
		Gizmo.Transform = WorldTransform.WithScale( 1f );
		Gizmo.Draw.Color = GizmoColor;
		Gizmo.Draw.LineBBox( new BBox( -BoxSize * 0.5f, BoxSize * 0.5f ) );
		Gizmo.Transform = prevTransform;
	}

	public static bool IsPointInsideAnyZone( Scene scene, Vector3 worldPoint )
	{
		if ( scene is null )
			return false;

		foreach ( var zone in ActiveZones )
		{
			if ( !zone.IsValid() || !zone.Enabled )
				continue;

			if ( zone.GameObject.Scene != scene )
				continue;

			if ( zone.ContainsWorldPoint( worldPoint ) )
				return true;
		}

		return false;
	}

	public bool ContainsWorldPoint( Vector3 worldPoint )
	{
		var halfExtents = BoxSize * 0.5f;
		if ( halfExtents.LengthSquared <= 0.001f )
			return false;

		return IsPointInOrientedBox( worldPoint, WorldTransform, halfExtents );
	}

	internal static bool IsPointInOrientedBox( Vector3 worldPoint, Transform boxTransform, Vector3 halfExtents )
	{
		var localPoint = boxTransform.PointToLocal( worldPoint );
		return Math.Abs( localPoint.x ) <= halfExtents.x
			&& Math.Abs( localPoint.y ) <= halfExtents.y
			&& Math.Abs( localPoint.z ) <= halfExtents.z;
	}
}
