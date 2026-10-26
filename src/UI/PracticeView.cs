using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using DB;
using Patice.Practice;
using Manager;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Patice.UI;

// All positions are in the cabinet's 1080 x 1080 lower display, measured from its top left.
public sealed class PracticeView : IPracticeView
{
    private const int UiLayer = 31;
    private readonly GameObject root;
    private readonly PreviewRenderer renderer;
    private readonly Camera uiCamera;
    private readonly Dictionary<int, CommonButtonObject> buttons = new();
    private readonly GameObject selector;
    private readonly GameObject controls;
    private readonly CommonWindow dialog;
    private readonly GenericMonitor messageMonitor;
    private bool exitVisible;
    private float buttonSyncTimer;
    private readonly RectTransform preview;
    private readonly TextMeshProUGUI nativeText;
    private readonly TextMeshProUGUI speed, seek, mode, name, range, page, count;
    private readonly int monitor;
    private readonly Sprite circle;

    public PracticeView(int monitor, Monitor.GameMonitor gameMonitor)
    {
        this.monitor = monitor;
        try
        {
            nativeText = gameMonitor.transform.Find("Canvas/Main/GameDispCtrl/UpperGauge/Base/Fast_Late/Title/FAST")
                .GetComponent<TextMeshProUGUI>();
            var main = Camera.main;
            renderer = new PreviewRenderer(monitor, UiLayer);
            var center = renderer.Center;

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
            circle = renderer.MakeCircle();
            selector = Rect("Selector", board, 0, 0, 1080, 1080).gameObject;
            var background = Circle("Frozen background", selector.transform, 0, 0, 1080);
            var frozen = renderer.BlurSnapshot();
            Raw(background, frozen, new Rect(0, 0, 1, 1));
            preview = Circle("Native chart preview", selector.transform, 285, 290, 510);
            Raw(preview, renderer.Live, renderer.Crop);
            speed = Setting(selector.transform, 1);
            seek = Setting(selector.transform, 2);
            seek.text = "Seek";
            mode = Setting(selector.transform, 8);
            controls = Rect("Controls", board, 0, 0, 1080, 1080).gameObject;
            AddButton(controls.transform, 6, false, true);
            AddButton(controls.transform, 3, false, false);
            AddButton(controls.transform, 4, true, false);
            AddButton(controls.transform, 5, true, false);
            buttons[5].SetSymbolSprite(ButtonControllerBase.GetFlatButtonParam(ButtonControllerBase.FlatButtonType.Cancel).Image, false);
            name = Text(selector.transform, "Segment", 260, 810, 560, 65, 40);
            range = Text(selector.transform, "Range", 330, 880, 420, 45, 28);
            page = Text(selector.transform, "Index", 440, 944, 200, 40, 26);
            name.color = range.color = page.color = Color.white;
            count = Text(board, "Countdown", 390, 390, 300, 300, 180);
            count.color = Color.white;
            messageMonitor = Resources.FindObjectsOfTypeAll<GenericMonitor>()
                .First(x => x.gameObject.scene.IsValid() && x.MonitorId == monitor);
            var windowPrefab = Traverse.Create(messageMonitor).Field<CommonWindow>("_small").Value;
            dialog = Object.Instantiate(windowPrefab, controls.transform, false);
            foreach (var label in dialog.GetComponentsInChildren<TextMeshProUGUI>(true)) ApplyTypography(label);
            dialog.SetVisible(false);
            Layer(root.transform);
        }
        catch { Dispose(); throw; }
    }

