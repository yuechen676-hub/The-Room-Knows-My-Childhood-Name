using System.Collections.Generic;
using UnityEngine;

// Movement colliders may enclose empty space. Test visible furniture surfaces
// for interaction, including targets beyond (not only inside) those colliders.
internal sealed class InteractionOcclusion
{
    private sealed class Geometry
    {
        private static readonly Unity.Profiling.ProfilerMarker BuildMarker = new Unity.Profiling.ProfilerMarker("Room.GeometryChunks");
        public Vector3[] vertices;
        public int[] triangles;
        private Bounds[] chunks;
        private const int ChunkIndices = 96;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
        private static readonly bool LegacyBoundsForDiagnostics = RoomPerformanceDiagnostics.Requested &&
            System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-legacy-chunk-bounds") >= 0;
#endif

        private static Bounds BuildBounds(Vector3[] points, int[] indices, int start, int end)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if(LegacyBoundsForDiagnostics)
            {
                var legacy=new Bounds(points[indices[start]],Vector3.zero);
                for(int i=start+1;i<end;i++)legacy.Encapsulate(points[indices[i]]);
                legacy.Expand(.00001f);return legacy;
            }
#endif
            // Preserve Bounds.Encapsulate's per-vertex rounding order. Merely
            // taking min/max once can change the boundary of a hit test.
            var first = points[indices[start]];
            float cx=first.x,cy=first.y,cz=first.z,ex=0,ey=0,ez=0;
            for(int i=start+1;i<end;i++)
            {
                var p=points[indices[i]];
                float lx=cx-ex,ly=cy-ey,lz=cz-ez;
                float hx=cx+ex,hy=cy+ey,hz=cz+ez;
                lx=lx<p.x?lx:p.x;ly=ly<p.y?ly:p.y;lz=lz<p.z?lz:p.z;
                hx=hx>p.x?hx:p.x;hy=hy>p.y?hy:p.y;hz=hz>p.z?hz:p.z;
                ex=(hx-lx)*.5f;ey=(hy-ly)*.5f;ez=(hz-lz)*.5f;
                cx=lx+ex;cy=ly+ey;cz=lz+ez;
            }
            var bounds=new Bounds { center=new Vector3(cx,cy,cz),extents=new Vector3(ex,ey,ez) };
            bounds.Expand(.00001f);
            return bounds;
        }

