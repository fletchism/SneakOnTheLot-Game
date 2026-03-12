#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using SOTL.API;
using SOTL.Core;

namespace SOTL.Editor
{
    public static class LotSceneBuilder
    {
        const string PLAYER_PREFAB = "Assets/Synty/AnimationBaseLocomotion/Samples/Prefabs/PF_SidekickPlayer.prefab";
        const string CAMERA_PREFAB = "Assets/Synty/AnimationBaseLocomotion/Samples/Prefabs/PF_SyntyCamera.prefab";
        const string NPC_PREFAB    = "Assets/Synty/SidekickCharacters/Characters/ModernCivilians/ModernCivilian_01/ModernCivilian_01.prefab";
        const string IDLE_CTRL     = "Assets/Animations/NPC/AC_NPC_Idle_Masculine.controller";

        [MenuItem("SOTL/2 - Build Lot Scene", false, 20)]
        public static void Build()
        {
            Debug.Log("[SOTL Scene] Build started.");

            var config = AssetDatabase.LoadAssetAtPath<SOTLConfig>("Assets/Resources/SOTLConfig.asset");
            if (config == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                    AssetDatabase.CreateFolder("Assets", "Resources");
                config = ScriptableObject.CreateInstance<SOTLConfig>();
                config.BaseUrl = "https://www.sneakonthelot.com";
                AssetDatabase.CreateAsset(config, "Assets/Resources/SOTLConfig.asset");
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            CreateGround();
            CreatePlayer();
            CreateNPC();
            CreateLighting();
            EnsureEventSystem();
            CreateStatsUI();
            CreateLinkOverlay();
            CreateDialogueUI();
            CreateManagers(config);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[SOTL Scene] Done. Save scene (Cmd+S) then hit Play.");
        }

        [MenuItem("SOTL/3 - Create NPC Idle Controller", false, 30)]
        public static void CreateNPCIdleController()
        {
            var guids = AssetDatabase.FindAssets("A_MOD_BL_Idle_Standing_Masc");
            string fbxPath = null;
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
                { fbxPath = path; break; }
            }

            if (fbxPath == null)
            {
                Debug.LogError("[SOTL] A_MOD_BL_Idle_Standing_Masc.fbx not found.");
                EditorUtility.DisplayDialog("FBX Not Found",
                    "A_MOD_BL_Idle_Standing_Masc.fbx not found. Import Synty Sidekick assets first.", "OK");
                return;
            }

            var idleClip = AssetDatabase.LoadAllAssetsAtPath(fbxPath)
                .OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview__"));

            if (idleClip == null) { Debug.LogError($"[SOTL] No clip in {fbxPath}."); return; }

            if (!AssetDatabase.IsValidFolder("Assets/Animations"))
                AssetDatabase.CreateFolder("Assets", "Animations");
            if (!AssetDatabase.IsValidFolder("Assets/Animations/NPC"))
                AssetDatabase.CreateFolder("Assets/Animations", "NPC");

            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(IDLE_CTRL);
            if (existing != null) AssetDatabase.DeleteAsset(IDLE_CTRL);

            var controller = AnimatorController.CreateAnimatorControllerAtPath(IDLE_CTRL);
            controller.AddMotion(idleClip);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[SOTL] Controller created: {IDLE_CTRL}");

            int assigned = 0;
            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (EditorUtility.IsPersistent(go)) continue;
                if (go.name == "NPC_Filmmaker" || go.GetComponent("LotNPC") != null)
                {
                    AssignController(go, controller);
                    assigned++;
                }
            }

            if (assigned == 0)
                Debug.LogWarning("[SOTL] No NPC in scene. Controller saved — assign manually.");
            else
                Debug.Log($"[SOTL] Assigned to {assigned} NPC(s). Save and hit Play.");

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        static void AssignController(GameObject npc, AnimatorController controller)
        {
            var animator = npc.GetComponentInChildren<Animator>();
            if (animator == null) animator = npc.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            EditorUtility.SetDirty(animator);
            Debug.Log($"[SOTL] Idle controller assigned to {npc.name}.");
        }

        static void CreateGround()
        {
            if (GameObject.Find("Ground")) return;
            var go = GameObject.CreatePrimitive(PrimitiveType.Plane);
            go.name = "Ground";
            go.transform.localScale = new Vector3(20f, 1f, 20f);
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = new Color(0.55f, 0.5f, 0.42f, 1f);
            mat.SetFloat("_Smoothness", 0.05f);
            go.GetComponent<Renderer>().material = mat;
            Undo.RegisterCreatedObjectUndo(go, "Create Ground");
            Debug.Log("[SOTL Scene] Ground created.");
        }

