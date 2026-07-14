using Sandbox;

/// <summary> Owner-only pending loadout while picker is open; force-commit on round resume. </summary>
public sealed class LoadoutClientState : Component
{
	[Property] public bool EnableLoadoutDebugLogs { get; set; }

	public SavedLoadoutData PendingLoadout { get; private set; } = LoadoutCatalog.CreatePreset();
	public bool IsPickerOpen { get; private set; }

	private PlayerLoadout playerLoadout;
	private PlayerTeam playerTeam;
	private MatchPhase lastObservedPhase = MatchPhase.Playing;

	protected override void OnStart()
	{
		if ( !Network.IsOwner )
			return;

		playerLoadout = Components.Get<PlayerLoadout>();
		playerTeam = Components.Get<PlayerTeam>();

		var steamId = ResolveOwnerSteamId();
		PendingLoadout = Clone( LoadoutPersistence.GetOrCreateCommitted( steamId ) );
		SyncPendingFromEquipped();

		if ( playerLoadout.IsValid() )
			playerLoadout.SubmitCommittedLoadoutFromOwnerRpc( PendingLoadout.ClassId, PendingLoadout.UltId, PendingLoadout.PassiveId, bypassPhaseGate: true );
	}

	protected override void OnUpdate()
	{
		if ( !Network.IsOwner )
			return;

		playerTeam ??= Components.Get<PlayerTeam>();
		if ( !playerTeam.IsValid() )
			return;

		var phase = playerTeam.SyncedMatchPhase;
		if ( phase == MatchPhase.MatchSetup && lastObservedPhase != MatchPhase.MatchSetup )
			IsPickerOpen = true;

		if ( (lastObservedPhase == MatchPhase.Intermission || lastObservedPhase == MatchPhase.MatchSetup)
			&& phase == MatchPhase.Playing )
			ForceCommitPending();

		lastObservedPhase = phase;

		if ( !LoadoutAuthority.IsLoadoutSwapAllowed( Scene, playerTeam ) )
		{
			if ( IsPickerOpen )
				IsPickerOpen = false;
			ApplyPickerInputMode();
			return;
		}

		if ( Input.Pressed( "Menu" ) )
			IsPickerOpen = !IsPickerOpen;

		ApplyPickerInputMode();
	}

	public void SetPendingClass( string classId )
	{
		if ( !Network.IsOwner )
			return;

		PendingLoadout = LoadoutCatalog.WithClassAutoFill( classId );
	}

	public void SetPendingUlt( string ultId )
	{
		if ( !Network.IsOwner )
			return;

		if ( !LoadoutCatalog.IsUltValidForClass( PendingLoadout.ClassId, ultId ) )
			return;

		PendingLoadout.UltId = ultId ?? "";
	}

	public void ConfirmPending()
	{
		if ( !Network.IsOwner )
			return;

		CommitPending( closePicker: true, bypassPhaseGate: false );
	}

	public void ForceCommitPending()
	{
		if ( !Network.IsOwner )
			return;

		CommitPending( closePicker: true, bypassPhaseGate: true );
	}

	private void CommitPending( bool closePicker, bool bypassPhaseGate )
	{
		var steamId = ResolveOwnerSteamId();
		var committed = Clone( PendingLoadout );
		committed = LoadoutCatalog.Normalize( committed );

		LoadoutPersistence.SaveCommitted( steamId, committed );
		PendingLoadout = Clone( committed );

		playerLoadout ??= Components.Get<PlayerLoadout>();
		if ( playerLoadout.IsValid() )
			playerLoadout.SubmitCommittedLoadoutFromOwnerRpc( committed.ClassId, committed.UltId, committed.PassiveId, bypassPhaseGate );

		if ( closePicker )
			IsPickerOpen = false;

		ApplyPickerInputMode();

		if ( EnableLoadoutDebugLogs )
			Log.Info( $"[LoadoutClient] Committed class={committed.ClassId} ult={committed.UltId} passive={committed.PassiveId}" );
	}

	private void SyncPendingFromEquipped()
	{
		playerLoadout ??= Components.Get<PlayerLoadout>();
		if ( !playerLoadout.IsValid() )
			return;

		PendingLoadout = new SavedLoadoutData
		{
			ClassId = playerLoadout.NetEquippedClassId,
			UltId = playerLoadout.NetEquippedUltId,
			PassiveId = playerLoadout.NetEquippedPassiveId
		};
	}

	private long ResolveOwnerSteamId()
	{
		if ( Network.Owner is not null )
			return (long)Network.Owner.SteamId;

		return Connection.Local is not null ? (long)Connection.Local.SteamId : 0;
	}

	private static SavedLoadoutData Clone( SavedLoadoutData data )
	{
		if ( data is null )
			return LoadoutCatalog.CreatePreset();

		return new SavedLoadoutData
		{
			ClassId = data.ClassId,
			UltId = data.UltId,
			PassiveId = data.PassiveId
		};
	}

	private void ApplyPickerInputMode()
	{
		Mouse.Visibility = IsPickerOpen ? MouseVisibility.Visible : MouseVisibility.Hidden;

		var pc = Components.Get<PlayerController>();
		if ( !pc.IsValid() )
			return;

		pc.UseLookControls = !IsPickerOpen;
		pc.UseInputControls = !IsPickerOpen;
	}

	protected override void OnDisabled()
	{
		if ( !Network.IsOwner )
			return;

		Mouse.Visibility = MouseVisibility.Hidden;

		var pc = Components.Get<PlayerController>();
		if ( pc.IsValid() )
		{
			pc.UseLookControls = true;
			pc.UseInputControls = true;
		}
	}
}
