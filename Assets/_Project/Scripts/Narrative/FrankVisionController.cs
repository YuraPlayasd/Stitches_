using System.Collections;
using System.Collections.Generic;
using Stitches.Combat;
using Stitches.Enemies;
using Stitches.Items;
using Stitches.Player;
using Stitches.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

namespace Stitches.Narrative
{
    public enum FrankDialoguePriority
    {
        Warning = 1,
        BodyPart = 2,
        Story = 3
    }

    /// <summary>
    /// Frank's Vision and Dialogue Reaction System.
    /// Scans nearby hazards, collectibles, and story locations once Frank's head is collected.
    /// </summary>
    public class FrankVisionController : MonoBehaviour
    {
        public static FrankVisionController Instance { get; private set; }

        [Header("Vision Settings")]
        public float visionRadius = 5.5f;
        public float scanInterval = 0.2f;
        public bool requireInCameraView = true;

        [Header("Speech / Dialogue UI")]
        public GameObject dialoguePanel;
        public TextMeshProUGUI speakerNameText;
        public TextMeshProUGUI dialogueText;
        public string speakerName = "Frank's Head";
        public float defaultSpeechDuration = 4.0f;
        public float typingSpeed = 0.035f;

        [Header("Speech Audio")]
        public AudioClip yapingAudioClip;

        [Header("Spike Warning Settings")]
        public float spikeWarningDistance = 2.8f;

        [Header("Level 2 Arrow Tutorial")]
        public float level2WalkDistanceForWarning = 2.0f;
        [TextArea(2, 4)]
        public string noArrowsWarningText = "Doctor, wait! It looks dangerous ahead! We should check to the left for a way to defend ourselves!";
        [TextArea(2, 4)]
        public string firstArrowPickupText = "Aha! You can throw these arrows by pressing [F]! I'll count how many we have in the top right corner!";

        [Header("Level 3 Story Dialogues")]
        [TextArea(2, 4)]
        public string level3OfficeStartText = "This is the office... I sense my final body part nearby! Doctor, save your arrows — I feel a terrible danger lurking ahead!";
        [TextArea(2, 4)]
        public string level3LabTubesReactionText = "Doctor... what are these horrors?! Look at those test tubes! Humanoids floating inside... gruesome, unnatural experiments! How could you do something so monstrous?!";

        private static bool _hasWarnedNoArrowsInLevel2;
        private static bool _hasExplainedArrows;
        private static bool _hasSpokenLevel3TubesReaction;
        private static bool _hasWarnedAboutSpikes;

        private readonly HashSet<string> _seenObjects = new HashSet<string>();
        private readonly HashSet<string> _announcedScenes = new HashSet<string>();

        private AudioSource _yapingAudioSource;
        private float _level2PlayerStartX = float.NaN;
        private bool _isLevel2Scene;
        private bool _isLevel3Scene;
        private bool _hasSpawnedLevel2Arrows;

        private Coroutine _speechCoroutine;
        private bool _isSpeaking;
        private FrankDialoguePriority _currentPriority = FrankDialoguePriority.Warning;
        private string _pendingWarningMessage;
        private float _pendingWarningDuration = -1f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            InitSceneContext(SceneManager.GetActiveScene().name);
        }

        private void Update()
        {
            if (BodyPartsTracker.HasHead && _isLevel2Scene && !_hasWarnedNoArrowsInLevel2)
            {
                CheckLevel2NoArrowsWarning();
            }
        }

        private void Start()
        {
            FindOrBuildDialogueUI();
            StartCoroutine(VisionScanRoutine());
            SceneManager.sceneLoaded += OnSceneLoaded;

            string activeScene = SceneManager.GetActiveScene().name;
            CheckLocationAnnouncement(activeScene);
            InitSceneContext(activeScene);
        }

        private void OnDisable()
        {
            StopYapingAudio();
        }

        private void OnDestroy()
        {
            StopYapingAudio();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (Instance == this) Instance = null;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            FindOrBuildDialogueUI();
            CheckLocationAnnouncement(scene.name);
            InitSceneContext(scene.name);
        }

        public static FrankVisionController EnsureExists(GameObject playerObj = null)
        {
            if (Instance != null) return Instance;

            if (playerObj == null)
            {
                PlayerController p = Object.FindAnyObjectByType<PlayerController>();
                if (p != null) playerObj = p.gameObject;
            }

            if (playerObj == null)
            {
                playerObj = GameObject.FindWithTag("Player");
            }

            if (playerObj != null)
            {
                FrankVisionController fv = playerObj.GetComponent<FrankVisionController>() ??
                                           playerObj.AddComponent<FrankVisionController>();
                Instance = fv;
                return fv;
            }

            return null;
        }

