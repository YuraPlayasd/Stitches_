using Stitches.Progression;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Stitches.Audio
{
    /// <summary>
    /// Manages looping ambient background music across levels.
    /// </summary>
    public class LevelAmbientAudioManager : MonoBehaviour
    {
        public static LevelAmbientAudioManager Instance { get; private set; }

        [Header("Ambient Audio Settings")]
        public AudioClip level1AmbientClip;
        public AudioClip level2AmbientClip;
        [Range(0f, 1f)]
        [Tooltip("Ambient background music volume (kept softer than footsteps)")]
        public float ambientVolume = 0.28f;

        private AudioSource _audioSource;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            _audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
            _audioSource.loop = true;
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0f;
            _audioSource.volume = ambientVolume;

            EnsureAudioClips();
        }

        private void Start()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            CheckSceneAmbient(SceneManager.GetActiveScene().name);
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (Instance == this) Instance = null;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            CheckSceneAmbient(scene.name);
        }

        public void EnsureAudioClips()
        {
            if (level1AmbientClip == null)
            {
                level1AmbientClip = Resources.Load<AudioClip>("Audio/level1_ambient") ??
                                    Resources.Load<AudioClip>("level1_ambient");
            }

            if (level2AmbientClip == null)
            {
                level2AmbientClip = Resources.Load<AudioClip>("Audio/level2_ambient") ??
                                    Resources.Load<AudioClip>("Audio/level2_embient") ??
                                    Resources.Load<AudioClip>("level2_embient");
            }
        }

        public static bool IsLevel1(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return false;
            return sceneName.IndexOf("Level 1", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   sceneName.IndexOf("Level1", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   sceneName.IndexOf("GameScene", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public void CheckSceneAmbient(string sceneName)
        {
            EnsureAudioClips();

            AudioClip targetClip = null;
            if (IsLevel1(sceneName)) targetClip = level1AmbientClip;
            else if (BodyPartsTracker.IsHubOrLevel2(sceneName)) targetClip = level2AmbientClip;

            if (targetClip != null)
            {
                if (_audioSource != null && (!_audioSource.isPlaying || _audioSource.clip != targetClip))
                {
                    _audioSource.clip = targetClip;
                    _audioSource.loop = true;
                    _audioSource.volume = ambientVolume;
                    _audioSource.Play();
                }
            }
            else if (_audioSource != null && _audioSource.isPlaying)
            {
                _audioSource.Stop();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureAmbientExists()
        {
            if (Instance == null)
            {
                GameObject go = new GameObject("SYS_LevelAmbientAudioManager");
                go.AddComponent<LevelAmbientAudioManager>();
            }
        }
    }
}
