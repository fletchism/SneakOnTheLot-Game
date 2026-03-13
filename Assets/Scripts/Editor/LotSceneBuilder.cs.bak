#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Unity.Cinemachine;
using SOTL.API;
using SOTL.Core;

namespace SOTL.Editor
{
    public static class LotSceneBuilder
    {
        [MenuItem("SOTL/2 - Build Lot Scene", false, 20)]
        public static void Build()
        {
            Debug.Log("[SOTL Scene] Build started.");

            var config = AssetDatabase.LoadAssetAtPath<SOTLConfig>("Assets/Resources/SOTLConfig.asset");
            if (config == null)
            {
                Debug.Log("[SOTL Scene] SOTLConfig not found — creating it now.");
                if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                    AssetDatabase.CreateFolder("Assets", "Resources");
                config = ScriptableObject.CreateInstance<SOTLConfig>();
                config.BaseUrl = "https://www.sneakonthelot.com";
                AssetDatabase.CreateAsset(config, "Assets/Resources/SOTLConfig.asset");
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("[SOTL Scene] SOTLConfig created.");
            }

            Debug.Log("[SOTL Scene] Config ready. Creating scene objects...");
            CreateGround();
            var player = CreatePlayer();
            CreateLighting();
            EnsureEventSystem();
            CreateStatsUI(player);
            CreateLinkOverlay();
            CreateManagers(config);
            CreateDialogueUI();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[SOTL Scene] Done. Press PLAY. WASD to move, Tab to open stats panel.");
        }

        // ── NPC Idle Controller ───────────────────────────────────────

        [MenuItem("SOTL/3 - Create NPC Idle Controller", false, 30)]
        public static void CreateNPCIdleController()
        {
            // Search for the idle FBX by name anywhere in the project
            var guids = AssetDatabase.FindAssets("A_MOD_BL_Idle_Standing_Masc");
            string fbxPath = null;
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
                {
                    fbxPath = path;
                    break;
                }
            }

            if (fbxPath == null)
            {
                Debug.LogError("[SOTL] Could not find A_MOD_BL_Idle_Standing_Masc.fbx. Make sure Synty assets are imported.");
                EditorUtility.DisplayDialog("FBX Not Found",
                    "A_MOD_BL_Idle_Standing_Masc.fbx was not found in the project.\nMake sure Synty Sidekick assets are imported.", "OK");
                return;
            }

            Debug.Log($"[SOTL] Found idle FBX at: {fbxPath}");

            // Extract animation clip — skip __preview__ clips Unity auto-generates
            var idleClip = AssetDatabase.LoadAllAssetsAtPath(fbxPath)
                .OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview__"));

            if (idleClip == null)
            {
                Debug.LogError($"[SOTL] No AnimationClip found inside {fbxPath}.");
                return;
            }

            Debug.Log($"[SOTL] Using clip: {idleClip.name}");

            // Create output folder
            if (!AssetDatabase.IsValidFolder("Assets/Animations"))
                AssetDatabase.CreateFolder("Assets", "Animations");
            if (!AssetDatabase.IsValidFolder("Assets/Animations/NPC"))
                AssetDatabase.CreateFolder("Assets/Animations", "NPC");

            const string controllerPath = "Assets/Animations/NPC/AC_NPC_Idle_Masculine.controller";

            // Overwrite if it already exists
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (existing != null)
                AssetDatabase.DeleteAsset(controllerPath);

            var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            controller.AddMotion(idleClip);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[SOTL] Controller created: {controllerPath}");

            // Search ALL GameObjects including inactive ones
            int assigned = 0;
            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                // skip assets, only scene objects
                if (UnityEditor.EditorUtility.IsPersistent(go)) continue;
                if (go.name == "NPC_Filmmaker" || go.GetComponent("LotNPC") != null)
                {
                    AssignController(go, controller);
                    assigned++;
                }
            }

