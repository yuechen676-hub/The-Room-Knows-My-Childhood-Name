using UnityEngine;

/// <summary>
/// Shared planar-mirror helper. A mirror only shows the part of the reflection
/// that falls inside its own screen rectangle, so the reflection camera's
/// projection is narrowed to that rectangle: fewer renderers survive culling
/// and the render target shrinks while keeping the original texel density.
/// Mirror shaders sample through _ReflectionVP, which includes this projection.
/// </summary>
public static class PlanarReflectionScissor
{
    /// <summary>Diagnostics switch for same-process comparisons. Default on.</summary>
    public static bool Enabled = true;

    const int Bucket = 32;
    static readonly Vector3[] corners = new Vector3[8];

    /// <summary>
    /// Viewport rectangle (0..1) covering the mirror bounds. Returns the full
    /// viewport when disabled or when any corner is behind the near plane, and
    /// false when the rectangle is empty.
    /// </summary>
    public static bool Fit(Camera camera, Bounds bounds, out Rect rect)
    {
        rect = new Rect(0, 0, 1, 1);
        if (!Enabled) return true;
        Vector3 c = bounds.center, e = bounds.extents;
        for (int i = 0; i < 8; i++)
            corners[i] = c + Vector3.Scale(e, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        foreach (var corner in corners)
        {
            Vector3 v = camera.WorldToViewportPoint(corner);
            if (v.z <= camera.nearClipPlane) return true;
            minX = Mathf.Min(minX, v.x); maxX = Mathf.Max(maxX, v.x);
            minY = Mathf.Min(minY, v.y); maxY = Mathf.Max(maxY, v.y);
        }
        // Two-pixel margin keeps bilinear taps at the mirror edge inside the target.
        float padX = 2f / Mathf.Max(1, camera.pixelWidth), padY = 2f / Mathf.Max(1, camera.pixelHeight);
        minX = Mathf.Clamp01(minX - padX); maxX = Mathf.Clamp01(maxX + padX);
        minY = Mathf.Clamp01(minY - padY); maxY = Mathf.Clamp01(maxY + padY);
        if (maxX <= minX || maxY <= minY) return false;
        rect = Rect.MinMaxRect(minX, minY, maxX, maxY);
        return true;
    }

    /// <summary>Remaps the rectangle to the whole clip space of the source projection.</summary>
    public static Matrix4x4 Project(Matrix4x4 projection, Rect rect)
    {
        if (rect.width >= 1 && rect.height >= 1) return projection;
        var crop = Matrix4x4.identity;
        crop.m00 = 1 / rect.width;
        crop.m03 = (1 - 2 * rect.x - rect.width) / rect.width;
        crop.m11 = 1 / rect.height;
        crop.m13 = (1 - 2 * rect.y - rect.height) / rect.height;
        return crop * projection;
    }

    /// <summary>Target size for the rectangle at the mirror's full-view texel density.</summary>
    public static void Size(int fullWidth, int fullHeight, Rect rect, out int width, out int height)
    {
        // A collapsed window gives the camera an extreme aspect; never ask for more than the device allows.
        int limit = Mathf.Min(SystemInfo.maxTextureSize, 4096);
        width = Mathf.Min(limit, Mathf.Min(fullWidth, RoundUp(fullWidth * rect.width)));
        height = Mathf.Min(limit, Mathf.Min(fullHeight, RoundUp(fullHeight * rect.height)));
    }

    static int RoundUp(float v) => Mathf.Max(Bucket, Mathf.CeilToInt(v / Bucket) * Bucket);

    /// <summary>
    /// Pooled target: sizes change as the view turns, so targets come from the
    /// temporary pool and the previous one returns to it after the swap.
    /// </summary>
    public static RenderTexture Acquire(RenderTexture current, int width, int height, RenderTextureFormat format, string name)
    {
        if (current && current.width == width && current.height == height && current.format == format) return current;
        var next = RenderTexture.GetTemporary(width, height, 24, format);
        next.name = name;
        next.filterMode = FilterMode.Bilinear;
        next.wrapMode = TextureWrapMode.Clamp;
        if (current) RenderTexture.ReleaseTemporary(current);
        return next;
    }

    public static void Release(ref RenderTexture target)
    {
        if (target) RenderTexture.ReleaseTemporary(target);
        target = null;
    }
}
