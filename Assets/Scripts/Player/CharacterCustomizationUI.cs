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
    /// cycling + body shape preset. Builds preview in real-time. On confirm, saves
    /// to Wix and applies via LocalCharacterSync.
    ///
    /// Flow:
    /// - GameManager or LocalCharacterSync calls Show() when no saved avatar exists
    /// - Player input is frozen while open
    /// - On Confirm: appearance applied, saved to Wix, canvas hidden
    ///
    /// Lives in Assembly-CSharp (bridges SOTL.Multiplayer + SOTL.Player + SOTL.API).
    /// UI elements are created at runtime — no prefab needed.
    /// </summary>
    public class CharacterCustomizationUI : MonoBehaviour
    {
        // ── State ─────────────────────────────────────────────────────────
        private Canvas _canvas;
        private bool _isOpen;
        private bool _initialized;

        // Preset lists
        private List<SidekickPartPreset> _headPresets;
        private List<SidekickPartPreset> _upperPresets;
        private List<SidekickPartPreset> _lowerPresets;
        private List<SidekickBodyShapePreset> _bodyPresets;

        // Current indices
        private int _headIndex;
        private int _upperIndex;
        private int _lowerIndex;
        private int _bodyIndex;

        // UI references
        private Text _headLabel;
        private Text _upperLabel;
        private Text _lowerLabel;
        private Text _bodyLabel;
        private Text _titleLabel;

        // Preview
        private GameObject _previewCharacter;

        // ── Public API ────────────────────────────────────────────────────

        public bool IsOpen => _isOpen;

        public void Show()
        {
            if (!EnsureInit()) return;
            _canvas.enabled = true;
            _isOpen = true;
            FreezePlayerInput(true);
            UpdatePreview();
            Debug.Log("[SOTL Customize] Opened.");
        }

        public void Hide()
        {
            _canvas.enabled = false;
            _isOpen = false;
            FreezePlayerInput(false);
            CleanupPreview();
            Debug.Log("[SOTL Customize] Closed.");
        }

        // ── Lifecycle ─────────────────────────────────────────────────────

        void Start()
        {
            _canvas = GetComponent<Canvas>();
            if (_canvas == null) _canvas = gameObject.AddComponent<Canvas>();
            _canvas.enabled = false;
        }

        void Update()
        {
            // Allow keyboard toggle for testing (C key when not open)
            if (!_isOpen && UnityEngine.InputSystem.Keyboard.current != null &&
                UnityEngine.InputSystem.Keyboard.current.cKey.wasPressedThisFrame)
            {
                Show();
            }
        }

        // ── Init ──────────────────────────────────────────────────────────

        bool EnsureInit()
        {
            if (_initialized) return true;

            var mgr = SidekickCharacterManager.Instance;
            if (mgr == null || !mgr.IsReady)
            {
                Debug.LogWarning("[SOTL Customize] SidekickCharacterManager not ready.");
                return false;
            }

            _headPresets  = mgr.GetPresetsForGroup(PartGroup.Head);
            _upperPresets = mgr.GetPresetsForGroup(PartGroup.UpperBody);
            _lowerPresets = mgr.GetPresetsForGroup(PartGroup.LowerBody);
            _bodyPresets  = mgr.GetBodyShapePresets();

            if (_headPresets.Count == 0 || _upperPresets.Count == 0 || _lowerPresets.Count == 0)
            {
                Debug.LogError("[SOTL Customize] Not enough presets available.");
                return false;
            }

            // Randomize starting indices
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
            // Canvas setup
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100;

            var scaler = gameObject.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            if (gameObject.GetComponent<GraphicRaycaster>() == null)
                gameObject.AddComponent<GraphicRaycaster>();

            // Semi-transparent background
            var bg = CreatePanel("Background", transform);
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(0f, 0f, 0f, 0.85f);
            SetAnchors(bg, Vector2.zero, Vector2.one);

            // Right panel for controls
            var panel = CreatePanel("ControlPanel", bg.transform);
            SetAnchors(panel, new Vector2(0.55f, 0.05f), new Vector2(0.95f, 0.95f));
            var panelImg = panel.AddComponent<Image>();
            panelImg.color = new Color(0.12f, 0.12f, 0.15f, 0.95f);

            float y = -30f;

            // Title
            _titleLabel = CreateLabel(panel.transform, "CREATE YOUR CHARACTER", 28, TextAnchor.UpperCenter);
            SetRect(_titleLabel.rectTransform, 0f, y, 0f, 40f, new Vector2(0.05f, 1f), new Vector2(0.95f, 1f));
            y -= 80f;

            // Head row
            _headLabel = CreatePresetRow(panel.transform, "HEAD", y, OnHeadPrev, OnHeadNext);
            y -= 80f;

            // Upper body row
            _upperLabel = CreatePresetRow(panel.transform, "OUTFIT TOP", y, OnUpperPrev, OnUpperNext);
            y -= 80f;

            // Lower body row
            _lowerLabel = CreatePresetRow(panel.transform, "OUTFIT BOTTOM", y, OnLowerPrev, OnLowerNext);
            y -= 80f;

            // Body shape row
            if (_bodyPresets.Count > 0)
            {
                _bodyLabel = CreatePresetRow(panel.transform, "BODY SHAPE", y, OnBodyPrev, OnBodyNext);
                y -= 80f;
            }

            // Confirm button
            y -= 20f;
            var confirmGO = CreatePanel("ConfirmBtn", panel.transform);
            SetRect(confirmGO.GetComponent<RectTransform>(), 40f, y, -40f, 50f, new Vector2(0f, 1f), new Vector2(1f, 1f));
            var confirmImg = confirmGO.AddComponent<Image>();
            confirmImg.color = new Color(0.2f, 0.6f, 0.3f, 1f);
            var confirmBtn = confirmGO.AddComponent<Button>();
            confirmBtn.onClick.AddListener(OnConfirm);
            var confirmLabel = CreateLabel(confirmGO.transform, "CONFIRM", 22, TextAnchor.MiddleCenter);
            SetAnchors(confirmLabel.gameObject, Vector2.zero, Vector2.one);

            // Instruction text
            y -= 70f;
            var hint = CreateLabel(panel.transform, "Use arrows to cycle options.\nPress CONFIRM when ready.", 14, TextAnchor.UpperCenter);
            hint.color = new Color(0.6f, 0.6f, 0.6f);
            SetRect(hint.rectTransform, 0f, y, 0f, 50f, new Vector2(0.05f, 1f), new Vector2(0.95f, 1f));

            UpdateLabels();
        }

        Text CreatePresetRow(Transform parent, string label, float yPos,
                             UnityEngine.Events.UnityAction onPrev, UnityEngine.Events.UnityAction onNext)
        {
            var row = CreatePanel($"Row_{label}", parent);
            SetRect(row.GetComponent<RectTransform>(), 10f, yPos, -10f, 65f, new Vector2(0f, 1f), new Vector2(1f, 1f));

            // Category label
            var catLabel = CreateLabel(row.transform, label, 14, TextAnchor.UpperLeft);
            catLabel.color = new Color(0.7f, 0.7f, 0.7f);
            SetRect(catLabel.rectTransform, 10f, -2f, 0f, 20f, new Vector2(0f, 1f), new Vector2(1f, 1f));

            // Prev button
            var prevGO = CreatePanel("Prev", row.transform);
            SetRect(prevGO.GetComponent<RectTransform>(), 10f, -22f, 0f, 35f, new Vector2(0f, 1f), new Vector2(0f, 1f));
            prevGO.GetComponent<RectTransform>().sizeDelta = new Vector2(50f, 35f);
            var prevImg = prevGO.AddComponent<Image>();
            prevImg.color = new Color(0.25f, 0.25f, 0.3f);
            var prevBtn = prevGO.AddComponent<Button>();
            prevBtn.onClick.AddListener(onPrev);
            var prevLabel = CreateLabel(prevGO.transform, "<", 20, TextAnchor.MiddleCenter);
            SetAnchors(prevLabel.gameObject, Vector2.zero, Vector2.one);

            // Value label
            var valueLabel = CreateLabel(row.transform, "---", 18, TextAnchor.MiddleCenter);
            SetRect(valueLabel.rectTransform, 70f, -22f, -70f, 35f, new Vector2(0f, 1f), new Vector2(1f, 1f));

            // Next button
            var nextGO = CreatePanel("Next", row.transform);
            var nextRT = nextGO.GetComponent<RectTransform>();
            nextRT.anchorMin = new Vector2(1f, 1f);
            nextRT.anchorMax = new Vector2(1f, 1f);
            nextRT.pivot = new Vector2(1f, 1f);
            nextRT.anchoredPosition = new Vector2(-10f, -22f);
            nextRT.sizeDelta = new Vector2(50f, 35f);
            var nextImg = nextGO.AddComponent<Image>();
            nextImg.color = new Color(0.25f, 0.25f, 0.3f);
            var nextBtn = nextGO.AddComponent<Button>();
            nextBtn.onClick.AddListener(onNext);
            var nextLabel = CreateLabel(nextGO.transform, ">", 20, TextAnchor.MiddleCenter);
            SetAnchors(nextLabel.gameObject, Vector2.zero, Vector2.one);

            return valueLabel;
        }

        // ── Cycling callbacks ─────────────────────────────────────────────

        void OnHeadPrev()  { _headIndex  = Wrap(_headIndex  - 1, _headPresets.Count);  UpdateLabels(); UpdatePreview(); }
        void OnHeadNext()  { _headIndex  = Wrap(_headIndex  + 1, _headPresets.Count);  UpdateLabels(); UpdatePreview(); }
        void OnUpperPrev() { _upperIndex = Wrap(_upperIndex - 1, _upperPresets.Count); UpdateLabels(); UpdatePreview(); }
        void OnUpperNext() { _upperIndex = Wrap(_upperIndex + 1, _upperPresets.Count); UpdateLabels(); UpdatePreview(); }
        void OnLowerPrev() { _lowerIndex = Wrap(_lowerIndex - 1, _lowerPresets.Count); UpdateLabels(); UpdatePreview(); }
        void OnLowerNext() { _lowerIndex = Wrap(_lowerIndex + 1, _lowerPresets.Count); UpdateLabels(); UpdatePreview(); }
        void OnBodyPrev()  { if (_bodyPresets.Count > 0) { _bodyIndex = Wrap(_bodyIndex - 1, _bodyPresets.Count); UpdateLabels(); UpdatePreview(); } }
        void OnBodyNext()  { if (_bodyPresets.Count > 0) { _bodyIndex = Wrap(_bodyIndex + 1, _bodyPresets.Count); UpdateLabels(); UpdatePreview(); } }

        int Wrap(int idx, int count) => ((idx % count) + count) % count;

        void UpdateLabels()
        {
            if (_headLabel  != null) _headLabel.text  = CleanPresetName(_headPresets[_headIndex].Name);
            if (_upperLabel != null) _upperLabel.text  = CleanPresetName(_upperPresets[_upperIndex].Name);
            if (_lowerLabel != null) _lowerLabel.text  = CleanPresetName(_lowerPresets[_lowerIndex].Name);
            if (_bodyLabel  != null && _bodyPresets.Count > 0) _bodyLabel.text = _bodyPresets[_bodyIndex].Name;
        }

        string CleanPresetName(string name)
        {
            // "Modern Civilians 02" → "Modern Civilians 02" (already clean)
            // Truncate if too long
            return name.Length > 25 ? name.Substring(0, 22) + "..." : name;
        }

        // ── Preview ───────────────────────────────────────────────────────

        void UpdatePreview()
        {
            var data = BuildAppearanceData();
            var mgr = SidekickCharacterManager.Instance;
            if (mgr == null || !mgr.IsReady) return;

            CleanupPreview();
            _previewCharacter = mgr.BuildCharacter(data, "CustomizationPreview");
            if (_previewCharacter != null)
            {
                // Position in front of camera for preview
                var cam = Camera.main;
                if (cam != null)
                {
                    _previewCharacter.transform.position = cam.transform.position + cam.transform.forward * 3f;
                    _previewCharacter.transform.position = new Vector3(
                        _previewCharacter.transform.position.x,
                        0f,
                        _previewCharacter.transform.position.z);
                    _previewCharacter.transform.LookAt(new Vector3(cam.transform.position.x, 0f, cam.transform.position.z));
                }
            }
        }

        void CleanupPreview()
        {
            if (_previewCharacter != null)
            {
                Destroy(_previewCharacter);
                _previewCharacter = null;
            }
        }

        // ── Confirm ───────────────────────────────────────────────────────

        void OnConfirm()
        {
            var data = BuildAppearanceData();

            // Apply to local player
            var player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                var sync = player.GetComponent<LocalCharacterSync>();
                if (sync != null)
                {
                    sync.ApplyAppearance(data);
                    Debug.Log("[SOTL Customize] Appearance applied to local player.");
                }
            }

            // Save to Wix
            var api = SOTLApiManager.Instance;
            if (api != null && api.IsLinked)
            {
                api.SaveAvatar(data.ToJson(), ok =>
                {
                    if (ok) Debug.Log("[SOTL Customize] Avatar saved to Wix.");
                    else    Debug.LogWarning("[SOTL Customize] Avatar save failed (fail-open).");
                });
            }

            Hide();
        }

        // ── Build appearance data from current selections ─────────────────

        CharacterAppearanceData BuildAppearanceData()
        {
            var mgr = SidekickCharacterManager.Instance;
            var data = new CharacterAppearanceData();

            // Resolve presets into parts
            foreach (var entry in mgr.ResolvePreset(_headPresets[_headIndex]))
                data.SetPart(entry.slot, entry.name);
            foreach (var entry in mgr.ResolvePreset(_upperPresets[_upperIndex]))
                data.SetPart(entry.slot, entry.name);
            foreach (var entry in mgr.ResolvePreset(_lowerPresets[_lowerIndex]))
                data.SetPart(entry.slot, entry.name);

            // Body shape
            if (_bodyPresets.Count > 0)
            {
                var bp = _bodyPresets[_bodyIndex];
                data.bodyType = bp.BodyType;
                data.muscles  = bp.Musculature;
                if (bp.BodySize > 0)
                {
                    data.heavy  = bp.BodySize;
                    data.skinny = 0;
                }
                else
                {
                    data.skinny = -bp.BodySize;
                    data.heavy  = 0;
                }
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
        }

        // ── UI Helpers ────────────────────────────────────────────────────

        static GameObject CreatePanel(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        static Text CreateLabel(Transform parent, string text, int fontSize, TextAnchor alignment)
        {
            var go = new GameObject("Label", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.text = text;
            t.fontSize = fontSize;
            t.alignment = alignment;
            t.color = Color.white;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        static void SetAnchors(GameObject go, Vector2 min, Vector2 max)
        {
            var rt = go.GetComponent<RectTransform>();
            if (rt == null) return;
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static void SetRect(RectTransform rt, float left, float top, float right, float height, Vector2 anchorMin, Vector2 anchorMax)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(0f, 1f);
            rt.offsetMin = new Vector2(left, 0f);
            rt.offsetMax = new Vector2(right, 0f);
            rt.anchoredPosition = new Vector2(left, top);
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, height);
        }
    }
}
