using System.Collections;
using Stitches.Combat;
using Stitches.Enemies;
using Stitches.Environment;
using Stitches.Narrative;
using Stitches.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

namespace Stitches.Player
{
    /// <summary>
    /// Controls 2D player movement, consistent physics-based jumping, slopes, platforms,
    /// combat (arrows), health, animations, and footstep audio.
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        private const string AnimStateIdle = "Idle";
        private const string AnimStateRun = "Run";
        private const string AnimStateJump = "Jump";
        private const string AnimStateFall = "Fall";

        private const float JumpCooldownDuration = 0.15f;
        private const float GroundCheckMaxUpwardVelocity = 0.1f;
        private const float FlatGroundContactTolerance = 0.08f;

        [Header("Health Settings")]
        public int health = 100;
        public int maxHealth = 100;
        public Image healthImage;
        public float invulnerabilityTime = 0.5f;

        [Header("Movement & Jump")]
        public float moveSpeed = 6.2f;
        [FormerlySerializedAs("jumpforce")]
        public float jumpForce = 11.5f;
        [FormerlySerializedAs("GroundChek")]
        public Transform groundCheck;
        [FormerlySerializedAs("GroundChekRadius")]
        public float groundCheckRadius = 0.15f;
        public LayerMask groundLayer;

        [Tooltip("Input buffer window (seconds) so pressing Jump slightly before landing still triggers a jump")]
        public float jumpBufferTime = 0.15f;
        [Tooltip("Grace window (seconds) allowing a ground jump immediately after walking off a ledge")]
        public float coyoteTime = 0.1f;
        [FormerlySerializedAs("extraJumpesValue")]
        public int extraJumpsValue = 1;

        [Header("Jump / Fall Feel")]
        [Tooltip("Multiplier applied to gravity while falling for crisp platformer arcs")]
        public float fallMultiplier = 2.5f;

        [Header("Footstep / Walk Audio")]
        public AudioClip walkLevel1Clip;
        public AudioClip walkLevel2Clip;
        [Range(0f, 1f)]
        [Tooltip("Footstep and landing audio volume (louder than ambient music)")]
        public float walkAudioVolume = 1.0f;

        [Header("Shooting & Ammo")]
        public GameObject arrowPrefab;
        public Transform arrowSpawnPoint;
        public float arrowCooldown = 0.5f;
        [Tooltip("Number of arrows remaining")]
        public int arrowsCount = 0;
        public TextMeshProUGUI arrowCountText;

        [Header("Hitbox / Collider Settings")]
        [Tooltip("Player's BoxCollider2D (automatically found if not assigned)")]
        public BoxCollider2D playerCollider;
        [Tooltip("Whether to resize the collider during the fall animation")]
        public bool adjustHitboxOnFall = true;
        [Tooltip("Collider size during fall animation")]
        public Vector2 fallColliderSize = new Vector2(1.8f, 4.14f);
        [Tooltip("Horizontal offset during fall; vertical offset is aligned to keep the collider bottom flush with the floor")]
        public Vector2 fallColliderOffset = new Vector2(0f, 0.275f);

        [Header("Slope Settings")]
        [Tooltip("Maximum slope angle in degrees that the player can walk on")]
        public float maxSlopeAngle = 55f;
        [Tooltip("Minimum slope angle in degrees before slope mechanics activate")]
        public float minSlopeAngle = 5f;
        [Tooltip("Distance below player collider to check for ground and slopes")]
        public float slopeCheckDistance = 0.5f;

        public static int CarriedArrowsCount { get; set; } = -1;

        private Rigidbody2D _rb;
        private Animator _animator;
        private SpriteRenderer _spriteRenderer;

        private AudioSource _landingAudioSource;
        private AudioSource _walkAudioSource;
        private Coroutine _stopLandingRoutine;

        private bool _isInvulnerable;
        private bool _isWalkAudioPaused;
        private bool _isGrounded;
        private bool _isOnSlope;
        private bool _isJumping;
        private bool _airJumpQueued;
        private bool _dropThroughRequested;
        private bool _isDefaultColliderCached;

        private float _moveInput;
        private float _airTime;
        private float _jumpBufferCounter;
        private float _coyoteTimeCounter;
        private float _jumpCooldownTimer;
        private float _dropThroughTimer;
        private float _nextArrowTime;
        private float _slopeAngle;

        private int _extraJumps;
        private string _currentAnimation;

        private Vector2 _slopeNormal = Vector2.up;
        private Vector2 _defaultColliderSize;
        private Vector2 _defaultColliderOffset;

        private MovingPlatform _activeMovingPlatform;
        private Collider2D _currentOneWayPlatform;

        public static void SaveArrowsCount(int count)
        {
            CarriedArrowsCount = count;
        }

        private void Awake()
        {
            string currentScene = SceneManager.GetActiveScene().name;
            if (IsLevel3Scene(currentScene))
            {
                if (CarriedArrowsCount >= 0)
                {
                    arrowsCount = CarriedArrowsCount;
                }
            }
            else
            {
                arrowsCount = 0;
            }

            CacheDefaultCollider();
            EnsureTilemapCompositeColliders();
            AutoFindHealthImage();
        }

