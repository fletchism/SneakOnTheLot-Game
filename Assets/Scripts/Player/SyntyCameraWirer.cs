using UnityEngine;

namespace SOTL.Player
{
    /// <summary>
    /// Wires SampleCameraController._syntyCharacter at Awake() — before Start() fires —
    /// so the field is set even when the scene was built procedurally.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class SyntyCameraWirer : MonoBehaviour
    {
        void Awake()
        {
            var player = GameObject.FindWithTag("Player");
            if (player == null)
            {
                Debug.LogWarning("[SyntyCameraWirer] No GameObject tagged 'Player' found.");
                return;
            }

            foreach (var mb in GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null || mb == this) continue;
                var field = mb.GetType().GetField("_syntyCharacter",
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.Instance);
                if (field == null) continue;
                field.SetValue(mb, player);
                Debug.Log($"[SyntyCameraWirer] Wired {mb.GetType().Name}._syntyCharacter → {player.name}");
                return;
            }

            Debug.LogWarning("[SyntyCameraWirer] No component with _syntyCharacter found on camera hierarchy.");
        }
    }
}
