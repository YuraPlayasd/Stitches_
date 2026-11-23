using System.Collections;
using System.Collections.Generic;
using Stitches.Combat;
using UnityEngine;

namespace Stitches.Enemies
{
    public enum EnemyType
    {
        Zombie,
        Skeleton,
        Spirit,
        Custom
    }

    public class EnemyController : MonoBehaviour
    {
        [Header("Enemy Type & Health")]
        public EnemyType enemyType = EnemyType.Zombie;
        [Tooltip("Health points: Zombie=1 (1 arrow), Skeleton=2 (2 arrows), Spirit=3 (3 arrows)")]
        public int health = 1;
        public int maxHealth = 1;

        [Header("Movement Speeds")]
        public float patrolSpeed = 2f;
        public float chaseSpeed = 3.5f;

        [Header("Patrol Waypoints")]
        public Transform[] waypoints;

        [Header("Target Tracking")]
        public Transform targetPlayer;
        [Tooltip("Horizontal sight range to detect the player")]
        public float detectionRadius = 5f;
        [Tooltip("Maximum vertical height difference to detect the player")]
        public float maxVerticalDistance = 4f;

        [Header("Obstacle & Ledge Detection")]
        [Tooltip("Layer considered solid ground / obstacles (defaults to Ground)")]
        public LayerMask groundLayer;
        [Tooltip("Prevents ground enemies (Skeleton/Zombie) from walking off ledges during patrol")]
        public bool stopAtLedges = true;
        [Tooltip("If true, enemies also stop at ledges when chasing the player")]
        public bool stopAtLedgesWhenChasing = false;
        [Tooltip("Requires clear line-of-sight to player (blocked by walls/floors)")]
        public bool requireLineOfSight = true;
        [Tooltip("When chasing, stop at patrol waypoint boundaries instead of running off")]
        public bool stayWithinWaypoints = false;

        [Header("Death Animation")]
        public float deathDuration = 0.65f;

        private static readonly HashSet<Collider2D> AllEnemyColliders = new HashSet<Collider2D>();

        private int _currentPointIndex;
        private SpriteRenderer _spriteRenderer;
        private Animator _animator;
        private Collider2D _enemyCollider;
        private Collider2D[] _myColliders;
        private Rigidbody2D _rb;
        private bool _isDead;

        public bool IsDead => _isDead;

        private void OnEnable()
        {
            RegisterEnemyColliders();
        }

        private void OnDisable()
        {
            UnregisterEnemyColliders();
        }

        private void OnDestroy()
        {
            UnregisterEnemyColliders();
        }

        private void RegisterEnemyColliders()
        {
            if (_enemyCollider == null)
            {
                _enemyCollider = GetComponent<Collider2D>();
            }

            _myColliders = GetComponentsInChildren<Collider2D>(true);
            if (_myColliders == null || _myColliders.Length == 0) return;

            AllEnemyColliders.RemoveWhere(col => col == null);

            foreach (Collider2D otherCol in AllEnemyColliders)
            {
                if (otherCol == null) continue;
                foreach (Collider2D myCol in _myColliders)
                {
                    if (myCol != null && myCol != otherCol)
                    {
                        Physics2D.IgnoreCollision(myCol, otherCol, true);
                    }
                }
            }

            foreach (Collider2D myCol in _myColliders)
            {
                if (myCol != null)
                {
                    AllEnemyColliders.Add(myCol);
                }
            }
        }

        private void UnregisterEnemyColliders()
        {
            if (_myColliders == null) return;

            foreach (Collider2D myCol in _myColliders)
            {
                if (myCol != null)
                {
                    AllEnemyColliders.Remove(myCol);
                }
            }
        }

        private void Awake()
        {
            InitializeHealth();
            InitGroundLayer();
        }

        private void InitGroundLayer()
        {
            if (groundLayer.value == 0)
            {
                int ground = LayerMask.NameToLayer("Ground");
                groundLayer = ground >= 0 ? (1 << ground) : (1 << 6);
            }
        }

        private void Start()
        {
            InitGroundLayer();

            _spriteRenderer = GetComponent<SpriteRenderer>() ?? GetComponentInChildren<SpriteRenderer>();
            if (_spriteRenderer != null)
            {
                _spriteRenderer.color = Color.white;
            }

            _animator = GetComponent<Animator>() ?? GetComponentInChildren<Animator>() ?? gameObject.AddComponent<Animator>();

            if (_animator.runtimeAnimatorController == null)
            {
                string ctrlName = enemyType == EnemyType.Zombie ? "AC_Enemy_Zombie" :
                                  enemyType == EnemyType.Skeleton ? "AC_Enemy_Skeleton" : "AC_Enemy_Spirit";
                RuntimeAnimatorController ctrl = Resources.Load<RuntimeAnimatorController>("EnemyControllers/" + ctrlName);
                if (ctrl != null)
                {
                    _animator.runtimeAnimatorController = ctrl;
                }
            }

            _enemyCollider = GetComponent<Collider2D>();
            _rb = GetComponent<Rigidbody2D>();

            if (enemyType == EnemyType.Spirit)
            {
                if (_rb != null)
                {
                    _rb.gravityScale = 0f;
                    _rb.bodyType = RigidbodyType2D.Kinematic;
                }

                if (_enemyCollider != null)
                {
                    _enemyCollider.isTrigger = true;
                }
            }

            if (targetPlayer == null)
            {
                GameObject p = GameObject.FindWithTag("Player");
                if (p != null) targetPlayer = p.transform;
            }

            RegisterEnemyColliders();
        }

