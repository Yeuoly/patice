using System;
using System.Collections.Generic;
using Patice.Practice;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Patice.UI;

internal sealed class PreviewRenderer : IDisposable
{
    private readonly Camera gameCamera;
    private readonly int originalUiLayerMask;
    private readonly int uiLayer;
    private readonly RenderTexture live;
    private readonly Rect crop;
    private readonly List<Object> owned = new();
    internal float Center { get; }
    internal RenderTexture Live => live;
    internal Rect Crop => crop;

    internal PreviewRenderer(int monitor, int uiLayer)
    {
        this.uiLayer = uiLayer;
        gameCamera = Camera.main;
        originalUiLayerMask = gameCamera.cullingMask & (1 << uiLayer);
        try
        {
            gameCamera.cullingMask &= ~(1 << uiLayer);
            live = new RenderTexture(Screen.width, Screen.height, 24, RenderTextureFormat.ARGB32);
            live.Create();
            CaptureGame();
            var playerWidth = Screen.height / 1920f * 1080;
            Center = Screen.width <= playerWidth * 1.1f ? Screen.width / 2f : Screen.width / 2f - playerWidth / 2 + monitor * playerWidth;
            crop = new Rect((Center - playerWidth / 2) / Screen.width, 0, playerWidth / Screen.width, playerWidth / Screen.height);
        }
        catch { Dispose(); throw; }
    }

    internal void Render(PracticeState state)
    {
        if (state == PracticeState.Selecting || state == PracticeState.Selected || state == PracticeState.Seeking) CaptureGame();
    }

    private void CaptureGame()
    {
        // ScreenSpaceCamera canvases belong to the original camera, not a copy.
        var target = gameCamera.targetTexture;
        var viewport = gameCamera.rect;
        try
        {
            gameCamera.targetTexture = live;
            gameCamera.rect = new Rect(0, 0, 1, 1);
            gameCamera.Render();
        }
        finally
        {
            gameCamera.targetTexture = target;
            gameCamera.rect = viewport;
        }
    }

    internal Sprite MakeCircle()
    {
        const int size = 1080;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        owned.Add(texture);
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
        {
            var distance = Math.Sqrt((x - (size - 1) / 2.0) * (x - (size - 1) / 2.0) + (y - (size - 1) / 2.0) * (y - (size - 1) / 2.0));
            pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Math.Max(0, Math.Min(1, size / 2.0 - distance)) * 255));
        }
        texture.SetPixels32(pixels); texture.Apply();
        var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f));
        owned.Add(sprite); return sprite;
    }

    internal Texture2D BlurSnapshot()
    {
        var small = RenderTexture.GetTemporary(180, 180, 0);
        var previous = RenderTexture.active;
        var texture = new Texture2D(180, 180, TextureFormat.RGB24, false); owned.Add(texture);
        try
        {
            Graphics.Blit(live, small, new Vector2(crop.width, crop.height), new Vector2(crop.x, crop.y));
            RenderTexture.active = small;
            texture.ReadPixels(new Rect(0, 0, 180, 180), 0, 0);
            var source = texture.GetPixels();
            // Separable box blur, captured once; no per-frame CPU readback and no added tint.
            for (int pass = 0; pass < 3; pass++) for (int axis = 0; axis < 2; axis++)
            {
                var target = new Color[source.Length];
                for (int y = 0; y < 180; y++) for (int x = 0; x < 180; x++)
                {
                    Color sum = Color.clear;
                    for (int k = -4; k <= 4; k++) sum += source[Mathf.Clamp(y + (axis == 1 ? k : 0), 0, 179) * 180 + Mathf.Clamp(x + (axis == 0 ? k : 0), 0, 179)];
                    target[y * 180 + x] = sum / 9;
                }
                source = target;
            }
            texture.SetPixels(source); texture.Apply(); return texture;
        }
        finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(small); }
    }

    public void Dispose()
    {
        if (gameCamera != null) gameCamera.cullingMask = (gameCamera.cullingMask & ~(1 << uiLayer)) | originalUiLayerMask;
        if (live != null) { live.Release(); Object.Destroy(live); }
        foreach (var item in owned) if (item != null) Object.Destroy(item);
        owned.Clear();
    }
}
