/// <summary>
/// Per-ult hook — each ult component declares its own charge cap.
/// <see cref="PlayerUltCharge"/> normalizes raw points to 0–100% using the equipped ult&apos;s <see cref="MaxChargePoints"/>.
/// </summary>
public interface IPlayerUlt
{
	/// <summary> Raw points required for 100% on this ult (HUD still shows 0–100%). </summary>
	float MaxChargePoints { get; }
}
