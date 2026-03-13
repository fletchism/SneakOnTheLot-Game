using UnityEngine;

namespace SOTL.API
{
    /// <summary>
    /// Project-level config. Create via Assets > Create > SOTL > Config.
    /// Store in Assets/Resources/SOTLConfig.asset so it loads at runtime.
    /// NEVER commit ApiSecret to source control — set it in the asset locally.
    /// </summary>
    [CreateAssetMenu(menuName = "SOTL/Config", fileName = "SOTLConfig")]
    public class SOTLConfig : ScriptableObject
    {
        [Tooltip("Wix site base URL — no trailing slash")]
        public string BaseUrl = "https://www.sneakonthelot.com";

        [Tooltip("U8f18a612e3bc1461af24140db4d88d47c9579781c5865b4af95cd69e43affe9b")]
        public string ApiSecret = "";

        [Header("Photon Realtime")]
        [Tooltip("Photon App ID — from dashboard.photonengine.com. Never commit.")]
        public string PhotonAppId = "";

        [Tooltip("Photon region — us, eu, asia, etc.")]
        public string PhotonRegion = "us";
    }
}
