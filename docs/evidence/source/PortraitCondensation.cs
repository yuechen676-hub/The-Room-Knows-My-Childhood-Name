using System.Collections.Generic;
using UnityEngine;

// A surface effect on the portrait only: the frame and people never animate.
public sealed class PortraitCondensation : MonoBehaviour
{
    private readonly List<Material> surfaces = new List<Material>();
    private float started, duration = 7f, progress;
    private bool running, shown, followsRecording;
    private AudioSource voice;
    public float Progress => progress;

    private void EnsureMaterials()
    {
        if (surfaces.Count > 0) return;
        var shader = Resources.Load<Shader>("Interaction/PortraitCondensation");
        if (shader == null) return;
        foreach (var renderer in GetComponentsInChildren<MeshRenderer>())
        {
            var materials = renderer.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] == null || !materials[i].name.StartsWith("Family_Portrait")) continue;
                var material = new Material(materials[i]) { shader = shader };
                materials[i] = material;
                surfaces.Add(material);
                changed = true;
            }
            if (changed) renderer.sharedMaterials = materials;
        }
        SetProgress(progress);
    }

    public void Begin(AudioSource recording = null)
    {
        EnsureMaterials();
        if (shown && recording == null) return;
        voice = recording;
        followsRecording = recording != null;
        duration = recording != null && recording.clip != null ? Mathf.Max(1, recording.clip.length) : 7f;
        started = Time.unscaledTime;
        running = shown = true;
        SetProgress(0);
    }

    private void Update()
    {
        if (!running) return;
        float elapsed = Time.unscaledTime - started;
        if (followsRecording && (voice == null || !voice.isPlaying) && elapsed < duration - .1f)
        {
            running = false;
            return;
        }
        SetProgress(Mathf.Clamp01(elapsed / duration));
        // Returning from a recording leaves the current moisture on the glass.
        if (progress >= 1) running = false;
    }

    public void SetProgress(float value)
    {
        progress = Mathf.Clamp01(value);
        foreach (var material in surfaces)
        {
            material.SetFloat("_MistAmount", Mathf.SmoothStep(0, 1, Mathf.Clamp01(progress / .65f)));
            material.SetFloat("_RainProgress", Mathf.SmoothStep(0, 1, Mathf.Clamp01((progress - .42f) / .58f)));
        }
    }

    private void OnDestroy()
    {
        foreach (var material in surfaces)
            if (Application.isPlaying) Destroy(material); else DestroyImmediate(material);
    }
}