            if (assigned == 0)
                Debug.LogWarning("[SOTL] No NPC found in scene. Controller saved — assign manually in Inspector.");
            else
                Debug.Log($"[SOTL] Controller assigned to {assigned} NPC(s). Save scene and hit Play.");

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        static void AssignController(GameObject npc, AnimatorController controller)
        {
            var animator = npc.GetComponentInChildren<Animator>();
            if (animator == null)
            {
                animator = npc.AddComponent<Animator>();
                Debug.Log($"[SOTL] Added Animator to {npc.name}.");
            }
            animator.runtimeAnimatorController = controller;
            EditorUtility.SetDirty(animator);
            Debug.Log($"[SOTL] Assigned AC_NPC_Idle_Masculine.controller to {npc.name}.");
        }

        [MenuItem("SOTL/Scene/Clean Rebuild (delete + rebuild)", false, 110)]
        public static void CleanRebuild()
        {
            if (!EditorUtility.DisplayDialog("Clean Rebuild",
                "Deletes: Ground, Player, Lighting, StatsCanvas, Managers.\nThen rebuilds.\n\nProceed?",
                "Clean Rebuild", "Cancel")) return;

            foreach (var name in new[] { "Ground", "Player", "SceneLighting", "StatsCanvas", "LinkOverlayCanvas", "[Managers]" })
            {
                var go = GameObject.Find(name);
                if (go != null) Object.DestroyImmediate(go);
            }

            AssetDatabase.Refresh();
            Build();
        }

        // ── Ground ────────────────────────────────────────────────────

        static void CreateGround()
        {
            if (GameObject.Find("Ground")) return;

            var go = GameObject.CreatePrimitive(PrimitiveType.Plane);
            go.name = "Ground";
            go.transform.position = Vector3.zero;
            go.transform.localScale = new Vector3(20f, 1f, 20f); // 200x200m lot

            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = new Color(0.55f, 0.5f, 0.42f, 1f); // asphalt/concrete
            mat.SetFloat("_Smoothness", 0.05f);
            go.GetComponent<Renderer>().material = mat;

            Undo.RegisterCreatedObjectUndo(go, "Create Ground");
            Debug.Log("[SOTL Scene] Ground created (200x200m).");
        }


        // ── Player ────────────────────────────────────────────────────

        static GameObject CreatePlayer()
        {
            if (GameObject.Find("Player")) return GameObject.Find("Player");

            var player = new GameObject("Player");
            player.transform.position = new Vector3(0f, 0.1f, 0f);
            player.tag = "Player";

            // Character Controller
            var cc = player.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.3f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.skinWidth = 0.01f;

            // Capsule visual stand-in until Synty assets imported
            var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            capsule.name = "CharacterModel";
            capsule.transform.SetParent(player.transform);
            capsule.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            Object.DestroyImmediate(capsule.GetComponent<CapsuleCollider>());
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = new Color(0.2f, 0.4f, 0.7f, 1f); // blue placeholder
            capsule.GetComponent<Renderer>().material = mat;

            // Head target for FPS camera
            var head = new GameObject("HeadTarget");
            head.transform.SetParent(player.transform);
            head.transform.localPosition = new Vector3(0f, 1.65f, 0.1f);

            // TPS pivot
            var tpsPivot = new GameObject("TPS_Pivot");
            tpsPivot.transform.SetParent(player.transform);
            tpsPivot.transform.localPosition = new Vector3(0f, 1.4f, 0f);

            // PlayerController (reflection — LotPlayerController may be in Assembly-CSharp)
            var playerCtrlType = System.Type.GetType("LotPlayerController") ??
                                 System.Type.GetType("SOTL.Player.LotPlayerController");
            if (playerCtrlType != null)
                player.AddComponent(playerCtrlType);

            // FPS VCam
            var fpsGO = new GameObject("VCam_FPS");
            fpsGO.transform.SetParent(player.transform);
            fpsGO.transform.localPosition = new Vector3(0f, 1.65f, 0.1f);
            var fpsCam = fpsGO.AddComponent<CinemachineCamera>();
            fpsCam.Priority = 5;  // TPS is default
            fpsCam.Follow = head.transform;
            fpsGO.AddComponent<CinemachineRotateWithFollowTarget>();

            // TPS VCam
            var tpsGO = new GameObject("VCam_TPS");
            tpsGO.transform.SetParent(player.transform);
            var tpsCam = tpsGO.AddComponent<CinemachineCamera>();
            tpsCam.Priority = 10; // TPS is default
            tpsCam.Follow = tpsPivot.transform;
            var tpsFollow = tpsGO.AddComponent<CinemachineThirdPersonFollow>();
            tpsFollow.ShoulderOffset = new Vector3(0.5f, 0f, 0f);
            tpsFollow.CameraDistance = 3.5f;
            tpsGO.AddComponent<CinemachineRotateWithFollowTarget>();

            // Wire cam refs into controller via reflection
            var ctrl = player.GetComponent(playerCtrlType);
            if (ctrl != null)
            {
                SetField(ctrl, "_fpsCam", fpsCam);
                SetField(ctrl, "_tpsCam", tpsCam);
                SetField(ctrl, "_tpsPivot", tpsPivot.transform);
            }

            SetupCinemachineBrain();

            Undo.RegisterCreatedObjectUndo(player, "Create Player");
            Debug.Log("[SOTL Scene] Player created.");
            return player;
        }

