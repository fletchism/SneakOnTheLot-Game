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

            // Disable player + camera so input goes to the overlay
            FreezeGame(true);

            if (_linkButton != null)
                _linkButton.onClick.AddListener(OnLinkPressed);

            // Wire skip button if it exists
            var skipBtn = GetComponentInChildren<Button>(true);
            // Find the skip button by searching children (it's not the link button)
            foreach (var btn in GetComponentsInChildren<Button>(true))
            {
                if (btn != _linkButton)
                {
                    btn.onClick.AddListener(Dismiss);
                    break;
                }
            }

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
            FreezeGame(false);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible   = false;
            gameObject.SetActive(false);
        }

        void FreezeGame(bool freeze)
        {
            var player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                foreach (var mb in player.GetComponents<MonoBehaviour>())
                {
                    var name = mb.GetType().Name;
                    if (name == "LotPlayerController" || name == "LotCameraController")
                        mb.enabled = !freeze;
                }
            }
            // Also check camera rig for camera controller
            foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if (mb.GetType().Name == "LotCameraController")
                    mb.enabled = !freeze;
            }
        }

        void SetStatus(string msg, Color color)
        {
            if (_statusText == null) return;
            _statusText.text  = msg;
            _statusText.color = color;
        }
    }
}
