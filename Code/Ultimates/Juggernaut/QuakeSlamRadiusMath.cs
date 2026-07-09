using Sandbox;

/// <summary>
/// Shared Quake Slam ring geometry — host hit tests and owner preview use the same math.
/// </summary>
public static class QuakeSlamRadiusMath
{
	public enum QuakeSlamRing : byte
	{
		Inner = 0,
		Mid = 1,
		Outer = 2,
	}

	public static bool IsGrounded( Scene scene, GameObject self, Vector3 worldPosition, float maxGroundDistance = 12f )
	{
		if ( scene is null || !self.IsValid() )
			return false;

		var start = worldPosition + Vector3.Up * 4f;
		var end = worldPosition + Vector3.Down * 48f;
		var trace = scene.Trace.Ray( start, end )
			.IgnoreGameObject( self )
			.Run();

		return trace.Hit && trace.Distance <= maxGroundDistance;
	}

	public static bool IsWithinVerticalCylinder( Vector3 origin, Vector3 target, float maxVerticalSeparation )
	{
		var cap = MathF.Max( 0f, maxVerticalSeparation );
		return MathF.Abs( target.z - origin.z ) <= cap;
	}

	public static float HorizontalDistance( Vector3 origin, Vector3 target )
	{
		return (target.WithZ( 0f ) - origin.WithZ( 0f )).Length;
	}

	public static bool IsInRing(
		Vector3 origin,
		Vector3 target,
		QuakeSlamRing ring,
		float innerRadius,
		float midRadius,
		float outerRadius,
		float maxVerticalSeparation )
	{
		if ( !IsWithinVerticalCylinder( origin, target, maxVerticalSeparation ) )
			return false;

		var dist = HorizontalDistance( origin, target );
		innerRadius = MathF.Max( 1f, innerRadius );
		midRadius = MathF.Max( innerRadius, midRadius );
		outerRadius = MathF.Max( midRadius, outerRadius );

		return ring switch
		{
			QuakeSlamRing.Inner => dist <= innerRadius,
			QuakeSlamRing.Mid => dist > innerRadius && dist <= midRadius,
			QuakeSlamRing.Outer => dist > midRadius && dist <= outerRadius,
			_ => false,
		};
	}

	/// <summary> Mostly up with a slight outward push from the slam origin. </summary>
	public static Vector3 BuildLaunchDirection( Vector3 slamOrigin, Vector3 victimPosition, float outwardFraction )
	{
		var outward = (victimPosition - slamOrigin).WithZ( 0f );
		var frac = outwardFraction.Clamp( 0f, 1f );
		if ( outward.Length < 1f || frac <= 0.001f )
			return Vector3.Up;

		return (Vector3.Up + outward.Normal * frac).Normal;
	}

	public static float GetRingPowerMultiplier( QuakeSlamRing ring, float innerMultiplier, float midMultiplier, float outerMultiplier )
	{
		return ring switch
		{
			QuakeSlamRing.Inner => innerMultiplier,
			QuakeSlamRing.Mid => midMultiplier,
			QuakeSlamRing.Outer => outerMultiplier,
			_ => 1f,
		};
	}
}