        public bool Hit(Vector3 origin, Vector3 direction, float limit, out float nearest, bool cullBackfaces = false, float winding = 1f)
        {
            if(chunks==null)
            {
                using var sample = BuildMarker.Auto();
                chunks=new Bounds[(triangles.Length+ChunkIndices-1)/ChunkIndices];
                for(int chunk=0;chunk<chunks.Length;chunk++)
                {
                    int start=chunk*ChunkIndices,end=Mathf.Min(start+ChunkIndices,triangles.Length);
                    chunks[chunk]=BuildBounds(vertices,triangles,start,end);
                }
            }
            nearest=limit;bool found=false;
            float scale=direction.magnitude;
            if(scale<=0)return false;
            var localRay=new Ray(origin,direction/scale);
            for(int chunk=0;chunk<chunks.Length;chunk++)
            {
                if(!chunks[chunk].Contains(origin) && (!chunks[chunk].IntersectRay(localRay,out var entry)||entry>nearest*scale))continue;
                int start=chunk*ChunkIndices,end=Mathf.Min(start+ChunkIndices,triangles.Length);
                for(int i=start;i<end;i+=3)
                {
                    if(cullBackfaces && Vector3.Dot(Vector3.Cross(vertices[triangles[i+1]]-vertices[triangles[i]],vertices[triangles[i+2]]-vertices[triangles[i]]),direction)*winding>=0)continue;
                    if(!TriangleHit(origin,direction,vertices[triangles[i]],vertices[triangles[i+1]],vertices[triangles[i+2]],out var t)||t<=.0001f||t>nearest)continue;
                    nearest=t;found=true;
                }
            }
            return found;
        }
    }

    private readonly Dictionary<Collider, MeshFilter[]> surfaces = new Dictionary<Collider, MeshFilter[]>();
    private readonly Dictionary<Mesh, Geometry> meshes = new Dictionary<Mesh, Geometry>();
    private readonly Dictionary<Transform, MeshFilter[]> targetSurfaces = new Dictionary<Transform, MeshFilter[]>();

    private static Mesh SourceMesh(MeshFilter filter)
    {
        var source=filter.GetComponent<InteractionMeshSource>();
        return source!=null && source.mesh!=null ? source.mesh : filter.sharedMesh;
    }

    private static MeshFilter[] SurfaceFilters(Transform root)
    {
        var result=new List<MeshFilter>();
        foreach(var filter in root.GetComponentsInChildren<MeshFilter>())
        {
            var group=filter.GetComponentInParent<LODGroup>();
            if(group!=null)
            {
                var levels=group.GetLODs();
                if(levels.Length>0 && System.Array.IndexOf(levels[0].renderers,filter.GetComponent<Renderer>())<0)continue;
            }
            result.Add(filter);
        }
        return result.ToArray();
    }

    public bool TrySurfaceHit(Transform root, Ray ray, float distance, out Vector3 point)
    {
        return TrySurfaceHitInternal(root,ray,distance,out point,false);
    }

    private bool TrySurfaceHitInternal(Transform root, Ray ray, float distance, out Vector3 point, bool wallFacesOnly)
    {
        point = default;
        if (!targetSurfaces.TryGetValue(root, out var filters))
        {
            filters = SurfaceFilters(root);
            targetSurfaces.Add(root, filters);
        }
        bool found = false;
        float nearest = distance;
        foreach (var filter in filters)
        {
            if (filter == null || !filter.gameObject.activeInHierarchy || filter.gameObject.layer == 2) continue;
            var renderer = filter.GetComponent<Renderer>();
            var mesh = SourceMesh(filter);
            if (renderer == null || !renderer.enabled || mesh == null
                || !renderer.bounds.IntersectRay(ray, out var entry) || entry > nearest) continue;
            if (!mesh.isReadable && !meshes.ContainsKey(mesh))
            {
                if (InteractionGeometryCatalog.TryRead(mesh, out var vertices, out var triangles))
                    meshes.Add(mesh, new Geometry { vertices = vertices, triangles = triangles });
                else if (WallGeometryCatalog.TryGet(mesh, out var baked))
                    meshes.Add(mesh, new Geometry { vertices = baked.vertices, triangles = baked.triangles });
                else { nearest = Mathf.Max(0, entry); found = true; continue; }
            }
            if (!meshes.TryGetValue(mesh, out var geometry))
            {
                bool trianglesOnly = true;
                for (int i = 0; i < mesh.subMeshCount; i++) trianglesOnly &= mesh.GetTopology(i) == MeshTopology.Triangles;
                if (!trianglesOnly) continue;
                geometry = new Geometry { vertices = mesh.vertices, triangles = mesh.triangles };
                meshes.Add(mesh, geometry);
            }
            var inverse = filter.transform.worldToLocalMatrix;
            var origin = inverse.MultiplyPoint3x4(ray.origin);
            var direction = inverse.MultiplyVector(ray.direction);
            bool cullBackfaces=wallFacesOnly;
            if(cullBackfaces)
                foreach(var material in renderer.sharedMaterials)
                    if(material!=null && material.HasProperty("_Cull") && material.GetFloat("_Cull")!=2f)cullBackfaces=false;
            float winding=filter.transform.localToWorldMatrix.determinant<0?-1f:1f;
            if(geometry.Hit(origin,direction,nearest,out var surfaceDistance,cullBackfaces,winding))
            { nearest=surfaceDistance;found=true; }
        }
        if (found) point = ray.GetPoint(nearest);
        return found;
    }

    public bool Blocks(Collider collider, Ray ray, float distance, Vector3 target, Transform surfaceRoot = null, Transform ignoredSurface = null)
    {
        var wall = collider.GetComponent<InteractionWallSurface>();
        if (wall != null && wall.surfaceRoot != null)
            return TrySurfaceHitInternal(wall.surfaceRoot, ray, distance - .001f, out _, true);
        if (!(collider is BoxCollider)) return true;
        if (!surfaces.TryGetValue(collider, out var filters))
        {
            var root = surfaceRoot != null ? surfaceRoot : collider.transform;
            filters = SurfaceFilters(root);
            // Authored compound boxes can live in a collider-only child group.
            // Resolve their owning prop instead of treating the group as solid.
            while (filters.Length == 0 && root.parent != null && surfaceRoot == null)
            {
                root = root.parent;
                filters = SurfaceFilters(root);
            }
            surfaces.Add(collider, filters);
        }
        bool checkedSurface = false;
        foreach (var filter in filters)
        {
            if (filter == null || !filter.gameObject.activeInHierarchy || filter.gameObject.layer == 2) continue;
            if (ignoredSurface != null && filter.transform.IsChildOf(ignoredSurface)) continue;
            var renderer = filter.GetComponent<Renderer>();
            if (renderer == null || !renderer.enabled || filter.sharedMesh == null) continue;
            checkedSurface = true;
            if (!renderer.bounds.IntersectRay(ray, out var entry) || entry > distance) continue;
            var mesh = SourceMesh(filter);
            // An unknown surface remains an occluder, rather than allowing the
            // player to interact through walls whose mesh cannot be inspected.
            if (!mesh.isReadable && !meshes.ContainsKey(mesh))
            {
                if (InteractionGeometryCatalog.TryRead(mesh, out var bakedVertices, out var bakedTriangles))
                    meshes.Add(mesh, new Geometry { vertices = bakedVertices, triangles = bakedTriangles });
                else return true;
            }
            if (!meshes.TryGetValue(mesh, out var geometry))
            {
                for (int i = 0; i < mesh.subMeshCount; i++)
                    if (mesh.GetTopology(i) != MeshTopology.Triangles) return true;
                geometry = new Geometry { vertices = mesh.vertices, triangles = mesh.triangles };
                meshes.Add(mesh, geometry);
            }
            var inverse = filter.transform.worldToLocalMatrix;
            var origin = inverse.MultiplyPoint3x4(ray.origin);
            // Do not normalize: the parameter t must remain a world distance
            // even for models imported with a scale of 100 or a mirrored axis.
            var direction = inverse.MultiplyVector(ray.direction);
            if(geometry.Hit(origin,direction,distance-.001f,out _))return true;
        }
        return !checkedSurface;
    }

    private static bool TriangleHit(Vector3 origin, Vector3 direction, Vector3 a, Vector3 b, Vector3 c, out float t)
    {
        t = 0;
        var edge1 = b - a;
        var edge2 = c - a;
        var p = Vector3.Cross(direction, edge2);
        float determinant = Vector3.Dot(edge1, p);
        // Imported meshes can have tiny local triangles and a transform scale of
        // 100. A fixed determinant cutoff rejects their visible surfaces. Compare
        // the angle instead, retaining the unnormalized ray for world-distance t.
        double determinantSquared = (double)determinant * determinant;
        double parallelLimit = 1e-14 * (double)edge1.sqrMagnitude * p.sqrMagnitude;
        if (determinantSquared <= parallelLimit) return false;
        float inverse = 1f / determinant;
        var relative = origin - a;
        float u = Vector3.Dot(relative, p) * inverse;
        if (u < 0 || u > 1) return false;
        var q = Vector3.Cross(relative, edge1);
        float v = Vector3.Dot(direction, q) * inverse;
        if (v < 0 || u + v > 1) return false;
        t = Vector3.Dot(edge2, q) * inverse;
        return true;
    }
}
