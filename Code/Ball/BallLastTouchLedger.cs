using Sandbox;

/// <summary>
/// Host-only last-touch credit + XY anchor for OOB sky-drop (credited player&apos;s feet at touch). Lives on <c>main_ball</c>.
/// </summary>
public sealed class BallLastTouchLedger : Component
{
	private GameObject creditedPlayer;
	private Vector3 anchorPosition;
	private bool hasCredit;

	public static BallLastTouchLedger Get( GameObject ball )
		=> ComponentRequire.On<BallLastTouchLedger>( ball, "BallLastTouchLedger" );

	/// <summary> Host: throw release, voluntary drop, or tackle knock-off carrier. </summary>
	public void NotifyTouchOnHost( GameObject player, Vector3 anchorWorldPosition )
	{
		if ( !Networking.IsHost || !player.IsValid() )
			return;

		if ( player.Tags.Has( CitizenAvatarLod.PracticeNpcTag ) )
			return;

		creditedPlayer = player;
		anchorPosition = anchorWorldPosition;
		hasCredit = true;
	}

	public bool TryGetDropAnchor( out Vector3 anchorWorldPosition )
	{
		if ( !hasCredit )
		{
			anchorWorldPosition = default;
			return false;
		}

		anchorWorldPosition = anchorPosition;
		return true;
	}

	public void ClearOnHost()
	{
		if ( !Networking.IsHost )
			return;

		creditedPlayer = null;
		anchorPosition = default;
		hasCredit = false;
	}
}
