#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using SOTL.API;

namespace SOTL.Editor
{
    public static class ProjectBootstrap
    {
        [MenuItem("SOTL/1 - Bootstrap Project", false, 10)]
        public static void Run()
        {
            Debug.Log("[SOTL Bootstrap] Starting...");

            EnsureFolder("Assets/Resources");
            EnsureFolder("Assets/SOTLData");
            EnsureFolder("Assets/Prefabs");
            EnsureFolder("Assets/Scenes");
            EnsureFolder("Assets/Synty");

            // SOTLConfig
            var config = CreateOrLoad<SOTLConfig>("Assets/Resources/SOTLConfig.asset");
            if (string.IsNullOrEmpty(config.BaseUrl))
                config.BaseUrl = "https://www.sneakonthelot.com";
            EditorUtility.SetDirty(config);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[SOTL Bootstrap] Done. Open Assets/Resources/SOTLConfig.asset and set ApiSecret.");
            EditorUtility.DisplayDialog(
                "SOTL Bootstrap Complete",
                "Assets created.\n\nNext: open Assets/Resources/SOTLConfig.asset and paste your UNITY_API_SECRET.",
                "OK");
        }

        static T CreateOrLoad<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace("\\", "/");
            string folder = System.IO.Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folder);
        }
    }
}
#endif
