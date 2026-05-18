using Sandbox;

/// <summary> Blocks <see cref="PlayerController"/> duck/crouch — unbinds are not always enough when built-in input is enabled. </summary>
[Order( 10000 )]
public sealed class PlayerDisableCrouch : Component
{
	[Property] public string DuckAction { get; set; } = "Duck";

	private PlayerController playerController;

	protected override void OnStart()
	{
		playerController = Components.Get<PlayerController>();
	}

	protected override void OnFixedUpdate()
	{
		playerController ??= Components.Get<PlayerController>();
		if ( !playerController.IsValid() || !playerController.Enabled )
			return;

		// Run after built-in input (high <see cref="Order"/>) so ctrl / gamepad B cannot duck.
		playerController.UpdateDucking( false );
	}
}
