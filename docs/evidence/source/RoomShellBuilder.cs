using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Builds the project's own room shell (modular walls, windows, doorway, floor,
// ceiling, curtain) and small set dressing in code, to the measurements of the
// pieces they replace, and swaps them into the scene in place. Structural
// surfaces get world-space UVs so neighbouring modules share one continuous
// texture. Batch: -executeMethod RoomShellBuilder.Replace
public static class RoomShellBuilder
{
    const string ScenePath = "Assets/Scenes/TheRoomKnowsMyChildhoodName.unity";
    const string Root = "Assets/RoomShell", MeshFolder = Root + "/Meshes", MaterialFolder = Root + "/Materials", TextureFolder = Root + "/Textures";
    const string ReportPath = "PerformanceReview/AssetBudget/shell-replace.txt";
    static readonly string[] Vendor = { "Assets/LeartesStudios/", "Assets/AK Studio Art/" };

    static bool IsVendor(UnityEngine.Object asset) { string p = asset ? AssetDatabase.GetAssetPath(asset) : ""; return Vendor.Any(p.StartsWith); }
    static string PathOf(Transform t) => t.parent ? PathOf(t.parent) + "/" + t.name : t.name;
    static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

    // ───────────────────────── geometry
    sealed class Shape
    {
        public readonly List<Vector3> Positions = new List<Vector3>(), Normals = new List<Vector3>();
        public readonly List<Vector2> Uvs = new List<Vector2>();
        public readonly List<bool> HasUv = new List<bool>();
        public readonly List<string> Slots = new List<string>();
        readonly Dictionary<string, List<int>> triangles = new Dictionary<string, List<int>>();
        // When set, new vertices take this UV instead of the world-space projection.
        public Func<Vector3, Vector3, Vector2> Uv;

