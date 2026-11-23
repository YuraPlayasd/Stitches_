using Stitches.Enemies;
using Stitches.Items;
using Stitches.Player;
using UnityEngine;

namespace Stitches.Combat
{
    public class ArrowProjectile : MonoBehaviour
    {
        public float speed = 12f;
        public float lifeTime = 3f;
        public int damage = 1;

        [Header("Flight Distance Settings")]
        [Tooltip("Fixed horizontal distance the arrow flies before dropping down as a collectible pickup")]
        public float maxFlightDistance = 8f;

        private Vector2 _startPosition;
        private bool _hasStartPosition;
        private Rigidbody2D _rb;
        private Collider2D _col;
        private float _direction = 1f;
        private bool _initialized;
        private bool _hasEnteredCameraView;
        private bool _hasDroppedAsPickup;
        private float _spawnTime;

        /// <summary>
        /// Explicitly initializes flight direction and visual orientation.
        /// Sprite arrow tip points LEFT at 0 deg; 180 deg points RIGHT.
        /// </summary>
        public void Init(float dir)
        {
            _direction = dir >= 0f ? 1f : -1f;
            transform.rotation = _direction > 0f ? Quaternion.Euler(0f, 0f, 180f) : Quaternion.identity;
            _initialized = true;

            if (!_hasStartPosition)
            {
                _startPosition = transform.position;
                _hasStartPosition = true;
            }

            if (_rb == null)
            {
                _rb = GetComponent<Rigidbody2D>();
            }

            if (_rb != null)
            {
                _rb.linearVelocity = new Vector2(_direction * speed, 0f);
            }
        }

        private void Start()
        {
            _spawnTime = Time.time;

            if (!_hasStartPosition)
            {
                _startPosition = transform.position;
                _hasStartPosition = true;
            }

            if (_rb == null) _rb = GetComponent<Rigidbody2D>();
            if (_col == null) _col = GetComponent<Collider2D>();

            if (!_initialized)
            {
                float zRot = transform.eulerAngles.z;
                _direction = (zRot > 90f && zRot < 270f) ? 1f : -1f;
            }

            if (_col != null)
            {
                _col.isTrigger = false;
            }

            if (_rb != null)
            {
                _rb.gravityScale = 0f;
                _rb.constraints = RigidbodyConstraints2D.FreezeRotation;
                _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                _rb.linearVelocity = new Vector2(_direction * speed, 0f);
            }

            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null && _col != null)
            {
                foreach (Collider2D pCol in playerObj.GetComponentsInChildren<Collider2D>())
                {
                    if (pCol != null)
                    {
                        Physics2D.IgnoreCollision(_col, pCol, true);
                    }
                }
            }

            ArrowProjectile[] otherArrows = Object.FindObjectsByType<ArrowProjectile>();
            if (_col != null)
            {
                foreach (ArrowProjectile other in otherArrows)
                {
                    if (other != null && other != this)
                    {
                        Collider2D otherCol = other.GetComponent<Collider2D>();
                        if (otherCol != null)
                        {
                            Physics2D.IgnoreCollision(_col, otherCol, true);
                        }
                    }
                }
            }

            CheckCollisionAhead(0.05f);
            Invoke(nameof(OnLifeTimeExpired), lifeTime);
        }

        private void OnLifeTimeExpired()
        {
            if (_hasDroppedAsPickup) return;
            Destroy(gameObject);
        }

        private void Update()
        {
            if (_hasDroppedAsPickup) return;

            CheckCollisionAhead(Time.deltaTime);
            if (_hasDroppedAsPickup) return;

            float traveled = Mathf.Abs(transform.position.x - _startPosition.x);
            if (traveled >= maxFlightDistance)
            {
                DropAsCollectible();
                return;
            }

            Camera cam = Camera.main;
            if (cam != null && !_hasDroppedAsPickup)
            {
                Vector3 vp = cam.WorldToViewportPoint(transform.position);
                bool inView = vp.z > 0f && vp.x >= -0.02f && vp.x <= 1.02f && vp.y >= -0.05f && vp.y <= 1.05f;

                if (inView)
                {
                    _hasEnteredCameraView = true;
                }
                else if (_hasEnteredCameraView || Time.time - _spawnTime > 0.35f)
                {
                    DropAsCollectible();
                }
            }
        }

        private void FixedUpdate()
        {
            if (_hasDroppedAsPickup) return;

            CheckCollisionAhead(Time.fixedDeltaTime);
            if (_hasDroppedAsPickup) return;

            float traveled = Mathf.Abs(transform.position.x - _startPosition.x);
            if (traveled >= maxFlightDistance)
            {
                DropAsCollectible();
                return;
            }

            if (_rb != null && !_hasDroppedAsPickup)
            {
                _rb.linearVelocity = new Vector2(_direction * speed, 0f);
            }
        }

