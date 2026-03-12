using UnityEngine;
using UnityEngine.UI;
using SOTL.API;

namespace SOTL.UI
{
    /// <summary>
    /// Full-screen overlay shown on launch if no Wix account is linked.
    /// Enter a link code from sneakonthelot.com/my-stats → dismisses on success.
    /// </summary>
    public class LinkOverlay : MonoBehaviour
    {
        [Header("UI References (auto-wired by builder)")]
        [SerializeField] InputField _codeInput;
        [SerializeField] Text       _statusText;
        [SerializeField] Button     _linkButton;

        void Start()
        {
            // Hide immediately if already linked
            if (SOTLApiManager.Instance != null && SOTLApiManager.Instance.IsLinked)
            {
                gameObject.SetActive(false);
                return;
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible   = true;

            // Disable player controller so keyboard goes to input field
            var player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                var ctrl = player.GetComponent<UnityEngine.MonoBehaviour>();
                // Disable all MonoBehaviours on Player except essential ones
                foreach (var mb in player.GetComponents<UnityEngine.MonoBehaviour>())
                {
                    if (mb.GetType().Name == "LotPlayerController")
                        mb.enabled = false;
                }
            }

            if (_linkButton != null)
                _linkButton.onClick.AddListener(OnLinkPressed);

            SetStatus("Enter your link code from sneakonthelot.com/my-stats", Color.white);
        }

        void OnLinkPressed()
        {
            var code = _codeInput != null ? _codeInput.text.Trim() : "";
            if (string.IsNullOrEmpty(code))
            {
                SetStatus("Please enter a code.", Color.yellow);
                return;
            }

            SetStatus("Linking...", Color.cyan);
            _linkButton.interactable = false;

            SOTLApiManager.Instance.LinkAccount(code, (ok, memberId) =>
            {
                if (ok)
                {
                    SetStatus("Linked! Welcome to the Lot.", Color.green);
                    Invoke(nameof(Dismiss), 1.2f);
                }
                else
                {
                    SetStatus("Code not found. Try again.", Color.red);
                    _linkButton.interactable = true;
                }
            });
        }

        void Dismiss()
        {
            // Re-enable player controller
            var player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                foreach (var mb in player.GetComponents<UnityEngine.MonoBehaviour>())
                {
                    if (mb.GetType().Name == "LotPlayerController")
                        mb.enabled = true;
                }
            }

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible   = false;
            gameObject.SetActive(false);
        }

        void SetStatus(string msg, Color color)
        {
            if (_statusText == null) return;
            _statusText.text  = msg;
            _statusText.color = color;
        }
    }
}