        private void Start()
        {
            _rb = GetComponent<Rigidbody2D>();
            if (_rb != null)
            {
                _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            }

            if (transform.parent != null && transform.parent.GetComponent<MovingPlatform>() != null)
            {
                transform.SetParent(null);
            }

            _animator = GetComponent<Animator>();
            _extraJumps = extraJumpsValue;
            _spriteRenderer = GetComponent<SpriteRenderer>();
            if (_spriteRenderer == null)
            {
                _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            CacheDefaultCollider();
            EnsureTilemapCompositeColliders();

            AutoFindHealthImage();
            health = maxHealth;
            UpdateHealthUI();

            string sceneName = SceneManager.GetActiveScene().name;
            if (BodyPartsTracker.IsHubOrLevel2(sceneName))
            {
                BodyPartsTracker.SetHasHead(true);
            }

            if (IsLevel3Scene(sceneName))
            {
                if (CarriedArrowsCount >= 0)
                {
                    arrowsCount = CarriedArrowsCount;
                }
            }
            else if (IsLevel1Scene(sceneName) || IsLevel2Scene(sceneName))
            {
                arrowsCount = 0;
            }

            EnsureArrowPrefab();
            AutoFindArrowCountUI();
            UpdateArrowUI();

            FrankVisionController.EnsureExists(gameObject);

            SetupWalkAudio();
            SceneManager.sceneLoaded += OnSceneLoadedForWalkAudio;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoadedForWalkAudio;
            if (_walkAudioSource != null && _walkAudioSource.isPlaying)
            {
                _walkAudioSource.Stop();
            }
        }

        private void Update()
        {
            // Read all player inputs in Update; execute physics velocity mutations exclusively in FixedUpdate
            _moveInput = Input.GetAxis("Horizontal");
            UpdateWalkAudio();

            if (Input.GetKeyDown(KeyCode.F))
            {
                Shoot();
            }

            if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))
            {
                _dropThroughRequested = true;
            }

            if (Input.GetKeyDown(KeyCode.Space))
            {
                _jumpBufferCounter = jumpBufferTime;
                _airJumpQueued = true;
            }

