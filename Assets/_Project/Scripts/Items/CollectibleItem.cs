using System.Collections;
using Stitches.Narrative;
using Stitches.Player;
using Stitches.Progression;
using TMPro;
using UnityEngine;

namespace Stitches.Items
{
    public enum CollectibleType
    {
        FrankHead,
        FrankArm,
        FrankTorso,
        FrankLeg,
        Health,
        Arrows,
        Custom
    }

    [RequireComponent(typeof(Collider2D))]
    public class CollectibleItem : MonoBehaviour
    {
        [Header("Collectible Type & Value")]
        public CollectibleType itemType = CollectibleType.FrankHead;
        [Tooltip("Display name used in pickup prompt (e.g., Frank's Head, Healing Potion, Arrows)")]
        public string itemName = "Frank's Head";
        [Tooltip("Amount restored/given (e.g. 25 for +25 HP, 5 for +5 Arrows, 1 for Body Part)")]
        public int amount = 1;

        [Header("Interaction Settings")]
        [Tooltip("If true, player must press a key. If false, collected instantly on touch.")]
        public bool requireKeyPress = true;
        public KeyCode pickupKey = KeyCode.E;

        [Header("Detection Range")]
        [Tooltip("Radius within which the player is considered nearby")]
        public float interactionDistance = 2.5f;

        [Header("UI Prompt")]
        public GameObject promptUI;
        public TextMeshProUGUI promptText;
        [Tooltip("Custom prompt message. If empty, automatically generated from itemName.")]
        public string customPromptMessage = "";

        [Header("Dialogue / Story (Optional)")]
        public GameObject dialoguePanel;
        public TextMeshProUGUI speakerNameText;
        public TextMeshProUGUI dialogueText;
        public string speakerName = "Frank's Head";
        [TextArea(2, 4)]
        public string pickupSpeech = "Careful, Doctor! Don't drop me! Now let's explore the castle and find all my body parts!";
        public float speechDuration = 3.5f;

        [Header("Visual Effects")]
        public bool enableBobbing = true;
        public float bobbingSpeed = 2.5f;
        public float bobbingHeight = 0.08f;

        [Header("Audio (Optional)")]
        public AudioSource audioSource;
        public AudioClip pickupSound;

        [Header("Status")]
        public bool isCollected = false;

        private bool _isPlayerNearby;
        private PlayerController _nearbyPlayer;
        private Vector3 _initialPosition;

        private void Awake()
        {
            Collider2D col = GetComponent<Collider2D>();
            if (col != null)
            {
                col.isTrigger = true;
                if (col is CircleCollider2D circle)
                {
                    circle.radius = Mathf.Max(circle.radius, 2.5f);
                }
            }

            _initialPosition = transform.position;

            if (promptUI == null)
            {
                GameObject dt = GameObject.Find("DoorText");
                if (dt != null)
                {
                    promptUI = dt;
                    if (promptText == null) promptText = dt.GetComponent<TextMeshProUGUI>();
                }
            }

            if (promptUI != null)
            {
                promptUI.SetActive(false);
            }
        }

        private void OnEnable()
        {
            _initialPosition = transform.position;
        }

        public void SetInitialPosition(Vector3 pos)
        {
            _initialPosition = pos;
            transform.position = pos;
        }

        private void Update()
        {
            if (isCollected) return;

            if (enableBobbing)
            {
                float newY = _initialPosition.y + Mathf.Sin(Time.time * bobbingSpeed) * bobbingHeight;
                transform.position = new Vector3(_initialPosition.x, newY, _initialPosition.z);
            }

            if (_nearbyPlayer == null)
            {
                _nearbyPlayer = Object.FindAnyObjectByType<PlayerController>();
            }

            if (_nearbyPlayer != null)
            {
                float dist = Vector2.Distance(transform.position, _nearbyPlayer.transform.position);
                bool inRange = dist <= interactionDistance;
                if (inRange != _isPlayerNearby)
                {
                    _isPlayerNearby = inRange;
                    ShowPrompt(inRange);
                }

                if (inRange)
                {
                    if (!requireKeyPress || dist <= 2.0f ||
                        Input.GetKeyDown(pickupKey) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.E))
                    {
                        Collect();
                    }
                }
            }
        }

