using Sandbox;
using System;

/// <summary> Local committed loadout per <see cref="Connection.SteamId"/> under <see cref="FileSystem.Data"/>. </summary>
public static class LoadoutPersistence
{
	private const string SaveDirectory = "loadouts";
	private static bool loggedSavePathHint;

	/// <summary> Log virtual save path once per session on first write (see SESSION_NOTES for on-disk editor <c>#local</c> folder). </summary>
	public static bool EnablePersistenceDebugLogs { get; set; } = true;

	public static SavedLoadoutData GetOrCreateCommitted( long steamId )
	{
		if ( steamId == 0 )
			return LoadoutCatalog.CreatePreset();

		var path = GetSavePath( steamId );
		if ( FileSystem.Data.FileExists( path ) )
		{
			try
			{
				var loaded = FileSystem.Data.ReadJson<SavedLoadoutData>( path );
				if ( loaded is not null )
					return LoadoutCatalog.Normalize( loaded );
			}
			catch ( Exception ex )
			{
				Log.Warning( $"[LoadoutPersistence] Failed to read '{path}': {ex.Message}" );
			}
		}

		var preset = LoadoutCatalog.CreatePreset();
		SaveCommitted( steamId, preset );
		return preset;
	}

	public static void SaveCommitted( long steamId, SavedLoadoutData data )
	{
		if ( steamId == 0 || data is null )
			return;

		data = LoadoutCatalog.Normalize( data );
		var path = GetSavePath( steamId );
		EnsureSaveDirectory();

		try
		{
			FileSystem.Data.WriteJson( path, data );
			if ( EnablePersistenceDebugLogs && !loggedSavePathHint )
			{
				loggedSavePathHint = true;
				Log.Info( $"[LoadoutPersistence] Save root is FileSystem.Data (editor: .../data/local/ultimate_throwdown#local/). Wrote {path}" );
			}
		}
		catch ( Exception ex )
		{
			Log.Warning( $"[LoadoutPersistence] Failed to write '{path}': {ex.Message}" );
		}
	}

	public static bool HasSave( long steamId ) =>
		steamId != 0 && FileSystem.Data.FileExists( GetSavePath( steamId ) );

	private static string GetSavePath( long steamId ) => $"{SaveDirectory}/{steamId}.json";

	private static void EnsureSaveDirectory()
	{
		if ( !FileSystem.Data.DirectoryExists( SaveDirectory ) )
			FileSystem.Data.CreateDirectory( SaveDirectory );
	}
}