    public void Update(PracticeSession session)
    {
        var selecting = session.State == PracticeState.Selecting || session.State == PracticeState.Selected;
        var seeking = session.State == PracticeState.Seeking;
        selector.SetActive(selecting || seeking);
        controls.SetActive(session.State != PracticeState.Playing && session.State != PracticeState.Closed);
        var exiting = session.State == PracticeState.ExitPrompt;
        if (exiting != exitVisible)
        {
            exitVisible = exiting;
            if (exiting)
            {
                dialog.Prepare(messageMonitor, WindowMessageID.EntryConfirmGuest, WindowPositionID.Middle,
                    Vector3.zero, new Process.WindowParam { hideTitle = true, replaceText = true,
                        text = "Exit practice?", changeSize = true, sizeID = WindowSizeID.Middle });
            }
            else
            {
                dialog.Close();
            }
        }
        dialog.UpdateView((long)(Time.unscaledDeltaTime * 1000));
        buttons[3].transform.parent.gameObject.SetActive(selecting || seeking);
        buttons[4].transform.parent.gameObject.SetActive(selecting || seeking || exiting);
        buttons[6].transform.parent.gameObject.SetActive(selecting || seeking);
        count.gameObject.SetActive(session.State == PracticeState.Countdown);
        count.text = session.Countdown.ToString();
        speed.text = session.Rate.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "X";
        mode.text = session.Loop ? "LOOP" : "ONCE";
        buttons[1].transform.parent.gameObject.SetActive(selecting || seeking);
        buttons[2].transform.parent.gameObject.SetActive(selecting);
        buttons[8].transform.parent.gameObject.SetActive(selecting);
        name.gameObject.SetActive(!seeking);
        page.gameObject.SetActive(!seeking);
        name.text = session.Current.name;
        range.text = $"{TimeSpan.FromMilliseconds(session.Current.startMs):mm\\:ss} — {TimeSpan.FromMilliseconds(session.Current.endMs):mm\\:ss}";
        if (seeking) range.text = TimeSpan.FromMilliseconds(session.SeekPositionMs).ToString(@"mm\:ss");
        page.text = $"{session.Index + 1} / {session.Count}";
        preview.localScale = Vector3.one * (session.State == PracticeState.Selected ? 1 + .06f * session.SelectionProgress : 1);
        // Native ButtonControllerBase passes a shared one-second animation phase.
        buttonSyncTimer += Time.unscaledDeltaTime;
        foreach (var button in buttons.Values) button.ViewUpdate(buttonSyncTimer);
        if (buttonSyncTimer > 1f) buttonSyncTimer = 0f;

    }

    public void RenderPreview(PracticeState state) => renderer.Render(state);

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
        var slot = Slot(parent, number, "MusicSelectInfo");
        var button = Object.Instantiate(CommonPrefab.GetCirclebuButtonObject(), slot, false);
        Configure(button, number);
        Symbol(button).enabled = false;
        return ButtonText(button);
    }

    private TextMeshProUGUI ButtonText(CommonButtonObject button)
    {
        // Keep the native circle background, particles and press animation.
        var face = button.transform.Find("IMG_Top").GetComponent<RectTransform>();
        var text = Text(face, "Value", 0, 0, face.rect.width, face.rect.height, 28);
        text.color = Symbol(button).color;
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
        return text;
    }

    private void AddButton(Transform parent, int number, bool flat, bool flip)
    {
        var slot = Slot(parent, number, "MusicSelect");
        var button = Object.Instantiate(flat ? CommonPrefab.GetFlatButtonObject() : CommonPrefab.GetCirclebuButtonObject(), slot, false);
        Configure(button, number);
        button.SetSymbolSprite(flat ? ButtonControllerBase.GetFlatButtonParam(ButtonControllerBase.FlatButtonType.Ok).Image
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

    private TextMeshProUGUI Text(Transform parent, string id, float x, float y, float w, float h, float size)
    {
        var rect = Rect(id, parent, x, y, w, h);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        rect.sizeDelta = new Vector2(w, h);
        text.enableWordWrapping = false;
        ApplyTypography(text);
        text.fontSize = size;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    private void ApplyTypography(TextMeshProUGUI text)
    {
        text.font = nativeText.font;
        text.fontSharedMaterial = nativeText.fontSharedMaterial;
        text.fontStyle = nativeText.fontStyle;
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

    private static void Layer(Transform transform)
    {
        transform.gameObject.layer = UiLayer;
        foreach (Transform child in transform) Layer(child);
    }

    public void Dispose()
    {
        renderer?.Dispose();
        if (uiCamera != null) Object.Destroy(uiCamera.gameObject);
        if (root != null) { root.SetActive(false); Object.Destroy(root); }
    }
}
