using SOTL.API;
using SOTL.Multiplayer;
using Synty.SidekickCharacters.Database.DTO;
using Synty.SidekickCharacters.Enums;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SOTL.Player
{
    /// <summary>
    /// First-launch character creator. Preset-based: Head / Upper Body / Lower Body
    /// cycling + body shape preset. On confirm, saves to Wix and applies via LocalCharacterSync.
    ///
    /// UI elements built at runtime — no prefab needed.
    /// </summary>
    public class CharacterCustomizationUI : MonoBehaviour
    {
        private Canvas _canvas;
        private bool _isOpen;
        private bool _initialized;

        private List<SidekickPartPreset> _headPresets;
        private List<SidekickPartPreset> _upperPresets;
        private List<SidekickPartPreset> _lowerPresets;
        private List<SidekickBodyShapePreset> _bodyPresets;

        private int _headIndex, _upperIndex, _lowerIndex, _bodyIndex;
        private Text _headLabel, _upperLabel, _lowerLabel, _bodyLabel;

        public bool IsOpen => _isOpen;

        public void Show()
        {
            if (!EnsureInit()) return;
            _canvas.enabled = true;
            _isOpen = true;
            FreezePlayerInput(true);
            Debug.Log("[SOTL Customize] Opened.");
        }

        public void Hide()
        {
            _canvas.enabled = false;
            _isOpen = false;
            FreezePlayerInput(false);
            Debug.Log("[SOTL Customize] Closed.");
        }

        void Start()
        {
            _canvas = GetComponent<Canvas>();
            if (_canvas == null) _canvas = gameObject.AddComponent<Canvas>();
            _canvas.enabled = false;
        }

        void Update()
        {
            if (!_isOpen && UnityEngine.InputSystem.Keyboard.current != null &&
                UnityEngine.InputSystem.Keyboard.current.cKey.wasPressedThisFrame)
            {
                Show();
            }
        }

        bool EnsureInit()
        {
            if (_initialized) return true;

            var mgr = SidekickCharacterManager.Instance;
            if (mgr == null || !mgr.IsReady) return false;

            _headPresets  = mgr.GetPresetsForGroup(PartGroup.Head);
            _upperPresets = mgr.GetPresetsForGroup(PartGroup.UpperBody);
            _lowerPresets = mgr.GetPresetsForGroup(PartGroup.LowerBody);
            _bodyPresets  = mgr.GetBodyShapePresets();

            if (_headPresets.Count == 0 || _upperPresets.Count == 0 || _lowerPresets.Count == 0)
            {
                Debug.LogError("[SOTL Customize] Not enough presets.");
                return false;
            }

            _headIndex  = Random.Range(0, _headPresets.Count);
            _upperIndex = Random.Range(0, _upperPresets.Count);
            _lowerIndex = Random.Range(0, _lowerPresets.Count);
            _bodyIndex  = _bodyPresets.Count > 0 ? Random.Range(0, _bodyPresets.Count) : 0;

            BuildUI();
            _initialized = true;
            return true;
        }

        // ── UI Construction ───────────────────────────────────────────────

        void BuildUI()
        {
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100;

            var scaler = gameObject.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            if (gameObject.GetComponent<GraphicRaycaster>() == null)
                gameObject.AddComponent<GraphicRaycaster>();

            // ── Full-screen dark overlay ──
            var bg = MakeFullRect("Background", transform);
            var bgImg = bg.gameObject.AddComponent<Image>();
            bgImg.color = new Color(0f, 0f, 0f, 0.85f);
            bgImg.raycastTarget = true;

            // ── Right panel ──
            var panel = new GameObject("Panel", typeof(RectTransform)).GetComponent<RectTransform>();
            panel.SetParent(bg.transform, false);
            panel.anchorMin = new Vector2(0.55f, 0.05f);
            panel.anchorMax = new Vector2(0.95f, 0.95f);
            panel.offsetMin = panel.offsetMax = Vector2.zero;
            var panelImg = panel.gameObject.AddComponent<Image>();
            panelImg.color = new Color(0.12f, 0.12f, 0.15f, 0.95f);
            panelImg.raycastTarget = false;

            // ── Vertical layout via fixed positions ──
            float y = -30f;

            // Title
            MakeText(panel, "CREATE YOUR CHARACTER", 28, y, 40f);
            y -= 70f;

            // Preset rows
            _headLabel  = MakePresetRow(panel, "HEAD", y, () => CyclePreset(ref _headIndex, _headPresets, -1), () => CyclePreset(ref _headIndex, _headPresets, 1));
            y -= 90f;
            _upperLabel = MakePresetRow(panel, "OUTFIT TOP", y, () => CyclePreset(ref _upperIndex, _upperPresets, -1), () => CyclePreset(ref _upperIndex, _upperPresets, 1));
            y -= 90f;
            _lowerLabel = MakePresetRow(panel, "OUTFIT BOTTOM", y, () => CyclePreset(ref _lowerIndex, _lowerPresets, -1), () => CyclePreset(ref _lowerIndex, _lowerPresets, 1));
            y -= 90f;

            if (_bodyPresets.Count > 0)
            {
                _bodyLabel = MakePresetRow(panel, "BODY SHAPE", y, () => CyclePreset(ref _bodyIndex, _bodyPresets, -1), () => CyclePreset(ref _bodyIndex, _bodyPresets, 1));
                y -= 90f;
            }

            // ── Confirm button ──
            y -= 10f;
            var confirmRT = MakeRect("ConfirmBtn", panel);
            confirmRT.anchorMin = new Vector2(0.1f, 1f);
            confirmRT.anchorMax = new Vector2(0.9f, 1f);
            confirmRT.pivot = new Vector2(0.5f, 1f);
            confirmRT.anchoredPosition = new Vector2(0f, y);
            confirmRT.sizeDelta = new Vector2(0f, 55f);
            var cImg = confirmRT.gameObject.AddComponent<Image>();
            cImg.color = new Color(0.2f, 0.6f, 0.3f, 1f);
            var cBtn = confirmRT.gameObject.AddComponent<Button>();
            cBtn.targetGraphic = cImg;
            cBtn.onClick.AddListener(OnConfirm);
            var cLabel = MakeTextDirect(confirmRT, "CONFIRM", 24);
            cLabel.alignment = TextAnchor.MiddleCenter;
            cLabel.raycastTarget = false;

            UpdateLabels();
        }

        Text MakePresetRow(RectTransform parent, string category, float yPos,
                           UnityEngine.Events.UnityAction onPrev, UnityEngine.Events.UnityAction onNext)
        {
            // Row container
            var row = MakeRect("Row_" + category, parent);
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(1f, 1f);
            row.pivot = new Vector2(0.5f, 1f);
            row.anchoredPosition = new Vector2(0f, yPos);
            row.sizeDelta = new Vector2(-20f, 75f);

            // Category label
            var catText = MakeTextDirect(row, category, 13);
            catText.rectTransform.anchorMin = new Vector2(0f, 1f);
            catText.rectTransform.anchorMax = new Vector2(1f, 1f);
            catText.rectTransform.pivot = new Vector2(0.5f, 1f);
            catText.rectTransform.anchoredPosition = new Vector2(0f, 0f);
            catText.rectTransform.sizeDelta = new Vector2(0f, 22f);
            catText.alignment = TextAnchor.MiddleCenter;
            catText.color = new Color(0.6f, 0.6f, 0.6f);
            catText.raycastTarget = false;

            // Bottom row for [<]  label  [>]
            var btnRow = MakeRect("BtnRow", row);
            btnRow.anchorMin = new Vector2(0f, 0f);
            btnRow.anchorMax = new Vector2(1f, 1f);
            btnRow.offsetMin = new Vector2(0f, 0f);
            btnRow.offsetMax = new Vector2(0f, -24f);

            // Prev button — anchored left
            var prevRT = MakeRect("Prev", btnRow);
            prevRT.anchorMin = new Vector2(0f, 0f);
            prevRT.anchorMax = new Vector2(0f, 1f);
            prevRT.pivot = new Vector2(0f, 0.5f);
            prevRT.anchoredPosition = new Vector2(5f, 0f);
            prevRT.sizeDelta = new Vector2(55f, 0f);
            var prevImg = prevRT.gameObject.AddComponent<Image>();
            prevImg.color = new Color(0.3f, 0.3f, 0.35f);
            var prevBtn = prevRT.gameObject.AddComponent<Button>();
            prevBtn.targetGraphic = prevImg;
            prevBtn.onClick.AddListener(onPrev);
            var prevLabel = MakeTextDirect(prevRT, "<", 26);
            prevLabel.alignment = TextAnchor.MiddleCenter;
            prevLabel.raycastTarget = false;

            // Next button — anchored right
            var nextRT = MakeRect("Next", btnRow);
            nextRT.anchorMin = new Vector2(1f, 0f);
            nextRT.anchorMax = new Vector2(1f, 1f);
            nextRT.pivot = new Vector2(1f, 0.5f);
            nextRT.anchoredPosition = new Vector2(-5f, 0f);
            nextRT.sizeDelta = new Vector2(55f, 0f);
            var nextImg = nextRT.gameObject.AddComponent<Image>();
            nextImg.color = new Color(0.3f, 0.3f, 0.35f);
            var nextBtn = nextRT.gameObject.AddComponent<Button>();
            nextBtn.targetGraphic = nextImg;
            nextBtn.onClick.AddListener(onNext);
            var nextLabel = MakeTextDirect(nextRT, ">", 26);
            nextLabel.alignment = TextAnchor.MiddleCenter;
            nextLabel.raycastTarget = false;

            // Value label — centered between buttons
            var valueRT = MakeRect("Value", btnRow);
            valueRT.anchorMin = new Vector2(0f, 0f);
            valueRT.anchorMax = new Vector2(1f, 1f);
            valueRT.offsetMin = new Vector2(65f, 0f);
            valueRT.offsetMax = new Vector2(-65f, 0f);
            var valueText = valueRT.gameObject.AddComponent<Text>();
            valueText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            valueText.fontSize = 20;
            valueText.color = Color.white;
            valueText.alignment = TextAnchor.MiddleCenter;
            valueText.raycastTarget = false;

            return valueText;
        }

        // ── Cycling ───────────────────────────────────────────────────────

        void CyclePreset(ref int index, IList<SidekickPartPreset> presets, int dir)
        {
            index = ((index + dir) % presets.Count + presets.Count) % presets.Count;
            UpdateLabels();
        }

        void CyclePreset(ref int index, IList<SidekickBodyShapePreset> presets, int dir)
        {
            index = ((index + dir) % presets.Count + presets.Count) % presets.Count;
            UpdateLabels();
        }

        void UpdateLabels()
        {
            if (_headLabel  != null) _headLabel.text  = TrimName(_headPresets[_headIndex].Name);
            if (_upperLabel != null) _upperLabel.text  = TrimName(_upperPresets[_upperIndex].Name);
            if (_lowerLabel != null) _lowerLabel.text  = TrimName(_lowerPresets[_lowerIndex].Name);
            if (_bodyLabel  != null && _bodyPresets.Count > 0) _bodyLabel.text = _bodyPresets[_bodyIndex].Name;
        }

        string TrimName(string n) => n.Length > 28 ? n.Substring(0, 25) + "..." : n;

        // ── Confirm ───────────────────────────────────────────────────────

        void OnConfirm()
        {
            var data = BuildAppearanceData();

            var player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                var sync = player.GetComponent<LocalCharacterSync>();
                if (sync != null)
                {
                    sync.ApplyAppearance(data);
                    Debug.Log("[SOTL Customize] Appearance applied.");
                }
            }

            var api = SOTLApiManager.Instance;
            if (api != null && api.IsLinked)
            {
                api.SaveAvatar(data.ToJson(), ok =>
                    Debug.Log(ok ? "[SOTL Customize] Avatar saved to Wix." : "[SOTL Customize] Avatar save failed (fail-open)."));
            }

            Hide();
        }

        CharacterAppearanceData BuildAppearanceData()
        {
            var mgr = SidekickCharacterManager.Instance;
            var data = new CharacterAppearanceData();

            foreach (var e in mgr.ResolvePreset(_headPresets[_headIndex]))   data.SetPart(e.slot, e.name);
            foreach (var e in mgr.ResolvePreset(_upperPresets[_upperIndex])) data.SetPart(e.slot, e.name);
            foreach (var e in mgr.ResolvePreset(_lowerPresets[_lowerIndex])) data.SetPart(e.slot, e.name);

            if (_bodyPresets.Count > 0)
            {
                var bp = _bodyPresets[_bodyIndex];
                data.bodyType = bp.BodyType;
                data.muscles  = bp.Musculature;
                data.heavy  = bp.BodySize > 0 ? bp.BodySize  : 0;
                data.skinny = bp.BodySize < 0 ? -bp.BodySize : 0;
            }

            return data;
        }

        // ── Input freeze ──────────────────────────────────────────────────

        void FreezePlayerInput(bool freeze)
        {
            var player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                var ctrl = player.GetComponent<LotPlayerController>();
                if (ctrl != null) ctrl.enabled = !freeze;
            }

            // Unlock cursor for UI interaction, re-lock when done
            if (freeze)
            {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }
            else
            {
                Cursor.visible = false;
                Cursor.lockState = CursorLockMode.Locked;
            }

            // Disable camera controller so mouse look doesn't fight the UI
            var cam = Object.FindFirstObjectByType<LotCameraController>();
            if (cam != null) cam.enabled = !freeze;
        }

        // ── UI Helpers ────────────────────────────────────────────────────

        static RectTransform MakeFullRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        static RectTransform MakeRect(string name, RectTransform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        static void MakeText(RectTransform parent, string text, int fontSize, float yPos, float height)
        {
            var t = MakeTextDirect(parent, text, fontSize);
            t.alignment = TextAnchor.MiddleCenter;
            t.rectTransform.anchorMin = new Vector2(0.05f, 1f);
            t.rectTransform.anchorMax = new Vector2(0.95f, 1f);
            t.rectTransform.pivot = new Vector2(0.5f, 1f);
            t.rectTransform.anchoredPosition = new Vector2(0f, yPos);
            t.rectTransform.sizeDelta = new Vector2(0f, height);
            t.raycastTarget = false;
        }

        static Text MakeTextDirect(RectTransform parent, string text, int fontSize)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var t = go.AddComponent<Text>();
            t.text = text;
            t.fontSize = fontSize;
            t.color = Color.white;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return t;
        }
    }
}
