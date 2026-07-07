using Sandbox;

/// <summary>
/// Required editor-wired components — <see cref="On{T}"/> / <see cref="WarnIfMissing{T}"/> log when missing instead of auto-adding.
/// </summary>
public static class ComponentRequire
{
	public static T On<T>( GameObject go, string context ) where T : Component
	{
		if ( !go.IsValid() )
			return null;

		var component = go.Components.Get<T>();
		if ( !component.IsValid() )
			Log.Warning( $"[{context}] Missing {typeof( T ).Name} on '{go.Name}' — add it in the editor." );

		return component;
	}

	public static T On<T>( Component host, string context ) where T : Component
		=> On<T>( host?.GameObject, context );

	public static void WarnIfMissing<T>( GameObject go, string context ) where T : Component
	{
		if ( !go.IsValid() )
			return;

		if ( !go.Components.Get<T>().IsValid() )
			Log.Warning( $"[{context}] Missing {typeof( T ).Name} on '{go.Name}' — add it in the editor." );
	}

	public static void WarnIfMissing<T>( Component host, string context ) where T : Component
		=> WarnIfMissing<T>( host?.GameObject, context );
}
