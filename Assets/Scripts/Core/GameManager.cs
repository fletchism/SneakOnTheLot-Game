using UnityEngine;

namespace SOTL.Core
{
    /// <summary>
    /// Scene bootstrap singleton. Add to a persistent GameObject.
    /// Does not reference SOTL.API directly — SOTLApiManager bootstraps itself.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log("[SOTL] GameManager ready.");
        }
    }
}
