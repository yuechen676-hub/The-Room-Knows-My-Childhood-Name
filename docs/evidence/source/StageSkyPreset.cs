using UnityEngine;

/// <summary>
/// Every value the sky, the environment lighting and the post stack take for one
/// narrative stage. Kept as a plain serialisable class so two stages can be
/// blended by <see cref="SkySettings.Lerp"/> without the controller having to
/// know the field list.
/// </summary>
[System.Serializable]
public sealed class SkySettings
{
    [Header("Gradient")]
    public Color horizonColor = new Color(0.22f, 0.14f, 0.11f);
    public Color zenithColor = new Color(0.035f, 0.05f, 0.11f);
    public Color groundColor = new Color(0.02f, 0.018f, 0.016f);
    [Range(-0.5f, 0.5f)] public float horizonHeight;
    [Range(0.01f, 1f)] public float horizonSoftness = 0.35f;
    [Range(0.2f, 4f)] public float horizonFalloff = 1.2f;

    [Header("Horizon glow")]
    [ColorUsage(false, true)] public Color glowColor = new Color(0.30f, 0.18f, 0.10f);
    [Range(0f, 4f)] public float glowIntensity = 0.35f;
    [Range(0.02f, 1f)] public float glowHeight = 0.18f;
    [Range(0.5f, 6f)] public float glowFalloff = 2f;
    [Range(0f, 360f)] public float glowAzimuth;
    [Range(0f, 8f)] public float glowDirectionality;

    [Header("Stars")]
    [Range(0f, 1f)] public float starDensity = 0.55f;
    [Range(0f, 4f)] public float starBrightness = 1.6f;
    [Range(0.5f, 4f)] public float starSize = 1f;
    [Range(0f, 1f)] public float starTwinkle = 0.35f;
    [Range(0f, 8f)] public float starTwinkleSpeed = 2f;
    [ColorUsage(false, true)] public Color starColor = new Color(0.85f, 0.88f, 1f);

    [Header("Moon")]
    [Range(0f, 360f)] public float moonAzimuth = 20f;
    [Range(-10f, 85f)] public float moonElevation = 28f;
    [Range(0.005f, 0.2f)] public float moonSize = 0.055f;
    [Range(0f, 1f)] public float moonPhase = 1f;
    [ColorUsage(false, true)] public Color moonColor = new Color(0.90f, 0.90f, 0.88f);
    [Range(0f, 6f)] public float moonIntensity = 2.2f;
    [Range(0f, 1f)] public float moonSurface = 0.60f;
    [ColorUsage(false, true)] public Color moonHaloColor = new Color(0.52f, 0.54f, 0.58f);
    [Range(0.02f, 1.5f)] public float moonHaloSize = 0.35f;
    [Range(0f, 3f)] public float moonHaloIntensity = 0.55f;

    [Header("Clouds")]
    [Range(0f, 1f)] public float cloudCoverage = 0.32f;
    [Range(0.02f, 1f)] public float cloudSoftness = 0.22f;
    [Range(0.1f, 8f)] public float cloudScale = 1f;
    [Range(0f, 0.2f)] public float cloudSpeed = 0.006f;
    [Range(0f, 360f)] public float cloudDriftAngle = 75f;
    public Color cloudColor = new Color(0.10f, 0.10f, 0.13f);
    public Color cloudLitColor = new Color(0.55f, 0.53f, 0.50f);
    [Range(0f, 1f)] public float cloudLighting = 0.8f;
    [Range(0f, 1f)] public float cloudOpacity = 0.85f;

    [Header("Rain veil")]
    [Range(0f, 1f)] public float rainAmount;
    public Color rainColor = new Color(0.115f, 0.12f, 0.135f);

    [Header("Sky output")]
    [Range(0f, 4f)] public float exposure = 1f;
    public Color skyTint = Color.white;

    [Header("Environment lighting (Trilight ambient)")]
    public Color ambientSky = new Color(0.32f, 0.34f, 0.38f);
    public Color ambientEquator = new Color(0.22f, 0.20f, 0.18f);
    public Color ambientGround = new Color(0.11f, 0.095f, 0.08f);

    [Header("Fog")]
    public Color fogColor = new Color(0.575f, 0.541f, 0.497f);
    [Range(0f, 0.15f)] public float fogDensity = 0.02f;

