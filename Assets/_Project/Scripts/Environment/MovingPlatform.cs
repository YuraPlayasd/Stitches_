using UnityEngine;

namespace Stitches.Environment
{
    public class MovingPlatform : MonoBehaviour
    {
        public float speed = 2f;
        public Transform[] points;

        private int _currentIndex;
        private Rigidbody2D _rb;

        public Vector2 Velocity { get; private set; }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>() ?? gameObject.AddComponent<Rigidbody2D>();
            _rb.bodyType = RigidbodyType2D.Kinematic;
            _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        }

        private void Start()
        {
            for (int childIdx = transform.childCount - 1; childIdx >= 0; childIdx--)
            {
                Transform child = transform.GetChild(childIdx);
                if (child.CompareTag("Player"))
                {
                    child.SetParent(null);
                }
            }

            if (points != null && points.Length > 0 && points[0] != null)
            {
                transform.position = points[0].position;
                if (_rb != null)
                {
                    _rb.position = points[0].position;
                }
            }
        }

        private void FixedUpdate()
        {
            if (points == null || points.Length == 0)
            {
                Velocity = Vector2.zero;
                return;
            }

            Transform targetPoint = points[_currentIndex];
            if (targetPoint == null)
            {
                Velocity = Vector2.zero;
                return;
            }

            Vector2 currentPos = _rb != null ? _rb.position : (Vector2)transform.position;
            Vector2 targetPos = targetPoint.position;

            if (Vector2.Distance(currentPos, targetPos) < 0.05f)
            {
                _currentIndex = (_currentIndex + 1) % points.Length;
                targetPoint = points[_currentIndex];
                if (targetPoint == null)
                {
                    Velocity = Vector2.zero;
                    return;
                }
                targetPos = targetPoint.position;
            }

            Vector2 nextPos = Vector2.MoveTowards(currentPos, targetPos, speed * Time.fixedDeltaTime);
            Velocity = (nextPos - currentPos) / Time.fixedDeltaTime;

            if (_rb != null)
            {
                _rb.MovePosition(nextPos);
            }
            else
            {
                transform.position = nextPos;
            }
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (collision.gameObject.CompareTag("Player") && collision.transform.parent == transform)
            {
                collision.transform.SetParent(null);
            }
        }

        private void OnCollisionExit2D(Collision2D collision)
        {
            if (collision.gameObject.CompareTag("Player") && collision.transform.parent == transform)
            {
                collision.transform.SetParent(null);
            }
        }
    }
}
