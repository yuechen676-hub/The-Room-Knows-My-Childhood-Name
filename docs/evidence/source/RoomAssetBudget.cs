using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Read-only scene budget report: where the triangles, shadow casters and
// texture memory are. Writes CSVs plus a summary; changes no asset.
// Batch: -executeMethod RoomAssetBudget.Report
public static class RoomAssetBudget
{
    const string ScenePath = "Assets/Scenes/TheRoomKnowsMyChildhoodName.unity";
    const string Folder = "PerformanceReview/AssetBudget";
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    class Row
    {
        public string path, pack, meshPath, meshName;
        public Mesh mesh;
        public Renderer renderer;
        public int tris, verts;
        public bool casts, inLod;
        public float size;
    }

    [MenuItem("Tools/Room/Asset Budget Report")]
    public static void Report()
    {
        try
        {
            var scene = SceneManager.GetActiveScene();
            if (Application.isBatchMode || scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath);
            Directory.CreateDirectory(Folder);
            var rows = Collect(scene);
            var summary = new StringBuilder("Scene: " + scene.path + "\nGenerated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm", Inv) + "\n\n");
            WriteRenderers(rows);
            WriteMeshes(rows, summary);
            WriteLights(scene, rows, summary);
            WriteTextures(rows, summary);
            File.WriteAllText(Folder + "/summary.txt", summary.ToString());
            Debug.Log("Asset budget written to " + Folder);
        }
        catch (Exception e)
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(Folder + "/summary.txt", "FAILED\n" + e);
            Debug.LogException(e);
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }

    static string PathOf(Transform t) => t.parent ? PathOf(t.parent) + "/" + t.name : t.name;
    static string Csv(string value) => "\"" + (value ?? "").Replace("\"", "\"\"") + "\"";
    // First folder under Assets: the prop pack (or third-party library) that owns the asset.
    static string Pack(string assetPath)
    {
        var parts = (assetPath ?? "").Split('/');
        return parts.Length > 2 && parts[0] == "Assets" ? parts[1] : string.IsNullOrEmpty(assetPath) ? "(scene)" : "(built-in)";
    }

    static List<Row> Collect(Scene scene)
    {
        var lodRenderers = new HashSet<Renderer>(scene.GetRootGameObjects()
            .SelectMany(r => r.GetComponentsInChildren<LODGroup>(false))
            .SelectMany(g => g.GetLODs()).SelectMany(l => l.renderers).Where(r => r));
        var rows = new List<Row>();
        foreach (var renderer in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Renderer>(false)))
        {
            if (!renderer.enabled) continue;
            Mesh mesh = renderer is SkinnedMeshRenderer skinned ? skinned.sharedMesh
                : renderer is MeshRenderer ? renderer.GetComponent<MeshFilter>()?.sharedMesh : null;
            if (!mesh) continue;
            long indices = 0;
            for (int i = 0; i < mesh.subMeshCount; i++)
                if (mesh.GetTopology(i) == MeshTopology.Triangles) indices += mesh.GetIndexCount(i);
            string meshPath = AssetDatabase.GetAssetPath(mesh);
            rows.Add(new Row {
                path = PathOf(renderer.transform), pack = Pack(meshPath), meshPath = meshPath, meshName = mesh.name, mesh = mesh, renderer = renderer,
                tris = (int)(indices / 3), verts = mesh.vertexCount, casts = renderer.shadowCastingMode != ShadowCastingMode.Off,
                inLod = lodRenderers.Contains(renderer), size = renderer.bounds.size.magnitude });
        }
        return rows;
    }

    static void WriteRenderers(List<Row> rows)
    {
        var csv = new StringBuilder("path,pack,mesh,triangles,vertices,materials,casts_shadows,in_lod_group,bounds_diagonal_m,triangles_per_m\n");
        foreach (var r in rows.OrderByDescending(r => r.tris))
            csv.AppendFormat(Inv, "{0},{1},{2},{3},{4},{5},{6},{7},{8:F3},{9:F0}\n", Csv(r.path), Csv(r.pack), Csv(r.meshName), r.tris, r.verts,
                r.renderer.sharedMaterials.Length, r.casts ? 1 : 0, r.inLod ? 1 : 0, r.size, r.tris / Mathf.Max(.01f, r.size));
        File.WriteAllText(Folder + "/renderers.csv", csv.ToString());
    }

    static void WriteMeshes(List<Row> rows, StringBuilder summary)
    {
        var csv = new StringBuilder("mesh,asset,pack,triangles,instances,scene_triangles,casting_instances,smallest_bounds_m,readable,in_lod_group\n");
        foreach (var g in rows.GroupBy(r => r.mesh).OrderByDescending(g => g.Sum(r => (long)r.tris)))
            csv.AppendFormat(Inv, "{0},{1},{2},{3},{4},{5},{6},{7:F3},{8},{9}\n", Csv(g.Key.name), Csv(g.First().meshPath), Csv(g.First().pack), g.First().tris,
                g.Count(), g.Sum(r => (long)r.tris), g.Count(r => r.casts), g.Min(r => r.size), g.Key.isReadable ? 1 : 0, g.Any(r => r.inLod) ? 1 : 0);
        File.WriteAllText(Folder + "/meshes.csv", csv.ToString());

        long total = rows.Sum(r => (long)r.tris), casting = rows.Where(r => r.casts).Sum(r => (long)r.tris);
        summary.AppendFormat(Inv, "GEOMETRY\nActive renderers: {0}; unique meshes: {1}; triangles: {2:N0}; shadow-casting triangles: {3:N0} ({4:P0})\n",
            rows.Count, rows.Select(r => r.mesh).Distinct().Count(), total, casting, casting / (double)Math.Max(1, total));
        summary.AppendFormat(Inv, "Renderers under a LODGroup: {0}; triangles covered by LOD: {1:N0}\n", rows.Count(r => r.inLod), rows.Where(r => r.inLod).Sum(r => (long)r.tris));
        summary.AppendFormat(Inv, "Readable meshes (CPU copy kept): {0}\n", rows.Select(r => r.mesh).Distinct().Count(m => m.isReadable));
        // A small prop with a large triangle count is the first reduction candidate.
        summary.AppendFormat(Inv, "Renderers over 50k triangles: {0}; over 20k triangles and under 0.5 m: {1}\n",
            rows.Count(r => r.tris > 50000), rows.Count(r => r.tris > 20000 && r.size < .5f));
        summary.AppendLine("\nTriangles by pack (top 20):");
        foreach (var g in rows.GroupBy(r => r.pack).OrderByDescending(g => g.Sum(r => (long)r.tris)).Take(20))
            summary.AppendFormat(Inv, "  {0,-24} {1,11:N0}  {2,5:P1}  renderers {3,4}  casting {4,11:N0}\n", g.Key, g.Sum(r => (long)r.tris),
                g.Sum(r => (long)r.tris) / (double)Math.Max(1, total), g.Count(), g.Where(r => r.casts).Sum(r => (long)r.tris));
        summary.AppendLine("\nDensest props (triangles per metre of bounds, over 20k triangles, top 15):");
        foreach (var r in rows.Where(r => r.tris > 20000).OrderByDescending(r => r.tris / Mathf.Max(.01f, r.size)).Take(15))
            summary.AppendFormat(Inv, "  {0,9:N0} tris  {1,5:F2} m  {2}\n", r.tris, r.size, r.path);
    }

    static void WriteLights(Scene scene, List<Row> rows, StringBuilder summary)
    {
        var casters = rows.Where(r => r.casts).ToArray();
        var csv = new StringBuilder("light,type,shadows,range_m,intensity,casters_in_range,triangles_in_range,passes\n");
        summary.AppendLine("\nSHADOW LIGHTS (triangles inside each light's range; a point light draws up to 6 faces)");
        long sum = 0;
        foreach (var light in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Light>(false)).Where(l => l.enabled).OrderBy(l => PathOf(l.transform), StringComparer.Ordinal))
        {
            bool shadowed = light.shadows != LightShadows.None;
            Row[] inRange = !shadowed ? new Row[0] : light.type == LightType.Directional ? casters
                : casters.Where(r => r.renderer.bounds.SqrDistance(light.transform.position) <= light.range * light.range).ToArray();
            long tris = inRange.Sum(r => (long)r.tris);
            string passes = !shadowed ? "0" : light.type == LightType.Directional ? "cascades" : light.type == LightType.Point ? "6" : "1";
            csv.AppendFormat(Inv, "{0},{1},{2},{3:F2},{4:F2},{5},{6},{7}\n", Csv(PathOf(light.transform)), light.type, light.shadows, light.range, light.intensity, inRange.Length, tris, passes);
            if (!shadowed) continue;
            sum += tris;
            summary.AppendFormat(Inv, "  {0,11:N0} tris  {1,5} casters  {2,-11} range {3,5:F1}  {4}\n", tris, inRange.Length, light.type, light.range, PathOf(light.transform));
        }
        summary.AppendFormat(Inv, "Sum over shadow lights, one pass each: {0:N0}\n", sum);
        File.WriteAllText(Folder + "/lights.csv", csv.ToString());
    }

    static void WriteTextures(List<Row> rows, StringBuilder summary)
    {
        var users = new Dictionary<Texture, HashSet<Material>>();
        foreach (var material in rows.SelectMany(r => r.renderer.sharedMaterials).Where(m => m).Distinct())
            foreach (int id in material.GetTexturePropertyNameIDs())
            {
                var texture = material.GetTexture(id);
                if (!texture) continue;
                if (!users.TryGetValue(texture, out var set)) users[texture] = set = new HashSet<Material>();
                set.Add(material);
            }
        var csv = new StringBuilder("texture,asset,pack,width,height,format,mipmaps,memory_mb,source_width,source_height,import_max_size,materials\n");
        long memory = 0;
        var byPack = new Dictionary<string, long>();
        int large = 0;
        foreach (var pair in users.OrderByDescending(p => UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(p.Key)))
        {
            var texture = pair.Key;
            string path = AssetDatabase.GetAssetPath(texture), pack = Pack(path);
            long bytes = UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(texture);
            int sourceWidth = 0, sourceHeight = 0, maxSize = 0;
            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                importer.GetSourceTextureWidthAndHeight(out sourceWidth, out sourceHeight);
                maxSize = importer.maxTextureSize;
            }
            memory += bytes;
            byPack[pack] = (byPack.TryGetValue(pack, out long old) ? old : 0) + bytes;
            if (Mathf.Max(texture.width, texture.height) >= 4096) large++;
            csv.AppendFormat(Inv, "{0},{1},{2},{3},{4},{5},{6},{7:F2},{8},{9},{10},{11}\n", Csv(texture.name), Csv(path), Csv(pack), texture.width, texture.height,
                texture is Texture2D flat ? flat.format.ToString() : texture.GetType().Name, texture.mipmapCount, bytes / 1048576.0, sourceWidth, sourceHeight, maxSize, pair.Value.Count);
        }
        File.WriteAllText(Folder + "/textures.csv", csv.ToString());
        summary.AppendFormat(Inv, "\nTEXTURES (referenced by active renderers)\nTextures: {0}; memory: {1:N0} MB; 4096 or larger: {2}\n", users.Count, memory / 1048576.0, large);
        summary.AppendLine("Memory by pack (top 12):");
        foreach (var pair in byPack.OrderByDescending(p => p.Value).Take(12))
            summary.AppendFormat(Inv, "  {0,-24} {1,7:N0} MB\n", pair.Key, pair.Value / 1048576.0);
    }
}
