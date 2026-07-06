using Sandbox;
using System;

/// <summary>
/// Host-authoritative equipped loadout on the spawned pawn. Synced ids for remote read;
/// applies <see cref="ClassData"/> and enables the equipped <see cref="IPlayerUlt"/>.
/// </summary>
public sealed class PlayerLoadout : Component
{
	[Sync( SyncFlags.FromHost )]
	public string NetEquippedClassId { get; private set; } = LoadoutCatalog.ClassSpeedster;

	[Sync( SyncFlags.FromHost )]
	public string NetEquippedUltId { get; private set; } = LoadoutCatalog.UltSpeedBlitz;

	[Sync( SyncFlags.FromHost )]
	public string NetEquippedPassiveId { get; private set; } = LoadoutCatalog.PassiveDefault;

	[Property] public bool EnableLoadoutDebugLogs { get; set; }

	/// <summary> Host: apply committed save data to this pawn (class stats + ult enable). </summary>
	public void ApplyCommittedLoadoutOnHost( SavedLoadoutData data )
	{
		if ( !Networking.IsHost || data is null )
			return;

		var steamId = Network.Owner is not null ? (long)Network.Owner.SteamId : 0;
		if ( !LoadoutAuthority.TryValidateCommittedLoadout( steamId, data, out var normalized ) )
		{
			if ( EnableLoadoutDebugLogs )
				Log.Warning( $"[PlayerLoadout] Rejected loadout for {GameObject.Name}." );
			return;
		}

		data = normalized;

		NetEquippedClassId = data.ClassId;
		NetEquippedUltId = data.UltId ?? "";
		NetEquippedPassiveId = data.PassiveId ?? LoadoutCatalog.PassiveDefault;

		ApplyClassDataOnHost( data.ClassId );
		ConfigureEquippedUltOnHost();
		ConfigureSpeedsterOnlyComponentsOnHost();

		var ultCharge = Components.Get<PlayerUltCharge>();
		if ( ultCharge.IsValid() )
			ultCharge.ResyncFromEquippedUltOnHost();

		if ( EnableLoadoutDebugLogs )
		{
			Log.Info( $"[PlayerLoadout] {GameObject.Name}: class={NetEquippedClassId} ult={NetEquippedUltId} passive={NetEquippedPassiveId}" );
		}
	}

	/// <summary> Owner: push local committed loadout to host (join sync + confirm). </summary>
	[Rpc.Host]
	public void SubmitCommittedLoadoutFromOwnerRpc( string classId, string ultId, string passiveId, bool bypassPhaseGate )
	{
		if ( Network.Owner is null || Rpc.Caller.SteamId != Network.Owner.SteamId )
			return;

		var data = new SavedLoadoutData
		{
			ClassId = classId,
			UltId = ultId,
			PassiveId = passiveId
		};

		GameNetworkManager.FindInScene( Scene )?.TryApplyCommittedLoadoutOnHost( Network.Owner, data, bypassPhaseGate );
	}

	/// <summary> Resolves the equipped ult component for <see cref="PlayerUltCharge"/> and ult input. </summary>
	public IPlayerUlt ResolveEquippedUlt()
	{
		if ( string.IsNullOrWhiteSpace( NetEquippedUltId ) )
			return null;

		if ( string.Equals( NetEquippedUltId, LoadoutCatalog.UltSpeedBlitz, StringComparison.OrdinalIgnoreCase ) )
		{
			var blitz = Components.Get<SpeedsterSpeedBlitzUlt>();
			if ( blitz.IsValid() && blitz.Enabled )
				return blitz;
		}

		return null;
	}

	public bool IsSpeedsterClass() =>
		string.Equals( NetEquippedClassId, LoadoutCatalog.ClassSpeedster, StringComparison.OrdinalIgnoreCase );

	private void ApplyClassDataOnHost( string classId )
	{
		var classData = LoadoutCatalog.GetClassData( classId );
		if ( classData is null )
		{
			Log.Warning( $"[PlayerLoadout] Missing ClassData for '{classId}'." );
			return;
		}

		var playerClass = Components.Get<PlayerClass>();
		if ( !playerClass.IsValid() )
			return;

		playerClass.CurrentClass = classData;
		playerClass.ApplyClassAppearance();
	}

	private void ConfigureEquippedUltOnHost()
	{
		if ( !Networking.IsHost )
			return;

		var blitz = Components.Get<SpeedsterSpeedBlitzUlt>();
		if ( blitz.IsValid() )
		{
			var shouldEnable = string.Equals( NetEquippedUltId, LoadoutCatalog.UltSpeedBlitz, StringComparison.OrdinalIgnoreCase )
				&& IsSpeedsterClass();
			blitz.Enabled = shouldEnable;
		}

		var preview = Components.Get<SpeedBlitzAimPreview>();
		if ( preview.IsValid() )
			preview.Enabled = blitz.IsValid() && blitz.Enabled;

		var dashCam = Components.Get<SpeedBlitzDashCamera>();
		if ( dashCam.IsValid() )
			dashCam.Enabled = blitz.IsValid() && blitz.Enabled;

		var windUpFeel = Components.Get<SpeedBlitzWindUpFeel>();
		if ( windUpFeel.IsValid() )
			windUpFeel.Enabled = blitz.IsValid() && blitz.Enabled;

		var bodyGlow = Components.Get<SpeedBlitzBodyGlow>();
		if ( bodyGlow.IsValid() )
			bodyGlow.Enabled = blitz.IsValid() && blitz.Enabled;
	}

	/// <summary> Until per-class prefabs ship, shared <c>Player</c> may still carry Speedster-only anim helpers. </summary>
	public void ConfigureSpeedsterOnlyComponentsOnHost()
	{
		if ( !Networking.IsHost )
			return;

		var isSpeedster = IsSpeedsterClass();

		SetComponentEnabled<PlayerSpeedBlitzWindUpAnim>( isSpeedster, createIfSpeedster: true );
		SetComponentEnabled<BlitzConnectPoseFreeze>( isSpeedster, createIfSpeedster: true );
	}

	private void SetComponentEnabled<T>( bool enabled, bool createIfSpeedster ) where T : Component, new()
	{
		var component = Components.Get<T>();
		if ( !component.IsValid() && enabled && createIfSpeedster )
			component = Components.Create<T>();

		if ( component.IsValid() )
			component.Enabled = enabled;
	}
}
