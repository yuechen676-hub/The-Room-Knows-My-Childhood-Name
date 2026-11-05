using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Previews any narrative stage's sky in the Scene view without entering Play
/// Mode, and scrubs the blend between two stages so transitions can be judged
/// rather than guessed.
///
/// Previewing is deliberately non-destructive: it drives a throwaway material
/// and in-memory RenderSettings, and restores everything when the window closes.
/// Writing a look into the scene is a separate, explicit button.
/// </summary>
public sealed class SkyStagePreviewWindow : EditorWindow
{
    private readonly StageSkyPreset[] presets = new StageSkyPreset[SkyAtmosphereController.StageCount];

    private Material previewMaterial;
    private Light keyLight;
    private Volume postVolume;

    private bool driveAmbient = true;
    private bool driveFog = true;
    private bool driveKeyLight = true;
    private bool drivePostProcessing;
    private bool driveLightShafts = true;
    private bool liveRefresh = true;

    private int stageA = 1;
    private int stageB = 2;
    private float scrub;
    private int appliedStage;

    private bool captured;
    private Material capturedSkybox;
    private AmbientMode capturedAmbientMode;
    private Color capturedAmbientSky;
    private Color capturedAmbientEquator;
    private Color capturedAmbientGround;
    private Color capturedFogColor;
    private float capturedFogDensity;
    private Quaternion capturedLightRotation;
    private Color capturedLightColor;
    private float capturedLightIntensity;
    private Light capturedLight;

    private Vector2 scroll;

    [MenuItem("Tools/Room/Sky/Stage Preview")]
    public static void Open()
    {
        var window = GetWindow<SkyStagePreviewWindow>("Sky Stages");
        window.minSize = new Vector2(340f, 430f);
        window.Show();
    }

    private void OnEnable()
    {
        LoadPresets();
        postVolume = FindGlobalVolume();
        keyLight = FindSingleDirectionalLight();
    }

