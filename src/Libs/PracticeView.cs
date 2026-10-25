using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using DB;
using Patice.Domain;
using Manager;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Patice.Libs;

// All positions are in the cabinet's 1080 x 1080 lower display, measured from its top left.
public sealed class PracticeView : IDisposable
{
    private const int UiLayer = 31;
    private readonly GameObject root;
    private readonly Camera gameCamera;
    private readonly int originalUiLayerMask;
    private readonly Camera uiCamera;
    private readonly RenderTexture live;
    private readonly List<Object> owned = new();
    private readonly Dictionary<int, CommonButtonObject> buttons = new();
    private readonly GameObject selector;
    private readonly GameObject controls;
    private readonly CommonWindow dialog;
    private readonly GenericMonitor messageMonitor;
    private bool exitVisible;
    private readonly TextMeshProUGUI exitChoice;
    private readonly Sprite confirmSymbol;
    private readonly RectTransform preview;
    private readonly TMP_FontAsset font;
    private readonly TextMeshProUGUI speed, mode, name, range, page, count;
    private readonly int monitor;
    private readonly Rect crop;
    private readonly Sprite circle;
    private readonly Sprite frame;

    public PracticeView(int monitor)
    {
        this.monitor = monitor;
        try
        {
            font = Resources.FindObjectsOfTypeAll<TMP_FontAsset>().FirstOrDefault(x => x.name == "SEGA_MaruGothicDB SDF")
                ?? throw new InvalidOperationException("Native SEGA_MaruGothicDB SDF font is not loaded.");
            var main = Camera.main;
            gameCamera = main;
            originalUiLayerMask = main.cullingMask & (1 << UiLayer);
            main.cullingMask &= ~(1 << UiLayer);
            live = new RenderTexture(Screen.width, Screen.height, 24, RenderTextureFormat.ARGB32);
            live.Create();
            CaptureGame();
            var playerWidth = Screen.height / 1920f * 1080;
            var center = Screen.width <= playerWidth * 1.1f ? Screen.width / 2f : Screen.width / 2f - playerWidth / 2 + monitor * playerWidth;
            crop = new Rect((center - playerWidth / 2) / Screen.width, 0, playerWidth / Screen.width, playerWidth / Screen.height);

            // Separate camera lets the original button particle systems render normally.
            uiCamera = new GameObject("Patice UI camera").AddComponent<Camera>();
            uiCamera.clearFlags = CameraClearFlags.Depth;
            uiCamera.cullingMask = 1 << UiLayer;
            uiCamera.depth = main.depth + 100;
            uiCamera.orthographic = true;
            uiCamera.orthographicSize = 960;
            uiCamera.transform.position = new Vector3(0, 0, -100);
            root = new GameObject("Patice", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = uiCamera;
            canvas.planeDistance = 10;
            canvas.sortingOrder = 30000;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1;
            var board = Rect("Lower display", root.transform, 0, 0, 1080, 1080);
            board.anchorMin = board.anchorMax = new Vector2(center / Screen.width, 0);
            board.pivot = new Vector2(.5f, 0);
            board.anchoredPosition = Vector2.zero;
            circle = MakeCircle();
            frame = LoadFrame();
            selector = Rect("Selector", board, 0, 0, 1080, 1080).gameObject;
            var background = Circle("Frozen background", selector.transform, 0, 0, 1080);
            var frozen = BlurSnapshot();
            Raw(background, frozen, new Rect(0, 0, 1, 1));
            preview = Circle("Native chart preview", selector.transform, 285, 290, 510);
            Raw(preview, live, crop);
            speed = Setting(selector.transform, 7);
            mode = Setting(selector.transform, 8);
            controls = Rect("Controls", board, 0, 0, 1080, 1080).gameObject;
            AddButton(controls.transform, 6, false, true);
            AddButton(controls.transform, 3, false, false);
            AddButton(controls.transform, 4, true, false);
            confirmSymbol = Symbol(buttons[4]).sprite;
            exitChoice = ButtonText(buttons[4], 40);
            exitChoice.gameObject.SetActive(false);
            name = Text(selector.transform, "Segment", 260, 810, 560, 65, 40);
            range = Text(selector.transform, "Range", 330, 880, 420, 45, 28);
            page = Text(selector.transform, "Index", 440, 944, 200, 40, 26);
            name.color = range.color = page.color = Color.white;
            count = Text(board, "Countdown", 390, 390, 300, 300, 180);
            count.color = Color.white;
            messageMonitor = Resources.FindObjectsOfTypeAll<GenericMonitor>()
                .First(x => x.gameObject.scene.IsValid() && x.MonitorId == monitor);
            var windowPrefab = Traverse.Create(messageMonitor).Field<CommonWindow>("_small").Value;
            dialog = Object.Instantiate(windowPrefab, board, false);
            dialog.SetVisible(false);
            Layer(root.transform);
        }
        catch { Dispose(); throw; }
    }

    public void Update(PracticeSession session)
    {
        var selecting = session.State == PracticeState.Selecting || session.State == PracticeState.Selected;
        selector.SetActive(selecting);
        controls.SetActive(selecting || session.State == PracticeState.ExitPrompt);
        var exiting = session.State == PracticeState.ExitPrompt;
        if (exiting != exitVisible)
        {
            exitVisible = exiting;
            if (exiting)
            {
                dialog.Prepare(messageMonitor, WindowMessageID.EntryConfirmGuest, WindowPositionID.Middle,
                    Vector3.zero, new Process.WindowParam { hideTitle = true, replaceText = true,
                        text = "要退出练习吗？", changeSize = true, sizeID = WindowSizeID.Middle });
                SetFrame(buttons[4]);
            }
            else
            {
                dialog.Close();
                buttons[4].SetSymbolSprite(confirmSymbol, false);
            }
        }
        dialog.UpdateView((long)(Time.unscaledDeltaTime * 1000));
        exitChoice.gameObject.SetActive(exiting);
        exitChoice.text = session.ConfirmExit ? "退出练习" : "继续练习";
        count.gameObject.SetActive(session.State == PracticeState.Countdown);
        count.text = session.Countdown.ToString();
        speed.text = session.Rate.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "X";
        mode.text = session.Loop ? "循环" : "单次";
        name.text = session.Current.name;
        range.text = $"{TimeSpan.FromMilliseconds(session.Current.startMs):mm\\:ss} — {TimeSpan.FromMilliseconds(session.Current.endMs):mm\\:ss}";
        page.text = $"{session.Index + 1} / {session.Count}";
        preview.localScale = Vector3.one * (session.State == PracticeState.Selected ? 1 + .06f * session.SelectionProgress : 1);
        foreach (var button in buttons.Values) button.ViewUpdate(Time.unscaledDeltaTime * 1000);

    }

    public void RenderPreview(PracticeState state)
    {
        if (state == PracticeState.Selecting || state == PracticeState.Selected) CaptureGame();
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

    public void Press(int button) => buttons[button].Pressed();

    // Clone the game's empty button anchors; preserve prefab offsets and animation transforms.
    private static Transform Slot(Transform parent, int number, string process)
    {
        var prefab = Resources.Load<GameObject>("Process/" + process + "/" + process + "Process");
        var source = prefab.transform.Find("Canvas/Main/UI_Buttons/UI_Button" + number.ToString("00"));
        return Object.Instantiate(source, parent, false);
    }

    private TextMeshProUGUI Setting(Transform parent, int number)
    {
        // The eight cabinet keys are 45 degrees apart. Rotate the native flat
        // button anchor around Main's centre, including its animation direction.
        var slot = Slot(parent, 4, "MusicSelect");
        var turn = Quaternion.AngleAxis(-45 * (number - 4), Vector3.forward);
        slot.localPosition = turn * slot.localPosition;
        slot.localRotation = turn * slot.localRotation;
        var button = Object.Instantiate(CommonPrefab.GetFlatButtonObject(), slot, false);
        Configure(button, number);
        SetFrame(button);
        var text = ButtonText(button, 40);
        text.rectTransform.localRotation = Quaternion.Euler(0, 0, 180);
        return text;
    }

    private void AddButton(Transform parent, int number, bool flat, bool flip)
    {
        var slot = Slot(parent, number, "MusicSelect");
        var button = Object.Instantiate(flat ? CommonPrefab.GetFlatButtonObject() : CommonPrefab.GetCirclebuButtonObject(), slot, false);
        Configure(button, number);
        button.SetSymbolSprite(flat ? ButtonControllerBase.GetFlatButtonParam(ButtonControllerBase.FlatButtonType.Set).Image
            : Resources.Load<Sprite>("Common/Sprites/Button/UI_CMN_Arrow"), flip);
    }

    private void Configure(CommonButtonObject button, int number)
    {
        button.Initialize(monitor, (InputManager.ButtonSetting)(number - 1), CommonButtonObject.LedColors.Yellow);
        button.SetSE(Mai2.Mai2Cue.Cue.SE_SYS_CURSOR);
        button.SetActiveImmediateButton(true);
        buttons[number] = button;
    }

    private static Image Symbol(CommonButtonObject button) =>
        Traverse.Create(button).Field<Image>("_symbolImage").Value;

    private void SetFrame(CommonButtonObject button)
    {
        var image = Symbol(button);
        var size = image.rectTransform.sizeDelta;
        button.SetSymbolSprite(frame, false);
        image.rectTransform.sizeDelta = size;
    }

    private TextMeshProUGUI ButtonText(CommonButtonObject button, float size)
    {
        var image = Symbol(button).rectTransform;
        var text = Text(image, "Value", 0, 0, image.rect.width, image.rect.height, size);
        var rect = text.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return text;
    }

    private TextMeshProUGUI Text(Transform parent, string id, float x, float y, float w, float h, float size)
    {
        var rect = Rect(id, parent, x, y, w, h);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        rect.sizeDelta = new Vector2(w, h);
        text.enableWordWrapping = false;
        text.font = font;
        text.fontSize = size;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color32(91, 76, 126, 255);
        text.raycastTarget = false;
        return text;
    }

    private static RectTransform Rect(string name, Transform parent, float x, float y, float w, float h)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = new Vector2(x + w / 2, -y - h / 2);
        rect.sizeDelta = new Vector2(w, h);
        return rect;
    }

    private RectTransform Circle(string name, Transform parent, float x, float y, float size)
    {
        var rect = Rect(name, parent, x, y, size, size);
        rect.gameObject.AddComponent<Image>().sprite = circle;
        rect.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        return rect;
    }

    private static void Raw(RectTransform parent, Texture texture, Rect uv)
    {
        var raw = Rect("Image", parent, 0, 0, parent.sizeDelta.x, parent.sizeDelta.y).gameObject.AddComponent<RawImage>();
        raw.texture = texture;
        raw.uvRect = uv;
        raw.raycastTarget = false;
    }

    private Sprite MakeCircle()
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

    private Sprite LoadFrame()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Patice.setting-frame.png");
        using var reader = new System.IO.BinaryReader(stream);
        var texture = new Texture2D(2, 2); owned.Add(texture);
        texture.LoadImage(reader.ReadBytes((int)stream.Length));
        var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f));
        owned.Add(sprite); return sprite;
    }

    private Texture2D BlurSnapshot()
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

    private static void Layer(Transform transform)
    {
        transform.gameObject.layer = UiLayer;
        foreach (Transform child in transform) Layer(child);
    }

    public void Dispose()
    {
        if (gameCamera != null) gameCamera.cullingMask = (gameCamera.cullingMask & ~(1 << UiLayer)) | originalUiLayerMask;
        if (uiCamera != null) Object.Destroy(uiCamera.gameObject);
        if (root != null) { root.SetActive(false); Object.Destroy(root); }
        if (live != null) { live.Release(); Object.Destroy(live); }
        foreach (var item in owned) if (item != null) Object.Destroy(item);
        owned.Clear();
    }
}
