using Stitches.Enemies;
using Stitches.Player;
using Stitches.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace Stitches.Items
{
    /// <summary>
    /// Allows the player to collect Frank's torso once the two guarding spirits are defeated.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class TorsoCollectible : MonoBehaviour
    {
        [Header("Guarding Spirits")]
        public EnemyController spirit1;
        public EnemyController spirit2;

        [Header("Events")]
        public UnityEvent onTorsoCollected;

        [Header("Interaction Settings")]
        public KeyCode pickupKey = KeyCode.E;
        public float interactionDistance = 2.8f;
        public bool requireKeyPress = true;

        [Header("Bobbing Animation")]
        public bool enableBobbing = true;
        public float bobbingSpeed = 2.5f;
        public float bobbingHeight = 0.08f;

        [Header("UI Prompt")]
        public GameObject promptUI;
        public TextMeshProUGUI promptText;

        private Vector3 _initialPosition;
        private bool _isPlayerNear;
        private bool _isCollected;
        private Transform _playerTransform;

        private void Awake()
        {
            _initialPosition = transform.position;
            AutoFindReferences();
        }

        private void Start()
        {
            _initialPosition = transform.position;
            AutoFindReferences();
            EnsureTriggerCollider();
            FindOrBuildPromptUI();
            if (promptUI != null) promptUI.SetActive(false);
        }

        private void AutoFindReferences()
        {
            if (spirit1 == null || spirit2 == null)
            {
                GameObject s1Obj = GameObject.Find("Enemy_Spirit");
                if (s1Obj != null) spirit1 = s1Obj.GetComponent<EnemyController>();

                GameObject s2Obj = GameObject.Find("Enemy_Spirit (1)");
                if (s2Obj != null) spirit2 = s2Obj.GetComponent<EnemyController>();

                if (spirit1 == null || spirit2 == null)
                {
                    EnemyController[] allEnemies = Object.FindObjectsByType<EnemyController>(FindObjectsInactive.Include);
                    foreach (EnemyController enemy in allEnemies)
                    {
                        if (enemy.enemyType == EnemyType.Spirit)
                        {
                            if (spirit1 == null) spirit1 = enemy;
                            else if (spirit2 == null && enemy != spirit1) spirit2 = enemy;
                        }
                    }
                }
            }

            if (_playerTransform == null)
            {
                PlayerController p = Object.FindAnyObjectByType<PlayerController>();
                if (p != null)
                {
                    _playerTransform = p.transform;
                }
                else
                {
                    GameObject pObj = GameObject.FindWithTag("Player");
                    if (pObj != null) _playerTransform = pObj.transform;
                }
            }
        }

        private void EnsureTriggerCollider()
        {
            Collider2D col = GetComponent<Collider2D>();
            if (col == null)
            {
                CircleCollider2D circle = gameObject.AddComponent<CircleCollider2D>();
                circle.isTrigger = true;
                circle.radius = 1.0f;
            }
            else
            {
                col.isTrigger = true;
            }
        }

        private void Update()
        {
            if (_isCollected) return;

            if (enableBobbing)
            {
                float newY = _initialPosition.y + Mathf.Sin(Time.time * bobbingSpeed) * bobbingHeight;
                transform.position = new Vector3(_initialPosition.x, newY, _initialPosition.z);
            }

            if (_playerTransform != null)
            {
                float dist = Vector2.Distance(transform.position, _playerTransform.position);
                bool near = dist <= interactionDistance;
                if (near != _isPlayerNear)
                {
                    _isPlayerNear = near;
                    UpdatePromptVisibility();
                }
            }

            if (_isPlayerNear)
            {
                bool spiritsDefeated = AreSpiritsDefeated();
                UpdatePromptText(spiritsDefeated);

                if (spiritsDefeated && (!requireKeyPress || Input.GetKeyDown(pickupKey)))
                {
                    CollectTorso();
                }
            }
        }

        private bool AreSpiritsDefeated()
        {
            bool s1Dead = spirit1 == null || spirit1.IsDead;
            bool s2Dead = spirit2 == null || spirit2.IsDead;
            return s1Dead && s2Dead;
        }

        private void UpdatePromptVisibility()
        {
            if (promptUI != null)
            {
                promptUI.SetActive(_isPlayerNear);
            }
        }

        private void UpdatePromptText(bool spiritsDefeated)
        {
            if (promptText == null) return;

            if (!spiritsDefeated)
            {
                promptText.text = "Defeat the spirits to claim the Torso!";
                promptText.color = new Color(1f, 0.4f, 0.4f);
            }
            else
            {
                promptText.text = $"[ {pickupKey} ] Pick up Frank's Torso";
                promptText.color = new Color(0.35f, 0.95f, 0.72f);
            }
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (collision.CompareTag("Player") || collision.GetComponent<PlayerController>() != null)
            {
                _isPlayerNear = true;
                UpdatePromptVisibility();
            }
        }

        private void OnTriggerExit2D(Collider2D collision)
        {
            if (collision.CompareTag("Player") || collision.GetComponent<PlayerController>() != null)
            {
                _isPlayerNear = false;
                UpdatePromptVisibility();
            }
        }

        private void CollectTorso()
        {
            if (_isCollected) return;
            _isCollected = true;

            if (promptUI != null) promptUI.SetActive(false);

            BodyPartsTracker.RegisterPart(CollectibleType.FrankTorso);

            SpriteRenderer sr = GetComponent<SpriteRenderer>();
            if (sr != null) sr.enabled = false;
            Collider2D col = GetComponent<Collider2D>();
            if (col != null) col.enabled = false;

            onTorsoCollected?.Invoke();
            gameObject.SetActive(false);
        }

        private void FindOrBuildPromptUI()
        {
            if (promptUI != null) return;

            GameObject existing = GameObject.Find("PickupPrompt");
            if (existing != null)
            {
                promptUI = existing;
                promptText = existing.GetComponentInChildren<TextMeshProUGUI>();
                return;
            }

            Canvas canvas = Object.FindAnyObjectByType<Canvas>();
            if (canvas == null) return;

            GameObject promptObj = new GameObject("TorsoPickupPrompt", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            promptObj.transform.SetParent(canvas.transform, false);

            RectTransform rt = promptObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.2f, 0.15f);
            rt.anchorMax = new Vector2(0.8f, 0.25f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            promptText = promptObj.GetComponent<TextMeshProUGUI>();
            promptText.text = $"[ {pickupKey} ] Pick up Frank's Torso";
            promptText.fontSize = 32;
            promptText.fontStyle = FontStyles.Bold;
            promptText.color = new Color(0.35f, 0.95f, 0.72f);
            promptText.alignment = TextAlignmentOptions.Center;
            promptText.raycastTarget = false;

            TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/Electronic Highway Sign SDF");
            if (font != null) promptText.font = font;

            promptUI = promptObj;
            promptUI.SetActive(false);
        }
    }
}