        static void SetupCinemachineBrain()
        {
            var mainCam = Camera.main;
            if (mainCam == null)
            {
                var camGO = new GameObject("Main Camera");
                camGO.tag = "MainCamera";
                mainCam = camGO.AddComponent<Camera>();
            }
            if (mainCam.GetComponent<CinemachineBrain>() == null)
                mainCam.gameObject.AddComponent<CinemachineBrain>();
        }


        // ── Lighting ──────────────────────────────────────────────────

        static void CreateLighting()
        {
            if (GameObject.Find("SceneLighting")) return;

            var root = new GameObject("SceneLighting");

            // Remove default directional lights
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) Object.DestroyImmediate(l.gameObject);

            var sunGO = new GameObject("Sun");
            sunGO.transform.SetParent(root.transform);
            sunGO.transform.rotation = Quaternion.Euler(50f, -20f, 0f);
            var sun = sunGO.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.95f, 0.85f);
            sun.intensity = 2f;
            sun.shadows = LightShadows.Soft;
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.6f, 0.7f, 0.9f);
            RenderSettings.ambientEquatorColor = new Color(0.5f, 0.55f, 0.6f);
            RenderSettings.ambientGroundColor = new Color(0.3f, 0.28f, 0.22f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 60f;
            RenderSettings.fogEndDistance = 200f;

            Undo.RegisterCreatedObjectUndo(root, "Create Lighting");
            Debug.Log("[SOTL Scene] Lighting configured.");
        }

        // ── Event System ──────────────────────────────────────────────

        static void EnsureEventSystem()
        {
            if (GameObject.Find("EventSystem") != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<UnityEngine.EventSystems.EventSystem>();
            go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            Undo.RegisterCreatedObjectUndo(go, "Create EventSystem");
            Debug.Log("[SOTL Scene] EventSystem created.");
        }

        // ── Link Overlay ──────────────────────────────────────────────

        static void CreateLinkOverlay()
        {
            if (GameObject.Find("LinkOverlayCanvas")) return;

            var canvasGO = new GameObject("LinkOverlayCanvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10; // above StatsCanvas
            canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
            canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            // Dark full-screen background
            var bg = new GameObject("Background", typeof(RectTransform));
            bg.transform.SetParent(canvasGO.transform, false);
            var bgRT = bg.GetComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero; bgRT.anchorMax = Vector2.one;
            bgRT.offsetMin = Vector2.zero; bgRT.offsetMax = Vector2.zero;
            var bgImg = bg.AddComponent<UnityEngine.UI.Image>();
            bgImg.color = new Color(0f, 0f, 0f, 0.85f);

            // Center panel
            var panel = new GameObject("Panel", typeof(RectTransform));
            panel.transform.SetParent(canvasGO.transform, false);
            var pRT = panel.GetComponent<RectTransform>();
            pRT.anchorMin = pRT.anchorMax = pRT.pivot = new Vector2(0.5f, 0.5f);
            pRT.sizeDelta = new Vector2(480f, 380f);
            pRT.anchoredPosition = Vector2.zero;
            var pImg = panel.AddComponent<UnityEngine.UI.Image>();
            pImg.color = new Color(0.08f, 0.08f, 0.12f, 0.98f);

            // Title
            var title = CreateLabel(panel.transform, "TitleLabel",
                "SNEAK ON THE LOT", new Vector2(0, -30));
            title.fontSize = 28; title.color = new Color(1f, 0.75f, 0.2f);

            var sub = CreateLabel(panel.transform, "SubLabel",
                "Link your account to track XP, Fame & Prestige", new Vector2(0, -75));
            sub.fontSize = 18; sub.color = new Color(0.8f, 0.8f, 0.8f);

            // Input field
            var inputGO = new GameObject("CodeInput", typeof(RectTransform));
            inputGO.transform.SetParent(panel.transform, false);
            var inputRT = inputGO.GetComponent<RectTransform>();
            inputRT.anchorMin = inputRT.anchorMax = new Vector2(0.5f, 1f);
            inputRT.pivot = new Vector2(0.5f, 1f);
            inputRT.sizeDelta = new Vector2(380f, 48f);
            inputRT.anchoredPosition = new Vector2(0, -125);
            var inputBG = inputGO.AddComponent<UnityEngine.UI.Image>();
            inputBG.color = new Color(0.15f, 0.15f, 0.2f);
            var inputField = inputGO.AddComponent<UnityEngine.UI.InputField>();

            // Placeholder
            var placeholder = CreateLabel(inputGO.transform, "Placeholder", "Enter link code…", Vector2.zero);
            placeholder.color = new Color(0.5f, 0.5f, 0.5f);
            placeholder.fontSize = 20;
            var placeholderRT = placeholder.GetComponent<RectTransform>();
            placeholderRT.anchorMin = Vector2.zero; placeholderRT.anchorMax = Vector2.one;
            placeholderRT.offsetMin = new Vector2(8, 0); placeholderRT.offsetMax = Vector2.zero;

            // Text
            var textComp = CreateLabel(inputGO.transform, "Text", "", Vector2.zero);
            textComp.fontSize = 20;
            var textRT = textComp.GetComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero; textRT.anchorMax = Vector2.one;
            textRT.offsetMin = new Vector2(8, 0); textRT.offsetMax = Vector2.zero;

            inputField.placeholder = placeholder;
            inputField.textComponent = textComp;

            // Link button
            var btnGO = new GameObject("LinkButton", typeof(RectTransform));
            btnGO.transform.SetParent(panel.transform, false);
            var btnRT = btnGO.GetComponent<RectTransform>();
            btnRT.anchorMin = btnRT.anchorMax = new Vector2(0.5f, 1f);
            btnRT.pivot = new Vector2(0.5f, 1f);
            btnRT.sizeDelta = new Vector2(200f, 50f);
            btnRT.anchoredPosition = new Vector2(0, -190);
            var btnImg = btnGO.AddComponent<UnityEngine.UI.Image>();
            btnImg.color = new Color(0.2f, 0.6f, 1f);
            var btn = btnGO.AddComponent<UnityEngine.UI.Button>();
            btn.targetGraphic = btnImg;
            var btnLabel = CreateLabel(btnGO.transform, "ButtonLabel", "Link Account", Vector2.zero);
            btnLabel.fontSize = 20;
            var btnLabelRT = btnLabel.GetComponent<RectTransform>();
            btnLabelRT.anchorMin = Vector2.zero; btnLabelRT.anchorMax = Vector2.one;
            btnLabelRT.offsetMin = btnLabelRT.offsetMax = Vector2.zero;

            // Status text
            var status = CreateLabel(panel.transform, "StatusText", "", new Vector2(0, -255));
            status.fontSize = 18;

            // Skip button (play without linking)
            var skipGO = new GameObject("SkipButton", typeof(RectTransform));
            skipGO.transform.SetParent(panel.transform, false);
            var skipRT = skipGO.GetComponent<RectTransform>();
            skipRT.anchorMin = skipRT.anchorMax = new Vector2(0.5f, 1f);
            skipRT.pivot = new Vector2(0.5f, 1f);
            skipRT.sizeDelta = new Vector2(160f, 36f);
            skipRT.anchoredPosition = new Vector2(0, -290);
            var skipBtn = skipGO.AddComponent<UnityEngine.UI.Button>();
            var skipLabel = CreateLabel(skipGO.transform, "SkipLabel", "Play without linking", Vector2.zero);
            skipLabel.fontSize = 16; skipLabel.color = new Color(0.6f, 0.6f, 0.6f);
            skipBtn.onClick.AddListener(() => canvasGO.SetActive(false));

            // Wire LinkOverlay component
            var overlayType = System.Type.GetType("SOTL.UI.LinkOverlay, SOTL.UI");
            if (overlayType != null)
            {
                var overlay = canvasGO.AddComponent(overlayType);
                SetField(overlay, "_codeInput",  inputField);
                SetField(overlay, "_statusText", status);
                SetField(overlay, "_linkButton", btn);
            }
            else
            {
                Debug.LogWarning("[SOTL Scene] LinkOverlay type not found — recompile and rebuild.");
            }

            Undo.RegisterCreatedObjectUndo(canvasGO, "Create Link Overlay");
            Debug.Log("[SOTL Scene] Link overlay created.");
        }

        // ── Stats UI ──────────────────────────────────────────────────

        static void CreateStatsUI(GameObject player)
        {
            if (GameObject.Find("StatsCanvas")) return;

            var canvasGO = new GameObject("StatsCanvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
            canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            // Stats panel (hidden by default, Tab to toggle)
            var panelGO = new GameObject("StatsPanel", typeof(RectTransform));
            panelGO.transform.SetParent(canvasGO.transform, false);
            var panelRT = panelGO.GetComponent<RectTransform>();
            panelRT.anchorMin = new Vector2(0.5f, 0.5f);
            panelRT.anchorMax = new Vector2(0.5f, 0.5f);
            panelRT.pivot = new Vector2(0.5f, 0.5f);
            panelRT.sizeDelta = new Vector2(400f, 320f);
            panelRT.anchoredPosition = Vector2.zero;
            var panelBG = panelGO.AddComponent<UnityEngine.UI.Image>();
            panelBG.color = new Color(0.05f, 0.05f, 0.07f, 0.92f);
            panelGO.SetActive(false);

            // Sub-panels for linked / not linked states
            var linkedPanel    = CreateChildPanel(panelGO.transform, "LinkedPanel");
            var linkPrompt     = CreateChildPanel(panelGO.transform, "LinkPromptPanel");

            // Labels inside linkedPanel
            var xpLabel        = CreateLabel(linkedPanel.transform,   "XPLabel",       "XP: —",       new Vector2(0, -60));
            var levelLabel     = CreateLabel(linkedPanel.transform,   "LevelLabel",    "Level —",     new Vector2(0, -100));
            var fameLabel      = CreateLabel(linkedPanel.transform,   "FameLabel",     "Fame: —",     new Vector2(0, -140));
            var prestigeLabel  = CreateLabel(linkedPanel.transform,   "PrestigeLabel", "Prestige: —", new Vector2(0, -180));

            // Link prompt text
            CreateLabel(linkPrompt.transform, "PromptText",
                "Open sneakonthelot.com/my-stats\nand click Link Unity Account\nto connect your SOTL profile.",
                new Vector2(0, -80));
            var statusLabel = CreateLabel(panelGO.transform, "StatusLabel", "", new Vector2(0, 120));

            // StatsPanelToggler — self-wiring via StatsPanelToggler.Start()
            var togglerGO = canvasGO.AddComponent(System.Type.GetType("SOTL.UI.StatsPanelToggler, SOTL.UI"));

            Undo.RegisterCreatedObjectUndo(canvasGO, "Create Stats Canvas");
            Debug.Log("[SOTL Scene] Stats UI created. Tab to toggle.");
        }

        static GameObject CreateChildPanel(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            return go;
        }

        static UnityEngine.UI.Text CreateLabel(Transform parent, string name, string text, Vector2 pos)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(360, 40);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f); rt.pivot = new Vector2(0.5f, 1f);
            var t = go.AddComponent<UnityEngine.UI.Text>();
            t.text = text;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = 22; t.color = Color.white;
            t.alignment = TextAnchor.MiddleCenter;
            return t;
        }

        // ── Managers ──────────────────────────────────────────────────

        static void CreateManagers(SOTLConfig config)
        {
            if (GameObject.Find("[Managers]")) return;

            var root = new GameObject("[Managers]");

            // GameManager
            root.AddComponent<GameManager>();

            // SOTLApiManager
            var apiGO = new GameObject("SOTLApiManager");
            apiGO.transform.SetParent(root.transform);
            var api = apiGO.AddComponent<SOTLApiManager>();
            var so = new SerializedObject(api);
            so.FindProperty("config").objectReferenceValue = config;
            so.ApplyModifiedPropertiesWithoutUndo();

            Undo.RegisterCreatedObjectUndo(root, "Create Managers");
            Debug.Log("[SOTL Scene] Managers created.");
        }

        // ── Dialogue UI ───────────────────────────────────────────────

        static void CreateDialogueUI()
        {
            if (GameObject.Find("DialogueCanvas")) return;

            var canvasGO = new GameObject("DialogueCanvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5;
            canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
            canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            // ── Prompt (bottom-center) ────────────────────────────────
            var promptRoot = new GameObject("PromptRoot", typeof(RectTransform));
            promptRoot.transform.SetParent(canvasGO.transform, false);
            var promptRT = promptRoot.GetComponent<RectTransform>();
            promptRT.anchorMin = new Vector2(0.5f, 0f);
            promptRT.anchorMax = new Vector2(0.5f, 0f);
            promptRT.pivot     = new Vector2(0.5f, 0f);
            promptRT.sizeDelta = new Vector2(400f, 50f);
            promptRT.anchoredPosition = new Vector2(0f, 80f);
            var promptBG = promptRoot.AddComponent<UnityEngine.UI.Image>();
            promptBG.color = new Color(0f, 0f, 0f, 0.6f);
            var promptText = CreateLabel(promptRoot.transform, "PromptText", "[E]  Talk to NPC", Vector2.zero);
            promptText.fontSize = 22;
            var promptTextRT = promptText.GetComponent<RectTransform>();
            promptTextRT.anchorMin = Vector2.zero; promptTextRT.anchorMax = Vector2.one;
            promptTextRT.offsetMin = promptTextRT.offsetMax = Vector2.zero;
            promptRoot.SetActive(false);

            // ── Dialogue panel (bottom) ───────────────────────────────
            var panelRoot = new GameObject("DialoguePanel", typeof(RectTransform));
            panelRoot.transform.SetParent(canvasGO.transform, false);
            var panelRT = panelRoot.GetComponent<RectTransform>();
            panelRT.anchorMin = new Vector2(0f, 0f);
            panelRT.anchorMax = new Vector2(1f, 0f);
            panelRT.pivot     = new Vector2(0.5f, 0f);
            panelRT.sizeDelta = new Vector2(0f, 180f);
            panelRT.anchoredPosition = Vector2.zero;
            var panelBG = panelRoot.AddComponent<UnityEngine.UI.Image>();
            panelBG.color = new Color(0.05f, 0.05f, 0.1f, 0.92f);

            var nameText = CreateLabel(panelRoot.transform, "NameText", "NPC", new Vector2(0f, -10f));
            nameText.fontSize = 20; nameText.color = new Color(1f, 0.75f, 0.2f);
            nameText.fontStyle = UnityEngine.FontStyle.Bold;
            var nameRT = nameText.GetComponent<RectTransform>();
            nameRT.anchorMin = new Vector2(0f, 1f); nameRT.anchorMax = new Vector2(1f, 1f);
            nameRT.pivot = new Vector2(0.5f, 1f);
            nameRT.sizeDelta = new Vector2(0f, 30f);
            nameRT.anchoredPosition = new Vector2(0f, -10f);

            var lineText = CreateLabel(panelRoot.transform, "LineText", "", new Vector2(0f, -50f));
            lineText.fontSize = 20; lineText.alignment = TextAnchor.UpperLeft;
            var lineRT = lineText.GetComponent<RectTransform>();
            lineRT.anchorMin = new Vector2(0f, 1f); lineRT.anchorMax = new Vector2(1f, 1f);
            lineRT.pivot = new Vector2(0.5f, 1f);
            lineRT.sizeDelta = new Vector2(-40f, 90f);
            lineRT.anchoredPosition = new Vector2(0f, -50f);

            var hintText = CreateLabel(panelRoot.transform, "ContinueHint", "[E] Continue", Vector2.zero);
            hintText.fontSize = 18; hintText.color = new Color(0.6f, 0.6f, 0.6f);
            hintText.alignment = TextAnchor.LowerRight;
            var hintRT = hintText.GetComponent<RectTransform>();
            hintRT.anchorMin = new Vector2(0f, 0f); hintRT.anchorMax = new Vector2(1f, 0f);
            hintRT.pivot = new Vector2(0.5f, 0f);
            hintRT.sizeDelta = new Vector2(-40f, 30f);
            hintRT.anchoredPosition = new Vector2(0f, 10f);

            panelRoot.SetActive(false);

            // ── Wire LotDialogueUI component ──────────────────────────
            var uiType = System.Type.GetType("SOTL.NPC.LotDialogueUI, Assembly-CSharp");
            if (uiType != null)
            {
                var ui = canvasGO.AddComponent(uiType);
                SetField(ui, "_promptRoot",   promptRoot);
                SetField(ui, "_promptText",   promptText);
                SetField(ui, "_panelRoot",    panelRoot);
                SetField(ui, "_nameText",     nameText);
                SetField(ui, "_lineText",     lineText);
                SetField(ui, "_continueHint", hintText);
                Debug.Log("[SOTL Scene] DialogueUI wired.");
            }
            else
            {
                Debug.LogWarning("[SOTL Scene] LotDialogueUI type not found — recompile and rebuild.");
            }

            Undo.RegisterCreatedObjectUndo(canvasGO, "Create Dialogue UI");
            Debug.Log("[SOTL Scene] Dialogue canvas created.");
        }

        // ── Reflection helper ─────────────────────────────────────────

        static void SetField(object obj, string fieldName, object value)
        {
            var f = obj.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.Instance);
            if (f != null) f.SetValue(obj, value);
            else Debug.LogWarning($"[SOTL Scene] Field not found: {obj.GetType().Name}.{fieldName}");
        }
    }
}
#endif
