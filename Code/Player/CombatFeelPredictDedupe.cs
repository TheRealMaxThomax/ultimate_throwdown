using Sandbox;

/// <summary>
/// Host-assigned combat feel apply ids + owner predict dedupe (Tier A3).
/// Model after <see cref="PlayerDodge"/> / <see cref="PlayerDodge.DodgeApplySequence"/>.
/// </summary>
public sealed class CombatFeelPredictDedupe : Component
{
	private int netCombatFeelApplyId;

	[Sync( SyncFlags.FromHost )]
	private int NetCombatFeelApplyId
	{
		get => netCombatFeelApplyId;
		set => netCombatFeelApplyId = value;
	}

	private bool ownerAttackerFeelPendingDedupe;
	private bool ownerVictimFeelPendingDedupe;
	private int ownerLastConsumedAttackerApplyId = -1;
	private int ownerLastConsumedVictimApplyId = -1;
	private bool ownerVictimFeelTriggeredThisKnockdown;

	/// <summary>Latest host-allocated apply id (debug / future features).</summary>
	public int CombatFeelApplySequence => netCombatFeelApplyId;

	/// <summary>Host: allocate the next id for an owner feel RPC on this pawn.</summary>
	public int AllocateCombatFeelApplyIdOnHost()
	{
		if ( !Networking.IsHost )
			return netCombatFeelApplyId;

		netCombatFeelApplyId++;
		return netCombatFeelApplyId;
	}

	/// <summary>Owner: local attacker feel ran before host confirm (tackle / blitz predict).</summary>
	public void MarkOwnerPredictedAttackerFeel()
	{
		if ( !Network.IsOwner )
			return;

		ownerAttackerFeelPendingDedupe = true;
	}

	/// <summary>Owner RPC: returns true when host feel should be skipped (predict already played).</summary>
	public bool TryConsumeHostAttackerFeelDedupe( int hostApplyId )
	{
		if ( !Network.IsOwner )
			return false;

		if ( ownerAttackerFeelPendingDedupe )
		{
			ownerAttackerFeelPendingDedupe = false;
			ownerLastConsumedAttackerApplyId = hostApplyId;
			return true;
		}

		if ( hostApplyId == ownerLastConsumedAttackerApplyId )
			return true;

		ownerLastConsumedAttackerApplyId = hostApplyId;
		return false;
	}

	/// <summary>Owner: first victim feel for this knockdown (pre-launch freeze — Tier A2).</summary>
	public bool TryBeginOwnerPredictedVictimFeel()
	{
		if ( !Network.IsOwner || ownerVictimFeelTriggeredThisKnockdown )
			return false;

		ownerVictimFeelTriggeredThisKnockdown = true;
		ownerVictimFeelPendingDedupe = true;
		return true;
	}

	/// <summary>Owner RPC: returns true when host victim feel should be skipped.</summary>
	public bool TryConsumeHostVictimFeelDedupe( int hostApplyId )
	{
		if ( !Network.IsOwner )
			return false;

		if ( ownerVictimFeelPendingDedupe )
		{
			ownerVictimFeelPendingDedupe = false;
			ownerLastConsumedVictimApplyId = hostApplyId;
			return true;
		}

		if ( hostApplyId == ownerLastConsumedVictimApplyId )
			return true;

		ownerLastConsumedVictimApplyId = hostApplyId;
		return false;
	}

	/// <summary>Owner: stand-up — allow victim predict on the next knockdown.</summary>
	public void ResetVictimKnockdownPredictState()
	{
		ownerVictimFeelTriggeredThisKnockdown = false;
	}
}
