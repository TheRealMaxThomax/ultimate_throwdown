using System.Collections.Generic;
using Sandbox;

/// <summary>
/// Runtime flat annulus meshes for <see cref="QuakeSlamAimPreview"/> mid/outer bands (XY plane, Z up).
/// </summary>
static class QuakeSlamPreviewAnnulusMesh
{
	const int MinSegments = 12;
	const int MaxSegments = 128;

	struct AnnulusVertex
	{
		[VertexLayout.Position] public Vector3 Position;
		[VertexLayout.Normal] public Vector3 Normal;
		[VertexLayout.Tangent] public Vector4 Tangent;
		[VertexLayout.TexCoord] public Vector2 TexCoord;
	}

	static readonly Dictionary<AnnulusCacheKey, Model> ModelCache = new();

	public static Model GetAnnulusModel( float innerRadius, float outerRadius, int segments )
	{
		innerRadius = MathF.Max( 0.01f, innerRadius );
		outerRadius = MathF.Max( innerRadius + 0.01f, outerRadius );
		segments = segments.Clamp( MinSegments, MaxSegments );

		var key = new AnnulusCacheKey( innerRadius, outerRadius, segments );
		if ( ModelCache.TryGetValue( key, out var cached ) && cached.IsValid() )
			return cached;

		var model = BuildAnnulusModel( innerRadius, outerRadius, segments );
		if ( model.IsValid() )
			ModelCache[key] = model;

		return model;
	}

	static Model BuildAnnulusModel( float innerRadius, float outerRadius, int segments )
	{
		var vertices = new List<AnnulusVertex>( segments * 2 );
		var indices = new List<int>( segments * 6 );
		var normal = Vector3.Up;
		var tangent = new Vector4( 1f, 0f, 0f, 1f );

		for ( var i = 0; i < segments; i++ )
		{
			var angle = i / (float)segments * MathF.PI * 2f;
			var dir = new Vector3( MathF.Cos( angle ), MathF.Sin( angle ), 0f );
			var inner = dir * innerRadius;
			var outer = dir * outerRadius;
			var uv = new Vector2( dir.x * 0.5f + 0.5f, dir.y * 0.5f + 0.5f );

			vertices.Add( new AnnulusVertex { Position = inner, Normal = normal, Tangent = tangent, TexCoord = uv } );
			vertices.Add( new AnnulusVertex { Position = outer, Normal = normal, Tangent = tangent, TexCoord = uv } );
		}

		for ( var i = 0; i < segments; i++ )
		{
			var i0 = i * 2;
			var o0 = i0 + 1;
			var i1 = ( ( i + 1 ) % segments ) * 2;
			var o1 = i1 + 1;

			indices.Add( i0 );
			indices.Add( o0 );
			indices.Add( o1 );

			indices.Add( i0 );
			indices.Add( o1 );
			indices.Add( i1 );
		}

		var mesh = new Mesh();
		mesh.CreateVertexBuffer( vertices.Count, vertices );
		mesh.CreateIndexBuffer( indices.Count, indices );
		mesh.Bounds = BBox.FromPositionAndSize( Vector3.Zero, new Vector3( outerRadius * 2f, outerRadius * 2f, 1f ) );

		return Model.Builder
			.WithName( "quake_slam_preview_annulus" )
			.AddMesh( mesh )
			.Create();
	}

	readonly struct AnnulusCacheKey : System.IEquatable<AnnulusCacheKey>
	{
		readonly int innerMm;
		readonly int outerMm;
		readonly int segments;

		public AnnulusCacheKey( float innerRadius, float outerRadius, int segments )
		{
			innerMm = (int)MathF.Round( innerRadius * 10f );
			outerMm = (int)MathF.Round( outerRadius * 10f );
			this.segments = segments;
		}

		public bool Equals( AnnulusCacheKey other ) => innerMm == other.innerMm && outerMm == other.outerMm && segments == other.segments;
		public override bool Equals( object obj ) => obj is AnnulusCacheKey other && Equals( other );
		public override int GetHashCode() => System.HashCode.Combine( innerMm, outerMm, segments );
	}
}