            UpdateHealthUI();
            SetAnimation(_moveInput);
        }

        private void FixedUpdate()
        {
            // 1. Advance all physics-coupled timers on the fixed timestep
            if (_jumpCooldownTimer > 0f)
            {
                _jumpCooldownTimer -= Time.fixedDeltaTime;
            }

            if (_dropThroughTimer > 0f)
            {
                _dropThroughTimer -= Time.fixedDeltaTime;
                if (_dropThroughTimer <= 0f)
                {
                    _currentOneWayPlatform = null;
                }
            }

            if (_jumpBufferCounter > 0f)
            {
                _jumpBufferCounter -= Time.fixedDeltaTime;
            }

            // 2. Refresh ground/slope state before processing jumps or horizontal movement
            CheckGrounded();

            if (_isGrounded || (_isOnSlope && !_isJumping))
            {
                _coyoteTimeCounter = coyoteTime;
                _extraJumps = extraJumpsValue;
            }
            else if (_coyoteTimeCounter > 0f)
            {
                _coyoteTimeCounter -= Time.fixedDeltaTime;
            }

            // 3. Process queued drop-through input in physics step
            if (_dropThroughRequested)
            {
                _dropThroughRequested = false;
                TryDropThroughPlatform();
            }

            // 4. Process queued jump in physics step for guaranteed, framerate-independent jump force
            bool jumpedThisStep = TryProcessJump();

            // 5. Apply horizontal/slope movement only when we did not just initiate a jump on this tick
            if (!jumpedThisStep)
            {
                ApplyMovement();
            }

            // 6. Apply enhanced fall gravity strictly while descending in mid-air
            if (_rb != null && _rb.linearVelocity.y < 0f && !_isGrounded)
            {
                _rb.linearVelocity += Vector2.up * (Physics2D.gravity.y * (fallMultiplier - 1f) * Time.fixedDeltaTime);
            }
        }

        private bool TryProcessJump()
        {
            if (_jumpBufferCounter <= 0f)
            {
                _airJumpQueued = false;
                return false;
            }

            // Ground or coyote-time jump
            if (_coyoteTimeCounter > 0f)
            {
                ExecuteJump();
                return true;
            }

            // Mid-air extra jump (only on fresh key press, not carried-over landing buffer)
            if (_airJumpQueued && _extraJumps > 0)
            {
                _extraJumps--;
                ExecuteJump();
                return true;
            }

            // If no extra jumps remain, clear air jump flag while letting jump buffer wait for landing
            _airJumpQueued = false;
            return false;
        }

        private void ExecuteJump()
        {
            _isJumping = true;
            _jumpCooldownTimer = JumpCooldownDuration;
            _isGrounded = false;
            _isOnSlope = false;
            _coyoteTimeCounter = 0f;
            _jumpBufferCounter = 0f;
            _airJumpQueued = false;

            if (_rb != null)
            {
                Vector2 platformVel = _activeMovingPlatform != null ? _activeMovingPlatform.Velocity : Vector2.zero;
                // Directly assign vertical velocity = jumpForce (never accumulate over residual Y velocity)
                _rb.linearVelocity = new Vector2(_moveInput * moveSpeed + platformVel.x, jumpForce);
            }

            _activeMovingPlatform = null;
        }

        public void AutoFindHealthImage()
        {
            if (healthImage != null)
            {
                EnsureHealthImageSettings();
                return;
            }

            GameObject healthObj = GameObject.Find("Health");
            if (healthObj != null)
            {
                healthImage = healthObj.GetComponent<Image>();
            }

            if (healthImage == null)
            {
                Image[] allImages = Object.FindObjectsByType<Image>(FindObjectsInactive.Include);
                foreach (Image img in allImages)
                {
                    if (img.gameObject.name.Equals("Health", System.StringComparison.OrdinalIgnoreCase))
                    {
                        healthImage = img;
                        break;
                    }
                }

                if (healthImage == null)
                {
                    foreach (Image img in allImages)
                    {
                        if (img.transform.parent != null &&
                            img.transform.parent.name.Equals("HealthBar", System.StringComparison.OrdinalIgnoreCase))
                        {
                            healthImage = img;
                            break;
                        }
                    }
                }
            }

            EnsureHealthImageSettings();
        }

        private void EnsureHealthImageSettings()
        {
            if (healthImage == null) return;

            if (healthImage.type != Image.Type.Filled)
            {
                healthImage.type = Image.Type.Filled;
                healthImage.fillMethod = Image.FillMethod.Horizontal;
                healthImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            }
        }

        public void UpdateHealthUI()
        {
            if (healthImage == null)
            {
                AutoFindHealthImage();
            }

            if (healthImage != null)
            {
                EnsureHealthImageSettings();
                float max = maxHealth > 0 ? maxHealth : 100f;
                healthImage.fillAmount = Mathf.Clamp01((float)health / max);
            }
        }

        public void TakeDamage(int damage)
        {
            if (_isInvulnerable || health <= 0) return;

            health = Mathf.Max(0, health - damage);
            UpdateHealthUI();

            if (_rb != null)
            {
                _isJumping = true;
                _jumpCooldownTimer = JumpCooldownDuration;
                _isGrounded = false;
                _isOnSlope = false;
                _coyoteTimeCounter = 0f;
                _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, 1.5f * jumpForce);
            }

            StartCoroutine(BlinkRed());

            if (health <= 0)
            {
                Die();
            }
        }

        public void Heal(int amount)
        {
            health = Mathf.Min(health + amount, maxHealth);
            UpdateHealthUI();
        }

        private void TryDropThroughPlatform()
        {
            if (playerCollider == null)
            {
                playerCollider = GetComponent<BoxCollider2D>();
                if (playerCollider == null) return;
            }

            Bounds bounds = playerCollider.bounds;
            Vector2 boxOrigin = bounds.center;
            Vector2 boxSize = new Vector2(bounds.size.x * 0.85f, 0.25f);
            float castDistance = bounds.extents.y + 0.35f;

            RaycastHit2D[] hits = Physics2D.BoxCastAll(boxOrigin, boxSize, 0f, Vector2.down, castDistance, groundLayer);
            foreach (RaycastHit2D hit in hits)
            {
                if (hit.collider == null || hit.collider == playerCollider) continue;

                OneWayPlatform platform = hit.collider.GetComponent<OneWayPlatform>() ??
                                          hit.collider.GetComponentInParent<OneWayPlatform>();

                if (platform != null)
                {
                    platform.DropPlayer(playerCollider);
                    _currentOneWayPlatform = hit.collider;
                    _dropThroughTimer = platform.dropDuration;
                    _isGrounded = false;
                    _isOnSlope = false;
                    _coyoteTimeCounter = 0f;
                    if (_rb != null)
                    {
                        _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, -2f);
                    }
                    break;
                }
            }
        }

        private void CheckGrounded()
        {
            // Never mark player as grounded during jump takeoff cooldown or while ascending upward
            if ((_isJumping && _jumpCooldownTimer > 0f) ||
                (_rb != null && _rb.linearVelocity.y > GroundCheckMaxUpwardVelocity))
            {
                _isGrounded = false;
                _isOnSlope = false;
                _activeMovingPlatform = null;
                _airTime += Time.fixedDeltaTime;
                return;
            }

            if (playerCollider == null)
            {
                playerCollider = GetComponent<BoxCollider2D>();
            }

            bool grounded = false;
            _isOnSlope = false;
            _slopeNormal = Vector2.up;
            _slopeAngle = 0f;
            Collider2D groundCollider = null;

            // 1. OverlapCircle check at groundCheck transform if assigned
            if (groundCheck != null)
            {
                Collider2D[] circleHits = Physics2D.OverlapCircleAll(groundCheck.position, groundCheckRadius, groundLayer);
                foreach (Collider2D col in circleHits)
                {
                    if (col != null && col != playerCollider &&
                        (_dropThroughTimer <= 0f || col != _currentOneWayPlatform))
                    {
                        grounded = true;
                        groundCollider = col;
                        break;
                    }
                }
            }

            // 2. BoxCast check across the width of the player collider for accurate flat/slope detection
            if (playerCollider != null)
            {
                Bounds bounds = playerCollider.bounds;
                Vector2 boxOrigin = bounds.center;
                Vector2 boxSize = new Vector2(bounds.size.x * 0.8f, 0.1f);
                float castDistance = bounds.extents.y + slopeCheckDistance;

                RaycastHit2D[] hits = Physics2D.BoxCastAll(boxOrigin, boxSize, 0f, Vector2.down, castDistance, groundLayer);
                RaycastHit2D validHit = default;
                bool foundHit = false;

                foreach (RaycastHit2D hit in hits)
                {
                    if (hit.collider != null && hit.collider != playerCollider)
                    {
                        if (_dropThroughTimer > 0f && hit.collider == _currentOneWayPlatform)
                            continue;

                        validHit = hit;
                        foundHit = true;
                        break;
                    }
                }

                if (foundHit)
                {
                    groundCollider = validHit.collider;
                    float distanceToGround = Mathf.Max(0f, validHit.distance - (bounds.extents.y - 0.05f));

                    float rayY = bounds.min.y + 0.2f;
                    float rayDist = slopeCheckDistance + 0.3f;
                    float halfWidth = bounds.extents.x * 0.45f;

                    RaycastHit2D centerHit = Physics2D.Raycast(new Vector2(bounds.center.x, rayY), Vector2.down, rayDist, groundLayer);
                    RaycastHit2D leftHit = Physics2D.Raycast(new Vector2(bounds.center.x - halfWidth, rayY), Vector2.down, rayDist, groundLayer);
                    RaycastHit2D rightHit = Physics2D.Raycast(new Vector2(bounds.center.x + halfWidth, rayY), Vector2.down, rayDist, groundLayer);

                    if (IsValidGroundHit(centerHit)) groundCollider = centerHit.collider;
                    else if (IsValidGroundHit(leftHit)) groundCollider = leftHit.collider;
                    else if (IsValidGroundHit(rightHit)) groundCollider = rightHit.collider;

                    Vector2 normal = Vector2.up;

                    if (IsFlatSurface(centerHit) || IsFlatSurface(leftHit) || IsFlatSurface(rightHit))
                    {
                        normal = Vector2.up;
                    }
                    else if (IsValidGroundHit(centerHit))
                    {
                        normal = centerHit.normal;
                    }
                    else if (IsValidGroundHit(leftHit))
                    {
                        normal = leftHit.normal;
                    }
                    else if (IsValidGroundHit(rightHit))
                    {
                        normal = rightHit.normal;
                    }
                    else if (validHit.normal.y > 0.4f && Vector2.Angle(validHit.normal, Vector2.up) < minSlopeAngle)
                    {
                        normal = Vector2.up;
                    }

                    float hitAngle = Vector2.Angle(normal, Vector2.up);

                    if (hitAngle <= maxSlopeAngle && normal.y > 0.3f)
                    {
                        // Tight flat-ground tolerance avoids triggering grounded while still high in the air
                        float allowedDistance = hitAngle >= minSlopeAngle ? slopeCheckDistance : FlatGroundContactTolerance;

                        if (distanceToGround <= allowedDistance)
                        {
                            grounded = true;

                            if (hitAngle >= minSlopeAngle)
                            {
                                _isOnSlope = true;
                                _slopeNormal = normal;
                                _slopeAngle = hitAngle;

                                // Downward slope snapping only when descending on a confirmed slope
                                if (!_isJumping && Mathf.Abs(_moveInput) > 0.01f &&
                                    distanceToGround > 0.04f && distanceToGround <= 0.3f &&
                                    _rb != null && _rb.linearVelocity.y <= 0.1f)
                                {
                                    _rb.position += Vector2.down * (distanceToGround - 0.01f);
                                }
                            }
                        }
                    }
                }
            }

            _isGrounded = grounded;

            if (_isGrounded && groundCollider != null)
            {
                _activeMovingPlatform = groundCollider.GetComponent<MovingPlatform>() ??
                                        groundCollider.GetComponentInParent<MovingPlatform>();
            }
            else if (!_isGrounded)
            {
                _activeMovingPlatform = null;
            }

            if (!_isGrounded)
            {
                _airTime += Time.fixedDeltaTime;
            }
            else
            {
                if (_airTime >= 0.12f && _jumpCooldownTimer <= 0f)
                {
                    PlayLandingSound();
                }

                _airTime = 0f;
                _isJumping = false;
            }
        }

        private void ApplyMovement()
        {
            if (_rb == null) return;

            Vector2 platformVel = Vector2.zero;
            if (_isGrounded && !_isJumping && _activeMovingPlatform != null)
            {
                platformVel = _activeMovingPlatform.Velocity;
            }

            if (_isJumping && _jumpCooldownTimer > 0f)
            {
                _rb.linearVelocity = new Vector2(_moveInput * moveSpeed, _rb.linearVelocity.y);
            }
            else if (_isOnSlope && _isGrounded)
            {
                Vector2 slopeDir = new Vector2(_slopeNormal.y, -_slopeNormal.x).normalized;

                if (Mathf.Abs(_moveInput) > 0.01f)
                {
                    _rb.linearVelocity = slopeDir * (_moveInput * moveSpeed) + platformVel;
                }
                else
                {
                    _rb.linearVelocity = platformVel;
                }
            }
            else
            {
                float yVel = _rb.linearVelocity.y;
                if (_isGrounded && yVel < 0.05f && yVel > -2f)
                {
                    yVel = Mathf.Abs(platformVel.y) > 0.01f ? platformVel.y : 0f;
                }

                _rb.linearVelocity = new Vector2(_moveInput * moveSpeed + platformVel.x, yVel);
            }
        }

        private void SetAnimation(float horizontalInput)
        {
            if (horizontalInput > 0f)
            {
                transform.localScale = new Vector3(Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
            }
            else if (horizontalInput < 0f)
            {
                transform.localScale = new Vector3(-Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
            }

            string newAnimation;

            if (_isGrounded || (_isOnSlope && !_isJumping))
            {
                newAnimation = Mathf.Abs(horizontalInput) < 0.01f ? AnimStateIdle : AnimStateRun;
            }
            else
            {
                if (!_isJumping && _airTime < 0.12f && Mathf.Abs(horizontalInput) > 0.01f &&
                    _rb != null && _rb.linearVelocity.y >= -1.5f)
                {
                    newAnimation = AnimStateRun;
                }
                else if (_rb != null && _rb.linearVelocity.y > 0.1f)
                {
                    newAnimation = AnimStateJump;
                }
                else
                {
                    newAnimation = AnimStateFall;
                }
            }

            ChangeAnimation(newAnimation);
        }

        private void ChangeAnimation(string newAnimation)
        {
            if (_currentAnimation == newAnimation)
            {
                if (_currentAnimation == AnimStateFall && adjustHitboxOnFall && playerCollider != null)
                {
                    Vector2 alignedOffset = GetAlignedFallColliderOffset();
                    if (playerCollider.size != fallColliderSize || playerCollider.offset != alignedOffset)
                    {
                        playerCollider.size = fallColliderSize;
                        playerCollider.offset = alignedOffset;
                    }
                }
                return;
            }

            if (_animator != null)
            {
                _animator.Play(newAnimation);
            }

            _currentAnimation = newAnimation;
            UpdateColliderForAnimation(newAnimation);
        }

        public void CacheDefaultCollider()
        {
            if (playerCollider == null)
            {
                playerCollider = GetComponent<BoxCollider2D>();
            }

            if (playerCollider != null)
            {
                if (playerCollider.edgeRadius < 0.05f)
                {
                    playerCollider.edgeRadius = 0.05f;
                }

                if (playerCollider.sharedMaterial == null)
                {
                    PhysicsMaterial2D mat = Resources.Load<PhysicsMaterial2D>("MovementMaterial");
                    if (mat == null)
                    {
                        mat = new PhysicsMaterial2D("MovementMaterial") { friction = 0f, bounciness = 0f };
                    }
                    playerCollider.sharedMaterial = mat;
                }

                if (!_isDefaultColliderCached)
                {
                    _defaultColliderSize = playerCollider.size;
                    _defaultColliderOffset = playerCollider.offset;
                    _isDefaultColliderCached = true;

                    if (fallColliderSize == Vector2.zero)
                    {
                        float bottomReduction = 0.55f;
                        fallColliderSize = new Vector2(_defaultColliderSize.x * 0.9f, Mathf.Max(0.1f, _defaultColliderSize.y - bottomReduction));
                        fallColliderOffset = new Vector2(_defaultColliderOffset.x, _defaultColliderOffset.y + bottomReduction * 0.5f);
                    }
                }
            }
        }

        private Vector2 GetAlignedFallColliderOffset()
        {
            // Keep bottom edge aligned with defaultCollider so transitioning out of Fall never penetrates the floor
            float defaultBottom = _defaultColliderOffset.y - _defaultColliderSize.y * 0.5f;
            return new Vector2(fallColliderOffset.x, defaultBottom + fallColliderSize.y * 0.5f);
        }

        private void UpdateColliderForAnimation(string animName)
        {
            if (playerCollider == null)
            {
                playerCollider = GetComponent<BoxCollider2D>();
                if (playerCollider == null) return;
            }

            if (!_isDefaultColliderCached)
            {
                CacheDefaultCollider();
            }

            if (adjustHitboxOnFall && animName == AnimStateFall)
            {
                playerCollider.size = fallColliderSize;
                playerCollider.offset = GetAlignedFallColliderOffset();
            }
            else
            {
                playerCollider.size = _defaultColliderSize;
                playerCollider.offset = _defaultColliderOffset;
            }
        }

        private void OnValidate()
        {
            if (playerCollider == null)
            {
                playerCollider = GetComponent<BoxCollider2D>();
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (adjustHitboxOnFall)
            {
                Gizmos.color = new Color(0f, 0.8f, 1f, 0.75f);
                float flipSign = transform.localScale.x >= 0 ? 1f : -1f;
                Vector2 previewOffset = _isDefaultColliderCached ? GetAlignedFallColliderOffset() : fallColliderOffset;
                Vector3 center = transform.position + new Vector3(previewOffset.x * flipSign, previewOffset.y, 0f);
                Gizmos.DrawWireCube(center, new Vector3(fallColliderSize.x, fallColliderSize.y, 0.1f));
            }

            if (playerCollider != null)
            {
                Gizmos.color = _isOnSlope ? Color.yellow : (_isGrounded ? Color.green : Color.red);
                Bounds bounds = playerCollider.bounds;
                Vector3 boxCenter = bounds.center + Vector3.down * (slopeCheckDistance * 0.5f);
                Gizmos.DrawWireCube(boxCenter, new Vector3(bounds.size.x * 0.85f, bounds.size.y + slopeCheckDistance, 0.1f));
            }
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            CheckDamage(collision.gameObject);
            CheckMovingPlatformContact(collision);
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            CheckDamage(collision.gameObject);
            CheckMovingPlatformContact(collision);
        }

        private void OnCollisionExit2D(Collision2D collision)
        {
            if (_activeMovingPlatform != null)
            {
                if (collision.gameObject == _activeMovingPlatform.gameObject ||
                    collision.transform.IsChildOf(_activeMovingPlatform.transform))
                {
                    _activeMovingPlatform = null;
                }
            }
        }

        private void CheckMovingPlatformContact(Collision2D collision)
        {
            if (_activeMovingPlatform == null && !_isJumping)
            {
                if (collision.gameObject.TryGetComponent(out MovingPlatform platform) ||
                    (collision.transform.parent != null && collision.transform.parent.TryGetComponent(out platform)))
                {
                    for (int i = 0; i < collision.contactCount; i++)
                    {
                        if (collision.GetContact(i).normal.y > 0.5f)
                        {
                            _activeMovingPlatform = platform;
                            break;
                        }
                    }
                }
            }
        }

        private void OnTriggerEnter2D(Collider2D collider)
        {
            CheckDamage(collider.gameObject);
        }

        private void OnTriggerStay2D(Collider2D collider)
        {
            CheckDamage(collider.gameObject);
        }

        private void CheckDamage(GameObject target)
        {
            if (target == null) return;

            bool isDamage = target.CompareTag("Damage") ||
                            (target.transform.parent != null && target.transform.parent.CompareTag("Damage")) ||
                            target.GetComponent<EnemyController>() != null ||
                            (target.transform.parent != null && target.transform.parent.GetComponent<EnemyController>() != null);

            if (isDamage)
            {
                TakeDamage(25);
            }
        }

        private IEnumerator BlinkRed()
        {
            _isInvulnerable = true;
            if (_spriteRenderer != null)
            {
                _spriteRenderer.color = Color.red;
            }

            yield return new WaitForSeconds(0.15f);

            if (_spriteRenderer != null)
            {
                _spriteRenderer.color = Color.white;
            }

            if (invulnerabilityTime > 0.15f)
            {
                yield return new WaitForSeconds(invulnerabilityTime - 0.15f);
            }

            _isInvulnerable = false;
        }

        public void AddArrows(int amount)
        {
            arrowsCount += amount;
            UpdateArrowUI();

            FrankVisionController fv = FrankVisionController.Instance ?? FrankVisionController.EnsureExists(gameObject);
            if (fv != null)
            {
                fv.OnArrowCollected();
            }
        }

        public void EnsureArrowPrefab()
        {
            if (arrowPrefab == null)
            {
                arrowPrefab = Resources.Load<GameObject>("Arrow") ??
                              Resources.Load<GameObject>("Prefabs/Arrow");
            }
        }

        public void AutoFindArrowCountUI()
        {
            if (arrowCountText != null) return;

            GameObject counterObj = GameObject.Find("ArrowCountText") ?? GameObject.Find("ArrowCounter");
            if (counterObj != null)
            {
                arrowCountText = counterObj.GetComponent<TextMeshProUGUI>() ??
                                 counterObj.GetComponentInChildren<TextMeshProUGUI>();
            }

            if (arrowCountText == null)
            {
                Canvas canvas = Object.FindAnyObjectByType<Canvas>();
                if (canvas != null)
                {
                    GameObject newObj = new GameObject("ArrowCounter");
                    newObj.transform.SetParent(canvas.transform, false);

                    RectTransform rt = newObj.AddComponent<RectTransform>();
                    rt.anchorMin = new Vector2(1f, 1f);
                    rt.anchorMax = new Vector2(1f, 1f);
                    rt.pivot = new Vector2(1f, 1f);
                    rt.anchoredPosition = new Vector2(-40f, -40f);
                    rt.sizeDelta = new Vector2(320f, 90f);

                    TextMeshProUGUI tmp = newObj.AddComponent<TextMeshProUGUI>();
                    tmp.fontSize = 56f;
                    tmp.fontStyle = FontStyles.Bold;
                    tmp.alignment = TextAlignmentOptions.TopRight;
                    tmp.color = Color.white;
                    tmp.outlineColor = Color.black;
                    tmp.outlineWidth = 0.2f;

                    TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/Electronic Highway Sign SDF") ??
                                         Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
                    if (font != null) tmp.font = font;

                    arrowCountText = tmp;
                }
            }
        }

        public void UpdateArrowUI()
        {
            if (arrowCountText == null)
            {
                AutoFindArrowCountUI();
            }

            if (arrowCountText != null)
            {
                arrowCountText.text = $"x {arrowsCount}";
            }
        }

        public void Die()
        {
            _activeMovingPlatform = null;
            if (_walkAudioSource != null && _walkAudioSource.isPlaying)
            {
                _walkAudioSource.Stop();
            }

            _isWalkAudioPaused = false;
            string currentScene = SceneManager.GetActiveScene().name;
            SceneManager.LoadScene(string.IsNullOrEmpty(currentScene) ? "Level 1" : currentScene);
        }

        private void Shoot()
        {
            if (arrowsCount <= 0 || Time.time < _nextArrowTime) return;

            EnsureArrowPrefab();
            if (arrowPrefab == null) return;

            _nextArrowTime = Time.time + arrowCooldown;
            arrowsCount--;
            UpdateArrowUI();

            float shootDir = transform.localScale.x >= 0f ? 1f : -1f;
            Vector2 rayOrigin = (Vector2)transform.position + new Vector2(0f, 0.2f);
            Vector2 shootVec = new Vector2(shootDir, 0f);
            float spawnDist = 0.65f;

            RaycastHit2D[] wallHits = Physics2D.RaycastAll(rayOrigin, shootVec, 0.85f);
            foreach (RaycastHit2D wh in wallHits)
            {
                if (wh.collider == null || wh.collider.gameObject == gameObject) continue;
                if (wh.collider.transform.IsChildOf(transform) || wh.collider.isTrigger) continue;
                spawnDist = Mathf.Min(spawnDist, Mathf.Max(0.15f, wh.distance - 0.2f));
            }

            Vector3 spawnPos = arrowSpawnPoint != null
                ? arrowSpawnPoint.position
                : transform.position + new Vector3(shootDir * spawnDist, 0.2f, 0f);

            Quaternion rot = shootDir > 0f ? Quaternion.Euler(0f, 0f, 180f) : Quaternion.identity;
            GameObject arrow = Instantiate(arrowPrefab, spawnPos, rot);

            Collider2D arrowCol = arrow.GetComponent<Collider2D>();
            if (arrowCol != null)
            {
                foreach (Collider2D pCol in GetComponentsInChildren<Collider2D>())
                {
                    if (pCol != null)
                    {
                        Physics2D.IgnoreCollision(arrowCol, pCol, true);
                    }
                }
            }

            ArrowProjectile arrowComp = arrow.GetComponent<ArrowProjectile>();
            if (arrowComp != null)
            {
                arrowComp.Init(shootDir);
            }
            else
            {
                Rigidbody2D arrowRb = arrow.GetComponent<Rigidbody2D>();
                if (arrowRb != null)
                {
                    arrowRb.linearVelocity = new Vector2(shootDir * 12f, 0f);
                }
            }
        }

        private void SetupWalkAudio()
        {
            if (walkLevel1Clip == null)
            {
                walkLevel1Clip = Resources.Load<AudioClip>("Audio/walk_level1") ??
                                 Resources.Load<AudioClip>("walk_level1");
            }

            if (walkLevel2Clip == null)
            {
                walkLevel2Clip = Resources.Load<AudioClip>("Audio/walk_level2") ??
                                 Resources.Load<AudioClip>("walk_level2");
            }

            if (_walkAudioSource == null)
            {
                Transform existing = transform.Find("PlayerWalkAudio");
                GameObject walkObj = existing != null ? existing.gameObject : new GameObject("PlayerWalkAudio");
                walkObj.transform.SetParent(transform);
                walkObj.transform.localPosition = Vector3.zero;

                _walkAudioSource = walkObj.GetComponent<AudioSource>() ?? walkObj.AddComponent<AudioSource>();
                _walkAudioSource.playOnAwake = false;
                _walkAudioSource.loop = true;
                _walkAudioSource.spatialBlend = 0f;
                _walkAudioSource.volume = walkAudioVolume;
            }

            string currentScene = SceneManager.GetActiveScene().name;
            AudioClip targetClip = IsLevel1Scene(currentScene) ? walkLevel1Clip : walkLevel2Clip;

            if (_walkAudioSource.clip != targetClip)
            {
                bool wasPlaying = _walkAudioSource.isPlaying;
                float prevTime = _walkAudioSource.clip != null ? _walkAudioSource.time : 0f;
                _walkAudioSource.clip = targetClip;
                if (wasPlaying && targetClip != null)
                {
                    _walkAudioSource.time = Mathf.Clamp(prevTime, 0f, targetClip.length - 0.01f);
                    _walkAudioSource.Play();
                }
            }
        }

        private void OnSceneLoadedForWalkAudio(Scene scene, LoadSceneMode mode)
        {
            EnsureTilemapCompositeColliders();
            SetupWalkAudio();
        }

        private static bool IsLevel1Scene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return true;
            string lower = sceneName.ToLowerInvariant();
            return lower.Contains("level 1") || lower.Contains("level1") || lower.Contains("gamescene");
        }

        private static bool IsLevel2Scene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return false;
            string lower = sceneName.ToLowerInvariant();
            return lower.Contains("level 2") || lower.Contains("level2");
        }

        private static bool IsLevel3Scene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return false;
            string lower = sceneName.ToLowerInvariant();
            return lower.Contains("level 3") || lower.Contains("level3");
        }

        private void UpdateWalkAudio()
        {
            if (_walkAudioSource == null) return;

            string currentScene = SceneManager.GetActiveScene().name;
            AudioClip targetClip = IsLevel1Scene(currentScene) ? walkLevel1Clip : walkLevel2Clip;
            if (_walkAudioSource.clip != targetClip && targetClip != null)
            {
                float prevTime = _walkAudioSource.clip != null ? _walkAudioSource.time : 0f;
                bool wasPlaying = _walkAudioSource.isPlaying;
                _walkAudioSource.clip = targetClip;
                _walkAudioSource.time = Mathf.Clamp(prevTime, 0f, targetClip.length - 0.01f);
                if (wasPlaying) _walkAudioSource.Play();
            }

            if (_walkAudioSource.clip == null) return;

            if (health <= 0)
            {
                if (_walkAudioSource.isPlaying) _walkAudioSource.Stop();
                _isWalkAudioPaused = false;
                return;
            }

            bool isWalking = (_isGrounded || (_isOnSlope && !_isJumping)) &&
                             Mathf.Abs(_moveInput) > 0.05f &&
                             (_rb == null || Mathf.Abs(_rb.linearVelocity.x) > 0.1f);

            if (isWalking)
            {
                if (!_walkAudioSource.isPlaying)
                {
                    if (_isWalkAudioPaused) _walkAudioSource.UnPause();
                    else _walkAudioSource.Play();
                    _isWalkAudioPaused = false;
                }
            }
            else if (_walkAudioSource.isPlaying)
            {
                _walkAudioSource.Pause();
                _isWalkAudioPaused = true;
            }
        }

        private void EnsureLandingAudio()
        {
            if (_landingAudioSource != null) return;

            Transform child = transform.Find("PlayerLandingAudio");
            if (child == null)
            {
                GameObject obj = new GameObject("PlayerLandingAudio");
                obj.transform.SetParent(transform, false);
                child = obj.transform;
            }

            _landingAudioSource = child.GetComponent<AudioSource>() ?? child.gameObject.AddComponent<AudioSource>();
            _landingAudioSource.loop = false;
            _landingAudioSource.playOnAwake = false;
            _landingAudioSource.spatialBlend = 0f;
        }

        private void PlayLandingSound()
        {
            if (health <= 0) return;

            string currentScene = SceneManager.GetActiveScene().name;
            AudioClip clip = IsLevel1Scene(currentScene) ? walkLevel1Clip : walkLevel2Clip;
            if (clip == null) return;

            EnsureLandingAudio();
            if (_landingAudioSource != null)
            {
                _landingAudioSource.clip = clip;
                _landingAudioSource.time = Mathf.Clamp(clip.length * 0.48f, 0f, clip.length - 0.1f);
                _landingAudioSource.volume = walkAudioVolume;
                _landingAudioSource.Play();

                if (_stopLandingRoutine != null)
                {
                    StopCoroutine(_stopLandingRoutine);
                }

                _stopLandingRoutine = StartCoroutine(StopLandingSoundAfter(0.32f));
            }
        }

        private IEnumerator StopLandingSoundAfter(float duration)
        {
            yield return new WaitForSeconds(duration);
            if (_landingAudioSource != null && _landingAudioSource.isPlaying)
            {
                _landingAudioSource.Stop();
            }
            _stopLandingRoutine = null;
        }

        public static void EnsureTilemapCompositeColliders()
        {
            TilemapCollider2D[] tilemapColliders = Object.FindObjectsByType<TilemapCollider2D>();
            foreach (TilemapCollider2D tc in tilemapColliders)
            {
                CompositeCollider2D comp = tc.GetComponent<CompositeCollider2D>();
                if (comp != null && tc.compositeOperation != Collider2D.CompositeOperation.Merge)
                {
                    tc.compositeOperation = Collider2D.CompositeOperation.Merge;
                    comp.GenerateGeometry();
                }
            }
        }

        private bool IsValidGroundHit(RaycastHit2D hit)
        {
            return hit.collider != null &&
                   hit.collider != playerCollider &&
                   hit.normal.y > 0.3f &&
                   (_dropThroughTimer <= 0f || hit.collider != _currentOneWayPlatform);
        }

        private bool IsFlatSurface(RaycastHit2D hit)
        {
            return IsValidGroundHit(hit) && Vector2.Angle(hit.normal, Vector2.up) < minSlopeAngle;
        }
    }
}
