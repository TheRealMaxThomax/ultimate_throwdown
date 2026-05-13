using Sandbox;

/// <summary> Shared rules for which scene roots get citizen LOD locking (pale low-LOD skin).</summary>
public static class CitizenAvatarLod
{
	/// <summary> Toggled from <see cref="GameNetworkManager"/> so one inspector flag disables every scene-wide pass.</summary>
	public static bool SceneWideLockEnabled { get; set; } = true;

	/// <summary> Matches <see cref="PlayerTackle"/> / map bootstrap.</summary>
	public const string PracticeNpcTag = "practice_npc";

	public static void ApplyToWholeScene( Scene scene )
	{
		if ( !SceneWideLockEnabled )
			return;

		foreach ( var go in scene.GetAllObjects( true ) )
		{
			if ( !go.Enabled || !IsCitizenAvatarRoot( go ) )
				continue;

			ApplyUnderRoot( go );
		}
	}

	public static void ApplyUnderRoot( GameObject root )
	{
		// Citizen clothing uses bonemerge + engine "LOD sync" (children follow the body's LOD). Forcing LodOverride on
		// merged child meshes fights that and can flash the wrong material (reads as pale) when the camera/body turns.
		foreach ( var renderer in root.Components.GetAll<ModelRenderer>( FindMode.EverythingInSelfAndDescendants ) )
		{
			if ( !renderer.IsValid() )
				continue;

			if ( renderer is SkinnedModelRenderer skinned && skinned.BoneMergeTarget.IsValid() )
				continue;

			renderer.LodOverride = 0;
		}
	}

	/// <summary> Root objects we treat as skinned citizens (players + dummies).</summary>
	public static bool IsCitizenAvatarRoot( GameObject go )
	{
		if ( go.Components.Get<PlayerCosmeticsSync>( FindMode.InSelf ).IsValid() )
			return true;

		if ( go.Tags.Has( PracticeNpcTag ) )
			return true;

		if ( !go.Components.Get<PlayerController>( FindMode.InSelf ).IsValid() )
			return false;

		return go.Components.Get<SkinnedModelRenderer>( FindMode.EverythingInSelfAndDescendants ).IsValid();
	}
}

/// <summary>
/// Runs citizen LOD lock at a <b>defined</b> pipeline point. Component <c>OnPreRender</c> order between GameObjects is not guaranteed;
/// a late FinishUpdate-stage listener plus <see cref="GameNetworkManager"/>&apos;s <c>OnPreRender</c> pass (same helper) covers host/client proxy LOD.
/// </summary>
public sealed class CitizenAvatarLodSystem : GameObjectSystem<CitizenAvatarLodSystem>
{
	public CitizenAvatarLodSystem( Scene scene )
		: base( scene )
	{
		Listen( Stage.FinishUpdate, 1000, OnFinishUpdate, nameof( CitizenAvatarLodSystem ) );
	}

	private void OnFinishUpdate()
	{
		CitizenAvatarLod.ApplyToWholeScene( Scene );
	}
}
