using Sandbox;

/// <summary>
/// Throw wind-up indicator (owner only). Replace with Razor/CodePen-derived HUD when ready.
/// </summary>
public sealed class ThrowChargeBar : Component
{
	[Property] public Vector3 ChargeBarOffset { get; set; } = new( 0f, 0f, 70f );

	private bool isVisible;
	private float charge01;

	public void Show()
	{
		isVisible = true;
	}

	public void Hide()
	{
		isVisible = false;
	}

	public void SetCharge( float charge )
	{
		charge01 = charge.Clamp( 0f, 1f );
	}

	protected override void OnUpdate()
	{
		if ( IsProxy )
			return;

		if ( !isVisible )
			return;

		var displayPosition = GameObject.WorldPosition + ChargeBarOffset;
		DebugOverlay.Text( displayPosition, BuildChargeBarText() );
	}

	private string BuildChargeBarText()
	{
		const int totalBlocks = 24;
		var filledBlocks = (int)(charge01 * totalBlocks + 0.5f);
		filledBlocks = filledBlocks.Clamp( 0, totalBlocks );

		var bar = "[";
		for ( var i = 0; i < totalBlocks; i++ )
		{
			if ( i == 8 || i == 16 )
				bar += "|";

			bar += i < filledBlocks ? "█" : "░";
		}
		bar += "]";

		return $"Throw Charge: {bar}";
	}
}
