#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Opt-in measurement only. Never runs in the normal game or changes scene assets.
public sealed class RoomPerformanceProbe : MonoBehaviour
{
    [Serializable] public class Sample
    {
        public string variant;
        public int frames;
        public double meanMs, medianMs, p95Ms, maxMs, meanFps, gpuMs, cpuMainMs, batches, triangles, gcBytes;
        public int gpuSamples;
    }
    [Serializable] public class Report
    {
        public string utc, unity, gpu, cpu, graphicsApi, resolution, scene, note;
        public int ramMB, vramMB, renderers, meshVertices, shadowLights, mirrorCount;
        public List<Sample> samples = new List<Sample>();
    }
    private readonly Report report = new Report();
    private string output;
    private readonly FrameTiming[] timing = new FrameTiming[1];
    private ProfilerRecorder batches, triangles, gc, main;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Begin()
    {
        var args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, "-room-performance");
        if (index < 0 || index + 1 >= args.Length) return;
        var probe = new GameObject("Performance measurement").AddComponent<RoomPerformanceProbe>();
        probe.output = args[index + 1];
        // No AI request is needed to measure rendering. Preserve normal runtime UI objects.
        var ui = FindFirstObjectByType<RoomConversationUI>();
        if (ui != null) ui.enabled = false;
    }

    private IEnumerator Start()
    {
        Application.runInBackground = true;
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = -1;
        yield return new WaitForSecondsRealtime(10);
        var camera = Camera.main;
        if (camera == null) { File.WriteAllText(output + ".error", "Missing main camera"); Application.Quit(2); yield break; }
        var ui = FindFirstObjectByType<RoomConversationUI>();
        if (ui != null)
        {
            ui.StopAllCoroutines();
            foreach (var canvas in ui.GetComponentsInChildren<Canvas>(true)) canvas.enabled = false;
        }
        var player = FirstPersonController.Instance;
        if (player != null) { player.SetControlEnabled(false); player.enabled = false; }
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        bool compareMirrors = Environment.GetCommandLineArgs().Contains("-mirror-comparison");
        bool compareShadows = Environment.GetCommandLineArgs().Contains("-shadow-comparison");
        var allMirrors = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
            .Where(m => m.GetType().Name.EndsWith("PlanarMirror")).ToArray();
        var mirrors = allMirrors.Where(m => m.enabled).ToArray();
        var mirrorShadows = allMirrors.Select(m => (bool)m.GetType().GetField("reflectShadows").GetValue(m)).ToArray();
        var mirrorSizes = allMirrors.Select(m => (int)m.GetType().GetField("resolution").GetValue(m)).ToArray();
        var lights = FindObjectsByType<Light>(FindObjectsSortMode.None).Where(l => l.enabled && l.shadows != LightShadows.None).ToArray();
        var shadowTypes = lights.Select(l => l.shadows).ToArray();
        var rp = (UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;
        float originalScale = rp.renderScale;
        var rotation = camera.transform.rotation;
        report.utc = DateTime.UtcNow.ToString("O"); report.unity = Application.unityVersion;
        report.gpu = SystemInfo.graphicsDeviceName; report.cpu = SystemInfo.processorType;
        report.graphicsApi = SystemInfo.graphicsDeviceType.ToString(); report.resolution = Screen.width + "x" + Screen.height;
        report.ramMB = SystemInfo.systemMemorySize; report.vramMB = SystemInfo.graphicsMemorySize;
        report.scene = camera.gameObject.scene.path;
        report.renderers = FindObjectsByType<Renderer>(FindObjectsSortMode.None).Length;
        report.meshVertices = FindObjectsByType<MeshFilter>(FindObjectsSortMode.None).Where(m => m.sharedMesh != null).Sum(m => m.sharedMesh.vertexCount);
        report.shadowLights = lights.Length; report.mirrorCount = mirrors.Length;
        report.note = "Development player; uncapped, VSync off, no AI/network request; initial stage stationary 360-degree camera sweep. Each case 3-second warmup + 12-second sweep. GPU timing excludes unavailable samples. No scene changes saved. Editor may remain open; repeat baseline detects drift.";
        batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count", 1);
        triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count", 1);
        gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1);
        main = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 1);
        var variants = compareShadows
            ? new[] { "reflection_shadows_on", "reflection_shadows_off", "reflection_shadows_on_repeat", "reflection_shadows_off_repeat" }
            : compareMirrors
            ? new[] { "original_five", "selected_three", "three_no_reflection_shadows", "three_512", "selected_three_repeat" }
            : new[] { "baseline", "no_mirror_rendering", "no_realtime_shadows", "no_mirrors_or_shadows", "render_scale_075", "baseline_repeat" };
        foreach (var variant in variants)
        {
            bool noMirrors = variant == "no_mirror_rendering" || variant == "no_mirrors_or_shadows";
            bool noShadows = variant == "no_realtime_shadows" || variant == "no_mirrors_or_shadows";
            foreach (var mirror in mirrors) mirror.enabled = !noMirrors;
            if (compareShadows)
                foreach (var mirror in mirrors)
                    mirror.GetType().GetField("reflectShadows").SetValue(mirror, variant.StartsWith("reflection_shadows_on"));
            if (compareMirrors)
                for (int i = 0; i < allMirrors.Length; i++)
                {
                    var mirror = allMirrors[i];
                    mirror.enabled = variant == "original_five" || mirrors.Contains(mirror);
                    mirror.GetType().GetField("reflectShadows").SetValue(mirror, variant == "three_no_reflection_shadows" ? false : mirrorShadows[i]);
                    mirror.GetType().GetField("resolution").SetValue(mirror, variant == "three_512" ? 512 : mirrorSizes[i]);
                }
            for (int i = 0; i < lights.Length; i++) lights[i].shadows = noShadows ? LightShadows.None : shadowTypes[i];
            rp.renderScale = variant == "render_scale_075" ? .75f : originalScale;
            camera.transform.rotation = rotation;
            File.WriteAllText(output + ".progress", variant);
            yield return new WaitForSecondsRealtime(3);
            var sample = new Sample { variant = variant };
            var frames = new List<double>();
            double start = Time.realtimeSinceStartupAsDouble;
            while (Time.realtimeSinceStartupAsDouble - start < 12)
            {
                camera.transform.rotation = Quaternion.AngleAxis((float)((Time.realtimeSinceStartupAsDouble - start) * 30), Vector3.up) * rotation;
                FrameTimingManager.CaptureFrameTimings();
                yield return null;
                frames.Add(Time.unscaledDeltaTime * 1000.0);
                if (batches.Valid) sample.batches += batches.LastValue;
                if (triangles.Valid) sample.triangles += triangles.LastValue;
                if (gc.Valid) sample.gcBytes += gc.LastValue;
                if (main.Valid) sample.cpuMainMs += main.LastValue / 1000000.0;
                if (FrameTimingManager.GetLatestTimings(1, timing) > 0 && timing[0].gpuFrameTime > 0)
                { sample.gpuMs += timing[0].gpuFrameTime; sample.gpuSamples++; }
            }
            sample.frames = frames.Count;
            frames.Sort(); sample.meanMs = frames.Average(); sample.meanFps = 1000 / sample.meanMs;
            sample.medianMs = frames[frames.Count / 2]; sample.p95Ms = frames[Math.Min(frames.Count - 1, (int)(frames.Count * .95))]; sample.maxMs = frames.Last();
            sample.batches /= sample.frames; sample.triangles /= sample.frames; sample.gcBytes /= sample.frames; sample.cpuMainMs /= sample.frames;
            if (sample.gpuSamples > 0) sample.gpuMs /= sample.gpuSamples;
            report.samples.Add(sample);
            File.WriteAllText(output, JsonUtility.ToJson(report, true));
            if (compareMirrors || compareShadows)
            {
                camera.transform.rotation = rotation;
                yield return new WaitForSecondsRealtime(.5f);
                ScreenCapture.CaptureScreenshot(output + "." + variant + ".png");
                yield return new WaitForSecondsRealtime(.5f);
            }
        }
        for (int i = 0; i < allMirrors.Length; i++)
        {
            allMirrors[i].enabled = mirrors.Contains(allMirrors[i]);
            allMirrors[i].GetType().GetField("reflectShadows").SetValue(allMirrors[i], mirrorShadows[i]);
            allMirrors[i].GetType().GetField("resolution").SetValue(allMirrors[i], mirrorSizes[i]);
        }
        for (int i = 0; i < lights.Length; i++) lights[i].shadows = shadowTypes[i];
        rp.renderScale = originalScale; camera.transform.rotation = rotation;
        batches.Dispose(); triangles.Dispose(); gc.Dispose(); main.Dispose();
        File.WriteAllText(output + ".progress", "COMPLETE");
        Application.Quit();
    }
}
#endif
