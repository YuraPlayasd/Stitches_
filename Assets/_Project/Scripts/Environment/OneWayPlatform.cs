using System.Collections;
using System.Collections.Generic;
using Stitches.Player;
using UnityEngine;

namespace Stitches.Environment
{
    /// <summary>
    /// One-way jump-through platform allowing the player to jump from below and drop down via Down key.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    [RequireComponent(typeof(PlatformEffector2D))]
    public class OneWayPlatform : MonoBehaviour
    {
        [Header("Drop-Through Settings")]
        public KeyCode dropKey = KeyCode.S;
        public KeyCode alternateDropKey = KeyCode.DownArrow;
        [Tooltip("Duration in seconds for which collision is disabled when dropping down")]
        public float dropDuration = 0.35f;

        private Collider2D _platformCollider;
        private PlatformEffector2D _effector;
        private bool _playerOnPlatform;
        private Collider2D _currentPlayerCol;

        private void Awake()
        {
            SetupComponents();
        }

        public void SetupComponents()
        {
            _platformCollider = GetComponent<Collider2D>();
            if (_platformCollider != null)
            {
                _platformCollider.usedByEffector = true;
                _platformCollider.isTrigger = false;
            }

            AreaEffector2D area = GetComponent<AreaEffector2D>();
            if (area != null)
            {
                if (Application.isPlaying) Destroy(area);
                else DestroyImmediate(area);
            }

            _effector = GetComponent<PlatformEffector2D>() ?? gameObject.AddComponent<PlatformEffector2D>();
            if (_effector != null)
            {
                _effector.useOneWay = true;
                _effector.useOneWayGrouping = true;
                _effector.surfaceArc = 170f;
                _effector.rotationalOffset = 0f;
                _effector.useColliderMask = false;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            SetupComponents();
        }

        private void Reset()
        {
            SetupComponents();
        }
#endif

        private void Update()
        {
            if (_playerOnPlatform && (Input.GetKeyDown(dropKey) || Input.GetKeyDown(alternateDropKey)))
            {
                if (_currentPlayerCol != null)
                {
                    DropPlayer(_currentPlayerCol);
                }
                else
                {
                    GameObject player = GameObject.FindWithTag("Player");
                    if (player != null)
                    {
                        Collider2D col = player.GetComponent<Collider2D>();
                        if (col != null) DropPlayer(col);
                    }
                }
            }
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            CheckPlayerCollision(collision);
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            CheckPlayerCollision(collision);
        }

        private void CheckPlayerCollision(Collision2D collision)
        {
            if (collision.gameObject.CompareTag("Player") || collision.gameObject.GetComponentInParent<PlayerController>() != null)
            {
                _playerOnPlatform = true;
                _currentPlayerCol = collision.collider;
            }
        }

        private void OnCollisionExit2D(Collision2D collision)
        {
            if (collision.gameObject.CompareTag("Player") || collision.gameObject.GetComponentInParent<PlayerController>() != null)
            {
                _playerOnPlatform = false;
                _currentPlayerCol = null;
            }
        }

        public void DropPlayer(Collider2D playerCol)
        {
            if (playerCol == null || _platformCollider == null) return;

            List<Collider2D> allCols = new List<Collider2D>();
            allCols.AddRange(playerCol.GetComponents<Collider2D>());
            if (playerCol.transform.parent != null)
            {
                allCols.AddRange(playerCol.transform.parent.GetComponents<Collider2D>());
            }

            StartCoroutine(DropPlayerThrough(allCols.ToArray()));
        }

        private IEnumerator DropPlayerThrough(Collider2D[] playerCols)
        {
            if (playerCols != null && _platformCollider != null)
            {
                foreach (Collider2D col in playerCols)
                {
                    if (col != null) Physics2D.IgnoreCollision(col, _platformCollider, true);
                }
            }

            yield return new WaitForSeconds(dropDuration);

            if (playerCols != null && _platformCollider != null)
            {
                foreach (Collider2D col in playerCols)
                {
                    if (col != null) Physics2D.IgnoreCollision(col, _platformCollider, false);
                }
            }
        }
    }
}
