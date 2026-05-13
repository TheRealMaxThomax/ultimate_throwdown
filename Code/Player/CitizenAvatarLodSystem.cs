using Sandbox;

/// <summary> Shared rules for which scene roots get citizen LOD locking (pale low-LOD skin).</summary>
public static class CitizenAvatarLod
{
	/// <summary> Toggled from <see cref="GameNetworkManager"/> so one inspector flag disables every scene-wide pass.</summary>
	public static bool SceneWideLockEnabled { get; set; } = true;

	/// <summary>
	/// When <c>false</c>, skips <see cref="SkinnedModelRenderer"/> pieces that use <see cref="SkinnedModelRenderer.BoneMergeTarget"/> — old workaround that avoided material flashes on clothing.
	/// When <c>true</c> (default), those meshes get <see cref="ModelRenderer.LodOverride"/> too; citizen body extras (e.g. arms) often merge to the torso and stayed on low LOD when skipped ("pale" skin).
	/// </summary>
	public static bool ApplyLodLockToBoneMergedSkinnedMeshes { get; set; } = true;

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
		foreach ( var renderer in root.Components.GetAll<ModelRenderer>( FindMode.EverythingInSelfAndDescendants ) )
		{
			if ( !renderer.IsValid() )
				continue;

			var skipMerged = !ApplyLodLockToBoneMergedSkinnedMeshes
			                   && renderer is SkinnedModelRenderer skinned
			                   && skinned.BoneMergeTarget.IsValid();
			if ( skipMerged )
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
/// registering the same scene pass during the Interpolation stage (after bone work) can help joining clients keep citizen LOD0 consistent.
/// </summary>
public sealed class CitizenAvatarLodSystem : GameObjectSystem<CitizenAvatarLodSystem>
{
	public CitizenAvatarLodSystem( Scene scene )
		: base( scene )
	{
		Listen( Stage.Interpolation, 10_000, OnInterpolation, nameof( CitizenAvatarLodSystem ) + ".Interpolation" );
		Listen( Stage.FinishUpdate, 1000, OnFinishUpdate, nameof( CitizenAvatarLodSystem ) );
	}

	private void OnInterpolation()
	{
		CitizenAvatarLod.ApplyToWholeScene( Scene );
	}

	private void OnFinishUpdate()
	{
		CitizenAvatarLod.ApplyToWholeScene( Scene );
	}
}
