using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Drives the procedural sky, the Trilight ambient, the fog, an optional
/// moonlight and an optional post stack from the narrative stage the backend
/// reports. Stage changes blend rather than cut.
///
/// The scene uses gradient ambient and has no baked lightmaps, so the sky can
/// change at runtime without a rebake, but it also means the sky does not light
/// the room by itself: the ambient colours are driven explicitly here.
/// </summary>
public sealed class SkyAtmosphereController : MonoBehaviour
{
    public const string PresetResourcePath = "Sky/StageSky";
    public const string MaterialResourcePath = "Sky/RoomProceduralSky";
    public const int StageCount = 6;

    public static SkyAtmosphereController Instance { get; private set; }

    [Header("Optional scene references")]
    [Tooltip("Directional light used as moonlight. Left empty the light is not touched.")]
    [SerializeField] private Light keyLight;

    [Tooltip("Global post-processing Volume. Found automatically when empty.")]
    [SerializeField] private Volume postVolume;

    [Header("What this controller is allowed to drive")]
    [SerializeField] private bool driveAmbient = true;
    [SerializeField] private bool driveFog = true;
    [SerializeField] private bool driveKeyLight = true;
    [SerializeField] private bool driveLightShafts = true;
    [SerializeField] private bool drivePostProcessing = true;

    private readonly StageSkyPreset[] presets = new StageSkyPreset[StageCount];
    private Material skyInstance;
    private Material originalSkybox;
    private SkySettings current;
    private Coroutine blend;
    private int currentStage = -1;
    private bool volumeSearched;
    private bool keyLightSearched;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureInstance()
    {
        if (Instance == null)
        {
            new GameObject("SkyAtmosphereController", typeof(SkyAtmosphereController));
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        for (var index = 0; index < presets.Length; index++)
        {
            presets[index] = Resources.Load<StageSkyPreset>($"{PresetResourcePath}{index + 1}");
            if (presets[index] == null)
            {
                Debug.LogError($"Missing sky preset: Resources/{PresetResourcePath}{index + 1}. " +
                               "Run Tools > Room > Sky > Create Stage Sky Assets.");
            }
        }
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        Instance = null;

        // Leave the scene's serialised skybox in place rather than the runtime clone.
        if (originalSkybox != null) RenderSettings.skybox = originalSkybox;
        if (skyInstance != null) Destroy(skyInstance);
    }

    /// <summary>Blends the sky to the given stage. Out-of-range stages are ignored.</summary>
    public void SetStage(int stage)
    {
        if (stage < 1 || stage > StageCount)
        {
            Debug.LogWarning($"Cannot apply sky for invalid stage {stage}.");
            return;
        }

        if (stage == currentStage) return;

        var preset = presets[stage - 1];
        if (preset == null) return;
        if (!EnsureSkyMaterial()) return;

        currentStage = stage;
        if (blend != null) StopCoroutine(blend);

        // Nothing to blend from on the first stage, so land on it directly.
        if (current == null || preset.transitionSeconds <= 0f)
        {
            current = preset.sky.Clone();
            ApplyCurrent();
            Debug.Log($"Sky set to stage {stage}: {preset.label}");
            return;
        }

        blend = StartCoroutine(BlendTo(preset));
    }

    public void ResetForRestart()
    {
        StopAllCoroutines();
        blend = null;
        currentStage = -1;
        current = null;
    }

    private IEnumerator BlendTo(StageSkyPreset preset)
    {
        var from = current.Clone();
        var to = preset.sky;
        var elapsed = 0f;

        while (elapsed < preset.transitionSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            var t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / preset.transitionSeconds));
            current = SkySettings.Lerp(from, to, t);
            ApplyCurrent();
            yield return null;
        }

