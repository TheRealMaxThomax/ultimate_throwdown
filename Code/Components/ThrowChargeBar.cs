using Sandbox;
using Sandbox.Diagnostics;

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
		if ( !isVisible )
			return;

		var displayPosition = Transform.Position + ChargeBarOffset;
		DebugOverlay.Text( displayPosition, BuildChargeBarText() );
	}

	private string BuildChargeBarText()
	{
		const int totalBlocks = 12;
		var filledBlocks = (int)(charge01 * totalBlocks + 0.5f);
		filledBlocks = filledBlocks.Clamp( 0, totalBlocks );

		var bar = "";
		for ( var i = 0; i < totalBlocks; i++ )
		{
			var isFilled = i < filledBlocks;
			if ( !isFilled )
			{
				bar += "⬛";
				continue;
			}

			if ( i < 4 )
				bar += "🟥";
			else if ( i < 8 )
				bar += "🟨";
			else
				bar += "🟩";
		}

		var percent = (int)(charge01 * 100f + 0.5f);
		return $"Throw Charge: {bar} {percent}%";
	}
}
