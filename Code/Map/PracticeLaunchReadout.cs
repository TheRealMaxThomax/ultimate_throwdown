using Sandbox;

/// <summary> Practice arena TV / sign — shows the latest launch band score from <see cref="PracticeLaunchMeasure"/>. </summary>
public sealed class PracticeLaunchReadout : Component
{
	[Property] public Vector2 PanelSize { get; set; } = new( 320f, 180f );

	[Property] public bool LookAtCamera { get; set; } = true;

	[Property, Group( "Score text" )] public int ScoreFontSize { get; set; } = 120;

	[Property, Group( "Score text" )] public int ScoreFontWeight { get; set; } = 800;

	/// <summary> CSS font family name — e.g. Poppins, Roboto, Arial. Must be available to s&amp;box UI. </summary>
	[Property, Group( "Score text" )] public string ScoreFontFamily { get; set; } = "Poppins";

	[Property, Group( "Score text" )] public Color ScoreColor { get; set; } = new( 0.36f, 1f, 0.54f );

	[Property, Group( "Score text" )] public Color PanelBackgroundColor { get; set; } = new( 0.03f, 0.05f, 0.07f, 0.92f );

	/// <summary> Seconds to keep the score visible before returning to idle. </summary>
	[Property] public float HoldSeconds { get; set; } = 5f;

	[Property] public bool EnableDebugLogs { get; set; }

	private PracticeLaunchReadoutRoot panelRoot;
	private WorldPanel worldPanel;
	private float clearAt = -1f;

	protected override void OnStart()
	{
		worldPanel = Components.Get<WorldPanel>();
		if ( !worldPanel.IsValid() )
		{
			worldPanel = Components.Create<WorldPanel>();
			worldPanel.PanelSize = PanelSize;
			worldPanel.LookAtCamera = LookAtCamera;
		}
		else
		{
			if ( PanelSize.x > 0f && PanelSize.y > 0f )
				worldPanel.PanelSize = PanelSize;

			worldPanel.LookAtCamera = LookAtCamera;
		}

		panelRoot = Components.GetOrCreate<PracticeLaunchReadoutRoot>();
		panelRoot.RefreshAppearance();
	}

	protected override void OnUpdate()
	{
		if ( clearAt < 0f || Time.Now < clearAt )
			return;

		clearAt = -1f;
		panelRoot?.SetIdle();
	}

	/// <summary> Host: broadcast band score to every client&apos;s sign. </summary>
	public void ShowScoreOnHost( int score )
	{
		if ( !Networking.IsHost )
			return;

		BroadcastScoreRpc( score );
	}

	[Rpc.Broadcast]
	private void BroadcastScoreRpc( int score )
	{
		if ( EnableDebugLogs )
			Log.Info( $"[PracticeLaunchReadout] score={score}" );

		panelRoot?.SetScore( score );
		clearAt = HoldSeconds > 0f ? Time.Now + HoldSeconds : -1f;
	}
}