        current = to.Clone();
        ApplyCurrent();
        blend = null;
        Debug.Log($"Sky blended to stage {preset.stage}: {preset.label}");
    }

    private void ApplyCurrent()
    {
        ApplyToMaterial(skyInstance, current);
        if (driveAmbient) ApplyAmbient(current);
        if (driveFog) ApplyFog(current);
        if (driveKeyLight) ApplyKeyLight(ResolveKeyLight(), current);
        if (driveLightShafts) ApplyLightShafts(current);
        if (drivePostProcessing) ApplyPostProcessing(ResolveVolume(), current);
    }

    /// <summary>
    /// Every shaft in the scene takes the same stage look. They register
    /// themselves, so nothing has to be wired up per window.
    /// </summary>
    public static void ApplyLightShafts(SkySettings settings)
    {
        if (settings == null) return;

        var shafts = RoomLightShaft.All;
        for (var index = 0; index < shafts.Count; index++)
        {
            shafts[index]?.SetLook(settings.shaftColor, settings.shaftIntensity, settings.shaftDustSpeed);
        }
    }

    /// <summary>
    /// The room has exactly one directional light, so it can be adopted as the
    /// moonlight without guessing. If that ever stops being true this returns
    /// null rather than picking one of several.
    /// </summary>
    private Light ResolveKeyLight()
    {
        if (keyLight != null) return keyLight;
        if (keyLightSearched) return null;

        keyLightSearched = true;
        Light found = null;

        var lights = FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var candidate in lights)
        {
            if (candidate.type != LightType.Directional) continue;
            if (found != null)
            {
                Debug.Log("More than one directional light in the scene; " +
                          "assign the moonlight on SkyAtmosphereController to drive it.");
                return null;
            }
            found = candidate;
        }

        if (found == null) Debug.Log("No directional light found; the sky is not driving a moonlight.");
        keyLight = found;
        return keyLight;
    }

    private bool EnsureSkyMaterial()
    {
        if (skyInstance != null) return true;

        var source = Resources.Load<Material>(MaterialResourcePath);
        if (source == null)
        {
            Debug.LogError($"Missing sky material: Resources/{MaterialResourcePath}. " +
                           "Run Tools > Room > Sky > Create Stage Sky Assets.");
            return false;
        }

        // Clone so runtime property writes never dirty the material asset.
        originalSkybox = RenderSettings.skybox;
        skyInstance = new Material(source) { name = source.name + " (Runtime)" };
        RenderSettings.skybox = skyInstance;
        return true;
    }

    private Volume ResolveVolume()
    {
        if (postVolume != null) return postVolume;
        if (volumeSearched) return null;

        volumeSearched = true;
        var volumes = FindObjectsByType<Volume>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var candidate in volumes)
        {
            if (!candidate.isGlobal) continue;
            if (postVolume == null || candidate.priority > postVolume.priority) postVolume = candidate;
        }

        if (postVolume == null) Debug.Log("No global Volume found; sky is not driving post processing.");
        return postVolume;
    }

    // --- Shared writers -----------------------------------------------------
    // Public and static so the editor preview window applies settings through
    // exactly the same code path as Play Mode.

    private static readonly int HorizonColorId = Shader.PropertyToID("_HorizonColor");
    private static readonly int ZenithColorId = Shader.PropertyToID("_ZenithColor");
    private static readonly int GroundColorId = Shader.PropertyToID("_GroundColor");
    private static readonly int HorizonHeightId = Shader.PropertyToID("_HorizonHeight");
    private static readonly int HorizonSoftnessId = Shader.PropertyToID("_HorizonSoftness");
    private static readonly int HorizonFalloffId = Shader.PropertyToID("_HorizonFalloff");
    private static readonly int GlowColorId = Shader.PropertyToID("_GlowColor");
    private static readonly int GlowIntensityId = Shader.PropertyToID("_GlowIntensity");
    private static readonly int GlowHeightId = Shader.PropertyToID("_GlowHeight");
    private static readonly int GlowFalloffId = Shader.PropertyToID("_GlowFalloff");
    private static readonly int GlowDirectionId = Shader.PropertyToID("_GlowDirection");
    private static readonly int GlowDirectionalityId = Shader.PropertyToID("_GlowDirectionality");
    private static readonly int StarDensityId = Shader.PropertyToID("_StarDensity");
    private static readonly int StarBrightnessId = Shader.PropertyToID("_StarBrightness");
    private static readonly int StarSizeId = Shader.PropertyToID("_StarSize");
    private static readonly int StarTwinkleId = Shader.PropertyToID("_StarTwinkle");
    private static readonly int StarTwinkleSpeedId = Shader.PropertyToID("_StarTwinkleSpeed");
    private static readonly int StarColorId = Shader.PropertyToID("_StarColor");
    private static readonly int MoonDirectionId = Shader.PropertyToID("_MoonDirection");
    private static readonly int MoonSizeId = Shader.PropertyToID("_MoonSize");
    private static readonly int MoonPhaseId = Shader.PropertyToID("_MoonPhase");
    private static readonly int MoonColorId = Shader.PropertyToID("_MoonColor");
    private static readonly int MoonIntensityId = Shader.PropertyToID("_MoonIntensity");
    private static readonly int MoonSurfaceId = Shader.PropertyToID("_MoonSurface");
    private static readonly int MoonHaloColorId = Shader.PropertyToID("_MoonHaloColor");
    private static readonly int MoonHaloSizeId = Shader.PropertyToID("_MoonHaloSize");
    private static readonly int MoonHaloIntensityId = Shader.PropertyToID("_MoonHaloIntensity");
    private static readonly int CloudCoverageId = Shader.PropertyToID("_CloudCoverage");
    private static readonly int CloudSoftnessId = Shader.PropertyToID("_CloudSoftness");
    private static readonly int CloudScaleId = Shader.PropertyToID("_CloudScale");
    private static readonly int CloudSpeedId = Shader.PropertyToID("_CloudSpeed");
    private static readonly int CloudDriftId = Shader.PropertyToID("_CloudDrift");
    private static readonly int CloudColorId = Shader.PropertyToID("_CloudColor");
    private static readonly int CloudLitColorId = Shader.PropertyToID("_CloudLitColor");
    private static readonly int CloudLightingId = Shader.PropertyToID("_CloudLighting");
    private static readonly int CloudOpacityId = Shader.PropertyToID("_CloudOpacity");
    private static readonly int RainAmountId = Shader.PropertyToID("_RainAmount");
    private static readonly int RainColorId = Shader.PropertyToID("_RainColor");
    private static readonly int ExposureId = Shader.PropertyToID("_Exposure");
    private static readonly int SkyTintId = Shader.PropertyToID("_SkyTint");

    public static void ApplyToMaterial(Material material, SkySettings settings)
    {
        if (material == null || settings == null) return;

        material.SetColor(HorizonColorId, settings.horizonColor);
        material.SetColor(ZenithColorId, settings.zenithColor);
        material.SetColor(GroundColorId, settings.groundColor);
        material.SetFloat(HorizonHeightId, settings.horizonHeight);
        material.SetFloat(HorizonSoftnessId, settings.horizonSoftness);
        material.SetFloat(HorizonFalloffId, settings.horizonFalloff);

        material.SetColor(GlowColorId, settings.glowColor);
        material.SetFloat(GlowIntensityId, settings.glowIntensity);
        material.SetFloat(GlowHeightId, settings.glowHeight);
        material.SetFloat(GlowFalloffId, settings.glowFalloff);
        material.SetVector(GlowDirectionId, settings.GlowDirection);
        material.SetFloat(GlowDirectionalityId, settings.glowDirectionality);

        material.SetFloat(StarDensityId, settings.starDensity);
        material.SetFloat(StarBrightnessId, settings.starBrightness);
        material.SetFloat(StarSizeId, settings.starSize);
        material.SetFloat(StarTwinkleId, settings.starTwinkle);
        material.SetFloat(StarTwinkleSpeedId, settings.starTwinkleSpeed);
        material.SetColor(StarColorId, settings.starColor);

        material.SetVector(MoonDirectionId, settings.MoonDirection);
        material.SetFloat(MoonSizeId, settings.moonSize);
        material.SetFloat(MoonPhaseId, settings.moonPhase);
        material.SetColor(MoonColorId, settings.moonColor);
        material.SetFloat(MoonIntensityId, settings.moonIntensity);
        material.SetFloat(MoonSurfaceId, settings.moonSurface);
        material.SetColor(MoonHaloColorId, settings.moonHaloColor);
        material.SetFloat(MoonHaloSizeId, settings.moonHaloSize);
        material.SetFloat(MoonHaloIntensityId, settings.moonHaloIntensity);

        material.SetFloat(CloudCoverageId, settings.cloudCoverage);
        material.SetFloat(CloudSoftnessId, settings.cloudSoftness);
        material.SetFloat(CloudScaleId, settings.cloudScale);
        material.SetFloat(CloudSpeedId, settings.cloudSpeed);
        material.SetVector(CloudDriftId, settings.CloudDrift);
        material.SetColor(CloudColorId, settings.cloudColor);
        material.SetColor(CloudLitColorId, settings.cloudLitColor);
        material.SetFloat(CloudLightingId, settings.cloudLighting);
        material.SetFloat(CloudOpacityId, settings.cloudOpacity);

        material.SetFloat(RainAmountId, settings.rainAmount);
        material.SetColor(RainColorId, settings.rainColor);

        material.SetFloat(ExposureId, settings.exposure);
        material.SetColor(SkyTintId, settings.skyTint);
    }

    public static void ApplyAmbient(SkySettings settings)
    {
        if (settings == null) return;

        // The scene authored gradient ambient; keep that mode so the sky never
        // silently takes over room lighting through an ambient probe bake.
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = settings.ambientSky;
        RenderSettings.ambientEquatorColor = settings.ambientEquator;
        RenderSettings.ambientGroundColor = settings.ambientGround;
    }

    public static void ApplyFog(SkySettings settings)
    {
        if (settings == null) return;

        RenderSettings.fogColor = settings.fogColor;
        RenderSettings.fogDensity = settings.fogDensity;
    }

    public static void ApplyKeyLight(Light light, SkySettings settings)
    {
        if (light == null || settings == null) return;

        light.color = settings.keyLightColor;
        light.intensity = settings.keyLightIntensity;

        if (!settings.alignKeyLightToMoon) return;

        // A directional light shines along its forward axis, so it must look
        // away from the moon to appear to come from it.
        light.transform.rotation = Quaternion.LookRotation(-settings.MoonDirection);
    }

    /// <summary>
    /// Tonemapping is a whole-project decision rather than a per-stage one, so it
    /// is set once here instead of living in <see cref="SkySettings"/>. The scene
    /// currently ships with it explicitly overridden to None, which hard-clips the
    /// sky's HDR moon and glow.
    /// </summary>
    public static void ApplyTonemapping(Volume volume, TonemappingMode mode)
    {
        if (volume == null) return;

        var profile = volume.profile;
        if (profile == null || !profile.TryGet<Tonemapping>(out var tonemapping)) return;

        tonemapping.mode.overrideState = true;
        tonemapping.mode.value = mode;
    }

    public static void ApplyPostProcessing(Volume volume, SkySettings settings)
    {
        if (volume == null || settings == null) return;

        // Volume.profile returns a runtime instance, so the shared .asset on
        // disk is never modified by this.
        var profile = volume.profile;
        if (profile == null) return;

        if (profile.TryGet<Vignette>(out var vignette))
        {
            vignette.intensity.overrideState = true;
            vignette.intensity.value = settings.vignetteIntensity;
        }

        if (profile.TryGet<Bloom>(out var bloom))
        {
            bloom.intensity.overrideState = true;
            bloom.intensity.value = settings.bloomIntensity;
        }

        if (profile.TryGet<ColorAdjustments>(out var colorAdjustments))
        {
            colorAdjustments.contrast.overrideState = true;
            colorAdjustments.contrast.value = settings.contrast;
            colorAdjustments.saturation.overrideState = true;
            colorAdjustments.saturation.value = settings.saturation;
            colorAdjustments.postExposure.overrideState = true;
            colorAdjustments.postExposure.value = settings.postExposure;
        }
    }
}