        private void OnValidate()
        {
            InitializeHealth();
        }

        private void InitializeHealth()
        {
            if (enemyType == EnemyType.Zombie)
            {
                maxHealth = 1;
                patrolSpeed = 0.9f;
                chaseSpeed = 1.6f;
            }
            else if (enemyType == EnemyType.Skeleton)
            {
                maxHealth = 2;
                patrolSpeed = 2.0f;
                chaseSpeed = 3.2f;
            }
            else if (enemyType == EnemyType.Spirit)
            {
                maxHealth = 3;
                patrolSpeed = 2.5f;
                chaseSpeed = 4.0f;
            }

            if (health <= 0 || health > maxHealth)
            {
                health = maxHealth;
            }
        }

        private void Update()
        {
            if (_isDead) return;

            if (transform.position.y < -35f)
            {
                Die();
                return;
            }

            if (CanSeePlayer())
            {
                ChasePlayer();
            }
            else
            {
                Patrol();
            }
        }

        private bool CanSeePlayer()
        {
            if (targetPlayer == null) return false;

            float dx = Mathf.Abs(transform.position.x - targetPlayer.position.x);
            float dy = Mathf.Abs(transform.position.y - targetPlayer.position.y);

            if (dx > detectionRadius || (maxVerticalDistance > 0f && dy > maxVerticalDistance))
            {
                return false;
            }

            if (enemyType == EnemyType.Spirit || !requireLineOfSight)
            {
                return true;
            }

            Vector2 eyeOrigin = (Vector2)transform.position + Vector2.up * 0.4f;
            Vector2 targetCenter = (Vector2)targetPlayer.position + Vector2.up * 0.4f;
            Vector2 toPlayer = targetCenter - eyeOrigin;
            float dist = toPlayer.magnitude;

            if (dist > 0.1f)
            {
                RaycastHit2D hit = Physics2D.Raycast(eyeOrigin, toPlayer.normalized, dist, groundLayer);
                if (hit.collider != null &&
                    hit.collider.gameObject != targetPlayer.gameObject &&
                    !hit.collider.transform.IsChildOf(targetPlayer))
                {
                    return false;
                }
            }

            return true;
        }

        public void TakeDamage(int damage)
        {
            if (_isDead) return;

            health -= damage;
            StartCoroutine(BlinkRed());

            if (health <= 0)
            {
                Die();
            }
        }

        private void Die()
        {
            _isDead = true;
            UnregisterEnemyColliders();

            if (_enemyCollider != null)
            {
                _enemyCollider.enabled = false;
            }

            if (_rb != null)
            {
                _rb.linearVelocity = Vector2.zero;
                _rb.bodyType = RigidbodyType2D.Kinematic;
            }

            if (_animator != null)
            {
                _animator.SetTrigger("Die");
                _animator.Play("Death");
            }

            Destroy(gameObject, deathDuration);
        }

        private IEnumerator BlinkRed()
        {
            if (_spriteRenderer != null)
            {
                _spriteRenderer.color = Color.red;
                yield return new WaitForSeconds(0.12f);
                if (_spriteRenderer != null)
                {
                    _spriteRenderer.color = Color.white;
                }
            }
        }

        public bool IsLedgeAhead(float moveDirX)
        {
            if (!stopAtLedges || enemyType == EnemyType.Spirit) return false;
            if (Mathf.Abs(moveDirX) < 0.01f) return false;

            float dirSign = moveDirX > 0f ? 1f : -1f;
            Bounds bounds = _enemyCollider != null ? _enemyCollider.bounds : new Bounds(transform.position, Vector3.one);

            float frontX = bounds.center.x + dirSign * (bounds.extents.x + 0.15f);
            float footY = bounds.min.y + 0.15f;
            Vector2 checkOrigin = new Vector2(frontX, footY);
            const float ledgeCheckDist = 0.8f;

            RaycastHit2D groundHit = Physics2D.Raycast(checkOrigin, Vector2.down, ledgeCheckDist, groundLayer);
            return groundHit.collider == null;
        }

        public bool IsWallAhead(float moveDirX)
        {
            if (enemyType == EnemyType.Spirit) return false;
            if (Mathf.Abs(moveDirX) < 0.01f) return false;

            float dirSign = moveDirX > 0f ? 1f : -1f;
            Bounds bounds = _enemyCollider != null ? _enemyCollider.bounds : new Bounds(transform.position, Vector3.one);

            float footY = bounds.min.y + 0.3f;
            Vector2 checkOrigin = new Vector2(bounds.center.x, footY);
            float checkDist = bounds.extents.x + 0.15f;

            RaycastHit2D wallHit = Physics2D.Raycast(checkOrigin, Vector2.right * dirSign, checkDist, groundLayer);
            return wallHit.collider != null;
        }

