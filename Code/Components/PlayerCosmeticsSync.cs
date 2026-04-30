using Sandbox;
using System;
using System.Linq;
using System.Threading;

public sealed class PlayerCosmeticsSync : Component
{
	[Property] public float FirstApplyDelay { get; set; } = 0.25f;
	[Property] public float RetryInterval { get; set; } = 1.0f;
	[Property] public int MaxApplyAttempts { get; set; } = 8;
	[Property] public bool LockHighestLodAfterApply { get; set; } = true;
	[Property] public bool EnableDebugLogs { get; set; } = false;

	private Dresser dresser;
	private SkinnedModelRenderer bodyRenderer;
	private float nextApplyAt;
	private int applyAttempts;
	private bool applying;
	private bool appliedSuccessfully;

	protected override void OnStart()
	{
		dresser = Components.Get<Dresser>( FindMode.EverythingInSelfAndDescendants );
		bodyRenderer = Components.Get<SkinnedModelRenderer>( FindMode.EverythingInSelfAndDescendants );
		nextApplyAt = Time.Now + FirstApplyDelay;
	}

	protected override void OnUpdate()
	{
		if ( appliedSuccessfully || applying )
			return;

		if ( Time.Now < nextApplyAt )
			return;

		if ( applyAttempts >= MaxApplyAttempts )
			return;

		if ( dresser is null )
		{
			dresser = Components.Get<Dresser>( FindMode.EverythingInSelfAndDescendants );
			ScheduleRetry( "Dresser missing on player." );
			return;
		}

		if ( !EnsureBodyTarget() )
		{
			ScheduleRetry( "Body renderer not ready for dresser." );
			return;
		}

		var ownerConnection = ResolveOwnerConnection();
		if ( ownerConnection is null )
		{
			ScheduleRetry( "Owner connection not available yet." );
			return;
		}

		_ = TryApplyCosmeticsAsync( ownerConnection );
	}

	private bool EnsureBodyTarget()
	{
		if ( dresser.BodyTarget.IsValid() )
		{
			bodyRenderer = dresser.BodyTarget;
			return true;
		}

		bodyRenderer = Components.Get<SkinnedModelRenderer>( FindMode.EverythingInSelfAndDescendants );
		if ( !bodyRenderer.IsValid() )
			return false;

		dresser.BodyTarget = bodyRenderer;
		return true;
	}

	private Connection ResolveOwnerConnection()
	{
		if ( Network.OwnerConnection is not null )
			return Network.OwnerConnection;

		var ownerId = Network.OwnerId;
		return Connection.All.FirstOrDefault( connection => connection is not null && connection.Id == ownerId );
	}

	private async System.Threading.Tasks.Task TryApplyCosmeticsAsync( Connection ownerConnection )
	{
		applying = true;
		applyAttempts++;

		try
		{
			// On remote clients, ownership verification can be unavailable for other players.
			// Using removeUnowned=false prevents valid remote cosmetics from being stripped.
			var clothing = ClothingContainer.CreateFromConnection( ownerConnection, false );
			await clothing.ApplyAsync( bodyRenderer, CancellationToken.None );

			if ( LockHighestLodAfterApply )
			{
				ApplyStableLodOverrides();
			}

			appliedSuccessfully = true;
			if ( EnableDebugLogs )
			{
				Log.Info( $"[Cosmetics] Applied for '{GameObject.Name}' owner={ownerConnection.DisplayName} after {applyAttempts} attempt(s)." );
			}
		}
		catch ( Exception ex )
		{
			if ( EnableDebugLogs )
			{
				Log.Warning( $"[Cosmetics] Apply failed for '{GameObject.Name}' on attempt {applyAttempts}: {ex.Message}" );
			}

			nextApplyAt = Time.Now + RetryInterval;
		}
		finally
		{
			applying = false;
		}
	}

	private void ScheduleRetry( string reason )
	{
		applyAttempts++;
		nextApplyAt = Time.Now + RetryInterval;

		if ( EnableDebugLogs )
		{
			Log.Info( $"[Cosmetics] Retry {applyAttempts}/{MaxApplyAttempts} for '{GameObject.Name}': {reason}" );
		}
	}

	private void ApplyStableLodOverrides()
	{
		foreach ( var renderer in Components.GetAll<ModelRenderer>( FindMode.EverythingInSelfAndDescendants ) )
		{
			if ( !renderer.IsValid() )
				continue;

			// Keep player body/clothing visuals stable across distance/camera angle changes.
			renderer.LodOverride = 0;
		}
	}
}
