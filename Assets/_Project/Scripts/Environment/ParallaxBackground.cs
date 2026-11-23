using UnityEngine;

namespace Stitches.Environment
{
    public class ParallaxBackground : MonoBehaviour
    {
        [Header("Camera")]
        [Tooltip("Camera for the background to follow. If empty, Camera.main is used.")]
        public Transform cameraTransform;

        [Header("Horizontal Follow (X)")]
        [Tooltip("1 = locked to camera; 0.8-0.9 = subtle parallax depth.")]
        [Range(0f, 1f)]
        public float parallaxEffectX = 1f;

        [Header("Vertical Follow (Y)")]
        public bool followY = true;
        [Range(0f, 1f)]
        public float parallaxEffectY = 1f;

        private float _startPosX;
        private float _startPosY;
        private float _startCameraX;
        private float _startCameraY;

        private void Start()
        {
            FindCamera();
        }

        private void FindCamera()
        {
            if (cameraTransform == null && Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }

            _startPosX = transform.position.x;
            _startPosY = transform.position.y;

            if (cameraTransform != null)
            {
                _startCameraX = cameraTransform.position.x;
                _startCameraY = cameraTransform.position.y;
            }
        }

        private void LateUpdate()
        {
            if (cameraTransform == null)
            {
                FindCamera();
                if (cameraTransform == null) return;
            }

            float distanceX = (cameraTransform.position.x - _startCameraX) * parallaxEffectX;
            float newPosX = _startPosX + distanceX;

            float newPosY = transform.position.y;
            if (followY)
            {
                float distanceY = (cameraTransform.position.y - _startCameraY) * parallaxEffectY;
                newPosY = _startPosY + distanceY;
            }

            transform.position = new Vector3(newPosX, newPosY, transform.position.z);
        }
    }
}
