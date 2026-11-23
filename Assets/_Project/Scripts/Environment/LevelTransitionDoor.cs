using Stitches.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Stitches.Environment
{
    public class LevelTransitionDoor : MonoBehaviour
    {
        [Header("Scene Transition Settings")]
        [Tooltip("Name of the scene to load upon entering the door")]
        public string nextSceneName = "Level 2";
        [Tooltip("If enabled, loads scene by build index instead of name")]
        public bool loadByIndex = false;
        public int nextSceneIndex = 1;

        [Header("Interaction Settings")]
        public KeyCode interactKey = KeyCode.E;

        [Header("UI Prompt")]
        public GameObject promptUI;

        [Header("Optional Door Visuals")]
        public Sprite openDoorSprite;

        private bool _isPlayerNear;
        private SpriteRenderer _spriteRenderer;

        private void Start()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();

            if (promptUI == null)
            {
                GameObject dt = GameObject.Find("DoorText");
                if (dt != null) promptUI = dt;
            }

            if (promptUI != null)
            {
                promptUI.SetActive(false);
            }
        }

        private void Update()
        {
            if (_isPlayerNear && Input.GetKeyDown(interactKey))
            {
                EnterLevel();
            }
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (collision.CompareTag("Player"))
            {
                _isPlayerNear = true;
                if (promptUI != null) promptUI.SetActive(true);
            }
        }

        private void OnTriggerExit2D(Collider2D collision)
        {
            if (collision.CompareTag("Player"))
            {
                _isPlayerNear = false;
                if (promptUI != null) promptUI.SetActive(false);
            }
        }

        private void EnterLevel()
        {
            PlayerController player = Object.FindAnyObjectByType<PlayerController>();
            if (player != null)
            {
                player.health = player.maxHealth;
                player.UpdateHealthUI();
                PlayerController.SaveArrowsCount(player.arrowsCount);
            }

            if (openDoorSprite != null && _spriteRenderer != null)
            {
                _spriteRenderer.sprite = openDoorSprite;
            }

            if (promptUI != null)
            {
                promptUI.SetActive(false);
            }

            if (loadByIndex)
            {
                SceneManager.LoadScene(nextSceneIndex);
            }
            else if (!string.IsNullOrEmpty(nextSceneName))
            {
                string target = nextSceneName;
                if ((target == "LevelHub" || target == "Level2") && Application.CanStreamedLevelBeLoaded("Level 2"))
                {
                    target = "Level 2";
                }
                SceneManager.LoadScene(target);
            }
            else
            {
                int currentIndex = SceneManager.GetActiveScene().buildIndex;
                SceneManager.LoadScene(currentIndex + 1);
            }
        }
    }
}
