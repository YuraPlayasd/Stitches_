using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Stitches.UI
{
    /// <summary>
    /// Controller for the Main Menu UI scene.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [Header("Scene Loading")]
        [Tooltip("Name of the scene to load upon continuing")]
        public string firstLevelSceneName = "Level 1";

        [Header("Logo Settings (Stitches)")]
        public TextMeshProUGUI logoText;
        public Image logoImage;

        [Header("Prompt / Button Settings")]
        public TextMeshProUGUI promptText;
        public Button continueButton;

        [Header("Prompt Message")]
        [TextArea(1, 3)]
        public string promptMessage = "PRESS ENTER TO CONTINUE";

        private bool _isTransitioning;

        private void Start()
        {
            TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/Electronic Highway Sign SDF") ??
                                 Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");

            if (logoText != null)
            {
                logoText.text = "STITCHES";
                if (font != null) logoText.font = font;
            }

            if (promptText != null)
            {
                promptText.text = promptMessage;
                if (font != null) promptText.font = font;
            }

            if (continueButton != null)
            {
                continueButton.onClick.AddListener(StartGame);
            }
        }

        private void Update()
        {
            if (_isTransitioning) return;

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
            {
                StartGame();
            }
            else if (Input.GetKeyDown(KeyCode.Escape))
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }
        }

        public void StartGame()
        {
            if (_isTransitioning) return;
            _isTransitioning = true;

            if (Application.CanStreamedLevelBeLoaded(firstLevelSceneName))
            {
                SceneManager.LoadScene(firstLevelSceneName);
            }
            else if (SceneManager.sceneCountInBuildSettings > 1)
            {
                SceneManager.LoadScene(1);
            }
            else
            {
                _isTransitioning = false;
                Debug.LogWarning($"[MainMenuController] Scene '{firstLevelSceneName}' is not in Build Settings yet.");
            }
        }
    }
}
