using Sandbox;

/// <summary>
/// Preloads Speed Blitz VFX assets so join-via-new-instance clients mount spark sprites/textures
/// (runtime prefab clones do not always pull kenney dependencies on the joining machine).
/// </summary>
internal static class SpeedBlitzVfxResources
{
	internal const string WindUpSparkSpritePath = "vfx/spark_01.sprite";
	internal const string WindUpSparkTexturePath = "vfx/spark_01.png";

	private static Sprite windUpSparkSprite;
	private static Texture windUpSparkTexture;
	private static bool preloadAttempted;

	internal static void EnsureLoaded()
	{
		if ( preloadAttempted )
			return;

		preloadAttempted = true;

		ResourceLibrary.Get<PrefabFile>( SpeedsterSpeedBlitzUlt.DefaultWindUpVfxPrefabPath );
		ResourceLibrary.Get<PrefabFile>( SpeedsterSpeedBlitzUlt.DefaultDischargeVfxPrefabPath );

		ResourceLibrary.Get<Texture>( WindUpSparkTexturePath );
		ResourceLibrary.Get<Texture>( "vfx/spark_02.png" );
		ResourceLibrary.Get<Texture>( "vfx/spark_03.png" );
		ResourceLibrary.Get<Texture>( "vfx/spark_04.png" );

		windUpSparkSprite = ResourceLibrary.Get<Sprite>( WindUpSparkSpritePath );
		windUpSparkTexture = ResourceLibrary.Get<Texture>( WindUpSparkTexturePath );

		if ( !windUpSparkSprite.IsValid() )
			Log.Warning( $"[SpeedBlitz] Wind-up spark sprite missing at '{WindUpSparkSpritePath}' — open the sprite in the editor and Save/Compile." );
	}

	internal static void ApplySparkSpriteToInstance( GameObject instance )
	{
		if ( !instance.IsValid() )
			return;

		EnsureLoaded();

		foreach ( var renderer in instance.GetComponentsInChildren<ParticleSpriteRenderer>( true ) )
		{
			if ( !renderer.IsValid() )
				continue;

			if ( windUpSparkSprite.IsValid() )
				renderer.Sprite = windUpSparkSprite;
		}
	}
}