        private IEnumerator VisionScanRoutine()
        {
            WaitForSeconds wait = new WaitForSeconds(scanInterval);

            while (true)
            {
                yield return wait;
                if (!BodyPartsTracker.HasHead) continue;

                ScanForBodyParts();
                ScanForDamageAndSpikes();
                CheckLevel2NoArrowsWarning();
                CheckLevel3LeftPatrolPointReaction();
            }
        }

        public bool IsInCameraView(Vector3 worldPos, float margin = 0.02f)
        {
            if (!requireInCameraView) return true;

            Camera cam = Camera.main;
            if (cam == null) return true;

            Vector3 vp = cam.WorldToViewportPoint(worldPos);
            return vp.z > 0f &&
                   vp.x >= margin && vp.x <= (1f - margin) &&
                   vp.y >= margin && vp.y <= (1f - margin);
        }

        private void ScanForBodyParts()
        {
            if (!BodyPartsTracker.HasHead) return;

            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, visionRadius);
            foreach (Collider2D col in hits)
            {
                if (col == null || col.gameObject == gameObject) continue;

                CollectibleItem item = col.GetComponent<CollectibleItem>() ?? col.GetComponentInParent<CollectibleItem>();
                if (item == null || item.isCollected) continue;
                if (!IsInCameraView(item.transform.position)) continue;

                string id = "BodyPart_" + item.itemType + "_" + item.gameObject.name;
                string typeKey = "SeenType_" + item.itemType;
                if (_seenObjects.Contains(id) || _seenObjects.Contains(typeKey)) continue;

                string line = GetBodyPartPhrase(item.itemType, item.itemName);
                if (!string.IsNullOrEmpty(line))
                {
                    _seenObjects.Add(id);
                    _seenObjects.Add(typeKey);
                    Speak(line, FrankDialoguePriority.BodyPart);
                    return;
                }
            }
        }

        private void ScanForDamageAndSpikes()
        {
            if (!BodyPartsTracker.HasHead || _hasWarnedAboutSpikes) return;

            string activeSceneName = SceneManager.GetActiveScene().name.ToLowerInvariant();
            if (activeSceneName.Contains("level 2") || activeSceneName.Contains("level2") ||
                activeSceneName.Contains("level 3") || activeSceneName.Contains("level3") ||
                activeSceneName.Contains("hub"))
            {
                return;
            }

            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, spikeWarningDistance);
            foreach (Collider2D col in hits)
            {
                if (col == null) continue;
                if (col.CompareTag("Player") ||
                    col.GetComponent<EnemyController>() != null ||
                    (col.transform.parent != null && col.transform.parent.GetComponent<EnemyController>() != null) ||
                    col.GetComponent<ArrowProjectile>() != null)
                {
                    continue;
                }

                bool isSpike = col.CompareTag("Damage") ||
                               (col.transform.parent != null && col.transform.parent.CompareTag("Damage")) ||
                               col.gameObject.name.IndexOf("Damage", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                               col.gameObject.name.IndexOf("Spike", System.StringComparison.OrdinalIgnoreCase) >= 0;

                if (isSpike)
                {
                    TriggerSpikeWarning();
                    return;
                }
            }

            GameObject damageObj = GameObject.Find("Damage");
            if (damageObj != null)
            {
                Tilemap tm = damageObj.GetComponent<Tilemap>();
                if (tm != null)
                {
                    Vector3Int centerCell = tm.WorldToCell(transform.position);
                    int checkRadius = Mathf.CeilToInt(spikeWarningDistance);
                    for (int x = -checkRadius; x <= checkRadius; x++)
                    {
                        for (int y = -checkRadius; y <= checkRadius; y++)
                        {
                            Vector3Int cell = new Vector3Int(centerCell.x + x, centerCell.y + y, 0);
                            if (tm.HasTile(cell) &&
                                Vector2.Distance(transform.position, tm.GetCellCenterWorld(cell)) <= spikeWarningDistance)
                            {
                                TriggerSpikeWarning();
                                return;
                            }
                        }
                    }
                }
            }
        }

        private void TriggerSpikeWarning()
        {
            _hasWarnedAboutSpikes = true;
            _seenObjects.Add("Spikes_First_Seen");
            Speak("Doctor, watch out! Sharp spikes ahead! Don't step on them, they will hurt us!", FrankDialoguePriority.Warning, 4.0f);
        }

        public void CheckLocationAnnouncement(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName) || _announcedScenes.Contains(sceneName)) return;

            if (BodyPartsTracker.IsHubOrLevel2(sceneName))
            {
                BodyPartsTracker.SetHasHead(true);
            }

            if (!BodyPartsTracker.HasHead) return;

            string phrase = GetLocationPhrase(sceneName);
            if (!string.IsNullOrEmpty(phrase))
            {
                _announcedScenes.Add(sceneName);
                StartCoroutine(DelayedLocationSpeak(phrase, 1.0f));
            }
        }

        private IEnumerator DelayedLocationSpeak(string phrase, float delay)
        {
            yield return new WaitForSeconds(delay);
            Speak(phrase, FrankDialoguePriority.Story, 5.0f);
        }

        private static string GetBodyPartPhrase(CollectibleType type, string itemName)
        {
            switch (type)
            {
                case CollectibleType.FrankArm:
                    return "Look, Doctor! That's my arm over there! Quick, let's pick it up!";
                case CollectibleType.FrankLeg:
                    return "My legs! Doctor, look, those are my legs! Finally, I can stand on my own two feet again! Let's pick them up!";
                case CollectibleType.FrankTorso:
                    return "Doctor! My torso is right ahead! We have to get it back!";
                default:
                    if (itemName != null && itemName.IndexOf("head", System.StringComparison.OrdinalIgnoreCase) >= 0)
                        return null;
                    if (type == CollectibleType.Health || type == CollectibleType.Arrows)
                        return null;
                    return "Doctor, look over there! That part belongs to me! Let's pick it up!";
            }
        }

        private string GetLocationPhrase(string sceneName)
        {
            string lower = sceneName.ToLowerInvariant();
            if (lower.Contains("hub") || lower.Contains("castle") || lower.Contains("level2") || lower.Contains("level 2"))
            {
                return "This is the castle hub! It is cold and dangerous here, but I can clearly sense one of my parts nearby!";
            }

            if (lower.Contains("level 3") || lower.Contains("level3") || lower.Contains("lab"))
            {
                return level3OfficeStartText;
            }

            return null;
        }

        public void Speak(string message, FrankDialoguePriority priority = FrankDialoguePriority.Story, float duration = -1f, string customSpeakerName = null)
        {
            if (string.IsNullOrEmpty(message)) return;

            string activeScene = SceneManager.GetActiveScene().name;
            if (BodyPartsTracker.IsHubOrLevel2(activeScene))
            {
                BodyPartsTracker.SetHasHead(true);
            }

            if (!BodyPartsTracker.HasHead) return;

            FindOrBuildDialogueUI();

            if (_isSpeaking && priority < _currentPriority)
            {
                if (priority == FrankDialoguePriority.Warning)
                {
                    _pendingWarningMessage = message;
                    _pendingWarningDuration = duration > 0f ? duration : defaultSpeechDuration;
                }
                return;
            }

            if (_speechCoroutine != null)
            {
                StopCoroutine(_speechCoroutine);
                StopYapingAudio();
            }

            _currentPriority = priority;
            _speechCoroutine = StartCoroutine(ShowSpeechRoutine(message, duration > 0f ? duration : defaultSpeechDuration, customSpeakerName));
        }

        private void EnsureYapingAudio()
        {
            if (_yapingAudioSource == null)
            {
                Transform existing = transform.Find("FrankSpeechAudio");
                GameObject speechObj = existing != null ? existing.gameObject : new GameObject("FrankSpeechAudio");
                speechObj.transform.SetParent(transform);
                speechObj.transform.localPosition = Vector3.zero;

                _yapingAudioSource = speechObj.GetComponent<AudioSource>() ?? speechObj.AddComponent<AudioSource>();
                _yapingAudioSource.playOnAwake = false;
                _yapingAudioSource.spatialBlend = 0f;
                _yapingAudioSource.loop = true;
            }

            if (yapingAudioClip == null)
            {
                yapingAudioClip = Resources.Load<AudioClip>("Audio/yaping") ?? Resources.Load<AudioClip>("yaping");
            }

            if (_yapingAudioSource != null && yapingAudioClip != null)
            {
                _yapingAudioSource.clip = yapingAudioClip;
            }
        }

        private void PlayYapingAudio()
        {
            EnsureYapingAudio();
            if (_yapingAudioSource != null && yapingAudioClip != null)
            {
                _yapingAudioSource.clip = yapingAudioClip;
                _yapingAudioSource.loop = true;
                if (!_yapingAudioSource.isPlaying)
                {
                    _yapingAudioSource.Play();
                }
            }
        }

        private void StopYapingAudio()
        {
            if (_yapingAudioSource != null && _yapingAudioSource.isPlaying)
            {
                _yapingAudioSource.Stop();
            }
        }

        private IEnumerator ShowSpeechRoutine(string message, float duration, string customSpeakerName = null)
        {
            _isSpeaking = true;

            if (dialoguePanel != null) dialoguePanel.SetActive(true);
            if (speakerNameText != null)
            {
                speakerNameText.text = !string.IsNullOrEmpty(customSpeakerName) ? customSpeakerName : speakerName;
            }

            if (dialogueText != null)
            {
                dialogueText.text = "";
                PlayYapingAudio();

                bool skipTyping = false;
                for (int i = 0; i < message.Length; i++)
                {
                    if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.E) || Input.GetMouseButtonDown(0))
                    {
                        skipTyping = true;
                    }

                    if (skipTyping)
                    {
                        dialogueText.text = message;
                        break;
                    }

                    dialogueText.text += message[i];
                    yield return new WaitForSeconds(typingSpeed);
                }

                StopYapingAudio();
            }

            float timer = 0f;
            while (timer < duration)
            {
                timer += Time.deltaTime;
                if (timer > 0.4f && (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.E)))
                {
                    break;
                }
                yield return null;
            }

            StopYapingAudio();

            if (dialoguePanel != null) dialoguePanel.SetActive(false);

            _isSpeaking = false;
            _speechCoroutine = null;

            if (!string.IsNullOrEmpty(_pendingWarningMessage))
            {
                string nextMessage = _pendingWarningMessage;
                float nextDuration = _pendingWarningDuration;
                _pendingWarningMessage = null;

                yield return new WaitForSeconds(0.4f);
                Speak(nextMessage, FrankDialoguePriority.Warning, nextDuration);
            }
        }

        public void FindOrBuildDialogueUI()
        {
            if (dialoguePanel != null && dialogueText != null) return;

            GameObject dp = GameObject.Find("DialoguePanel");
            if (dp != null)
            {
                dialoguePanel = dp;
                if (speakerNameText == null) speakerNameText = dp.transform.Find("SpeakerName")?.GetComponent<TextMeshProUGUI>();
                if (dialogueText == null) dialogueText = dp.transform.Find("DialogueText")?.GetComponent<TextMeshProUGUI>();
                return;
            }

            Canvas canvas = Object.FindAnyObjectByType<Canvas>();
            if (canvas == null) return;

            GameObject newPanel = new GameObject("DialoguePanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            newPanel.transform.SetParent(canvas.transform, false);

            RectTransform panelRt = newPanel.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.08f, 0.04f);
            panelRt.anchorMax = new Vector2(0.92f, 0.38f);
            panelRt.offsetMin = Vector2.zero;
            panelRt.offsetMax = Vector2.zero;
            panelRt.pivot = new Vector2(0.5f, 0.5f);

            Image panelImg = newPanel.GetComponent<Image>();
            panelImg.color = new Color(0.06f, 0.07f, 0.12f, 0.94f);
            panelImg.raycastTarget = false;

            GameObject nameGo = new GameObject("SpeakerName", typeof(RectTransform), typeof(TextMeshProUGUI));
            nameGo.transform.SetParent(newPanel.transform, false);
            RectTransform nameRt = nameGo.GetComponent<RectTransform>();
            nameRt.anchorMin = new Vector2(0.04f, 0.76f);
            nameRt.anchorMax = new Vector2(0.96f, 0.96f);
            nameRt.offsetMin = Vector2.zero;
            nameRt.offsetMax = Vector2.zero;

            TextMeshProUGUI nameTmp = nameGo.GetComponent<TextMeshProUGUI>();
            nameTmp.text = speakerName;
            nameTmp.fontSize = 40;
            nameTmp.fontStyle = FontStyles.Bold;
            nameTmp.color = new Color(0.35f, 0.95f, 0.72f);
            nameTmp.alignment = TextAlignmentOptions.TopLeft;
            nameTmp.raycastTarget = false;

            GameObject textGo = new GameObject("DialogueText", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(newPanel.transform, false);
            RectTransform textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = new Vector2(0.04f, 0.12f);
            textRt.anchorMax = new Vector2(0.96f, 0.74f);
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            TextMeshProUGUI textTmp = textGo.GetComponent<TextMeshProUGUI>();
            textTmp.text = "...";
            textTmp.fontSize = 42;
            textTmp.fontStyle = FontStyles.Normal;
            textTmp.color = Color.white;
            textTmp.alignment = TextAlignmentOptions.TopLeft;
            textTmp.raycastTarget = false;

            TMP_FontAsset electronicFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/Electronic Highway Sign SDF");
            if (electronicFont != null)
            {
                nameTmp.font = electronicFont;
                textTmp.font = electronicFont;
            }

            dialoguePanel = newPanel;
            speakerNameText = nameTmp;
            dialogueText = textTmp;
            dialoguePanel.SetActive(false);
        }

        private void InitSceneContext(string sceneName)
        {
            string lower = sceneName.ToLowerInvariant();
            _isLevel2Scene = (lower.Contains("level 2") || lower.Contains("level2") || lower.Contains("hub")) &&
                             !lower.Contains("level 3") && !lower.Contains("level3") &&
                             !lower.Contains("level 1") && !lower.Contains("level1");
            _isLevel3Scene = lower.Contains("level 3") || lower.Contains("level3");

            if (_isLevel2Scene)
            {
                BodyPartsTracker.SetHasHead(true);
                _hasWarnedNoArrowsInLevel2 = false;
                _hasExplainedArrows = false;

                PlayerController player = GetComponent<PlayerController>() ?? Object.FindAnyObjectByType<PlayerController>();
                _level2PlayerStartX = player != null ? player.transform.position.x : transform.position.x;

                SpawnLevel2TutorialArrows();
            }
            else if (_isLevel3Scene)
            {
                BodyPartsTracker.SetHasHead(true);
                _hasSpokenLevel3TubesReaction = false;
            }
        }

        private void CheckLevel3LeftPatrolPointReaction()
        {
            if (!_isLevel3Scene || _hasSpokenLevel3TubesReaction || !BodyPartsTracker.HasHead) return;

            PlayerController player = GetComponent<PlayerController>() ?? Object.FindAnyObjectByType<PlayerController>();
            if (player == null) return;

            GameObject leftPointGo = GameObject.Find("Point1_Patrol");
            Vector3 checkPos = leftPointGo != null ? leftPointGo.transform.position : new Vector3(111.0f, 25.39f, 0f);

            float dist = Vector2.Distance(player.transform.position, checkPos);
            if (dist <= 6.5f || (player.transform.position.x >= 106.0f && player.transform.position.x <= 118.0f && player.transform.position.y > 18.0f))
            {
                _hasSpokenLevel3TubesReaction = true;
                Speak(level3LabTubesReactionText, FrankDialoguePriority.Story, 6.0f);
            }
        }

        private void SpawnLevel2TutorialArrows()
        {
            if (_hasSpawnedLevel2Arrows) return;

            ArrowPickup[] existingPickups = Object.FindObjectsByType<ArrowPickup>();
            if (existingPickups != null && existingPickups.Length >= 3)
            {
                _hasSpawnedLevel2Arrows = true;
                return;
            }

            if (GameObject.Find("Level2_TutorialArrows") != null)
            {
                _hasSpawnedLevel2Arrows = true;
                return;
            }

            _hasSpawnedLevel2Arrows = true;
            GameObject parentObj = new GameObject("Level2_TutorialArrows");

            Vector3 basePos = new Vector3(25.79f, 1.97f, 0f);
            Vector3[] spawnPositions =
            {
                basePos + new Vector3(-0.8f, 0f, 0f),
                basePos,
                basePos + new Vector3(0.8f, 0f, 0f)
            };

            foreach (Vector3 pos in spawnPositions)
            {
                ArrowPickup.SpawnSingleArrowPickup(pos, parentObj.transform, isFalling: false);
            }
        }

        private void CheckLevel2NoArrowsWarning()
        {
            if (!_isLevel2Scene || _hasWarnedNoArrowsInLevel2 || !BodyPartsTracker.HasHead) return;

            PlayerController player = GetComponent<PlayerController>() ?? Object.FindAnyObjectByType<PlayerController>();
            if (player == null) return;

            if (float.IsNaN(_level2PlayerStartX))
            {
                _level2PlayerStartX = player.transform.position.x;
                return;
            }

            if (player.arrowsCount == 0 && player.transform.position.x > _level2PlayerStartX + level2WalkDistanceForWarning)
            {
                _hasWarnedNoArrowsInLevel2 = true;
                Speak(noArrowsWarningText, FrankDialoguePriority.Story, 5.5f);
            }
        }

        public void OnArrowCollected()
        {
            if (_hasExplainedArrows) return;

            _hasExplainedArrows = true;
            string sceneName = SceneManager.GetActiveScene().name;
            if (BodyPartsTracker.IsHubOrLevel2(sceneName))
            {
                BodyPartsTracker.SetHasHead(true);
            }
            Speak(firstArrowPickupText, FrankDialoguePriority.Story, 5.5f);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.35f, 0.95f, 0.72f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, visionRadius);

            Gizmos.color = new Color(1f, 0.35f, 0.35f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, spikeWarningDistance);
        }
    }
}