        private void Patrol()
        {
            if (waypoints == null || waypoints.Length == 0) return;

            Transform targetPoint = waypoints[_currentPointIndex];
            if (targetPoint == null) return;

            float dirX = targetPoint.position.x - transform.position.x;
            float distanceX = Mathf.Abs(dirX);

            if (distanceX < 0.25f || IsLedgeAhead(dirX) || IsWallAhead(dirX))
            {
                _currentPointIndex = (_currentPointIndex + 1) % waypoints.Length;
                targetPoint = waypoints[_currentPointIndex];
                if (targetPoint == null) return;
                dirX = targetPoint.position.x - transform.position.x;
            }

            if (!IsLedgeAhead(dirX) && !IsWallAhead(dirX))
            {
                Vector2 targetPosition = new Vector2(targetPoint.position.x, transform.position.y);
                transform.position = Vector2.MoveTowards(transform.position, targetPosition, patrolSpeed * Time.deltaTime);
            }

            FlipSprite(targetPoint.position.x);
        }

        private void ChasePlayer()
        {
            if (targetPlayer == null) return;

            float dirX = targetPlayer.position.x - transform.position.x;

            if ((stopAtLedgesWhenChasing && IsLedgeAhead(dirX)) || IsWallAhead(dirX))
            {
                FlipSprite(targetPlayer.position.x);
                return;
            }

            if (stayWithinWaypoints && waypoints != null && waypoints.Length >= 2)
            {
                float minX = float.MaxValue;
                float maxX = float.MinValue;
                foreach (Transform wp in waypoints)
                {
                    if (wp != null)
                    {
                        minX = Mathf.Min(minX, wp.position.x);
                        maxX = Mathf.Max(maxX, wp.position.x);
                    }
                }

                if (minX <= maxX)
                {
                    if ((dirX < 0f && transform.position.x <= minX) ||
                        (dirX > 0f && transform.position.x >= maxX))
                    {
                        FlipSprite(targetPlayer.position.x);
                        return;
                    }
                }
            }

            Vector2 targetPosition = new Vector2(targetPlayer.position.x, transform.position.y);
            transform.position = Vector2.MoveTowards(transform.position, targetPosition, chaseSpeed * Time.deltaTime);
            FlipSprite(targetPlayer.position.x);
        }

        private void FlipSprite(float targetX)
        {
            if (_spriteRenderer != null)
            {
                _spriteRenderer.flipX = (transform.position.x - targetX) < 0f;
            }
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (TryIgnoreEnemyCollision(collision)) return;
            CheckHazard(collision.gameObject);
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            if (TryIgnoreEnemyCollision(collision)) return;
            CheckHazard(collision.gameObject);
        }

        private bool TryIgnoreEnemyCollision(Collision2D collision)
        {
            if (collision.collider == null) return false;
            if (collision.collider.GetComponent<EnemyController>() == null &&
                collision.collider.GetComponentInParent<EnemyController>() == null)
            {
                return false;
            }

            if (_myColliders != null)
            {
                foreach (Collider2D myCol in _myColliders)
                {
                    if (myCol != null)
                    {
                        Physics2D.IgnoreCollision(myCol, collision.collider, true);
                    }
                }
            }

            return true;
        }

        private void OnTriggerEnter2D(Collider2D collider)
        {
            CheckHazard(collider.gameObject);
        }

        private void OnTriggerStay2D(Collider2D collider)
        {
            CheckHazard(collider.gameObject);
        }

        private void CheckHazard(GameObject hitObj)
        {
            if (_isDead || enemyType == EnemyType.Spirit || hitObj == null) return;
            if (hitObj.CompareTag("Player") ||
                hitObj.GetComponent<EnemyController>() != null ||
                hitObj.GetComponent<ArrowProjectile>() != null)
            {
                return;
            }

            bool isHazard = hitObj.CompareTag("Damage") ||
                            (hitObj.transform.parent != null && hitObj.transform.parent.CompareTag("Damage")) ||
                            hitObj.name.IndexOf("Damage", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                            hitObj.name.IndexOf("Spike", System.StringComparison.OrdinalIgnoreCase) >= 0;

            if (isHazard)
            {
                Die();
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(transform.position, new Vector3(detectionRadius * 2f, maxVerticalDistance * 2f, 0.1f));

            if (_enemyCollider != null)
            {
                float dirSign = (_spriteRenderer != null && _spriteRenderer.flipX) ? 1f : -1f;
                Bounds bounds = _enemyCollider.bounds;
                float frontX = bounds.center.x + dirSign * (bounds.extents.x + 0.15f);
                float footY = bounds.min.y + 0.15f;
                Vector2 checkOrigin = new Vector2(frontX, footY);
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(checkOrigin, checkOrigin + Vector2.down * 0.8f);
            }
        }
    }
}