    [Header("Moonlight (only if a key light is assigned)")]
    public Color keyLightColor = new Color(0.72f, 0.78f, 0.95f);
    [Range(0f, 4f)] public float keyLightIntensity = 0.55f;
    [Tooltip("Rotate the assigned directional light so it shines from the moon.")]
    public bool alignKeyLightToMoon = true;

    [Header("Window light shaft")]
    [Tooltip("The sky is a small patch through the windows; this is what the player actually reads.")]
    [ColorUsage(false, true)] public Color shaftColor = new Color(0.72f, 0.78f, 0.95f);
    [Range(0f, 4f)] public float shaftIntensity = 0.6f;
    [Range(0f, 1f)] public float shaftDustSpeed = 0.05f;

    [Header("Post processing (only if a Volume is assigned)")]
    [Range(0f, 1f)] public float vignetteIntensity = 0.18f;
    [Range(0f, 4f)] public float bloomIntensity = 0.9f;
    [Range(-100f, 100f)] public float contrast = 12f;
    [Range(-100f, 100f)] public float saturation;
    [Range(-3f, 3f)] public float postExposure;

    /// <summary>World-space direction the moon sits in.</summary>
    public Vector3 MoonDirection => DirectionFrom(moonAzimuth, moonElevation);

    /// <summary>World-space compass direction the horizon glow leans towards.</summary>
    public Vector3 GlowDirection => DirectionFrom(glowAzimuth, 0f);

    public Vector2 CloudDrift
    {
        get
        {
            var radians = cloudDriftAngle * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }
    }

    private static Vector3 DirectionFrom(float azimuthDegrees, float elevationDegrees)
    {
        var azimuth = azimuthDegrees * Mathf.Deg2Rad;
        var elevation = elevationDegrees * Mathf.Deg2Rad;
        var horizontal = Mathf.Cos(elevation);
        return new Vector3(Mathf.Sin(azimuth) * horizontal, Mathf.Sin(elevation), Mathf.Cos(azimuth) * horizontal);
    }

    public SkySettings Clone()
    {
        return (SkySettings)MemberwiseClone();
    }