        static void CreatePlayer()
        {
            if (GameObject.Find("PF_SidekickPlayer") || GameObject.Find("SyntyCamera")) return;

            var camPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CAMERA_PREFAB);
            if (camPrefab != null)
            {
                var cam = (GameObject)PrefabUtility.InstantiatePrefab(camPrefab);
                cam.name = "SyntyCamera";
                Undo.RegisterCreatedObjectUndo(cam, "Create Camera");
                Debug.Log("[SOTL Scene] SyntyCamera instantiated.");
            }
            else Debug.LogWarning($"[SOTL Scene] Camera prefab not found: {CAMERA_PREFAB}");

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PLAYER_PREFAB);
            if (prefab != null)
            {
                var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                player.transform.position = Vector3.zero;
                player.tag = "Player";
                Undo.RegisterCreatedObjectUndo(player, "Create Player");
                Debug.Log("[SOTL Scene] PF_SidekickPlayer instantiated.");
            }
            else Debug.LogWarning($"[SOTL Scene] Player prefab not found: {PLAYER_PREFAB}");
        }

        static void CreateNPC()
        {
            if (GameObject.Find("NPC_Filmmaker")) return;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(NPC_PREFAB);
            if (prefab == null)
            {
                Debug.LogWarning($"[SOTL Scene] NPC prefab not found: {NPC_PREFAB}");
                return;
            }

            var npc = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            npc.name = "NPC_Filmmaker";
            npc.transform.position = new Vector3(3f, 0f, 3f);
            npc.transform.rotation = Quaternion.Euler(0f, 225f, 0f);

            var lotNpcType = System.Type.GetType("LotNPC, Assembly-CSharp");
            if (lotNpcType != null)
                npc.AddComponent(lotNpcType);
            else
                Debug.LogWarning("[SOTL Scene] LotNPC type not found — add manually.");

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(IDLE_CTRL);
            if (controller != null)
                AssignController(npc, controller);
            else
                Debug.LogWarning("[SOTL Scene] Idle controller not found — run SOTL/3 first, then SOTL/2.");

            Undo.RegisterCreatedObjectUndo(npc, "Create NPC");
            Debug.Log("[SOTL Scene] NPC_Filmmaker created.");
        }

        static void CreateLighting()
        {
            if (GameObject.Find("SceneLighting")) return;
            var root = new GameObject("SceneLighting");
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
            RenderSettings.ambientSkyColor    = new Color(0.6f, 0.7f, 0.9f);
            RenderSettings.ambientEquatorColor = new Color(0.5f, 0.55f, 0.6f);
            RenderSettings.ambientGroundColor  = new Color(0.3f, 0.28f, 0.22f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 60f;
            RenderSettings.fogEndDistance   = 200f;
            Undo.RegisterCreatedObjectUndo(root, "Create Lighting");
            Debug.Log("[SOTL Scene] Lighting configured.");
        }

        static void EnsureEventSystem()
        {
            if (GameObject.Find("EventSystem") != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<UnityEngine.EventSystems.EventSystem>();
            go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            Undo.RegisterCreatedObjectUndo(go, "Create EventSystem");
        }

        static void CreateDialogueUI()
        {
            if (GameObject.Find("DialogueCanvas")) return;

            var canvasGO = new GameObject("DialogueCanvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5;
            canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
            canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            var promptRoot = new GameObject("PromptRoot", typeof(RectTransform));
            promptRoot.transform.SetParent(canvasGO.transform, false);
            var promptRT = promptRoot.GetComponent<RectTransform>();
            promptRT.anchorMin = new Vector2(0.5f, 0f);
            promptRT.anchorMax = new Vector2(0.5f, 0f);
            promptRT.pivot     = new Vector2(0.5f, 0f);
            promptRT.sizeDelta = new Vector2(400f, 50f);
            promptRT.anchoredPosition = new Vector2(0f, 80f);
            promptRoot.AddComponent<UnityEngine.UI.Image>().color = new Color(0f, 0f, 0f, 0.6f);
            var promptText = CreateLabel(promptRoot.transform, "PromptText", "[E]  Talk to NPC", Vector2.zero);
            promptText.fontSize = 22;
            var promptTextRT = promptText.GetComponent<RectTransform>();
            promptTextRT.anchorMin = Vector2.zero; promptTextRT.anchorMax = Vector2.one;
            promptTextRT.offsetMin = promptTextRT.offsetMax = Vector2.zero;
            promptRoot.SetActive(false);

            var panelRoot = new GameObject("DialoguePanel", typeof(RectTransform));
            panelRoot.transform.SetParent(canvasGO.transform, false);
            var panelRT = panelRoot.GetComponent<RectTransform>();
            panelRT.anchorMin = new Vector2(0f, 0f);
            panelRT.anchorMax = new Vector2(1f, 0f);
            panelRT.pivot     = new Vector2(0.5f, 0f);
            panelRT.sizeDelta = new Vector2(0f, 180f);
            panelRT.anchoredPosition = Vector2.zero;
            panelRoot.AddComponent<UnityEngine.UI.Image>().color = new Color(0.05f, 0.05f, 0.1f, 0.92f);

            var nameText = CreateLabel(panelRoot.transform, "NameText", "NPC", Vector2.zero);
            nameText.fontSize = 20; nameText.color = new Color(1f, 0.75f, 0.2f);
            nameText.fontStyle = UnityEngine.FontStyle.Bold;
            var nameRT = nameText.GetComponent<RectTransform>();
            nameRT.anchorMin = new Vector2(0f, 1f); nameRT.anchorMax = new Vector2(1f, 1f);
            nameRT.pivot = new Vector2(0.5f, 1f);
            nameRT.sizeDelta = new Vector2(0f, 30f);
            nameRT.anchoredPosition = new Vector2(0f, -10f);

            var lineText = CreateLabel(panelRoot.transform, "LineText", "", Vector2.zero);
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
            else Debug.LogWarning("[SOTL Scene] LotDialogueUI not found — recompile and run Build again.");

            Undo.RegisterCreatedObjectUndo(canvasGO, "Create Dialogue UI");
            Debug.Log("[SOTL Scene] Dialogue canvas created.");
        }

        static void CreateLinkOverlay()
        {
            if (GameObject.Find("LinkOverlayCanvas")) return;

            var canvasGO = new GameObject("LinkOverlayCanvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
            canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            var bg = new GameObject("Background", typeof(RectTransform));
            bg.transform.SetParent(canvasGO.transform, false);
            var bgRT = bg.GetComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero; bgRT.anchorMax = Vector2.one;
            bgRT.offsetMin = bgRT.offsetMax = Vector2.zero;
            bg.AddComponent<UnityEngine.UI.Image>().color = new Color(0f, 0f, 0f, 0.85f);

            var panel = new GameObject("Panel", typeof(RectTransform));
            panel.transform.SetParent(canvasGO.transform, false);
            var pRT = panel.GetComponent<RectTransform>();
            pRT.anchorMin = pRT.anchorMax = pRT.pivot = new Vector2(0.5f, 0.5f);
            pRT.sizeDelta = new Vector2(480f, 380f);
            panel.AddComponent<UnityEngine.UI.Image>().color = new Color(0.08f, 0.08f, 0.12f, 0.98f);

            var title = CreateLabel(panel.transform, "TitleLabel", "SNEAK ON THE LOT", new Vector2(0, -30));
            title.fontSize = 28; title.color = new Color(1f, 0.75f, 0.2f);
            var sub = CreateLabel(panel.transform, "SubLabel", "Link your account to track XP, Fame & Prestige", new Vector2(0, -75));
            sub.fontSize = 18; sub.color = new Color(0.8f, 0.8f, 0.8f);

            var inputGO = new GameObject("CodeInput", typeof(RectTransform));
            inputGO.transform.SetParent(panel.transform, false);
            var inputRT = inputGO.GetComponent<RectTransform>();
            inputRT.anchorMin = inputRT.anchorMax = new Vector2(0.5f, 1f);
            inputRT.pivot = new Vector2(0.5f, 1f);
            inputRT.sizeDelta = new Vector2(380f, 48f);
            inputRT.anchoredPosition = new Vector2(0, -125);
            inputGO.AddComponent<UnityEngine.UI.Image>().color = new Color(0.15f, 0.15f, 0.2f);
            var inputField = inputGO.AddComponent<UnityEngine.UI.InputField>();
            var ph = CreateLabel(inputGO.transform, "Placeholder", "Enter link code…", Vector2.zero);
            ph.color = new Color(0.5f, 0.5f, 0.5f); ph.fontSize = 20;
            var phRT = ph.GetComponent<RectTransform>();
            phRT.anchorMin = Vector2.zero; phRT.anchorMax = Vector2.one;
            phRT.offsetMin = new Vector2(8, 0); phRT.offsetMax = Vector2.zero;
            var tc = CreateLabel(inputGO.transform, "Text", "", Vector2.zero);
            tc.fontSize = 20;
            var tcRT = tc.GetComponent<RectTransform>();
            tcRT.anchorMin = Vector2.zero; tcRT.anchorMax = Vector2.one;
            tcRT.offsetMin = new Vector2(8, 0); tcRT.offsetMax = Vector2.zero;
            inputField.placeholder = ph; inputField.textComponent = tc;

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
            var bl = CreateLabel(btnGO.transform, "ButtonLabel", "Link Account", Vector2.zero);
            bl.fontSize = 20;
            var blRT = bl.GetComponent<RectTransform>();
            blRT.anchorMin = Vector2.zero; blRT.anchorMax = Vector2.one;
            blRT.offsetMin = blRT.offsetMax = Vector2.zero;

            var status = CreateLabel(panel.transform, "StatusText", "", new Vector2(0, -255));
            status.fontSize = 18;

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

            var overlayType = System.Type.GetType("SOTL.UI.LinkOverlay, SOTL.UI");
            if (overlayType != null)
            {
                var overlay = canvasGO.AddComponent(overlayType);
                SetField(overlay, "_codeInput",  inputField);
                SetField(overlay, "_statusText", status);
                SetField(overlay, "_linkButton", btn);
            }

            Undo.RegisterCreatedObjectUndo(canvasGO, "Create Link Overlay");
            Debug.Log("[SOTL Scene] Link overlay created.");
        }

        static void CreateStatsUI()
        {
            if (GameObject.Find("StatsCanvas")) return;

            var canvasGO = new GameObject("StatsCanvas");
            canvasGO.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
            canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            var panelGO = new GameObject("StatsPanel", typeof(RectTransform));
            panelGO.transform.SetParent(canvasGO.transform, false);
            var panelRT = panelGO.GetComponent<RectTransform>();
            panelRT.anchorMin = panelRT.anchorMax = panelRT.pivot = new Vector2(0.5f, 0.5f);
            panelRT.sizeDelta = new Vector2(400f, 320f);
            panelGO.AddComponent<UnityEngine.UI.Image>().color = new Color(0.05f, 0.05f, 0.07f, 0.92f);
            panelGO.SetActive(false);

            var linked = CreateChildPanel(panelGO.transform, "LinkedPanel");
            CreateChildPanel(panelGO.transform, "LinkPromptPanel");
            CreateLabel(linked.transform,   "XPLabel",       "XP: —",       new Vector2(0, -60));
            CreateLabel(linked.transform,   "LevelLabel",    "Level —",     new Vector2(0, -100));
            CreateLabel(linked.transform,   "FameLabel",     "Fame: —",     new Vector2(0, -140));
            CreateLabel(linked.transform,   "PrestigeLabel", "Prestige: —", new Vector2(0, -180));
            CreateLabel(panelGO.transform,  "StatusLabel",   "",            new Vector2(0, 120));

            canvasGO.AddComponent(System.Type.GetType("SOTL.UI.StatsPanelToggler, SOTL.UI"));
            Undo.RegisterCreatedObjectUndo(canvasGO, "Create Stats Canvas");
            Debug.Log("[SOTL Scene] Stats UI created.");
        }

        static void CreateManagers(SOTLConfig config)
        {
            if (GameObject.Find("[Managers]")) return;
            var root = new GameObject("[Managers]");
            root.AddComponent<GameManager>();
            var apiGO = new GameObject("SOTLApiManager");
            apiGO.transform.SetParent(root.transform);
            var api = apiGO.AddComponent<SOTLApiManager>();
            var so = new SerializedObject(api);
            so.FindProperty("config").objectReferenceValue = config;
            so.ApplyModifiedPropertiesWithoutUndo();
            Undo.RegisterCreatedObjectUndo(root, "Create Managers");
            Debug.Log("[SOTL Scene] Managers created.");
        }

        static GameObject CreateChildPanel(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return go;
        }

        static UnityEngine.UI.Text CreateLabel(Transform parent, string name, string text, Vector2 pos)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(360, 40);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            var t = go.AddComponent<UnityEngine.UI.Text>();
            t.text = text;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = 22; t.color = Color.white;
            t.alignment = TextAnchor.MiddleCenter;
            return t;
        }

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
