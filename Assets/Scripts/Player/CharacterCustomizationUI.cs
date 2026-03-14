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
    /// Character creator UI. Left panel with controls, character visible on right.
    /// Preset cycling for Head/Outfit, real sliders for body shape.
    /// </summary>
    public class CharacterCustomizationUI : MonoBehaviour
    {
        private Canvas _canvas;
        private bool _isOpen;
        private bool _initialized;

        // Presets
        private List<SidekickPartPreset> _headPresets;
        private List<SidekickPartPreset> _upperPresets;
        private List<SidekickPartPreset> _lowerPresets;

        private int _headIndex, _upperIndex, _lowerIndex;
        private bool _isFeminine;
        private Text _headLabel, _upperLabel, _lowerLabel;

        // Sliders
        private Slider _bodyTypeSlider;   // 0=Masculine, 100=Feminine
        private Slider _bodySizeSlider;   // -100=Slim, +100=Heavy
        private Slider _muscleSlider;     // 0=Lean, 100=Muscular

        // Camera/Lighting for character creation mode
        private Transform _camOrigParent;
        private Vector3 _camOrigLocalPos;
        private Quaternion _camOrigLocalRot;
        private GameObject _backdrop;
        private GameObject _spotlight;
        private float _charRotation;
        private float _playerOrigRotY;
        private bool _isDragging;
        private float _dragStartX;

        public bool IsOpen => _isOpen;

        public void Show()
        {
            if (!EnsureInit()) return;
            _canvas.enabled = true;
            _isOpen = true;
            FreezePlayerInput(true);
            EnterCreationMode();
            Debug.Log("[SOTL Customize] Opened.");
        }

        public void Hide()
        {
            ExitCreationMode();
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
                Show();

            if (_isOpen)
                HandleCharacterRotation();
        }

        bool EnsureInit()
        {
            if (_initialized) return true;
            var mgr = SidekickCharacterManager.Instance;
            if (mgr == null || !mgr.IsReady) return false;

            _headPresets  = mgr.GetPresetsForGroup(PartGroup.Head);
            _upperPresets = mgr.GetPresetsForGroup(PartGroup.UpperBody);
            _lowerPresets = mgr.GetPresetsForGroup(PartGroup.LowerBody);

            if (_headPresets.Count == 0 || _upperPresets.Count == 0 || _lowerPresets.Count == 0)
            {
                Debug.LogError("[SOTL Customize] Not enough presets.");
                return false;
            }

            _headIndex  = Random.Range(0, _headPresets.Count);
            _upperIndex = Random.Range(0, _upperPresets.Count);
            _lowerIndex = Random.Range(0, _lowerPresets.Count);

            BuildUI();
            _initialized = true;
            return true;
        }

        // ═══════════════════════════════════════════════════════════════════
        // UI CONSTRUCTION
        // ═══════════════════════════════════════════════════════════════════

        void BuildUI()
        {
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100;

            var scaler = gameObject.GetComponent<CanvasScaler>() ?? gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            if (gameObject.GetComponent<GraphicRaycaster>() == null)
                gameObject.AddComponent<GraphicRaycaster>();

            // ── Left panel background (covers ~50% of screen) ──
            var panelGO = new GameObject("LeftPanel", typeof(RectTransform));
            panelGO.transform.SetParent(transform, false);
            var panelRT = panelGO.GetComponent<RectTransform>();
            panelRT.anchorMin = new Vector2(0f, 0f);
            panelRT.anchorMax = new Vector2(0.42f, 1f);
            panelRT.offsetMin = panelRT.offsetMax = Vector2.zero;
            var panelImg = panelGO.AddComponent<Image>();
            panelImg.color = new Color(0.08f, 0.07f, 0.1f, 1f);
            panelImg.raycastTarget = true; // block clicks from hitting game

            // ── Title ──
            float y = -50f;
            var title = MakeText(panelRT, "CREATE YOUR CHARACTER", 30, FontStyle.Bold);
            SetTopAnchored(title.rectTransform, 0.05f, 0.95f, y, 45f);
            y -= 70f;

            // ── Preset sections ──
            y = BuildPresetSection(panelRT, "STYLE", y, OnGenderPrev, OnGenderNext, out var genderLabel);
            _headLabel = genderLabel; // reuse variable — we'll set it below
            // Actually, need separate label for gender
            var _genderLabel = genderLabel;

            y = BuildPresetSection(panelRT, "HEAD", y, OnHeadPrev, OnHeadNext, out _headLabel);
            y = BuildPresetSection(panelRT, "OUTFIT TOP", y, OnUpperPrev, OnUpperNext, out _upperLabel);
            y = BuildPresetSection(panelRT, "OUTFIT BOTTOM", y, OnLowerPrev, OnLowerNext, out _lowerLabel);

            y -= 20f;

            // ── Divider ──
            var divider = new GameObject("Divider", typeof(RectTransform));
            divider.transform.SetParent(panelRT, false);
            var divRT = divider.GetComponent<RectTransform>();
            SetTopAnchored(divRT, 0.08f, 0.92f, y, 1f);
            var divImg = divider.AddComponent<Image>();
            divImg.color = new Color(1f, 1f, 1f, 0.15f);
            divImg.raycastTarget = false;
            y -= 25f;

            // ── Sliders ──
            _bodyTypeSlider = BuildSlider(panelRT, "BODY TYPE", "MASCULINE", "FEMININE", 0f, 100f, 0f, ref y);
            _bodyTypeSlider.onValueChanged.AddListener(_ => OnSliderChanged());

            _bodySizeSlider = BuildSlider(panelRT, "BODY SIZE", "SLIM", "HEAVY", -100f, 100f, 0f, ref y);
            _bodySizeSlider.onValueChanged.AddListener(_ => OnSliderChanged());

            _muscleSlider = BuildSlider(panelRT, "MUSCULATURE", "LEAN", "MUSCULAR", 0f, 100f, 50f, ref y);
            _muscleSlider.onValueChanged.AddListener(_ => OnSliderChanged());

            y -= 30f;

            // ── Buttons row ──
            var btnRow = new GameObject("ButtonRow", typeof(RectTransform));
            btnRow.transform.SetParent(panelRT, false);
            var btnRowRT = btnRow.GetComponent<RectTransform>();
            SetTopAnchored(btnRowRT, 0.08f, 0.92f, y, 50f);

            // Randomize button (left half)
            var randRT = MakeRect("RandomizeBtn", btnRowRT);
            randRT.anchorMin = new Vector2(0f, 0f);
            randRT.anchorMax = new Vector2(0.48f, 1f);
            randRT.offsetMin = randRT.offsetMax = Vector2.zero;
            var randImg = randRT.gameObject.AddComponent<Image>();
            randImg.color = new Color(0.25f, 0.25f, 0.3f, 1f);
            var randBtn = randRT.gameObject.AddComponent<Button>();
            randBtn.targetGraphic = randImg;
            randBtn.onClick.AddListener(OnRandomize);
            var randLabel = MakeText(randRT, "RANDOMIZE", 18, FontStyle.Normal);
            FillParent(randLabel.rectTransform);
            randLabel.alignment = TextAnchor.MiddleCenter;
            randLabel.raycastTarget = false;

            // Confirm button (right half)
            var confirmRT = MakeRect("ConfirmBtn", btnRowRT);
            confirmRT.anchorMin = new Vector2(0.52f, 0f);
            confirmRT.anchorMax = new Vector2(1f, 1f);
            confirmRT.offsetMin = confirmRT.offsetMax = Vector2.zero;
            var confirmImg = confirmRT.gameObject.AddComponent<Image>();
            confirmImg.color = new Color(0.2f, 0.55f, 0.3f, 1f);
            var confirmBtn = confirmRT.gameObject.AddComponent<Button>();
            confirmBtn.targetGraphic = confirmImg;
            confirmBtn.onClick.AddListener(OnConfirm);
            var confirmLabel = MakeText(confirmRT, "CONFIRM", 20, FontStyle.Bold);
            FillParent(confirmLabel.rectTransform);
            confirmLabel.alignment = TextAnchor.MiddleCenter;
            confirmLabel.raycastTarget = false;

            // Store gender label properly
            // We accidentally assigned genderLabel to _headLabel above — fix
            // Actually the BuildPresetSection out param handles it. Let me re-check.
            // The _genderLabel local is what we need. Let me wire UpdateLabels properly.

            // Fix: store reference so UpdateLabels can reach it
            _genderLabelRef = _genderLabel;

            UpdateLabels();
        }

        // Internal ref for gender label (built inline)
        private Text _genderLabelRef;

        // ── Preset section builder ────────────────────────────────────────

        float BuildPresetSection(RectTransform parent, string category, float yPos,
                                  UnityEngine.Events.UnityAction onPrev, UnityEngine.Events.UnityAction onNext,
                                  out Text valueLabel)
        {
            // Category label
            var catText = MakeText(parent, category, 13, FontStyle.Normal);
            SetTopAnchored(catText.rectTransform, 0.08f, 0.92f, yPos, 20f);
            catText.color = new Color(0.55f, 0.55f, 0.6f);
            catText.raycastTarget = false;
            yPos -= 22f;

            // Row container
            var row = MakeRect("Row_" + category, parent);
            SetTopAnchored(row, 0.08f, 0.92f, yPos, 40f);

            // Prev button
            var prevRT = MakeRect("Prev", row);
            prevRT.anchorMin = new Vector2(0f, 0f);
            prevRT.anchorMax = new Vector2(0f, 1f);
            prevRT.pivot = new Vector2(0f, 0.5f);
            prevRT.anchoredPosition = Vector2.zero;
            prevRT.sizeDelta = new Vector2(45f, 0f);
            var prevImg = prevRT.gameObject.AddComponent<Image>();
            prevImg.color = new Color(0.22f, 0.22f, 0.27f, 1f);
            var prevBtn = prevRT.gameObject.AddComponent<Button>();
            prevBtn.targetGraphic = prevImg;
            prevBtn.onClick.AddListener(onPrev);
            var pl = MakeText(prevRT, "◀", 18, FontStyle.Normal);
            FillParent(pl.rectTransform);
            pl.alignment = TextAnchor.MiddleCenter;
            pl.raycastTarget = false;

            // Next button
            var nextRT = MakeRect("Next", row);
            nextRT.anchorMin = new Vector2(1f, 0f);
            nextRT.anchorMax = new Vector2(1f, 1f);
            nextRT.pivot = new Vector2(1f, 0.5f);
            nextRT.anchoredPosition = Vector2.zero;
            nextRT.sizeDelta = new Vector2(45f, 0f);
            var nextImg = nextRT.gameObject.AddComponent<Image>();
            nextImg.color = new Color(0.22f, 0.22f, 0.27f, 1f);
            var nextBtn = nextRT.gameObject.AddComponent<Button>();
            nextBtn.targetGraphic = nextImg;
            nextBtn.onClick.AddListener(onNext);
            var nl = MakeText(nextRT, "▶", 18, FontStyle.Normal);
            FillParent(nl.rectTransform);
            nl.alignment = TextAnchor.MiddleCenter;
            nl.raycastTarget = false;

            // Value label (centered between buttons)
            var valRT = MakeRect("Value", row);
            valRT.anchorMin = new Vector2(0f, 0f);
            valRT.anchorMax = new Vector2(1f, 1f);
            valRT.offsetMin = new Vector2(50f, 0f);
            valRT.offsetMax = new Vector2(-50f, 0f);
            valueLabel = valRT.gameObject.AddComponent<Text>();
            valueLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            valueLabel.fontSize = 20;
            valueLabel.color = Color.white;
            valueLabel.alignment = TextAnchor.MiddleCenter;
            valueLabel.raycastTarget = false;

            yPos -= 50f;
            return yPos;
        }

        // ── Slider builder ────────────────────────────────────────────────

        Slider BuildSlider(RectTransform parent, string label, string leftLabel, string rightLabel,
                           float min, float max, float defaultVal, ref float yPos)
        {
            // Label
            var catText = MakeText(parent, label, 13, FontStyle.Normal);
            SetTopAnchored(catText.rectTransform, 0.08f, 0.92f, yPos, 20f);
            catText.color = new Color(0.55f, 0.55f, 0.6f);
            catText.raycastTarget = false;
            yPos -= 24f;

            // Slider container
            var sliderGO = new GameObject("Slider_" + label, typeof(RectTransform));
            sliderGO.transform.SetParent(parent, false);
            var sliderRT = sliderGO.GetComponent<RectTransform>();
            SetTopAnchored(sliderRT, 0.08f, 0.92f, yPos, 24f);

            // Background bar
            var bgGO = new GameObject("Background", typeof(RectTransform));
            bgGO.transform.SetParent(sliderGO.transform, false);
            var bgRT = bgGO.GetComponent<RectTransform>();
            FillParent(bgRT);
            // Thin bar centered vertically
            bgRT.anchorMin = new Vector2(0f, 0.35f);
            bgRT.anchorMax = new Vector2(1f, 0.65f);
            bgRT.offsetMin = bgRT.offsetMax = Vector2.zero;
            var bgImg = bgGO.AddComponent<Image>();
            bgImg.color = new Color(0.3f, 0.3f, 0.35f, 1f);

            // Fill area
            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(sliderGO.transform, false);
            var fillAreaRT = fillArea.GetComponent<RectTransform>();
            fillAreaRT.anchorMin = new Vector2(0f, 0.35f);
            fillAreaRT.anchorMax = new Vector2(1f, 0.65f);
            fillAreaRT.offsetMin = fillAreaRT.offsetMax = Vector2.zero;

            var fill = new GameObject("Fill", typeof(RectTransform));
            fill.transform.SetParent(fillArea.transform, false);
            var fillRT = fill.GetComponent<RectTransform>();
            FillParent(fillRT);
            var fillImg = fill.AddComponent<Image>();
            fillImg.color = new Color(0.5f, 0.5f, 0.55f, 0.6f);

            // Handle
            var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(sliderGO.transform, false);
            var handleAreaRT = handleArea.GetComponent<RectTransform>();
            FillParent(handleAreaRT);

            var handle = new GameObject("Handle", typeof(RectTransform));
            handle.transform.SetParent(handleArea.transform, false);
            var handleRT = handle.GetComponent<RectTransform>();
            handleRT.sizeDelta = new Vector2(16f, 28f);
            var handleImg = handle.AddComponent<Image>();
            handleImg.color = new Color(0.85f, 0.85f, 0.88f, 1f);

            // Slider component
            var slider = sliderGO.AddComponent<Slider>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = defaultVal;
            slider.targetGraphic = handleImg;
            slider.fillRect = fillRT;
            slider.handleRect = handleRT;

            yPos -= 28f;

            // End labels row
            var labelsRT = MakeRect("Labels", parent);
            SetTopAnchored(labelsRT, 0.08f, 0.92f, yPos, 18f);

            var leftText = MakeText(labelsRT, leftLabel, 12, FontStyle.Normal);
            leftText.rectTransform.anchorMin = new Vector2(0f, 0f);
            leftText.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            leftText.rectTransform.offsetMin = leftText.rectTransform.offsetMax = Vector2.zero;
            leftText.alignment = TextAnchor.MiddleLeft;
            leftText.color = new Color(0.5f, 0.5f, 0.55f);
            leftText.raycastTarget = false;

            var rightText = MakeText(labelsRT, rightLabel, 12, FontStyle.Normal);
            rightText.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            rightText.rectTransform.anchorMax = new Vector2(1f, 1f);
            rightText.rectTransform.offsetMin = rightText.rectTransform.offsetMax = Vector2.zero;
            rightText.alignment = TextAnchor.MiddleRight;
            rightText.color = new Color(0.5f, 0.5f, 0.55f);
            rightText.raycastTarget = false;

            yPos -= 35f;
            return slider;
        }

        // ═══════════════════════════════════════════════════════════════════
        // CHARACTER CREATION MODE (camera, lighting, rotation)
        // ═══════════════════════════════════════════════════════════════════

        void EnterCreationMode()
        {
            var player = GameObject.FindWithTag("Player");
            if (player == null) return;

            _playerOrigRotY = player.transform.eulerAngles.y;

            // ── Camera: detach from rig, position in front of player ──
            var cam = Camera.main;
            if (cam != null)
            {
                _camOrigParent   = cam.transform.parent;
                _camOrigLocalPos = cam.transform.localPosition;
                _camOrigLocalRot = cam.transform.localRotation;

                // Detach from camera rig
                cam.transform.SetParent(null, true);

                // Position camera in front of player, at chest height, looking at center mass
                var playerPos = player.transform.position;
                var lookTarget = playerPos + Vector3.up * 0.95f;
                // Place camera forward of the player, offset right so char appears right of center
                var camPos = playerPos + Vector3.up * 1.0f + player.transform.forward * 2.4f;
                camPos += player.transform.right * 0.3f;

                cam.transform.position = camPos;
                cam.transform.LookAt(lookTarget);

                // Face the player toward the camera
                player.transform.LookAt(new Vector3(camPos.x, playerPos.y, camPos.z));
                _charRotation = player.transform.eulerAngles.y;
            }

            // ── Backdrop: large 3D quad behind the player ──
            _backdrop = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _backdrop.name = "CreationBackdrop";
            // Remove collider so it doesn't interfere with gameplay
            var col = _backdrop.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            // Use URP Unlit material
            var bdMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            bdMat.color = new Color(0.06f, 0.05f, 0.08f, 1f);
            _backdrop.GetComponent<Renderer>().material = bdMat;
            // Position behind the player relative to camera view
            var camFwd = cam.transform.forward;
            var behindPlayer = player.transform.position - camFwd * 2.5f;
            _backdrop.transform.position = behindPlayer + Vector3.up * 1.5f;
            _backdrop.transform.localScale = new Vector3(15f, 10f, 1f);
            // Face the camera
            _backdrop.transform.rotation = cam.transform.rotation;

            // ── Spotlight on the character ──
            _spotlight = new GameObject("CreationSpotlight");
            var light = _spotlight.AddComponent<Light>();
            light.type = LightType.Spot;
            light.color = new Color(1f, 0.95f, 0.85f);
            light.intensity = 8f;
            light.range = 10f;
            light.spotAngle = 50f;
            light.shadows = LightShadows.Soft;
            _spotlight.transform.position = player.transform.position + Vector3.up * 3.5f + player.transform.forward * 1.5f;
            _spotlight.transform.LookAt(player.transform.position + Vector3.up * 0.8f);

            // ── Hide HUD and other overlays ──
            SetCanvasesVisible(false);
        }

        void ExitCreationMode()
        {
            // ── Restore camera ──
            var cam = Camera.main;
            if (cam != null && _camOrigParent != null)
            {
                cam.transform.SetParent(_camOrigParent, false);
                cam.transform.localPosition = _camOrigLocalPos;
                cam.transform.localRotation = _camOrigLocalRot;
            }

            // ── Restore player rotation ──
            var player = GameObject.FindWithTag("Player");
            if (player != null)
                player.transform.eulerAngles = new Vector3(0f, _playerOrigRotY, 0f);

            // ── Clean up backdrop and light ──
            if (_backdrop != null) { Destroy(_backdrop); _backdrop = null; }
            if (_spotlight != null) { Destroy(_spotlight); _spotlight = null; }

            // ── Show HUD again ──
            SetCanvasesVisible(true);
        }

        void HandleCharacterRotation()
        {
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse == null) return;

            // Only rotate when right mouse button is held OR when dragging in the right half of screen
            bool mouseDown = mouse.leftButton.isPressed;
            float mouseX = mouse.position.ReadValue().x;
            bool inCharacterArea = mouseX > Screen.width * 0.48f; // right side of screen

            if (mouseDown && inCharacterArea)
            {
                if (!_isDragging)
                {
                    _isDragging = true;
                    _dragStartX = mouse.position.ReadValue().x;
                }
                else
                {
                    float deltaX = mouse.position.ReadValue().x - _dragStartX;
                    _dragStartX = mouse.position.ReadValue().x;
                    _charRotation -= deltaX * 0.5f;

                    var player = GameObject.FindWithTag("Player");
                    if (player != null)
                        player.transform.eulerAngles = new Vector3(0f, _charRotation, 0f);
                }
            }
            else
            {
                _isDragging = false;
            }
        }

        void SetCanvasesVisible(bool visible)
        {
            // Hide/show all canvases except this one and LinkOverlay
            var names = new[] { "StatsCanvas", "HUD_MilitaryCombat_LooterShooter", "DialogueCanvas" };
            foreach (var n in names)
            {
                var go = GameObject.Find(n);
                if (go != null) go.SetActive(visible);
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // CALLBACKS
        // ═══════════════════════════════════════════════════════════════════

        void OnGenderPrev() { _isFeminine = !_isFeminine; UpdateLabels(); ApplyLivePreview(); }
        void OnGenderNext() { _isFeminine = !_isFeminine; UpdateLabels(); ApplyLivePreview(); }
        void OnHeadPrev()   { _headIndex  = Wrap(_headIndex  - 1, _headPresets.Count);  UpdateLabels(); ApplyLivePreview(); }
        void OnHeadNext()   { _headIndex  = Wrap(_headIndex  + 1, _headPresets.Count);  UpdateLabels(); ApplyLivePreview(); }
        void OnUpperPrev()  { _upperIndex = Wrap(_upperIndex - 1, _upperPresets.Count); UpdateLabels(); ApplyLivePreview(); }
        void OnUpperNext()  { _upperIndex = Wrap(_upperIndex + 1, _upperPresets.Count); UpdateLabels(); ApplyLivePreview(); }
        void OnLowerPrev()  { _lowerIndex = Wrap(_lowerIndex - 1, _lowerPresets.Count); UpdateLabels(); ApplyLivePreview(); }
        void OnLowerNext()  { _lowerIndex = Wrap(_lowerIndex + 1, _lowerPresets.Count); UpdateLabels(); ApplyLivePreview(); }

        void OnSliderChanged() { ApplyLivePreview(); }

        void OnRandomize()
        {
            _headIndex  = Random.Range(0, _headPresets.Count);
            _upperIndex = Random.Range(0, _upperPresets.Count);
            _lowerIndex = Random.Range(0, _lowerPresets.Count);
            _isFeminine = Random.value > 0.5f;

            _bodyTypeSlider.value = Random.Range(0f, 100f);
            _bodySizeSlider.value = Random.Range(-100f, 100f);
            _muscleSlider.value   = Random.Range(0f, 100f);

            UpdateLabels();
            ApplyLivePreview();
        }

        int Wrap(int idx, int count) => ((idx % count) + count) % count;

        void UpdateLabels()
        {
            if (_genderLabelRef != null) _genderLabelRef.text = _isFeminine ? "Feminine" : "Masculine";
            if (_headLabel  != null) _headLabel.text  = TrimName(_headPresets[_headIndex].Name);
            if (_upperLabel != null) _upperLabel.text  = TrimName(_upperPresets[_upperIndex].Name);
            if (_lowerLabel != null) _lowerLabel.text  = TrimName(_lowerPresets[_lowerIndex].Name);
        }

        string TrimName(string n) => n.Length > 28 ? n.Substring(0, 25) + "..." : n;

        // ── Live preview ──────────────────────────────────────────────────

        void ApplyLivePreview()
        {
            var player = GameObject.FindWithTag("Player");
            if (player == null) return;
            var sync = player.GetComponent<LocalCharacterSync>();
            if (sync == null) return;
            sync.ApplyAppearance(BuildAppearanceData());
        }

        // ── Confirm ───────────────────────────────────────────────────────

        void OnConfirm()
        {
            var data = BuildAppearanceData();

            var player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                var sync = player.GetComponent<LocalCharacterSync>();
                if (sync != null) sync.ApplyAppearance(data);
            }

            var api = SOTLApiManager.Instance;
            if (api != null && api.IsLinked)
            {
                api.SaveAvatar(data.ToJson(), ok =>
                    Debug.Log(ok ? "[SOTL Customize] Avatar saved to Wix." : "[SOTL Customize] Avatar save failed (fail-open)."));
            }

            Hide();
        }

        // ── Build appearance data ─────────────────────────────────────────

        CharacterAppearanceData BuildAppearanceData()
        {
            var mgr = SidekickCharacterManager.Instance;
            var data = new CharacterAppearanceData();
            data.isFeminine = _isFeminine;

            foreach (var e in mgr.ResolvePreset(_headPresets[_headIndex]))   data.SetPart(e.slot, e.name);
            foreach (var e in mgr.ResolvePreset(_upperPresets[_upperIndex])) data.SetPart(e.slot, e.name);
            foreach (var e in mgr.ResolvePreset(_lowerPresets[_lowerIndex])) data.SetPart(e.slot, e.name);

            data.bodyType = _bodyTypeSlider.value;
            data.muscles  = _muscleSlider.value;

            float size = _bodySizeSlider.value;
            data.heavy  = size > 0 ? size  : 0;
            data.skinny = size < 0 ? -size : 0;

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

            var cam = Object.FindFirstObjectByType<LotCameraController>();
            if (cam != null) cam.enabled = !freeze;
        }

        // ═══════════════════════════════════════════════════════════════════
        // UI HELPERS
        // ═══════════════════════════════════════════════════════════════════

        static RectTransform MakeRect(string name, RectTransform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        static Text MakeText(RectTransform parent, string text, int fontSize, FontStyle style)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.text = text;
            t.fontSize = fontSize;
            t.fontStyle = style;
            t.color = Color.white;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return t;
        }

        static void SetTopAnchored(RectTransform rt, float xMin, float xMax, float yPos, float height)
        {
            rt.anchorMin = new Vector2(xMin, 1f);
            rt.anchorMax = new Vector2(xMax, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, yPos);
            rt.sizeDelta = new Vector2(0f, height);
        }

        static void FillParent(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