        List<int> Slot(string slot)
        {
            if (!triangles.TryGetValue(slot, out var list)) { triangles[slot] = list = new List<int>(); Slots.Add(slot); }
            return list;
        }
        int Add(Vector3 position, Vector3 normal)
        {
            Positions.Add(position); Normals.Add(normal);
            Uvs.Add(Uv != null ? Uv(position, normal) : Vector2.zero); HasUv.Add(Uv != null);
            return Positions.Count - 1;
        }
        // Flat convex polygon; winding is chosen so the face points along the normal.
        public void Face(string slot, Vector3 normal, params Vector3[] polygon)
        {
            if (Vector3.Dot(Vector3.Cross(polygon[1] - polygon[0], polygon[2] - polygon[0]), normal) < 0) Array.Reverse(polygon);
            int first = Positions.Count;
            foreach (var p in polygon) Add(p, normal.normalized);
            var list = Slot(slot);
            for (int i = 1; i + 1 < polygon.Length; i++) { list.Add(first); list.Add(first + i); list.Add(first + i + 1); }
        }
        public void Box(string slot, Vector3 min, Vector3 max)
        {
            Vector3 a = min, b = max;
            Face(slot, Vector3.right, V(b.x, a.y, a.z), V(b.x, b.y, a.z), V(b.x, b.y, b.z), V(b.x, a.y, b.z));
            Face(slot, Vector3.left, V(a.x, a.y, a.z), V(a.x, b.y, a.z), V(a.x, b.y, b.z), V(a.x, a.y, b.z));
            Face(slot, Vector3.up, V(a.x, b.y, a.z), V(b.x, b.y, a.z), V(b.x, b.y, b.z), V(a.x, b.y, b.z));
            Face(slot, Vector3.down, V(a.x, a.y, a.z), V(b.x, a.y, a.z), V(b.x, a.y, b.z), V(a.x, a.y, b.z));
            Face(slot, Vector3.forward, V(a.x, a.y, b.z), V(b.x, a.y, b.z), V(b.x, b.y, b.z), V(a.x, b.y, b.z));
            Face(slot, Vector3.back, V(a.x, a.y, a.z), V(b.x, a.y, a.z), V(b.x, b.y, a.z), V(a.x, b.y, a.z));
        }
        // Profile in the frame's XY plane, swept along the frame's Z from z0 to z1.
        public void Extrude(string slot, Vector2[] profile, float z0, float z1, Matrix4x4 frame)
        {
            float area = 0;
            for (int i = 0; i < profile.Length; i++) { var p = profile[i]; var q = profile[(i + 1) % profile.Length]; area += p.x * q.y - q.x * p.y; }
            if (area < 0) profile = profile.Reverse().ToArray();
            Vector3 P(Vector2 p, float z) => frame.MultiplyPoint3x4(V(p.x, p.y, z));
            for (int i = 0; i < profile.Length; i++)
            {
                var p = profile[i]; var q = profile[(i + 1) % profile.Length];
                var outward = frame.MultiplyVector(V(q.y - p.y, p.x - q.x, 0));
                Face(slot, outward, P(p, z0), P(q, z0), P(q, z1), P(p, z1));
            }
            foreach (var t in Triangulate(profile))
            {
                Face(slot, frame.MultiplyVector(Vector3.back), P(profile[t.x], z0), P(profile[t.y], z0), P(profile[t.z], z0));
                Face(slot, frame.MultiplyVector(Vector3.forward), P(profile[t.x], z1), P(profile[t.y], z1), P(profile[t.z], z1));
            }
        }
        // Ear clipping for a counter-clockwise simple polygon.
        static List<Vector3Int> Triangulate(Vector2[] polygon)
        {
            var result = new List<Vector3Int>();
            var remaining = Enumerable.Range(0, polygon.Length).ToList();
            float Cross(Vector2 a, Vector2 b, Vector2 c) => (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
            int guard = 0;
            while (remaining.Count > 3 && guard++ < 1000)
                for (int i = 0; i < remaining.Count; i++)
                {
                    int ia = remaining[(i + remaining.Count - 1) % remaining.Count], ib = remaining[i], ic = remaining[(i + 1) % remaining.Count];
                    Vector2 a = polygon[ia], b = polygon[ib], c = polygon[ic];
                    if (Cross(a, b, c) <= 1e-9f) continue;
                    if (remaining.Any(k => k != ia && k != ib && k != ic && Cross(a, b, polygon[k]) > 0 && Cross(b, c, polygon[k]) > 0 && Cross(c, a, polygon[k]) > 0)) continue;
                    result.Add(new Vector3Int(ia, ib, ic)); remaining.RemoveAt(i); break;
                }
            if (remaining.Count == 3) result.Add(new Vector3Int(remaining[0], remaining[1], remaining[2]));
            return result;
        }
        // Smooth-shaded grid. A null normal function shades from the surface itself.
        public void Grid(string slot, int columns, int rows, Func<float, float, Vector3> point, Func<float, float, Vector3> normal, bool doubleSided)
        {
            for (int side = 0; side < (doubleSided ? 2 : 1); side++)
            {
                int first = Positions.Count;
                for (int r = 0; r <= rows; r++)
                    for (int c = 0; c <= columns; c++)
                    {
                        float u = c / (float)columns, v = r / (float)rows;
                        var p = point(u, v);
                        Vector3 n = normal != null ? normal(u, v)
                            : Vector3.Cross(point(u, Mathf.Min(1, v + .01f)) - point(u, Mathf.Max(0, v - .01f)), point(Mathf.Min(1, u + .002f), v) - point(Mathf.Max(0, u - .002f), v));
                        Add(p, (side == 0 ? n : -n).normalized);
                    }
                var list = Slot(slot);
                for (int r = 0; r < rows; r++)
                    for (int c = 0; c < columns; c++)
                    {
                        int a = first + r * (columns + 1) + c, b = a + 1, d = a + columns + 1, e = d + 1;
                        bool flip = Vector3.Dot(Vector3.Cross(Positions[b] - Positions[a], Positions[d] - Positions[a]), Normals[a]) < 0;
                        list.AddRange(flip ? new[] { a, d, b, b, d, e } : new[] { a, b, d, b, e, d });
                    }
            }
        }
        // Round tube along a polyline, capped unless the path is closed.
        public void Tube(string slot, IList<Vector3> path, float radius, int sides, bool closed)
        {
            int count = path.Count;
            Vector3 Tangent(int i) => closed ? (path[(i + 1) % count] - path[(i + count - 1) % count]).normalized
                : (path[Mathf.Min(count - 1, i + 1)] - path[Mathf.Max(0, i - 1)]).normalized;
            var up = Mathf.Abs(Vector3.Dot(Tangent(0), Vector3.up)) > .9f ? Vector3.right : Vector3.up;
            var frames = new Vector3[count];
            frames[0] = Vector3.Cross(Tangent(0), up).normalized;
            for (int i = 1; i < count; i++) frames[i] = (frames[i - 1] - Tangent(i) * Vector3.Dot(frames[i - 1], Tangent(i))).normalized;
            Vector3 Ring(int i, float a, out Vector3 n)
            {
                int k = i % count;
                var side = frames[k]; var other = Vector3.Cross(Tangent(k), side);
                n = side * Mathf.Cos(a) + other * Mathf.Sin(a);
                return path[k] + n * radius;
            }
            int steps = closed ? count : count - 1;
            Grid(slot, sides, steps, (u, v) => Ring(Mathf.RoundToInt(v * steps), u * Mathf.PI * 2, out _), (u, v) => { Ring(Mathf.RoundToInt(v * steps), u * Mathf.PI * 2, out var n); return n; }, false);
            if (closed) return;
            foreach (int end in new[] { 0, count - 1 })
            {
                var cap = Enumerable.Range(0, sides).Select(s => Ring(end, s / (float)sides * Mathf.PI * 2, out _)).ToArray();
                Face(slot, end == 0 ? -Tangent(0) : Tangent(count - 1), cap);
            }
        }
        public Mesh ToMesh(string name, Matrix4x4 localToWorld)
        {
            var normalMatrix = localToWorld.inverse.transpose;
            var uv = new Vector2[Positions.Count];
            for (int i = 0; i < uv.Length; i++)
            {
                if (HasUv[i]) { uv[i] = Uvs[i]; continue; }
                // Metres in world space, projected along the face's dominant axis.
                var p = localToWorld.MultiplyPoint3x4(Positions[i]);
                var n = normalMatrix.MultiplyVector(Normals[i]);
                float x = Mathf.Abs(n.x), y = Mathf.Abs(n.y), z = Mathf.Abs(n.z);
                uv[i] = y >= x && y >= z ? new Vector2(p.x, p.z) : x >= z ? new Vector2(p.z, p.y) : new Vector2(p.x, p.y);
            }
            var mesh = new Mesh { name = name, indexFormat = Positions.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(Positions); mesh.SetNormals(Normals); mesh.SetUVs(0, uv);
            mesh.subMeshCount = Slots.Count;
            for (int i = 0; i < Slots.Count; i++) mesh.SetTriangles(triangles[Slots[i]], i);
            mesh.RecalculateBounds(); mesh.RecalculateTangents();
            return mesh;
        }
    }

    // ───────────────────────── shell modules (local space of the piece they replace)
    const string Plaster = "Plaster_Wall", Trim = "Trim_Paint", Glass = "Window_Glass", Carpet = "Carpet_Grey", Ceiling = "Plaster_Ceiling", Metal = "Rail_Metal", Paper = "Paper_Plain";
    static readonly Vector2[] BaseboardProfile = { new Vector2(0, 0), new Vector2(.03f, 0), new Vector2(.03f, .11f), new Vector2(.012f, .14f), new Vector2(0, .14f) };
    static readonly Vector2[] CrownProfile = { new Vector2(0, 2.86f), new Vector2(.018f, 2.87f), new Vector2(.04f, 2.95f), new Vector2(.04f, 3), new Vector2(0, 3) };
    // Frames: profile X is "out from the wall face", Z is "along the wall".
    static readonly Matrix4x4 AlongZ = Matrix4x4.identity;
    static Matrix4x4 Frame(Vector3 outward, Vector3 along, Vector3 origin)
    {
        var m = Matrix4x4.identity;
        m.SetColumn(0, outward); m.SetColumn(1, Vector3.up); m.SetColumn(2, along); m.SetColumn(3, new Vector4(origin.x, origin.y, origin.z, 1));
        return m;
    }
    static void Skirting(Shape s, float z0, float z1, bool crown = true)
    {
        s.Extrude(Trim, BaseboardProfile, z0, z1, AlongZ);
        if (crown) s.Extrude(Trim, CrownProfile, z0, z1, AlongZ);
    }

    static Shape Wall(float length)
    {
        var s = new Shape();
        s.Box(Plaster, V(-.25f, 0, 0), V(0, 3, length));
        Skirting(s, 0, length);
        return s;
    }
    static Shape Corner()
    {
        var s = new Shape();
        s.Box(Plaster, V(0, 0, -.25f), V(1, 3, 0));
        s.Box(Plaster, V(0, 0, -1), V(.25f, 3, -.25f));
        var front = Frame(Vector3.forward, Vector3.right, Vector3.zero);     // face z = 0, looking +z
        var side = Frame(Vector3.left, Vector3.forward, Vector3.zero);       // face x = 0, looking -x
        s.Extrude(Trim, BaseboardProfile, -.03f, 1, front); s.Extrude(Trim, CrownProfile, -.04f, 1, front);
        s.Extrude(Trim, BaseboardProfile, -1, .03f, side); s.Extrude(Trim, CrownProfile, -1, .04f, side);
        return s;
    }
    static Shape Sloped()
    {
        var s = new Shape();
        // Knee wall up to 1.98 m, then the roof slope leaning into the room.
        s.Extrude(Plaster, new[] { new Vector2(-.02f, 0), new Vector2(0, 0), new Vector2(0, 1.98f), new Vector2(.87f, 2.88f), new Vector2(.87f, 3), new Vector2(-.02f, 3) }, 0, 2, AlongZ);
        s.Extrude(Trim, BaseboardProfile, 0, 2, AlongZ);
        s.Box(Trim, V(.87f, 2.9f, 0), V(.91f, 3, 2));
        return s;
    }
    static Shape Window()
    {
        var s = new Shape();
        // Opening: y 0.75–2.75, z 0–1.5. Two mirrored pieces make one three-metre window.
        s.Box(Plaster, V(-.36f, 0, 1.5f), V(0, 3, 2));
        s.Box(Plaster, V(-.36f, 0, 0), V(0, .75f, 1.5f));
        s.Box(Plaster, V(-.36f, 2.75f, 0), V(0, 3, 1.5f));
        Skirting(s, 0, 2);
        // Frame and glazing bars.
        s.Box(Trim, V(-.30f, .75f, 0), V(-.14f, .84f, 1.5f));
        s.Box(Trim, V(-.30f, 2.66f, 0), V(-.14f, 2.75f, 1.5f));
        s.Box(Trim, V(-.30f, .84f, 1.41f), V(-.14f, 2.66f, 1.5f));
        s.Box(Trim, V(-.30f, .84f, 0), V(-.14f, 2.66f, .04f));
        s.Box(Trim, V(-.27f, .84f, .725f), V(-.17f, 2.66f, .775f));
        s.Box(Trim, V(-.27f, 1.40f, .04f), V(-.17f, 1.45f, 1.41f));
        s.Box(Glass, V(-.225f, .84f, .04f), V(-.215f, 2.66f, 1.41f));
        // Interior casing, sill and apron.
        s.Box(Trim, V(0, .68f, 1.5f), V(.02f, 2.83f, 1.58f));
        s.Box(Trim, V(0, 2.75f, 0), V(.02f, 2.83f, 1.5f));
        s.Box(Trim, V(0, .70f, 0), V(.05f, .75f, 1.6f));
        s.Box(Trim, V(0, .62f, 0), V(.015f, .70f, 1.56f));
        return s;
    }
    static Shape Doorway()
    {
        var s = new Shape();
        // Opening: z 0.47–1.52 up to 2.29 m.
        s.Box(Plaster, V(-.25f, 0, 0), V(0, 3, .47f));
        s.Box(Plaster, V(-.25f, 0, 1.52f), V(0, 3, 2));
        s.Box(Plaster, V(-.25f, 2.29f, .47f), V(0, 3, 1.52f));
        s.Extrude(Trim, BaseboardProfile, 0, .38f, AlongZ); s.Extrude(Trim, BaseboardProfile, 1.62f, 2, AlongZ);
        s.Extrude(Trim, CrownProfile, 0, 2, AlongZ);
        foreach (var x in new[] { new Vector2(0, .03f), new Vector2(-.28f, -.25f) })
        {
            s.Box(Trim, V(x.x, 0, .38f), V(x.y, 2.39f, .47f));
            s.Box(Trim, V(x.x, 0, 1.52f), V(x.y, 2.39f, 1.62f));
            s.Box(Trim, V(x.x, 2.29f, .38f), V(x.y, 2.39f, 1.62f));
        }
        return s;
    }
    static Shape Slab(string slot, Vector3 min, Vector3 max) { var s = new Shape(); s.Box(slot, min, max); return s; }
    // Curtains drawn open: one gathered panel at each side of the window.
    static Shape Curtain(Source source)
    {
        // Cloth pattern scale (texture repeats per metre) measured from the old drape.
        double uvArea = 0, area = 0;
        if (source != null)
            for (int i = 0; i + 2 < source.Triangles.Length; i += 3)
            {
                int a = source.Triangles[i], b = source.Triangles[i + 1], c = source.Triangles[i + 2];
                area += Vector3.Cross(source.Positions[b] - source.Positions[a], source.Positions[c] - source.Positions[a]).magnitude;
                Vector2 e = source.Uvs[b] - source.Uvs[a], f = source.Uvs[c] - source.Uvs[a];
                uvArea += Mathf.Abs(e.x * f.y - e.y * f.x);
            }
        float density = area > 0 && uvArea > 0 ? (float)Math.Sqrt(uvArea / area) : 1;
        const int pleats = 5; const float gather = 1.5f;   // cloth length per metre of rail
        var s = new Shape();
        foreach (var span in new[] { new Vector2(-1.51f, -1.0f), new Vector2(.86f, 1.554f) })
        {
            s.Uv = (p, n) => new Vector2((p.z - span.x) * gather, p.y) * density;
            s.Grid("@0", 60, 10, (u, v) =>
            {
                float fall = 1 - v, phase = u * Mathf.PI * 2 * pleats;
                float depth = Mathf.Lerp(.05f, .036f, fall) * Mathf.Sin(phase) + .01f * fall * Mathf.Sin(phase * .37f + span.x);
                return V(.04f + depth, Mathf.Lerp(.273f, 2.772f, v), Mathf.Lerp(span.x, span.y, u));
            }, null, true);
        }
        return s;
    }
    // Rebuilds only the curtain, keeping the mesh asset every reference points at.
    public static void RebuildCurtain()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath);
        var filter = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MeshFilter>(true))
            .First(f => f.sharedMesh && f.sharedMesh.name.StartsWith("Room_Curtain_0"));
        var old = AssetDatabase.FindAssets("SM_Curtains t:Mesh").Select(AssetDatabase.GUIDToAssetPath).Where(p => Vendor.Any(p.StartsWith))
            .SelectMany(AssetDatabase.LoadAllAssetsAtPath).OfType<Mesh>().FirstOrDefault(m => m.name == "SM_Curtains_LOD0");
        var built = Curtain(old ? new Source(old) : null).ToMesh(filter.sharedMesh.name, filter.transform.localToWorldMatrix);
        EditorUtility.CopySerialized(built, filter.sharedMesh);
        EditorUtility.SetDirty(filter.sharedMesh); AssetDatabase.SaveAssets();
        EditorApplication.ExecuteMenuItem("Tools/Room/Rebuild interaction surfaces");
        File.AppendAllText(ReportPath, "\ncurtain rebuilt as two open panels; pattern density " + (old ? "measured" : "default") + "; " + built.triangles.Length / 3 + " tris\n");
    }
    static Shape Rail()
    {
        var s = new Shape();
        s.Tube(Metal, new[] { V(.04f, 2.75f, -1.6f), V(.04f, 2.75f, 1.6f) }, .012f, 10, false);
        foreach (float z in new[] { -1.625f, 1.625f }) s.Tube(Metal, new[] { V(.04f, 2.75f, z - .025f), V(.04f, 2.75f, z + .025f) }, .022f, 10, false);
        foreach (float z in new[] { -1.2f, 0, 1.2f }) s.Box(Metal, V(-.057f, 2.742f, z - .01f), V(.04f, 2.758f, z + .01f));
        return s;
    }

    // ───────────────────────── set dressing, sized from the replaced object's bounds
    sealed class Source
    {
        public Vector3[] Positions, Normals; public Vector2[] Uvs; public int[] Triangles; public Bounds Bounds;
        public List<int[]> Islands = new List<int[]>();
        public Source(Mesh mesh)
        {
            Positions = mesh.vertices; Normals = mesh.normals; Uvs = mesh.uv; Triangles = mesh.triangles; Bounds = mesh.bounds;
            if (Uvs.Length != Positions.Length) Uvs = new Vector2[Positions.Length];
            float step = Mathf.Max(Bounds.size.magnitude * 1e-5f, 1e-7f);
            var welded = new Dictionary<Vector3Int, int>();
            var parent = new int[Positions.Length];
            for (int i = 0; i < parent.Length; i++)
            {
                var key = Vector3Int.RoundToInt(Positions[i] / step);
                if (!welded.TryGetValue(key, out int first)) welded[key] = first = i;
                parent[i] = first;
            }
            int Find(int i) { while (parent[i] != i) i = parent[i] = parent[parent[i]]; return i; }
            var indices = mesh.triangles;
            for (int i = 0; i + 2 < indices.Length; i += 3) { int a = Find(indices[i]); parent[Find(indices[i + 1])] = a; parent[Find(indices[i + 2])] = a; }
            Islands = Enumerable.Range(0, parent.Length).GroupBy(Find).Select(g => g.ToArray()).ToList();
        }
        public Bounds BoundsOf(int[] island) { var b = new Bounds(Positions[island[0]], Vector3.zero); foreach (int i in island) b.Encapsulate(Positions[i]); return b; }
    }
    static int Thin(Vector3 size) => size.x <= size.y && size.x <= size.z ? 0 : size.y <= size.z ? 1 : 2;
    static int Long(Vector3 size) => size.x >= size.y && size.x >= size.z ? 0 : size.y >= size.z ? 1 : 2;
    static Vector3 Axis(int i) => i == 0 ? Vector3.right : i == 1 ? Vector3.up : Vector3.forward;

    // Least-squares map from two position axes to the texture coordinates the
    // replaced object used, so the project's own image lands where it did.
    static Func<Vector3, Vector3, Vector2> FitUv(Source source, IEnumerable<int> vertices, int a, int b)
    {
        double[,] m = new double[3, 3]; double[] ru = new double[3], rv = new double[3]; int n = 0;
        foreach (int i in vertices)
        {
            double[] row = { source.Positions[i][a], source.Positions[i][b], 1 };
            for (int r = 0; r < 3; r++) { for (int c = 0; c < 3; c++) m[r, c] += row[r] * row[c]; ru[r] += row[r] * source.Uvs[i].x; rv[r] += row[r] * source.Uvs[i].y; }
            n++;
        }
        double Det(double[,] k) => k[0, 0] * (k[1, 1] * k[2, 2] - k[1, 2] * k[2, 1]) - k[0, 1] * (k[1, 0] * k[2, 2] - k[1, 2] * k[2, 0]) + k[0, 2] * (k[1, 0] * k[2, 1] - k[1, 1] * k[2, 0]);
        double det = Det(m);
        if (n < 3 || Math.Abs(det) < 1e-12)
        {
            var box = source.Bounds;
            return (p, _) => new Vector2((p[a] - box.min[a]) / Mathf.Max(1e-5f, box.size[a]), (p[b] - box.min[b]) / Mathf.Max(1e-5f, box.size[b]));
        }
        double[] Solve(double[] rhs)
        {
            var x = new double[3];
            for (int c = 0; c < 3; c++) { var k = (double[,])m.Clone(); for (int r = 0; r < 3; r++) k[r, c] = rhs[r]; x[c] = Det(k) / det; }
            return x;
        }
        double[] cu = Solve(ru), cv = Solve(rv);
        return (p, _) => new Vector2((float)(cu[0] * p[a] + cu[1] * p[b] + cu[2]), (float)(cv[0] * p[a] + cv[1] * p[b] + cv[2]));
    }

    // Each separate sheet of the original becomes one flat double-sided card.
    static Shape Cards(Source source)
    {
        var s = new Shape();
        foreach (var island in source.Islands)
        {
            var box = source.BoundsOf(island);
            int t = Thin(box.size), a = (t + 1) % 3, b = (t + 2) % 3;
            s.Uv = FitUv(source, island, a, b);
            Vector3 P(float u, float v) { var p = box.center; p[a] = box.min[a] + box.size[a] * u; p[b] = box.min[b] + box.size[b] * v; return p; }
            s.Face("@0", Axis(t), P(0, 0), P(1, 0), P(1, 1), P(0, 1));
            s.Face("@0", -Axis(t), P(0, 0), P(1, 0), P(1, 1), P(0, 1));
        }
        return s;
    }
    static Shape Leaf(Source source)
    {
        var s = new Shape(); var box = source.Bounds;
        int t = Thin(box.size), a = Long(box.size), b = 3 - t - a;
        var outline = Enumerable.Range(0, 10).Select(i =>
        {
            float angle = i / 10f * Mathf.PI * 2, u = .5f + .5f * Mathf.Cos(angle), v = .5f + .5f * Mathf.Sin(angle) * Mathf.Pow(Mathf.Sin(u * Mathf.PI), .6f);
            var p = box.center; p[a] = box.min[a] + box.size[a] * u; p[b] = box.min[b] + box.size[b] * v; return p;
        }).ToArray();
        s.Face("Leaf_Green", Axis(t), (Vector3[])outline.Clone());
        s.Face("Leaf_Green", -Axis(t), (Vector3[])outline.Clone());
        return s;
    }
    static Shape Book(Source source)
    {
        var s = new Shape(); var box = source.Bounds;
        int t = Thin(box.size), a = Long(box.size), b = 3 - t - a;
        float board = box.size[t] * .07f, inset = box.size[b] * .03f;
        Vector3 Min(float tt, float aa, float bb) { var p = Vector3.zero; p[t] = tt; p[a] = aa; p[b] = bb; return p; }
        // Covers and spine carry the cover image; the page block is plain paper.
        s.Uv = FitUv(source, Enumerable.Range(0, source.Positions.Length).Where(i => Mathf.Abs(source.Normals[i][t]) > .8f), a, b);
        s.Box("@0", Min(box.min[t], box.min[a], box.min[b]), Min(box.min[t] + board, box.max[a], box.max[b]));
        s.Box("@0", Min(box.max[t] - board, box.min[a], box.min[b]), Min(box.max[t], box.max[a], box.max[b]));
        s.Box("@0", Min(box.min[t], box.min[a], box.min[b]), Min(box.max[t], box.max[a], box.min[b] + board));
        s.Uv = null;
        s.Box(Paper, Min(box.min[t] + board, box.min[a] + inset, box.min[b] + board), Min(box.max[t] - board, box.max[a] - inset, box.max[b] - inset));
        return s;
    }
    // Crumpled paper: a faceted ball with every vertex pushed in or out.
    static Shape PaperBall(Source source)
    {
        var s = new Shape(); var box = source.Bounds;
        float g = (1 + Mathf.Sqrt(5)) / 2;
        var points = new List<Vector3> { V(-1, g, 0), V(1, g, 0), V(-1, -g, 0), V(1, -g, 0), V(0, -1, g), V(0, 1, g), V(0, -1, -g), V(0, 1, -g), V(g, 0, -1), V(g, 0, 1), V(-g, 0, -1), V(-g, 0, 1) }.Select(p => p.normalized).ToList();
        var faces = new List<int[]> { new[]{0,11,5}, new[]{0,5,1}, new[]{0,1,7}, new[]{0,7,10}, new[]{0,10,11}, new[]{1,5,9}, new[]{5,11,4}, new[]{11,10,2}, new[]{10,7,6}, new[]{7,1,8},
            new[]{3,9,4}, new[]{3,4,2}, new[]{3,2,6}, new[]{3,6,8}, new[]{3,8,9}, new[]{4,9,5}, new[]{2,4,11}, new[]{6,2,10}, new[]{8,6,7}, new[]{9,8,1} };
        for (int level = 0; level < 2; level++)
        {
            var next = new List<int[]>(); var mid = new Dictionary<long, int>();
            int Middle(int i, int j)
            {
                long key = Math.Min(i, j) * 100000L + Math.Max(i, j);
                if (!mid.TryGetValue(key, out int k)) { mid[key] = k = points.Count; points.Add(((points[i] + points[j]) / 2).normalized); }
                return k;
            }
            foreach (var f in faces) { int ab = Middle(f[0], f[1]), bc = Middle(f[1], f[2]), ca = Middle(f[2], f[0]); next.Add(new[] { f[0], ab, ca }); next.Add(new[] { f[1], bc, ab }); next.Add(new[] { f[2], ca, bc }); next.Add(new[] { ab, bc, ca }); }
            faces = next;
        }
        var random = new System.Random(7);
        var crumpled = points.Select(p => box.center + Vector3.Scale(p * (.72f + .28f * (float)random.NextDouble()), box.extents)).ToArray();
        s.Uv = (p, n) => { var d = (p - box.center).normalized; return new Vector2(.5f + Mathf.Atan2(d.z, d.x) / (2 * Mathf.PI), .5f + Mathf.Asin(Mathf.Clamp(d.y, -1, 1)) / Mathf.PI); };
        foreach (var f in faces)
        {
            Vector3 a = crumpled[f[0]], b = crumpled[f[1]], c = crumpled[f[2]];
            var n = Vector3.Cross(b - a, c - a); if (Vector3.Dot(n, a - box.center) < 0) n = -n;
            s.Face("@0", n, a, b, c);
        }
        return s;
    }
    static Shape Cigarette(Source source)
    {
        var s = new Shape(); var box = source.Bounds; int l = Long(box.size);
        float radius = Mathf.Max(box.size[(l + 1) % 3], box.size[(l + 2) % 3]) / 2;
        Vector3 At(float u) { var p = box.center; p[l] = box.min[l] + box.size[l] * u; return p; }
        s.Tube(Paper, new[] { At(0), At(.72f) }, radius, 10, false);
        s.Tube("Cigarette_Filter", new[] { At(.72f), At(1) }, radius, 10, false);
        return s;
    }
    static Shape Socket(Source source)
    {
        var s = new Shape(); var box = source.Bounds; int t = Thin(box.size), a = (t + 1) % 3, b = (t + 2) % 3;
        s.Box(Trim, box.min, box.max);
        foreach (float side in new[] { .3f, .7f })
        {
            Vector3 min = box.min - Axis(t) * .0005f, max = box.max + Axis(t) * .0005f;
            min[a] = box.min[a] + box.size[a] * (side - .06f); max[a] = box.min[a] + box.size[a] * (side + .06f);
            min[b] = box.min[b] + box.size[b] * .38f; max[b] = box.min[b] + box.size[b] * .62f;
            s.Box("Socket_Dark", min, max);
        }
        return s;
    }
    static Shape Hanger(Source source)
    {
        var s = new Shape(); var box = source.Bounds; int t = Thin(box.size), w = Long(box.size), h = 3 - t - w;
        float radius = Mathf.Clamp(box.size[t] / 2, .002f, .006f);
        Vector3 P(float u, float v) { var p = box.center; p[w] = box.min[w] + box.size[w] * u; p[h] = box.min[h] + box.size[h] * v; return p; }
        s.Tube(Metal, new[] { P(.02f, .04f), P(.98f, .04f), P(.5f, .5f) }, radius, 8, true);
        var hook = new List<Vector3> { P(.5f, .5f), P(.5f, .68f) };
        for (int i = 0; i <= 10; i++) { float angle = Mathf.PI * (1 - i / 10f * 1.35f); hook.Add(P(.5f + .08f + .08f * Mathf.Cos(angle), .82f + .16f * Mathf.Sin(angle))); }
        s.Tube(Metal, hook, radius, 8, false);
        return s;
    }

    static Shape Build(string type, Source source, Func<Vector3, Vector3, Vector2> wholeFit)
    {
        if (type.StartsWith("SM_Wall_Bedroom_2x3")) return Wall(2);
        if (type.StartsWith("SM_Wall_Bedroom_1x3")) return Wall(1);
        if (type.StartsWith("SM_Wall_Bedroom_Corner")) return Corner();
        if (type.StartsWith("SM_Wall_Bedroom_Sloped")) return Sloped();
        if (type.StartsWith("SM_Window_Bedroom")) return Window();
        if (type.StartsWith("SM_Doorway_Bedroom")) return Doorway();
        if (type.StartsWith("SM_Doorway_MetalStrip")) return Slab(Metal, V(-.024f, 0, .474f), V(.022f, .003f, 1.522f));
        if (type.StartsWith("SM_Ceiling")) return Slab(Ceiling, V(0, 0, 0), V(8, .2f, 8));
        if (type.StartsWith("Floor_400x400")) return Slab(Carpet, V(0, -.2f, 0), V(4, 0, 4));
        if (type.StartsWith("SM_Curtains")) return Curtain(source);
        if (type.StartsWith("SM_Curtain_Rail")) return Rail();
        if (type.StartsWith("SM_Book")) return Book(source);
        if (type.StartsWith("SM_PaperBall")) return PaperBall(source);
        if (type.StartsWith("SM_Cigarette")) return Cigarette(source);
        if (type.StartsWith("SM_PlugSocket")) return Socket(source);
        if (type.StartsWith("SM_CoatHanger")) return Hanger(source);
        if (type.StartsWith("SM_Leaf")) return Leaf(source);
        return Cards(source);
    }
    static string Friendly(string type)
    {
        foreach (var pair in new[] { ("SM_Wall_Bedroom_2x3_03m", "Wall_2m"), ("SM_Wall_Bedroom_1x3M", "Wall_1m"), ("SM_Wall_Bedroom_Corner", "Wall_Corner"), ("SM_Wall_Bedroom_Sloped_2M", "Wall_Sloped"),
            ("SM_Window_Bedroom_2x3M", "Window"), ("SM_Doorway_Bedroom", "Doorway"), ("SM_Doorway_MetalStrip", "Threshold_Strip"), ("SM_Ceiling", "Ceiling"), ("Floor_400x400", "Floor"),
            ("SM_Curtains", "Curtain"), ("SM_Curtain_Rail", "Curtain_Rail") })
            if (type.StartsWith(pair.Item1)) return pair.Item2;
        return type.StartsWith("SM_") ? type.Substring(3) : type;
    }
    static bool IsStructural(string type) => type.Contains("Wall") || type.Contains("Window") || type.Contains("Doorway") || type.Contains("Ceiling") || type.Contains("Floor");

    // ───────────────────────── materials
    static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();
    static Texture2D Texture(string name, bool normal)
    {
        string path = TextureFolder + "/" + name + ".png";
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer == null) throw new FileNotFoundException("Run Assets/RoomShell/Source~/generate_textures.py first: " + path);
        if (normal != (importer.textureType == TextureImporterType.NormalMap)) { importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default; importer.SaveAndReimport(); }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
    static Material Save(Material material, string name)
    {
        string path = MaterialFolder + "/" + name + ".mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing) { existing.shader = material.shader; existing.CopyPropertiesFromMaterial(material); EditorUtility.SetDirty(existing); return Materials[name] = existing; }
        material.name = name; AssetDatabase.CreateAsset(material, path);
        return Materials[name] = material;
    }
    static Material Lit(string name, Color color, string texture, float metresPerTile, float smoothness, float metallic = 0, float bump = 1)
    {
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.SetColor("_BaseColor", color); m.SetFloat("_Smoothness", smoothness); m.SetFloat("_Metallic", metallic);
        if (texture != null)
        {
            var scale = Vector2.one / metresPerTile;
            m.SetTexture("_BaseMap", Texture(texture + "_BaseColor", false)); m.SetTextureScale("_BaseMap", scale);
            m.SetTexture("_BumpMap", Texture(texture + "_Normal", true)); m.SetTextureScale("_BumpMap", scale);
            m.SetFloat("_BumpScale", bump); m.EnableKeyword("_NORMALMAP");
        }
        return Save(m, name);
    }
    static void CreateMaterials()
    {
        Directory.CreateDirectory(MaterialFolder); Directory.CreateDirectory(MeshFolder);
        AssetDatabase.Refresh();
        // Tints, smoothness and metalness keep the room's existing light response.
        Lit(Plaster, new Color(.68f, .61f, .55f), "Plaster_Wall", 4, .146f, 0, .4f);   // full strength reads as stucco under the lamps
        Lit(Trim, new Color(.91f, .82f, .76f), "Trim_Paint", 1, .394f);
        Lit(Carpet, new Color(.33f, .30f, .28f), "Carpet_Grey", 2, .075f);
        Lit(Ceiling, new Color(1, .94f, .87f), "Plaster_Ceiling", 4, .27f, 0, .5f);
        Lit(Metal, new Color(.64f, .64f, .64f), null, 1, .9f, .542f);
        Lit(Paper, new Color(.9f, .9f, .9f), "Paper_Plain", 1, .106f);
        Lit("Cigarette_Filter", new Color(.72f, .52f, .30f), null, 1, .15f);
        Lit("Socket_Dark", new Color(.05f, .05f, .05f), null, 1, .3f);
        Lit("Leaf_Green", new Color(.17f, .22f, .12f), null, 1, .42f);
    }
    // A project material standing in for a vendor one: same surface settings and
    // any project-made textures it carried; vendor textures are dropped.
    static Material Convert(Material vendor)
    {
        if (!IsVendor(vendor)) return vendor;
        string name = (vendor.name.StartsWith("M_") ? vendor.name.Substring(2) : vendor.name) + "_Room";
        if (name.StartsWith("Glass")) name = Glass;
        if (Materials.TryGetValue(name, out var done)) return done;
        var m = new Material(vendor);
        if (IsVendor(m.shader)) m.shader = Shader.Find("Universal Render Pipeline/Lit");
        foreach (string property in m.GetTexturePropertyNames())
            if (IsVendor(m.GetTexture(property)))
                m.SetTexture(property, property == "_BumpMap" && !name.StartsWith("Window") && !name.Contains("Metal") && !name.Contains("Wire") ? Texture("Paper_Plain_Normal", true) : null);
        if (!m.GetTexture("_BumpMap")) m.DisableKeyword("_NORMALMAP");
        return Save(m, name);
    }

    // ───────────────────────── scene replacement
    public static void Replace()
    {
        var report = new StringBuilder();
        try
        {
            var scene = EditorSceneManager.OpenScene(ScenePath);
            CreateMaterials();
            var transforms = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
            foreach (var root in transforms.Where(t => t && PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject) && Vendor.Any(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject).StartsWith)).ToArray())
                PrefabUtility.UnpackPrefabInstance(root.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            var sources = new Dictionary<Mesh, Source>();
            Source SourceOf(Mesh mesh) { if (!sources.TryGetValue(mesh, out var s)) sources[mesh] = s = new Source(mesh); return s; }
            var counters = new Dictionary<string, int>();
            var wallEntries = new List<WallGeometryCatalog.Entry>();
            int replaced = 0, lodLevels = 0;
            foreach (var filter in transforms.Where(t => t).Select(t => t.GetComponent<MeshFilter>()).Where(f => f && IsVendor(f.sharedMesh)).ToArray())
            {
                if (!filter) continue;
                var vendor = filter.sharedMesh;
                var renderer = filter.GetComponent<MeshRenderer>();
                int lod = vendor.name.LastIndexOf("_LOD", StringComparison.Ordinal);
                if (lod >= 0 && !vendor.name.EndsWith("_LOD0"))
                {
                    // Lower levels of the old object; the new piece is a single mesh.
                    UnityEngine.Object.DestroyImmediate(filter.gameObject); lodLevels++; continue;
                }
                string type = lod >= 0 ? vendor.name.Substring(0, lod) : vendor.name;
                var source = SourceOf(vendor);
                var shape = Build(type, source, FitUv(source, Enumerable.Range(0, source.Positions.Length), 2, 1));
                counters[type] = counters.TryGetValue(type, out int n) ? n + 1 : 1;
                var mesh = shape.ToMesh("Room_" + Friendly(type) + "_" + counters[type].ToString("D2"), filter.transform.localToWorldMatrix);
                AssetDatabase.CreateAsset(mesh, MeshFolder + "/" + mesh.name + ".asset");
                var old = renderer ? renderer.sharedMaterials : new Material[0];
                filter.sharedMesh = mesh;
                var interaction = filter.GetComponent<InteractionMeshSource>();
                if (interaction) interaction.mesh = mesh;
                if (renderer)
                    renderer.sharedMaterials = shape.Slots.Select(slot => slot.StartsWith("@") ? Convert(old.Length > 0 ? old[Mathf.Min(int.Parse(slot.Substring(1)), old.Length - 1)] : Materials[Paper])
                        // The glazing keeps the transparency settings of the pane it replaces.
                        : slot == Glass && !Materials.ContainsKey(Glass) ? Convert(old.First(m => m && m.name.Contains("Glass"))) : Materials[slot]).ToArray();
                var group = filter.GetComponentInParent<LODGroup>(true);
                if (group) UnityEngine.Object.DestroyImmediate(group);
                if (type.StartsWith("SM_Wall_Bedroom")) wallEntries.Add(new WallGeometryCatalog.Entry { name = mesh.name, vertices = mesh.vertices, triangles = mesh.triangles });
                report.AppendLine(PathOf(filter.transform) + " -> " + mesh.name + " (" + mesh.triangles.Length / 3 + " tris)");
                replaced++;
            }

            // Collision keeps its objects; only the shape asset behind each one changes.
            var collisionMeshes = new Dictionary<string, Mesh>();
            int colliders = 0;
            foreach (var collider in transforms.Where(t => t).Select(t => t.GetComponent<MeshCollider>()).Where(c => c && IsVendor(c.sharedMesh)).ToArray())
            {
                string type = collider.sharedMesh.name.Replace("_ConvexHulls", "");
                int lod = type.LastIndexOf("_LOD", StringComparison.Ordinal); if (lod >= 0) type = type.Substring(0, lod);
                if (!collisionMeshes.TryGetValue(type, out var mesh))
                {
                    var source = SourceOf(collider.sharedMesh);
                    var shape = IsStructural(type) ? Build(type, source, null) : Slab(Paper, source.Bounds.min, source.Bounds.max);
                    mesh = shape.ToMesh("Room_" + Friendly(type) + "_Collision", Matrix4x4.identity);
                    AssetDatabase.CreateAsset(mesh, MeshFolder + "/" + mesh.name + ".asset");
                    collisionMeshes[type] = mesh;
                }
                collider.convex = false; collider.sharedMesh = mesh; colliders++;
            }

            int materials = 0;
            foreach (var renderer in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Renderer>(true)))
            {
                var current = renderer.sharedMaterials;
                if (!current.Any(IsVendor)) continue;
                renderer.sharedMaterials = current.Select(m => !IsVendor(m) ? m : m.name.Contains("WallPaper") ? Materials[Plaster] : m.name.Contains("Trim") ? Materials[Trim] : Convert(m)).ToArray();
                materials++;
            }
            foreach (string path in AssetDatabase.FindAssets("t:Material", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath).Where(p => !Vendor.Any(p.StartsWith)))
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!m || !m.GetTexturePropertyNames().Any(p => IsVendor(m.GetTexture(p)))) continue;
                // The corridor stone was the only project material built on vendor maps.
                foreach (string property in m.GetTexturePropertyNames())
                    if (IsVendor(m.GetTexture(property)))
                        m.SetTexture(property, property == "_BumpMap" ? Texture("Worn_Stone_Normal", true) : property == "_BaseMap" || property == "_MainTex" ? Texture("Worn_Stone_BaseColor", false) : null);
                EditorUtility.SetDirty(m); report.AppendLine("material textures replaced: " + path);
            }

            File.WriteAllText("Assets/Resources/Interaction/WallGeometry.json", JsonUtility.ToJson(new WallGeometryCatalog.Data { entries = wallEntries.ToArray() }));
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Save failed");
            AssetDatabase.Refresh();
            // Re-bake the interaction surface catalog, which is keyed by mesh asset.
            if (!EditorApplication.ExecuteMenuItem("Tools/Room/Rebuild interaction surfaces")) report.AppendLine("WARNING: interaction surface bake did not run");

            var left = AssetDatabase.GetDependencies(new[] { ScenePath }, true).Where(d => Vendor.Any(d.StartsWith)).ToArray();
            report.Insert(0, (left.Length == 0 ? "COMPLETE" : "INCOMPLETE") + "\nPieces rebuilt: " + replaced + "\nOld LOD levels removed: " + lodLevels + "\nColliders re-pointed: " + colliders +
                "\nRenderers with materials swapped: " + materials + "\nVendor assets the scene still depends on: " + left.Length + "\n" + string.Join("\n", left.Take(40)) + "\n\n");
            File.WriteAllText(ReportPath, report.ToString());
        }
        catch (Exception e)
        {
            File.WriteAllText(ReportPath, "FAILED\n" + e + "\n\n" + report);
            Debug.LogException(e);
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }
}