        private void CheckCollisionAhead(float dt)
        {
            if (_hasDroppedAsPickup) return;

            Vector2 sweepDir = new Vector2(_direction, 0f);
            Vector2 currentPos = _rb != null ? _rb.position : (Vector2)transform.position;
            float stepDist = speed * Mathf.Max(dt, 0.02f);
            float checkDist = stepDist + 0.35f;

            Vector2[] origins =
            {
                currentPos,
                currentPos + new Vector2(0f, 0.22f),
                currentPos + new Vector2(0f, -0.22f)
            };

            RaycastHit2D bestHit = default;
            float bestDist = float.MaxValue;
            bool foundHit = false;

            foreach (Vector2 origin in origins)
            {
                Vector2 rayStart = origin - sweepDir * 0.25f;
                RaycastHit2D[] hits = Physics2D.RaycastAll(rayStart, sweepDir, checkDist + 0.35f);
                foreach (RaycastHit2D hit in hits)
                {
                    if (!IsValidSweepHit(hit)) continue;

                    if (hit.distance < bestDist)
                    {
                        bestDist = hit.distance;
                        bestHit = hit;
                        foundHit = true;
                    }
                }
            }

            RaycastHit2D[] boxHits = Physics2D.BoxCastAll(currentPos, new Vector2(0.5f, 0.4f), 0f, sweepDir, checkDist);
            foreach (RaycastHit2D hit in boxHits)
            {
                if (!IsValidSweepHit(hit)) continue;

                if (hit.distance < bestDist)
                {
                    bestDist = hit.distance;
                    bestHit = hit;
                    foundHit = true;
                }
            }

            if (foundHit && bestHit.collider != null)
            {
                Vector2 contact = bestHit.point == Vector2.zero ? currentPos : bestHit.point;
                transform.position = contact - sweepDir * 0.35f;
                HandleHit(bestHit.collider.gameObject);
            }
        }

        private bool IsValidSweepHit(RaycastHit2D hit)
        {
            if (hit.collider == null) return false;
            if (hit.collider.gameObject == gameObject || hit.collider.transform.IsChildOf(transform)) return false;

            if (hit.collider.CompareTag("Player") || hit.collider.GetComponentInParent<PlayerController>() != null)
                return false;

            if (hit.collider.GetComponent<ArrowProjectile>() != null || hit.collider.GetComponent<ArrowPickup>() != null)
                return false;

            if (hit.collider.GetComponentInParent<EnemyController>() != null)
                return true;

            return !hit.collider.isTrigger;
        }

        public void DropAsCollectible()
        {
            if (_hasDroppedAsPickup) return;
            _hasDroppedAsPickup = true;

            CancelInvoke(nameof(OnLifeTimeExpired));
            transform.rotation = Quaternion.Euler(0f, 0f, -90f);

            if (_rb == null) _rb = GetComponent<Rigidbody2D>();
            if (_rb != null)
            {
                _rb.linearVelocity = Vector2.zero;
                _rb.gravityScale = 2f;
                _rb.constraints = RigidbodyConstraints2D.FreezeRotation;
                _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            }

            if (_col == null) _col = GetComponent<Collider2D>();
            if (_col != null)
            {
                _col.isTrigger = true;
                if (_col is BoxCollider2D box)
                {
                    box.size = new Vector2(0.8f, 1.6f);
                    box.offset = Vector2.zero;
                }

                GameObject playerObj = GameObject.FindWithTag("Player");
                if (playerObj != null)
                {
                    foreach (Collider2D pCol in playerObj.GetComponentsInChildren<Collider2D>())
                    {
                        if (pCol != null)
                        {
                            Physics2D.IgnoreCollision(_col, pCol, false);
                        }
                    }
                }
            }

            ArrowPickup pickup = GetComponent<ArrowPickup>() ?? gameObject.AddComponent<ArrowPickup>();
            pickup.arrowsToAdd = 1;
            pickup.isFalling = true;
            pickup.groundLayer = ~0;

            damage = 0;
            enabled = false;
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            HandleHit(collision.gameObject);
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            HandleHit(collision.gameObject);
        }

        private void HandleHit(GameObject hitObj)
        {
            if (_hasDroppedAsPickup || hitObj == null) return;
            if (hitObj == gameObject || hitObj.transform.IsChildOf(transform)) return;
            if (hitObj.CompareTag("Player") || hitObj.GetComponentInParent<PlayerController>() != null) return;
            if (hitObj.GetComponent<ArrowProjectile>() != null || hitObj.GetComponent<ArrowPickup>() != null) return;

            EnemyController enemy = hitObj.GetComponent<EnemyController>() ??
                                    hitObj.GetComponentInParent<EnemyController>();

            if (enemy != null)
            {
                enemy.TakeDamage(damage);
                Destroy(gameObject);
                return;
            }

            DropAsCollectible();
        }
    }
}
