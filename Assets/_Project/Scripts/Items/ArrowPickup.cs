using Stitches.Combat;
using Stitches.Player;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Stitches.Items
{
    /// <summary>
    /// Collectible arrow pickup supporting standing bundles (+3) and fallen single arrows (+1).
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class ArrowPickup : MonoBehaviour
    {
        [Header("Pickup Settings")]
        [Tooltip("Number of arrows added to the player upon collection")]
        public int arrowsToAdd = 3;

        [Header("Falling Physics")]
        public bool isFalling = false;
        public LayerMask groundLayer = ~0;

        [Header("Bobbing Animation")]
        public bool enableBobbing = true;
        public float bobbingSpeed = 2.5f;
        public float bobbingHeight = 0.12f;

        private Vector3 _initialPosition;
        private bool _isCollected;
        private Rigidbody2D _rb;
        private Collider2D _col;
        private bool _hasLanded;

        private void Awake()
        {
            if (groundLayer.value == 0)
            {
                groundLayer = ~0;
            }
        }

        private void Start()
        {
            _initialPosition = transform.position;

            _col = GetComponent<Collider2D>();
            if (_col != null)
            {
                _col.isTrigger = true;

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

            _rb = GetComponent<Rigidbody2D>();
            if (isFalling)
            {
                if (_rb == null) _rb = gameObject.AddComponent<Rigidbody2D>();
                _rb.gravityScale = 2f;
                _rb.constraints = RigidbodyConstraints2D.FreezeRotation;
                _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                CheckGroundAhead(0.5f);
            }
            else
            {
                if (_rb != null)
                {
                    _rb.bodyType = RigidbodyType2D.Kinematic;
                    _rb.linearVelocity = Vector2.zero;
                    _rb.gravityScale = 0f;
                }
                _hasLanded = true;
            }
        }

        private void Update()
        {
            if (_isCollected) return;

            PlayerController player = Object.FindAnyObjectByType<PlayerController>();
            if (player != null)
            {
                float dist = Vector2.Distance(transform.position, player.transform.position);
                if (dist < 1.35f)
                {
                    TryCollect(player.gameObject);
                    return;
                }
            }

            if (_hasLanded && enableBobbing)
            {
                float newY = _initialPosition.y + Mathf.Sin(Time.time * bobbingSpeed) * bobbingHeight;
                transform.position = new Vector3(_initialPosition.x, newY, _initialPosition.z);
            }
        }

        private void FixedUpdate()
        {
            if (_isCollected || !isFalling || _hasLanded) return;

            float fallSpeed = _rb != null ? Mathf.Abs(_rb.linearVelocity.y) : 6f;
            float checkDist = Mathf.Max(0.6f, fallSpeed * Time.fixedDeltaTime + 0.35f);

            CheckGroundAhead(checkDist);

            if (transform.position.y < -60f)
            {
                Destroy(gameObject);
            }
        }

        private void CheckGroundAhead(float distance)
        {
            if (_hasLanded || _isCollected) return;

            Vector2 pos = _rb != null ? _rb.position : (Vector2)transform.position;
            Vector2 startPoint = pos + Vector2.up * 0.25f;

            Vector2[] origins =
            {
                startPoint,
                startPoint + Vector2.left * 0.2f,
                startPoint + Vector2.right * 0.2f
            };

            RaycastHit2D bestHit = default;
            float bestDist = float.MaxValue;
            bool foundGround = false;

            foreach (Vector2 origin in origins)
            {
                RaycastHit2D[] hits = Physics2D.RaycastAll(origin, Vector2.down, distance + 0.3f, groundLayer);
                foreach (RaycastHit2D hit in hits)
                {
                    if (!IsValidGroundHit(hit)) continue;

                    if (hit.distance < bestDist)
                    {
                        bestDist = hit.distance;
                        bestHit = hit;
                        foundGround = true;
                    }
                }
            }

            RaycastHit2D[] boxHits = Physics2D.BoxCastAll(startPoint, new Vector2(0.4f, 0.2f), 0f, Vector2.down, distance, groundLayer);
            foreach (RaycastHit2D hit in boxHits)
            {
                if (!IsValidGroundHit(hit)) continue;

                if (hit.distance < bestDist)
                {
                    bestDist = hit.distance;
                    bestHit = hit;
                    foundGround = true;
                }
            }

            if (foundGround && bestHit.collider != null)
            {
                bool isSpike = IsSpikeOrPlantOrDamage(bestHit.collider, bestHit.point);
                float offset = isSpike ? 1.25f : 1.05f;
                LandOnGround(bestHit.point.y + offset);
            }
        }

        private bool IsValidGroundHit(RaycastHit2D hit)
        {
            if (hit.collider == null) return false;
            if (hit.collider.gameObject == gameObject || hit.collider.transform.IsChildOf(transform)) return false;

            if (hit.collider.CompareTag("Player") || hit.collider.GetComponentInParent<PlayerController>() != null)
                return false;

            if (hit.collider.GetComponent<ArrowProjectile>() != null || hit.collider.GetComponent<ArrowPickup>() != null)
                return false;

            return !hit.collider.isTrigger;
        }

        public void LandOnGround(float targetY)
        {
            if (_hasLanded) return;
            _hasLanded = true;
            isFalling = false;

            transform.position = new Vector3(transform.position.x, targetY, transform.position.z);
            _initialPosition = transform.position;

            if (_rb != null)
            {
                _rb.linearVelocity = Vector2.zero;
                _rb.gravityScale = 0f;
                _rb.bodyType = RigidbodyType2D.Kinematic;
            }

            if (_col == null) _col = GetComponent<Collider2D>();
            if (_col != null)
            {
                _col.isTrigger = true;
            }
        }

        public static bool IsSpikeOrPlantOrDamage(Collider2D col, Vector3 position)
        {
            if (col == null) return false;

            if (col.CompareTag("Damage")) return true;
            if (col.transform.parent != null && col.transform.parent.CompareTag("Damage")) return true;

            if (ContainsHazardKeyword(col.name)) return true;
            if (col.transform.parent != null && ContainsHazardKeyword(col.transform.parent.name)) return true;

            Tilemap tilemap = col.GetComponent<Tilemap>() ?? col.GetComponentInParent<Tilemap>();
            if (tilemap != null)
            {
                Vector3Int cellPos = tilemap.WorldToCell(position);
                Vector3Int[] candidateCells =
                {
                    cellPos,
                    cellPos + Vector3Int.down,
                    cellPos + Vector3Int.left,
                    cellPos + Vector3Int.right
                };

                foreach (Vector3Int cell in candidateCells)
                {
                    TileBase tile = tilemap.GetTile(cell);
                    if (tile != null && ContainsHazardKeyword(tile.name))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool ContainsHazardKeyword(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            return value.IndexOf("Damage", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("Spike", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("Plant", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("Hazard", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("Thorn", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (isFalling && !_hasLanded && collision != null)
            {
                if (!collision.isTrigger &&
                    !collision.CompareTag("Player") &&
                    collision.GetComponentInParent<PlayerController>() == null &&
                    collision.GetComponent<ArrowProjectile>() == null &&
                    collision.GetComponent<ArrowPickup>() == null)
                {
                    float topY = collision.bounds.max.y;
                    if (transform.position.y >= topY - 0.25f)
                    {
                        bool isSpike = IsSpikeOrPlantOrDamage(collision, transform.position);
                        float offset = isSpike ? 1.25f : 1.05f;
                        LandOnGround(topY + offset);
                        return;
                    }
                }
            }

            TryCollect(collision.gameObject);
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (isFalling && !_hasLanded && collision != null && collision.gameObject != null)
            {
                if (!collision.gameObject.CompareTag("Player") &&
                    collision.gameObject.GetComponentInParent<PlayerController>() == null &&
                    collision.gameObject.GetComponent<ArrowProjectile>() == null &&
                    collision.gameObject.GetComponent<ArrowPickup>() == null)
                {
                    if (collision.contactCount > 0 && collision.GetContact(0).normal.y > 0.4f)
                    {
                        Vector2 contact = collision.GetContact(0).point;
                        bool isSpike = IsSpikeOrPlantOrDamage(collision.collider, contact);
                        float offset = isSpike ? 1.25f : 1.05f;
                        LandOnGround(contact.y + offset);
                        return;
                    }

                    if (transform.position.y >= collision.collider.bounds.max.y - 0.25f)
                    {
                        float topY = collision.collider.bounds.max.y;
                        bool isSpike = IsSpikeOrPlantOrDamage(collision.collider, transform.position);
                        float offset = isSpike ? 1.25f : 1.05f;
                        LandOnGround(topY + offset);
                        return;
                    }
                }
            }

            TryCollect(collision.gameObject);
        }

        private void TryCollect(GameObject target)
        {
            if (_isCollected || target == null) return;

            PlayerController player = target.GetComponent<PlayerController>() ??
                                      target.GetComponentInParent<PlayerController>();

            if (player != null)
            {
                _isCollected = true;
                player.AddArrows(arrowsToAdd);
                Destroy(gameObject);
            }
        }

        public static GameObject SpawnSingleArrowPickup(Vector3 position, Transform parent = null, bool isFalling = false)
        {
            GameObject obj = new GameObject("SingleArrowPickup");
            if (parent != null) obj.transform.SetParent(parent);
            obj.transform.position = position;
            obj.transform.rotation = Quaternion.Euler(0f, 0f, -90f);
            obj.transform.localScale = new Vector3(0.85f, 0.85f, 1f);

            SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
            Sprite arrowSprite = Resources.Load<Sprite>("SPR_Projectile_Arrow") ??
                                 Resources.Load<Sprite>("Art/Arrow/sprite_0") ??
                                 Resources.Load<Sprite>("sprite_0");
            if (arrowSprite == null)
            {
                GameObject tempArrow = Resources.Load<GameObject>("Arrow") ??
                                       Resources.Load<GameObject>("Prefabs/Arrow");
                if (tempArrow != null)
                {
                    SpriteRenderer tempSr = tempArrow.GetComponent<SpriteRenderer>();
                    if (tempSr != null) arrowSprite = tempSr.sprite;
                }
            }
            sr.sprite = arrowSprite;
            sr.sortingLayerName = "Player";
            sr.sortingOrder = 5;
            sr.color = Color.white;

            BoxCollider2D box = obj.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(0.5f, 1.4f);

            Rigidbody2D rb = obj.AddComponent<Rigidbody2D>();
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;

            ArrowPickup pickup = obj.AddComponent<ArrowPickup>();
            pickup.arrowsToAdd = 1;
            pickup.groundLayer = ~0;

            if (isFalling)
            {
                rb.gravityScale = 2f;
                rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                pickup.isFalling = true;
            }
            else
            {
                rb.gravityScale = 0f;
                rb.bodyType = RigidbodyType2D.Kinematic;
                rb.linearVelocity = Vector2.zero;
                pickup.isFalling = false;
                pickup.LandOnGround(position.y);
            }

            return obj;
        }
    }
}