        private static bool IsPlayer(Collider2D other, out PlayerController player)
        {
            player = null;
            if (other == null) return false;

            player = other.GetComponent<PlayerController>() ?? other.GetComponentInParent<PlayerController>();
            if (player != null) return true;

            if (other.CompareTag("Player") || other.name == "Player") return true;

            if (other.attachedRigidbody != null)
            {
                player = other.attachedRigidbody.GetComponent<PlayerController>();
                if (player != null || other.attachedRigidbody.CompareTag("Player") || other.attachedRigidbody.name == "Player")
                    return true;
            }

            return false;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (isCollected) return;

            if (IsPlayer(other, out PlayerController player))
            {
                _isPlayerNearby = true;
                _nearbyPlayer = player;
                Collect();
            }
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (isCollected) return;

            if (IsPlayer(other, out PlayerController player))
            {
                _isPlayerNearby = true;
                _nearbyPlayer = player;
                Collect();
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (IsPlayer(other, out _))
            {
                _isPlayerNearby = false;
                ShowPrompt(false);
            }
        }

        public string GetFormattedPrompt()
        {
            if (!string.IsNullOrEmpty(customPromptMessage))
            {
                return customPromptMessage;
            }

            return itemType switch
            {
                CollectibleType.FrankHead => $"[ {pickupKey} ] Pick up Frank's Head",
                CollectibleType.FrankArm => $"[ {pickupKey} ] Pick up Frank's Arm",
                CollectibleType.FrankTorso => $"[ {pickupKey} ] Pick up Frank's Torso",
                CollectibleType.FrankLeg => $"[ {pickupKey} ] Pick up Frank's Leg",
                CollectibleType.Health => $"[ {pickupKey} ] Pick up Health (+{amount} HP)",
                CollectibleType.Arrows => $"[ {pickupKey} ] Pick up Arrows (+{amount})",
                _ => $"[ {pickupKey} ] Pick up {itemName}"
            };
        }

        private void ShowPrompt(bool show)
        {
            if (promptUI == null) return;

            if (promptText != null && show)
            {
                promptText.text = GetFormattedPrompt();
            }
            promptUI.SetActive(show);
        }

        public void Collect()
        {
            if (isCollected) return;
            isCollected = true;
            ShowPrompt(false);

            if (itemType == CollectibleType.FrankHead ||
                itemType == CollectibleType.FrankArm ||
                itemType == CollectibleType.FrankTorso ||
                itemType == CollectibleType.FrankLeg)
            {
                BodyPartsTracker.RegisterPart(itemType);
            }

            if (_nearbyPlayer != null && itemType == CollectibleType.Health)
            {
                _nearbyPlayer.Heal(amount);
            }

            if (audioSource != null && pickupSound != null)
            {
                audioSource.PlayOneShot(pickupSound);
            }

            if (itemType == CollectibleType.FrankHead &&
                FrankVisionController.Instance != null &&
                !string.IsNullOrEmpty(pickupSpeech))
            {
                FrankVisionController.Instance.Speak(pickupSpeech, FrankDialoguePriority.Story, speechDuration);
                gameObject.SetActive(false);
                return;
            }

            if (dialoguePanel != null && dialogueText != null && !string.IsNullOrEmpty(pickupSpeech))
            {
                StartCoroutine(ShowPickupSpeechRoutine());
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        private IEnumerator ShowPickupSpeechRoutine()
        {
            dialoguePanel.SetActive(true);
            if (speakerNameText != null) speakerNameText.text = speakerName;
            dialogueText.text = pickupSpeech;

            SpriteRenderer sr = GetComponent<SpriteRenderer>();
            if (sr != null) sr.enabled = false;

            Collider2D col = GetComponent<Collider2D>();
            if (col != null) col.enabled = false;

            float timer = 0f;
            while (timer < speechDuration)
            {
                timer += Time.deltaTime;
                if (timer > 0.35f && (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.E) || Input.GetMouseButtonDown(0)))
                {
                    break;
                }
                yield return null;
            }

            dialoguePanel.SetActive(false);
            gameObject.SetActive(false);
        }
    }
}
