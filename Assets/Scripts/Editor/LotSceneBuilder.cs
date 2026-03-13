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
        // Character mesh — confirmed present in project
        const string CHAR_PREFAB  = "Assets/Synty/SidekickCharacters/Characters/HumanSpecies/HumanSpecies_01/HumanSpecies_01.prefab";
        // Locomotion controller — confirmed present in project
        const string PLAYER_ANIM  = "Assets/Synty/AnimationBaseLocomotion/Animations/Sidekick/AC_Sidekick_Masculine.controller";
        // NPC
        const string NPC_PREFAB   = "Assets/Synty/SidekickCharacters/Characters/ModernCivilians/ModernCivilian_01/ModernCivilian_01.prefab";
        const string IDLE_CTRL    = "Assets/Animations/NPC/AC_NPC_Idle_Masculine.controller";

        // Reflection type strings (Assembly-CSharp — no direct asmdef reference from Editor)
        const string HUD_PREFAB        = "Assets/Synty/InterfaceMilitaryCombatHUD/Prefabs/_PreMadeHUDs/Screen_MilitaryCombat_HUD_LooterShooter_01.prefab";
        const string T_HUD_CTRL        = "SOTL.UI.LotHUDController, SOTL.UI";
        const string T_INPUT_READER    = "Synty.AnimationBaseLocomotion.Samples.InputSystem.InputReader, Assembly-CSharp";
        const string T_PLAYER_CTRL     = "SOTL.Player.LotPlayerController, Assembly-CSharp";
        const string T_CAMERA_CTRL     = "SOTL.Player.LotCameraController, Assembly-CSharp";
        const string T_PRESTIGE_SYNC   = "SOTL.Pickups.PrestigeSyncManager, Assembly-CSharp";
        const string T_PRESTIGE_PICKUP = "SOTL.Pickups.PrestigePickup, Assembly-CSharp";
        const string T_LOCAL_CHAR_SYNC = "SOTL.Player.LocalCharacterSync, Assembly-CSharp";
        const string T_SIDEKICK_MGR    = "SOTL.Multiplayer.SidekickCharacterManager, SOTL.Multiplayer";
        const string T_REMOTE_MGR      = "SOTL.Multiplayer.RemotePlayerManager, SOTL.Multiplayer";
        const string T_CUSTOMIZE_UI   = "SOTL.Player.CharacterCustomizationUI, Assembly-CSharp";

        [MenuItem("SOTL/Scene/Clean Rebuild (delete + rebuild)", false, 10)]
        public static void CleanRebuild()
        {
            bool confirmed = EditorUtility.DisplayDialog(
                "Clean Rebuild",
                "This will delete ALL objects in the current scene and rebuild from scratch.\n\nContinue?",
                "Rebuild", "Cancel");
            if (!confirmed) return;

            // Delete every root GameObject in the scene
            var roots = EditorSceneManager.GetActiveScene().GetRootGameObjects();
            foreach (var go in roots)
                Object.DestroyImmediate(go);

            Debug.Log("[SOTL Scene] Scene cleared. Rebuilding...");

            // Create NPC idle controller asset first (Build() depends on it)
            CreateNPCIdleController();

            // Full build
            Build();
        }

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
            CreateCameraRig();
            CreateNPC();
            CreateLighting();
            EnsureEventSystem();
            CreateStatsUI();
            CreateLinkOverlay();
            CreateDialogueUI();
            CreateManagers(config);
            CreatePrestigePickup();
            CreateHUD();
            CreateCustomizationUI();

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
            // Remove stale Synty sample prefab instances (never our objects)
            foreach (var n in new[] { "SyntyCamera", "PF_SyntyCamera", "PF_SidekickPlayer" })
            {
                var stale = GameObject.Find(n);
                if (stale != null) { Object.DestroyImmediate(stale); Debug.Log($"[SOTL Scene] Removed stale: {n}"); }
            }

            // If a fully-built Player already exists, leave it alone
            var existing = GameObject.Find("Player");
            if (existing != null)
            {
                var hasCtrl = existing.GetComponent(System.Type.GetType(T_PLAYER_CTRL)) != null;
                if (hasCtrl) { Debug.Log("[SOTL Scene] Player already built — skipping."); return; }
                // Player exists but is missing our component — remove and rebuild
                Object.DestroyImmediate(existing);
                Debug.Log("[SOTL Scene] Removed incomplete Player — rebuilding.");
            }

            // ── Root ──────────────────────────────────────────────────
            var player = new GameObject("Player");
            player.tag   = "Player";
            player.layer = 3; // Character layer (matches original PF_SidekickPlayer)

            // ── CharacterController ───────────────────────────────────
            var cc = player.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.radius = 0.3f;

            // ── InputReader (Assembly-CSharp via reflection) ──────────
            var inputReaderType = System.Type.GetType(T_INPUT_READER);
            if (inputReaderType != null)
                player.AddComponent(inputReaderType);
            else
                Debug.LogWarning("[SOTL Scene] InputReader type not found — recompile first.");

            // ── Helper transforms (camera + ray casts) ────────────────
            var rearRay = new GameObject("RearRayPos").transform;
            rearRay.SetParent(player.transform);
            rearRay.localPosition = new Vector3(0f, 0.8f, -0.15f);

            var frontRay = new GameObject("FrontRayPos").transform;
            frontRay.SetParent(player.transform);
            frontRay.localPosition = new Vector3(0f, 0.8f, 0.15f);

            // Camera follows this point
            var lookAt = new GameObject("SyntyPlayer_LookAt").transform;
            lookAt.SetParent(player.transform);
            lookAt.localPosition = new Vector3(0f, 1.6f, 0f);

            // Lock-on target
            var lockOn = new GameObject("TargetLockOnPos").transform;
            lockOn.SetParent(player.transform);
            lockOn.localPosition = new Vector3(0f, 1.0f, 0f);

            // ── Character mesh ────────────────────────────────────────
            Animator meshAnimator = null;
            var charPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CHAR_PREFAB);
            if (charPrefab != null)
            {
                var meshGO = (GameObject)PrefabUtility.InstantiatePrefab(charPrefab, player.transform);
                meshGO.transform.localPosition = Vector3.zero;
                meshGO.transform.localRotation = Quaternion.identity;
                meshAnimator = meshGO.GetComponentInChildren<Animator>();

                var animCtrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(PLAYER_ANIM);
                if (animCtrl != null && meshAnimator != null)
                {
                    meshAnimator.runtimeAnimatorController = animCtrl;
                    Debug.Log("[SOTL Scene] HumanSpecies_01 mesh attached, AC_Sidekick_Masculine assigned.");
                }
                else
                    Debug.LogWarning("[SOTL Scene] Could not assign player animator controller.");
            }
            else
                Debug.LogWarning($"[SOTL Scene] Character prefab not found: {CHAR_PREFAB}");

            // ── LotPlayerController (Assembly-CSharp via reflection) ──
            var playerCtrlType = System.Type.GetType(T_PLAYER_CTRL);
            if (playerCtrlType != null)
            {
                var ctrl = player.AddComponent(playerCtrlType);
                if (meshAnimator != null) SetField(ctrl, "_animator",    meshAnimator);
                SetField(ctrl, "_rearRayPos",  rearRay);
                SetField(ctrl, "_frontRayPos", frontRay);
                // _cameraController wired in CreateCameraRig() after rig exists
            }
            else
                Debug.LogWarning("[SOTL Scene] LotPlayerController type not found — recompile first.");

            // ── LocalCharacterSync (broadcasts appearance to Photon) ──
            var localSyncType = System.Type.GetType(T_LOCAL_CHAR_SYNC);
            if (localSyncType != null)
                player.AddComponent(localSyncType);
            else
                Debug.LogWarning("[SOTL Scene] LocalCharacterSync type not found — compile first.");

            Undo.RegisterCreatedObjectUndo(player, "Create Player");
            Debug.Log("[SOTL Scene] Player created.");
        }

        static void CreateCameraRig()
        {
            // If a fully-built CameraRig already exists, leave it alone
            var existingRig = GameObject.Find("CameraRig");
            if (existingRig != null)
            {
                var hasCtrl = existingRig.GetComponent(System.Type.GetType(T_CAMERA_CTRL)) != null;
                if (hasCtrl) { Debug.Log("[SOTL Scene] CameraRig already built — skipping."); return; }
                Object.DestroyImmediate(existingRig);
                Debug.Log("[SOTL Scene] Removed incomplete CameraRig — rebuilding.");
            }

            // Remove default Main Camera if not already under a rig
            var defaultCam = GameObject.Find("Main Camera");
            if (defaultCam != null && defaultCam.transform.parent == null)
            {
                Object.DestroyImmediate(defaultCam);
                Debug.Log("[SOTL Scene] Removed default Main Camera.");
            }

            var rig = new GameObject("CameraRig");

            // ── Main Camera as child ──────────────────────────────────
            var camGO = new GameObject("Main Camera");
            camGO.transform.SetParent(rig.transform);
            camGO.transform.localPosition = new Vector3(0f, 0.5f, -3.5f);
            camGO.tag = "MainCamera";
            var cam = camGO.AddComponent<Camera>();
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane  = 500f;
            camGO.AddComponent<AudioListener>();

            // URP camera data
            camGO.AddComponent<UniversalAdditionalCameraData>();

            // ── LotCameraController ───────────────────────────────────
            var player = GameObject.FindWithTag("Player");
            var camCtrlType = System.Type.GetType(T_CAMERA_CTRL);
            Component camCtrl = null;
            if (camCtrlType != null)
            {
                camCtrl = rig.AddComponent(camCtrlType);
                if (player != null) SetField(camCtrl, "_character",   player);
                SetField(camCtrl, "_mainCamera", cam);
                Debug.Log("[SOTL Scene] LotCameraController added to CameraRig.");
            }
            else
                Debug.LogWarning("[SOTL Scene] LotCameraController type not found — recompile first.");

            // ── Wire camera controller back into LotPlayerController ──
            if (player != null && camCtrl != null)
            {
                var playerCtrlType = System.Type.GetType(T_PLAYER_CTRL);
                if (playerCtrlType != null)
                {
                    var playerCtrl = player.GetComponent(playerCtrlType);
                    if (playerCtrl != null)
                        SetField(playerCtrl, "_cameraController", camCtrl);
                }
            }

            Undo.RegisterCreatedObjectUndo(rig, "Create CameraRig");
            Debug.Log("[SOTL Scene] CameraRig created.");
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
            var scaler = canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode         = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight  = 0.5f;
            canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            var panelGO = new GameObject("StatsPanel", typeof(RectTransform));
            panelGO.transform.SetParent(canvasGO.transform, false);
            var panelRT = panelGO.GetComponent<RectTransform>();
            panelRT.anchorMin = panelRT.anchorMax = panelRT.pivot = new Vector2(0.5f, 0.5f);
            panelRT.sizeDelta = new Vector2(520f, 420f);
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

            // Network manager — wired with config so PhotonAppId is available at Start()
            var netGO   = new GameObject("LotNetworkManager");
            netGO.transform.SetParent(root.transform);
            var netType = System.Type.GetType("SOTL.Multiplayer.LotNetworkManager, SOTL.Multiplayer");
            if (netType != null)
            {
                var net   = netGO.AddComponent(netType);
                var netSO = new SerializedObject(net);
                netSO.FindProperty("_config").objectReferenceValue = config;
                netSO.ApplyModifiedPropertiesWithoutUndo();
            }
            else
                Debug.LogWarning("[SOTL Scene] LotNetworkManager type not found — compile first.");

            var apiGO = new GameObject("SOTLApiManager");
            apiGO.transform.SetParent(root.transform);
            var api = apiGO.AddComponent<SOTLApiManager>();
            var so = new SerializedObject(api);
            so.FindProperty("config").objectReferenceValue = config;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Prestige sync — accumulates pickup prestige, flushes to Wix every 15 min
            var syncGO = new GameObject("PrestigeSyncManager");
            syncGO.transform.SetParent(root.transform);
            var syncType = System.Type.GetType(T_PRESTIGE_SYNC);
            if (syncType != null)
                syncGO.AddComponent(syncType);
            else
                Debug.LogWarning("[SOTL Scene] PrestigeSyncManager type not found — compile first.");

            // SidekickCharacterManager — wraps Synty runtime for character building
            var skMgrGO = new GameObject("SidekickCharacterManager");
            skMgrGO.transform.SetParent(root.transform);
            var skMgrType = System.Type.GetType(T_SIDEKICK_MGR);
            if (skMgrType != null)
            {
                var skMgr = skMgrGO.AddComponent(skMgrType);
                // Wire the locomotion controller so built characters get animation
                var animCtrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(PLAYER_ANIM);
                if (animCtrl != null)
                {
                    var skSO = new SerializedObject(skMgr);
                    skSO.FindProperty("_animatorController").objectReferenceValue = animCtrl;
                    skSO.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            else
                Debug.LogWarning("[SOTL Scene] SidekickCharacterManager type not found — compile first.");

            // RemotePlayerManager — spawns/rebuilds remote players from Photon properties
            var remoteMgrGO = new GameObject("RemotePlayerManager");
            remoteMgrGO.transform.SetParent(root.transform);
            var remoteMgrType = System.Type.GetType(T_REMOTE_MGR);
            if (remoteMgrType != null)
                remoteMgrGO.AddComponent(remoteMgrType);
            else
                Debug.LogWarning("[SOTL Scene] RemotePlayerManager type not found — compile first.");

            Undo.RegisterCreatedObjectUndo(root, "Create Managers");
            Debug.Log("[SOTL Scene] Managers created.");
        }

        static void CreatePrestigePickup()
        {
            const string FX_PATH     = "Assets/PolygonParticleFX/Prefabs/FX_Pickup_Boost_01.prefab";
            const string PICKUP_NAME = "PrestigePickup_01";

            if (GameObject.Find(PICKUP_NAME))
            {
                Debug.Log("[SOTL Scene] PrestigePickup already in scene.");
                return;
            }

            // Root — trigger collider only, no mesh
            var go = new GameObject(PICKUP_NAME);
            go.transform.position = new Vector3(-4f, 0.6f, 4f);

            var col = go.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius    = 0.8f;

            // Visual — FX_Pickup_Boost_01 looping particles as child
            var fxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FX_PATH);
            if (fxPrefab != null)
            {
                var fx = (GameObject)PrefabUtility.InstantiatePrefab(fxPrefab, go.transform);
                fx.transform.localPosition = Vector3.zero;
                fx.transform.localRotation = Quaternion.identity;
            }
            else
                Debug.LogWarning("[SOTL Scene] FX_Pickup_Boost_01.prefab not found — assign visual manually.");

            // PrestigePickup component
            var pickupType = System.Type.GetType(T_PRESTIGE_PICKUP);
            if (pickupType == null)
            {
                Debug.LogWarning("[SOTL Scene] PrestigePickup type not found — compile first.");
                return;
            }
            var pickup   = go.AddComponent(pickupType);
            var pickupSO = new SerializedObject(pickup);
            pickupSO.FindProperty("prestigeAmount").floatValue = 1f;
            pickupSO.ApplyModifiedPropertiesWithoutUndo();

            Undo.RegisterCreatedObjectUndo(go, "Create Prestige Pickup");
            Debug.Log("[SOTL Scene] PrestigePickup_01 placed at (-4, 0.6, 4) — clear of NPC at (3,0,3).");
        }

        static void CreateHUD()
        {
            const string HUD_GO_NAME = "HUD_MilitaryCombat_LooterShooter";
            if (GameObject.Find(HUD_GO_NAME))
            {
                Debug.Log("[SOTL Scene] HUD already in scene — skipping.");
                return;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HUD_PREFAB);
            if (prefab == null)
            {
                Debug.LogWarning($"[SOTL Scene] HUD prefab not found: {HUD_PREFAB}");
                return;
            }

            // ── Canvas wrapper (prefab has no Canvas of its own) ──────────────
            var canvasGO = new GameObject(HUD_GO_NAME);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1;
            canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
            canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            // ── Instantiate Synty HUD as child of canvas ──────────────────────
            var hudGO = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvasGO.transform);
            hudGO.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;

            // ── Find layout regions ───────────────────────────────────────────
            var topLeft  = FindDeepChild(hudGO.transform, "Top Left");
            var topRight = FindDeepChild(hudGO.transform, "Top Right");

            // ── Create TMP stat labels ────────────────────────────────────────
            var xpText       = CreateTMPLabel(topLeft,  "XP_Text",       "XP  —",       new Vector2(10f, -10f));
            var levelText    = CreateTMPLabel(topLeft,  "Level_Text",    "LVL  —",      new Vector2(10f, -50f));
            var fameText     = CreateTMPLabel(topRight, "Fame_Text",     "FAME  —",     new Vector2(-10f, -10f));
            var prestigeText = CreateTMPLabel(topRight, "Prestige_Text", "PRESTIGE  —", new Vector2(-10f, -50f));

            // ── Add and wire LotHUDController ─────────────────────────────────
            var hudCtrlType = System.Type.GetType(T_HUD_CTRL);
            if (hudCtrlType == null)
            {
                Debug.LogWarning("[SOTL Scene] LotHUDController type not found — recompile first.");
                Undo.RegisterCreatedObjectUndo(canvasGO, "Create HUD");
                return;
            }

            var ctrl = canvasGO.AddComponent(hudCtrlType);
            SetField(ctrl, "_xpText",       xpText);
            SetField(ctrl, "_levelText",    levelText);
            SetField(ctrl, "_fameText",     fameText);
            SetField(ctrl, "_prestigeText", prestigeText);

            Undo.RegisterCreatedObjectUndo(canvasGO, "Create HUD");
            Debug.Log("[SOTL Scene] HUD created and wired.");
        }

        static void CreateCustomizationUI()
        {
            if (GameObject.Find("CustomizationCanvas"))
            {
                Debug.Log("[SOTL Scene] CustomizationCanvas already in scene — skipping.");
                return;
            }

            var canvasGO = new GameObject("CustomizationCanvas");
            var customizeType = System.Type.GetType(T_CUSTOMIZE_UI);
            if (customizeType != null)
                canvasGO.AddComponent(customizeType);
            else
                Debug.LogWarning("[SOTL Scene] CharacterCustomizationUI type not found — compile first.");

            Undo.RegisterCreatedObjectUndo(canvasGO, "Create CustomizationCanvas");
            Debug.Log("[SOTL Scene] CustomizationCanvas created.");
        }

        static Transform FindDeepChild(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name) return child;
                var found = FindDeepChild(child, name);
                if (found != null) return found;
            }
            return null;
        }

        static TMPro.TMP_Text CreateTMPLabel(Transform parent, string name, string defaultText, Vector2 anchoredPos)
        {
            if (parent == null)
            {
                Debug.LogWarning($"[SOTL Scene] CreateTMPLabel: parent is null for {name}");
                return null;
            }
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta       = new Vector2(300f, 40f);
            rt.anchorMin       = new Vector2(0f, 1f);
            rt.anchorMax       = new Vector2(0f, 1f);
            rt.pivot           = new Vector2(0f, 1f);
            rt.anchoredPosition = anchoredPos;
            var tmp = go.AddComponent<TMPro.TextMeshProUGUI>();
            tmp.text      = defaultText;
            tmp.fontSize  = 22f;
            tmp.color     = Color.white;
            return tmp;
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
            if (f != null)
            {
                f.SetValue(obj, value);
                // Mark dirty so Unity serializes the reference to the scene file
                if (obj is UnityEngine.Object unityObj)
                    EditorUtility.SetDirty(unityObj);
            }
            else
                Debug.LogWarning($"[SOTL Scene] Field not found: {obj.GetType().Name}.{fieldName}");
        }
    }
}
#endif

