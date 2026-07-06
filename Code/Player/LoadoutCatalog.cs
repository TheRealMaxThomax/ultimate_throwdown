using Sandbox;
using System;
using System.Collections.Generic;

/// <summary> Static catalog of loadout class / ult / passive string ids and <see cref="ClassData"/> paths. </summary>
public static class LoadoutCatalog
{
	public const string ClassSpeedster = "speedster";
	public const string ClassJuggernaut = "juggernaut";
	public const string ClassSniper = "sniper";

	public const string UltSpeedBlitz = "speed_blitz";

	public const string PassiveDefault = "default";
	public const string PassiveTackleRamp = "tackle_ramp";

	public const string ClassDataSpeedsterPath = "classes/speedster.cdata";
	public const string ClassDataJuggernautPath = "classes/juggernaut.cdata";
	public const string ClassDataSniperPath = "classes/sniper.cdata";

	private static readonly string[] AllClassIds = { ClassSpeedster, ClassJuggernaut, ClassSniper };

	private static readonly Dictionary<string, string> ClassDataPathById = new( StringComparer.OrdinalIgnoreCase )
	{
		[ClassSpeedster] = ClassDataSpeedsterPath,
		[ClassJuggernaut] = ClassDataJuggernautPath,
		[ClassSniper] = ClassDataSniperPath,
	};

	private static readonly Dictionary<string, string[]> UltIdsByClass = new( StringComparer.OrdinalIgnoreCase )
	{
		[ClassSpeedster] = new[] { UltSpeedBlitz },
		[ClassJuggernaut] = Array.Empty<string>(),
		[ClassSniper] = Array.Empty<string>(),
	};

	private static readonly Dictionary<string, string[]> PassiveIdsByClass = new( StringComparer.OrdinalIgnoreCase )
	{
		[ClassSpeedster] = new[] { PassiveDefault },
		[ClassJuggernaut] = new[] { PassiveTackleRamp },
		[ClassSniper] = new[] { PassiveDefault },
	};

	/// <summary> First-ever / missing save preset. </summary>
	public static SavedLoadoutData CreatePreset() => new()
	{
		ClassId = ClassSpeedster,
		UltId = UltSpeedBlitz,
		PassiveId = PassiveDefault,
	};

	public static bool IsValidClassId( string classId ) =>
		!string.IsNullOrWhiteSpace( classId ) && ClassDataPathById.ContainsKey( classId );

	public static string GetClassDataPath( string classId )
	{
		if ( string.IsNullOrWhiteSpace( classId ) )
			return ClassDataSpeedsterPath;

		return ClassDataPathById.TryGetValue( classId, out var path )
			? path
			: ClassDataSpeedsterPath;
	}

	public static ClassData GetClassData( string classId )
	{
		var path = GetClassDataPath( classId );
		return ResourceLibrary.Get<ClassData>( path );
	}

	public static string GetFirstUltIdForClass( string classId )
	{
		if ( !UltIdsByClass.TryGetValue( classId ?? "", out var ults ) || ults.Length == 0 )
			return "";

		return ults[0];
	}

	public static string GetFirstPassiveIdForClass( string classId )
	{
		if ( !PassiveIdsByClass.TryGetValue( classId ?? "", out var passives ) || passives.Length == 0 )
			return PassiveDefault;

		return passives[0];
	}

	public static bool IsUltValidForClass( string classId, string ultId )
	{
		if ( string.IsNullOrWhiteSpace( ultId ) )
			return UltIdsByClass.TryGetValue( classId ?? "", out var ults ) && ults.Length == 0;

		if ( !UltIdsByClass.TryGetValue( classId ?? "", out var list ) )
			return false;

		foreach ( var id in list )
		{
			if ( string.Equals( id, ultId, StringComparison.OrdinalIgnoreCase ) )
				return true;
		}

		return false;
	}

	/// <summary> Fix invalid ids; auto first ult/passive when class changes or catalog grows. </summary>
	public static SavedLoadoutData Normalize( SavedLoadoutData data )
	{
		data ??= CreatePreset();

		if ( !IsValidClassId( data.ClassId ) )
			data.ClassId = ClassSpeedster;

		if ( !IsUltValidForClass( data.ClassId, data.UltId ) )
			data.UltId = GetFirstUltIdForClass( data.ClassId );

		var passives = PassiveIdsByClass.TryGetValue( data.ClassId, out var list ) ? list : new[] { PassiveDefault };
		var passiveOk = false;
		foreach ( var passiveId in passives )
		{
			if ( !string.Equals( passiveId, data.PassiveId, StringComparison.OrdinalIgnoreCase ) )
				continue;

			passiveOk = true;
			data.PassiveId = passiveId;
			break;
		}

		if ( !passiveOk )
			data.PassiveId = GetFirstPassiveIdForClass( data.ClassId );

		return data;
	}

	/// <summary> Picker class change — auto first ult + passive for that class. </summary>
	public static SavedLoadoutData WithClassAutoFill( string classId )
	{
		var data = CreatePreset();
		data.ClassId = IsValidClassId( classId ) ? classId : ClassSpeedster;
		data.UltId = GetFirstUltIdForClass( data.ClassId );
		data.PassiveId = GetFirstPassiveIdForClass( data.ClassId );
		return data;
	}

	public static IReadOnlyList<string> GetAllClassIds() => AllClassIds;
}

/// <summary> Serializable committed loadout (local save + spawn apply). Properties only — <see cref="FileSystem.Data"/> JSON. </summary>
public sealed class SavedLoadoutData
{
	public string ClassId { get; set; } = LoadoutCatalog.ClassSpeedster;
	public string UltId { get; set; } = LoadoutCatalog.UltSpeedBlitz;
	public string PassiveId { get; set; } = LoadoutCatalog.PassiveDefault;
}
