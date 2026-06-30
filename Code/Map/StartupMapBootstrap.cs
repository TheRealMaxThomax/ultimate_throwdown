using Sandbox;



/// <summary>

/// Scene startup helpers. Map geometry lives in <c>.scene</c> files (not auto-loaded Hammer maps).

/// </summary>

public sealed class StartupMapBootstrap : GameObjectSystem<StartupMapBootstrap>, ISceneStartup

{

	/// Same tag as <c>PlayerTackle</c> practice dummies — keeps scene NPCs from sliding when players bump their dynamic <see cref="Rigidbody"/>.

	private const string PracticeNpcTag = "practice_npc";



	public StartupMapBootstrap( Scene scene )

		: base( scene )

	{

	}



	void ISceneStartup.OnHostPreInitialize( SceneFile sceneFile )

	{

	}



	void ISceneStartup.OnHostInitialize()

	{

		ApplyPracticeNpcRigidbodyLocks();

	}



	void ISceneStartup.OnClientInitialize()

	{

		ApplyPracticeNpcRigidbodyLocks();

	}



	/// <summary>

	/// Practice NPCs use the same prefab as players (<see cref="Rigidbody"/> + <see cref="PlayerController"/>).

	/// A dynamic body gets pushed by other character controllers and slides; locking translation makes them act like solid obstacles.

	/// Patrol runners move via host teleport in <see cref="PracticeNpcPatrol"/> — lock keeps them from being shoved off lane.

	/// </summary>

	private void ApplyPracticeNpcRigidbodyLocks()

	{

		foreach ( var go in Scene.GetAllObjects( true ) )

		{

			if ( !go.Tags.Has( PracticeNpcTag ) )

				continue;



			var rb = go.Components.Get<Rigidbody>();

			if ( !rb.IsValid() )

				continue;



			var lockTx = rb.Locking;

			lockTx.X = true;

			lockTx.Y = true;

			lockTx.Z = true;

			rb.Locking = lockTx;

		}

	}

}