    /// <summary>
    /// Blends every field. Azimuths use <see cref="Mathf.LerpAngle"/> so a
    /// transition across 0/360 takes the short way round instead of sweeping the
    /// moon across the whole sky.
    /// </summary>
    public static SkySettings Lerp(SkySettings from, SkySettings to, float t)
    {
        var result = new SkySettings
        {
            horizonColor = Color.Lerp(from.horizonColor, to.horizonColor, t),
            zenithColor = Color.Lerp(from.zenithColor, to.zenithColor, t),
            groundColor = Color.Lerp(from.groundColor, to.groundColor, t),
            horizonHeight = Mathf.Lerp(from.horizonHeight, to.horizonHeight, t),
            horizonSoftness = Mathf.Lerp(from.horizonSoftness, to.horizonSoftness, t),
            horizonFalloff = Mathf.Lerp(from.horizonFalloff, to.horizonFalloff, t),

            glowColor = Color.Lerp(from.glowColor, to.glowColor, t),
            glowIntensity = Mathf.Lerp(from.glowIntensity, to.glowIntensity, t),
            glowHeight = Mathf.Lerp(from.glowHeight, to.glowHeight, t),
            glowFalloff = Mathf.Lerp(from.glowFalloff, to.glowFalloff, t),
            glowAzimuth = Mathf.LerpAngle(from.glowAzimuth, to.glowAzimuth, t),
            glowDirectionality = Mathf.Lerp(from.glowDirectionality, to.glowDirectionality, t),

            starDensity = Mathf.Lerp(from.starDensity, to.starDensity, t),
            starBrightness = Mathf.Lerp(from.starBrightness, to.starBrightness, t),
            starSize = Mathf.Lerp(from.starSize, to.starSize, t),
            starTwinkle = Mathf.Lerp(from.starTwinkle, to.starTwinkle, t),
            starTwinkleSpeed = Mathf.Lerp(from.starTwinkleSpeed, to.starTwinkleSpeed, t),
            starColor = Color.Lerp(from.starColor, to.starColor, t),

            moonAzimuth = Mathf.LerpAngle(from.moonAzimuth, to.moonAzimuth, t),
            moonElevation = Mathf.Lerp(from.moonElevation, to.moonElevation, t),
            moonSize = Mathf.Lerp(from.moonSize, to.moonSize, t),
            moonPhase = Mathf.Lerp(from.moonPhase, to.moonPhase, t),
            moonColor = Color.Lerp(from.moonColor, to.moonColor, t),
            moonIntensity = Mathf.Lerp(from.moonIntensity, to.moonIntensity, t),
            moonSurface = Mathf.Lerp(from.moonSurface, to.moonSurface, t),
            moonHaloColor = Color.Lerp(from.moonHaloColor, to.moonHaloColor, t),
            moonHaloSize = Mathf.Lerp(from.moonHaloSize, to.moonHaloSize, t),
            moonHaloIntensity = Mathf.Lerp(from.moonHaloIntensity, to.moonHaloIntensity, t),

            cloudCoverage = Mathf.Lerp(from.cloudCoverage, to.cloudCoverage, t),
            cloudSoftness = Mathf.Lerp(from.cloudSoftness, to.cloudSoftness, t),
            cloudScale = Mathf.Lerp(from.cloudScale, to.cloudScale, t),
            cloudSpeed = Mathf.Lerp(from.cloudSpeed, to.cloudSpeed, t),
            cloudDriftAngle = Mathf.LerpAngle(from.cloudDriftAngle, to.cloudDriftAngle, t),
            cloudColor = Color.Lerp(from.cloudColor, to.cloudColor, t),
            cloudLitColor = Color.Lerp(from.cloudLitColor, to.cloudLitColor, t),
            cloudLighting = Mathf.Lerp(from.cloudLighting, to.cloudLighting, t),
            cloudOpacity = Mathf.Lerp(from.cloudOpacity, to.cloudOpacity, t),

            rainAmount = Mathf.Lerp(from.rainAmount, to.rainAmount, t),
            rainColor = Color.Lerp(from.rainColor, to.rainColor, t),

            exposure = Mathf.Lerp(from.exposure, to.exposure, t),
            skyTint = Color.Lerp(from.skyTint, to.skyTint, t),

            ambientSky = Color.Lerp(from.ambientSky, to.ambientSky, t),
            ambientEquator = Color.Lerp(from.ambientEquator, to.ambientEquator, t),
            ambientGround = Color.Lerp(from.ambientGround, to.ambientGround, t),

            fogColor = Color.Lerp(from.fogColor, to.fogColor, t),
            fogDensity = Mathf.Lerp(from.fogDensity, to.fogDensity, t),

            keyLightColor = Color.Lerp(from.keyLightColor, to.keyLightColor, t),
            keyLightIntensity = Mathf.Lerp(from.keyLightIntensity, to.keyLightIntensity, t),
            alignKeyLightToMoon = t < 0.5f ? from.alignKeyLightToMoon : to.alignKeyLightToMoon,

            shaftColor = Color.Lerp(from.shaftColor, to.shaftColor, t),
            shaftIntensity = Mathf.Lerp(from.shaftIntensity, to.shaftIntensity, t),
            shaftDustSpeed = Mathf.Lerp(from.shaftDustSpeed, to.shaftDustSpeed, t),

            vignetteIntensity = Mathf.Lerp(from.vignetteIntensity, to.vignetteIntensity, t),
            bloomIntensity = Mathf.Lerp(from.bloomIntensity, to.bloomIntensity, t),
            contrast = Mathf.Lerp(from.contrast, to.contrast, t),
            saturation = Mathf.Lerp(from.saturation, to.saturation, t),
            postExposure = Mathf.Lerp(from.postExposure, to.postExposure, t),
        };
        return result;
    }
}

/// <summary>
/// One narrative stage's sky. Authored as an asset so the look can be iterated
/// without entering Play Mode and without touching code.
/// </summary>
[CreateAssetMenu(menuName = "Room/Stage Sky Preset", fileName = "StageSky")]
public sealed class StageSkyPreset : ScriptableObject
{
    [Header("Identity")]
    [Range(1, 6)] public int stage = 1;
    public string label = "";

    [Tooltip("Why the sky looks like this. Notes for the next iteration, not used at runtime.")]
    [TextArea(2, 6)] public string intent = "";

    [Header("Transition")]
    [Tooltip("Seconds to blend in from the previous stage. 0 applies instantly.")]
    [Min(0f)] public float transitionSeconds = 8f;

    [Header("Look")]
    public SkySettings sky = new SkySettings();
}
