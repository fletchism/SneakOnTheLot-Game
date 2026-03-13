using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace SOTL.API
{
    /// <summary>
    /// Singleton. Handles all REST calls to the Wix SOTL backend.
    /// Endpoints: memberState, xpAward, unityLink
    /// Auth: Bearer UNITY_API_SECRET (set in SOTLConfig ScriptableObject)
    /// </summary>
    public class SOTLApiManager : MonoBehaviour
    {
        public static SOTLApiManager Instance { get; private set; }

        [Header("Config")]
        [SerializeField] private SOTLConfig config;

        // Cached player state
        public PlayerState CurrentState { get; private set; }
        public bool IsLinked => !string.IsNullOrEmpty(LinkedMemberId);
        public string LinkedMemberId
        {
            get => PlayerPrefs.GetString("sotl_member_id", "");
            private set => PlayerPrefs.SetString("sotl_member_id", value);
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // ── Link ──────────────────────────────────────────────────────────────

        /// <summary>POST /_functions/unityLink  body:{code}</summary>
        public void LinkAccount(string code, Action<bool, string> callback)
        {
            StartCoroutine(PostLink(code, callback));
        }

        IEnumerator PostLink(string code, Action<bool, string> callback)
        {
            var url = $"{config.BaseUrl}/_functions/unityLink";
            var json = $"{{\"code\":\"{code}\"}}";
            using var req = new UnityWebRequest(url, "POST");
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                var resp = JsonUtility.FromJson<LinkResponse>(req.downloadHandler.text);
                LinkedMemberId = resp.memberId;
                callback?.Invoke(true, resp.memberId);
            }
            else
            {
                Debug.LogWarning($"[SOTL] Link failed: {req.error}");
                callback?.Invoke(false, null);
            }
        }

        // ── Member State ──────────────────────────────────────────────────────

        /// <summary>GET /_functions/memberState?memberId=</summary>
        public void FetchMemberState(Action<PlayerState> callback)
        {
            if (!IsLinked) { callback?.Invoke(null); return; }
            StartCoroutine(GetMemberState(callback));
        }

        IEnumerator GetMemberState(Action<PlayerState> callback)
        {
            var url = $"{config.BaseUrl}/_functions/memberState?memberId={LinkedMemberId}";
            using var req = UnityWebRequest.Get(url);
            req.SetRequestHeader("Authorization", $"Bearer {config.ApiSecret}");
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("[SOTL] FetchState response: " + req.downloadHandler.text);
                CurrentState = JsonUtility.FromJson<PlayerState>(req.downloadHandler.text);
                callback?.Invoke(CurrentState);
            }
            else
            {
                Debug.LogWarning("[SOTL] FetchState failed: " + req.error + " | HTTP " + req.responseCode + " | " + req.downloadHandler.text);
                callback?.Invoke(null);
            }
        }

        // ── XP Award ─────────────────────────────────────────────────────────

        /// <summary>POST /_functions/xpAward  — fire and forget, fail-open</summary>
        public void AwardXP(string source, string sourceId = null, string metadata = null)
        {
            if (!IsLinked) return;
            StartCoroutine(PostXpAward(source, sourceId, metadata));
        }

        IEnumerator PostXpAward(string source, string sourceId, string metadata)
        {
            var url = $"{config.BaseUrl}/_functions/xpAward";
            var body = new XpAwardRequest
            {
                memberId = LinkedMemberId,
                source = source,
                sourceId = sourceId ?? "",
                metadata = metadata ?? ""
            };
            var json = JsonUtility.ToJson(body);
            using var req = new UnityWebRequest(url, "POST");
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", $"Bearer {config.ApiSecret}");
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
                Debug.LogWarning($"[SOTL] XP award failed (fail-open): {req.error}");
        }

        // ── Prestige Award ────────────────────────────────────────────────────

        /// <summary>POST /_functions/prestigeAward — batched flush, fail-open</summary>
        public void AwardPrestige(float amount, Action<bool, float> callback = null)
        {
            if (!IsLinked) return;
            StartCoroutine(PostPrestigeAward(amount, callback));
        }

        IEnumerator PostPrestigeAward(float amount, Action<bool, float> callback)
        {
            var url  = $"{config.BaseUrl}/_functions/prestigeAward";
            var body = new PrestigeAwardRequest { memberId = LinkedMemberId, amount = amount };
            var json = JsonUtility.ToJson(body);
            using var req = new UnityWebRequest(url, "POST");
            req.uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type",  "application/json");
            req.SetRequestHeader("Authorization", $"Bearer {config.ApiSecret}");
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                var resp = JsonUtility.FromJson<PrestigeAwardResponse>(req.downloadHandler.text);
                if (CurrentState != null) CurrentState.prestigeBalance = resp.prestigeBalance;
                callback?.Invoke(true, resp.prestigeBalance);
            }
            else
            {
                Debug.LogWarning($"[SOTL] Prestige award failed (fail-open): {req.error}");
                callback?.Invoke(false, 0f);
            }
        }
    }

    // ── Data models ───────────────────────────────────────────────────────────

    [Serializable] public class LinkResponse         { public string memberId; }
    [Serializable] public class XpAwardRequest       { public string memberId, source, sourceId, metadata; }
    [Serializable] public class PrestigeAwardRequest { public string memberId; public float amount; }
    [Serializable] public class PrestigeAwardResponse{ public bool success; public float prestigeBalance; }

    [Serializable]
    public class PlayerState
    {
        public string memberId;
        public int totalXp;
        public int xpLevel;
        public float prestigeBalance;
        public float fame;
        public bool unityLinked;
    }
}