    /// <summary>
    /// The room has exactly one directional light, so it is adopted as the
    /// moonlight. With several, none is chosen rather than guessing wrong.
    /// </summary>
    private static Light FindSingleDirectionalLight()
    {
        Light found = null;
        var lights = FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var candidate in lights)
        {
            if (candidate.type != LightType.Directional) continue;
            if (found != null) return null;
            found = candidate;
        }
        return found;
    }

    private void OnDisable()
    {
        RestoreScene();
        if (previewMaterial != null) DestroyImmediate(previewMaterial);
    }

    private void LoadPresets()
    {
        for (var index = 0; index < presets.Length; index++)
        {
            presets[index] = AssetDatabase.LoadAssetAtPath<StageSkyPreset>(
                $"Assets/Resources/Sky/StageSky{index + 1}.asset");
        }
    }

    private static Volume FindGlobalVolume()
    {
        var volumes = FindObjectsByType<Volume>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        Volume best = null;
        foreach (var candidate in volumes)
        {
            if (!candidate.isGlobal) continue;
            if (best == null || candidate.priority > best.priority) best = candidate;
        }
        return best;
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        if (!PresetsReady())
        {
            EditorGUILayout.HelpBox(
                "Stage sky presets not found under Assets/Resources/Sky.\n" +
                "Run Tools > Room > Sky > Create Stage Sky Assets first.",
                MessageType.Warning);

            if (GUILayout.Button("Create Stage Sky Assets"))
            {
                SkyStageSetup.CreateAssets();
                LoadPresets();
            }

            EditorGUILayout.EndScrollView();
            return;
        }

        DrawStageButtons();
        EditorGUILayout.Space();
        DrawMoonAiming();
        EditorGUILayout.Space();
        DrawScrub();
        EditorGUILayout.Space();
        DrawTargets();
        EditorGUILayout.Space();
        DrawFooter();

        EditorGUILayout.EndScrollView();
    }

    private bool PresetsReady()
    {
        foreach (var preset in presets)
        {
            if (preset == null) return false;
        }
        return true;
    }

    private void DrawStageButtons()
    {
        EditorGUILayout.LabelField("Preview a stage", EditorStyles.boldLabel);

        for (var stage = 1; stage <= presets.Length; stage++)
        {
            var preset = presets[stage - 1];
            var isApplied = appliedStage == stage;

            using (new EditorGUILayout.HorizontalScope())
            {
                var label = $"{stage}.  {preset.label}";
                var previous = GUI.backgroundColor;
                if (isApplied) GUI.backgroundColor = new Color(0.55f, 0.78f, 1f);

                if (GUILayout.Button(label, GUILayout.Height(24f)))
                {
                    ApplySettings(preset.sky);
                    appliedStage = stage;
                }

                GUI.backgroundColor = previous;

                if (GUILayout.Button("Edit", GUILayout.Width(44f), GUILayout.Height(24f)))
                {
                    Selection.activeObject = preset;
                    EditorGUIUtility.PingObject(preset);
                }
            }
        }

        if (appliedStage >= 1)
        {
            var intent = presets[appliedStage - 1].intent;
            if (!string.IsNullOrEmpty(intent)) EditorGUILayout.HelpBox(intent, MessageType.None);
        }
    }

    /// <summary>
    /// Aiming the moon is the one value that cannot be authored blind: it has to
    /// end up inside the window. Driving it from the Scene view direction means
    /// looking out of the window and pressing a button, instead of guessing an
    /// angle and re-previewing.
    /// </summary>
    private void DrawMoonAiming()
    {
        // These sliders used to disappear whenever no stage was applied, which
        // includes after every assembly reload because appliedStage is not
        // serialised. Falling back to stage 1 keeps aiming always reachable.
        var stage = appliedStage >= 1 ? appliedStage : 1;
        var preset = presets[stage - 1];

        EditorGUILayout.LabelField($"Aim the moon — stage {stage}: {preset.label}", EditorStyles.boldLabel);

        if (appliedStage < 1)
        {
            EditorGUILayout.HelpBox(
                "No stage previewed yet, so these edit stage 1. Click a stage button above to preview one.",
                MessageType.None);
        }

        EditorGUI.BeginChangeCheck();
        var azimuth = EditorGUILayout.Slider("Azimuth (compass)", preset.sky.moonAzimuth, 0f, 360f);
        var elevation = EditorGUILayout.Slider("Elevation", preset.sky.moonElevation, -10f, 85f);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(preset, "Aim moon");
            preset.sky.moonAzimuth = azimuth;
            preset.sky.moonElevation = elevation;
            EditorUtility.SetDirty(preset);
            ReapplyCurrent();
        }

        if (GUILayout.Button("Take azimuth from the Scene view (elevation unchanged)"))
        {
            AimMoonAtSceneView(preset);
        }

        if (GUILayout.Button("Copy this direction to all six stages"))
        {
            CopyMoonDirectionToAllStages(preset);
        }
    }

    private void AimMoonAtSceneView(StageSkyPreset preset)
    {
        var view = SceneView.lastActiveSceneView;
        if (view == null || view.camera == null)
        {
            EditorUtility.DisplayDialog("No Scene view",
                "Open a Scene view and point it out of the window first.", "OK");
            return;
        }

        var forward = view.camera.transform.forward;

        // Azimuth only. Taking the Scene view's pitch as well flattened the moon
        // onto the horizon, because looking at a window is a near-horizontal
        // view, and the moon then sat below the window opening. Elevation stays
        // on its slider, where it is an art decision rather than a side effect.
        Undo.RecordObject(preset, "Aim moon azimuth");
        preset.sky.moonAzimuth = Mathf.Repeat(Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg, 360f);
        EditorUtility.SetDirty(preset);
        ReapplyCurrent();
    }

    private void CopyMoonDirectionToAllStages(StageSkyPreset source)
    {
        foreach (var preset in presets)
        {
            if (preset == null || preset == source) continue;
            Undo.RecordObject(preset, "Copy moon direction");
            preset.sky.moonAzimuth = source.sky.moonAzimuth;
            preset.sky.moonElevation = source.sky.moonElevation;
            EditorUtility.SetDirty(preset);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"Moon direction copied to all stages: azimuth {source.sky.moonAzimuth:F1}, " +
                  $"elevation {source.sky.moonElevation:F1}.");
    }

    private void DrawScrub()
    {
        EditorGUILayout.LabelField("Scrub a transition", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            stageA = EditorGUILayout.IntSlider(stageA, 1, presets.Length, GUILayout.Width(150f));
            EditorGUILayout.LabelField("to", GUILayout.Width(18f));
            stageB = EditorGUILayout.IntSlider(stageB, 1, presets.Length, GUILayout.Width(150f));
        }

        EditorGUI.BeginChangeCheck();
        scrub = EditorGUILayout.Slider("Blend", scrub, 0f, 1f);
        if (EditorGUI.EndChangeCheck())
        {
            // SmoothStep matches the curve the runtime controller uses, so what
            // is scrubbed here is what plays.
            var t = Mathf.SmoothStep(0f, 1f, scrub);
            ApplySettings(SkySettings.Lerp(presets[stageA - 1].sky, presets[stageB - 1].sky, t));
            appliedStage = 0;
        }
    }

    private void DrawTargets()
    {
        EditorGUILayout.LabelField("What the preview drives", EditorStyles.boldLabel);

        liveRefresh = EditorGUILayout.Toggle(
            new GUIContent("Live refresh", "Follow Inspector edits to the preset in the Scene view."),
            liveRefresh);
        driveAmbient = EditorGUILayout.Toggle("Ambient (Trilight)", driveAmbient);
        driveFog = EditorGUILayout.Toggle("Fog", driveFog);
        driveLightShafts = EditorGUILayout.Toggle("Window light shafts", driveLightShafts);

        keyLight = (Light)EditorGUILayout.ObjectField("Moonlight", keyLight, typeof(Light), true);
        using (new EditorGUI.DisabledScope(keyLight == null))
        {
            driveKeyLight = EditorGUILayout.Toggle("Drive moonlight", driveKeyLight && keyLight != null);
        }

        if (keyLight == null)
        {
            EditorGUILayout.HelpBox(
                "No moonlight assigned. The scene has 16 shadow-casting lights, so this tool " +
                "will not guess which one is the moon. Drop a directional light here to let the " +
                "presets drive its colour, intensity and direction.",
                MessageType.Info);
        }

        postVolume = (Volume)EditorGUILayout.ObjectField("Post Volume", postVolume, typeof(Volume), true);
        using (new EditorGUI.DisabledScope(postVolume == null))
        {
            drivePostProcessing = EditorGUILayout.Toggle("Drive post processing", drivePostProcessing && postVolume != null);
        }

        if (drivePostProcessing)
        {
            EditorGUILayout.HelpBox(
                "Post processing is applied to a runtime instance of the profile. " +
                "The RoomContrast.asset on disk is not modified.",
                MessageType.None);
        }

        DrawTonemappingCompare();
    }

    /// <summary>
    /// RoomContrast currently overrides tonemapping to None, so anything brighter
    /// than white clips flat — including the sky's HDR moon and horizon glow.
    /// These three buttons make that an A/B decision instead of a guess.
    /// </summary>
    private void DrawTonemappingCompare()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Compare tonemapping", EditorStyles.boldLabel);

        if (postVolume == null)
        {
            EditorGUILayout.HelpBox("Assign a post Volume above to compare.", MessageType.None);
            return;
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            foreach (TonemappingMode mode in System.Enum.GetValues(typeof(TonemappingMode)))
            {
                if (!GUILayout.Button(mode.ToString(), GUILayout.Height(22f))) continue;
                SkyAtmosphereController.ApplyTonemapping(postVolume, mode);
                SceneView.RepaintAll();
                Debug.Log($"Tonemapping preview: {mode}. This changes a runtime instance only.");
            }
        }

        EditorGUILayout.HelpBox(
            "The scene ships with None. Switching changes how every material in the room reads, " +
            "not just the sky, so compare screenshots before committing.",
            MessageType.None);
    }

    private void DrawFooter()
    {
        EditorGUILayout.HelpBox(
            "Previewing changes RenderSettings in memory only and restores them when this " +
            "window closes. Nothing is saved to the scene unless you press the button below.",
            MessageType.None);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Restore", GUILayout.Height(22f)))
            {
                RestoreScene();
                appliedStage = 0;
                SceneView.RepaintAll();
            }

            if (GUILayout.Button("Write current look into scene", GUILayout.Height(22f)))
            {
                WriteIntoScene();
            }
        }
    }

    /// <summary>
    /// Re-applies whatever is currently being previewed. Driven at editor tick
    /// rate so edits made in the Inspector show up in the Scene view without
    /// having to press the stage button again.
    /// </summary>
    private void OnInspectorUpdate()
    {
        if (!liveRefresh || !captured) return;
        ReapplyCurrent();
    }

    private void ReapplyCurrent()
    {
        if (!PresetsReady()) return;

        if (appliedStage >= 1)
        {
            ApplySettings(presets[appliedStage - 1].sky);
            return;
        }

        var t = Mathf.SmoothStep(0f, 1f, scrub);
        ApplySettings(SkySettings.Lerp(presets[stageA - 1].sky, presets[stageB - 1].sky, t));
    }

    private void ApplySettings(SkySettings settings)
    {
        CaptureScene();
        EnsurePreviewMaterial();

        SkyAtmosphereController.ApplyToMaterial(previewMaterial, settings);
        RenderSettings.skybox = previewMaterial;

        if (driveAmbient) SkyAtmosphereController.ApplyAmbient(settings);
        if (driveFog) SkyAtmosphereController.ApplyFog(settings);
        if (driveKeyLight && keyLight != null) SkyAtmosphereController.ApplyKeyLight(keyLight, settings);
        if (driveLightShafts) SkyAtmosphereController.ApplyLightShafts(settings);
        if (drivePostProcessing && postVolume != null) SkyAtmosphereController.ApplyPostProcessing(postVolume, settings);

        SceneView.RepaintAll();
    }

    private void EnsurePreviewMaterial()
    {
        if (previewMaterial != null) return;

        var shader = Shader.Find("Room/Procedural Night Sky");
        if (shader == null)
        {
            Debug.LogError("Shader not found: Room/Procedural Night Sky.");
            return;
        }

        // HideAndDontSave keeps the preview material out of the scene and out of
        // the asset database, so previewing can never dirty a saved file.
        previewMaterial = new Material(shader)
        {
            name = "RoomProceduralSky (Preview)",
            hideFlags = HideFlags.HideAndDontSave,
        };
    }

    private void CaptureScene()
    {
        if (captured) return;

        capturedSkybox = RenderSettings.skybox;
        capturedAmbientMode = RenderSettings.ambientMode;
        capturedAmbientSky = RenderSettings.ambientSkyColor;
        capturedAmbientEquator = RenderSettings.ambientEquatorColor;
        capturedAmbientGround = RenderSettings.ambientGroundColor;
        capturedFogColor = RenderSettings.fogColor;
        capturedFogDensity = RenderSettings.fogDensity;

        capturedLight = keyLight;
        if (capturedLight != null)
        {
            capturedLightRotation = capturedLight.transform.rotation;
            capturedLightColor = capturedLight.color;
            capturedLightIntensity = capturedLight.intensity;
        }

        captured = true;
    }

    private void RestoreScene()
    {
        if (!captured) return;

        RenderSettings.skybox = capturedSkybox;
        RenderSettings.ambientMode = capturedAmbientMode;
        RenderSettings.ambientSkyColor = capturedAmbientSky;
        RenderSettings.ambientEquatorColor = capturedAmbientEquator;
        RenderSettings.ambientGroundColor = capturedAmbientGround;
        RenderSettings.fogColor = capturedFogColor;
        RenderSettings.fogDensity = capturedFogDensity;

        if (capturedLight != null)
        {
            capturedLight.transform.rotation = capturedLightRotation;
            capturedLight.color = capturedLightColor;
            capturedLight.intensity = capturedLightIntensity;
        }

        captured = false;
    }

    /// <summary>
    /// Bakes whatever is currently previewed into the saved sky material and the
    /// scene's RenderSettings, so the room opens on this look before the backend
    /// has reported any stage.
    /// </summary>
    private void WriteIntoScene()
    {
        if (previewMaterial == null)
        {
            EditorUtility.DisplayDialog("Nothing to write", "Preview a stage first.", "OK");
            return;
        }

        const string assetPath = "Assets/Resources/Sky/RoomProceduralSky.mat";
        var asset = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
        if (asset == null)
        {
            EditorUtility.DisplayDialog("Missing material",
                $"{assetPath} does not exist. Run Tools > Room > Sky > Create Stage Sky Assets.", "OK");
            return;
        }

        var confirmed = EditorUtility.DisplayDialog(
            "Write sky into scene",
            "This overwrites RoomProceduralSky.mat and sets the scene's skybox, ambient and fog " +
            "to the previewed look.\n\nThe scene is marked dirty; you still choose whether to save it.",
            "Write", "Cancel");

        if (!confirmed) return;

        Undo.RecordObject(asset, "Write sky into scene");
        asset.CopyPropertiesFromMaterial(previewMaterial);
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();

        RenderSettings.skybox = asset;
        capturedSkybox = asset;

        // The previewed RenderSettings values become the captured baseline, so
        // closing the window no longer reverts them.
        capturedAmbientMode = RenderSettings.ambientMode;
        capturedAmbientSky = RenderSettings.ambientSkyColor;
        capturedAmbientEquator = RenderSettings.ambientEquatorColor;
        capturedAmbientGround = RenderSettings.ambientGroundColor;
        capturedFogColor = RenderSettings.fogColor;
        capturedFogDensity = RenderSettings.fogDensity;

        if (capturedLight != null)
        {
            capturedLightRotation = capturedLight.transform.rotation;
            capturedLightColor = capturedLight.color;
            capturedLightIntensity = capturedLight.intensity;
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("Sky written into RoomProceduralSky.mat and the scene RenderSettings. Save the scene to keep it.");
    }
}
