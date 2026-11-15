using UnityEngine;

namespace TekkenNetcodeDemo
{
    /// <summary>
    /// Dynamic 2.5D Framing Camera:
    /// - Tracks the center of all active fighters on stage (supports 1, 2, 3, or 4 players).
    /// - Automatically zooms in and out based on the widest distance between fighters.
    /// </summary>
    public class TekkenCamera : MonoBehaviour
    {
        [Header("Framing Settings")]
        [SerializeField] private float heightOffset = 1.8f;
        [SerializeField] private float baseDistance = 6.0f;
        [SerializeField] private float zoomMultiplier = 0.6f;
        [SerializeField] private float minDistance = 5.0f;
        [SerializeField] private float maxDistance = 14.0f;
        [SerializeField] private float smoothTime = 0.15f;

        private Vector3 currentVelocity;

        private void LateUpdate()
        {
            var fighters = FindObjectsByType<DummyFighterController>(FindObjectsInactive.Exclude);
            if (fighters.Length == 0) return;

            Vector3 center = Vector3.zero;
            float minX = float.MaxValue;
            float maxX = float.MinValue;

            for (int i = 0; i < fighters.Length; i++)
            {
                Vector3 pos = fighters[i].transform.position;
                center += pos;
                if (pos.x < minX) minX = pos.x;
                if (pos.x > maxX) maxX = pos.x;
            }

            center /= fighters.Length;
            float spread = (fighters.Length > 1) ? (maxX - minX) : 0f;

            float requiredZ = -Mathf.Clamp(baseDistance + (spread * zoomMultiplier), minDistance, maxDistance);
            Vector3 targetPosition = new Vector3(center.x, center.y + heightOffset, requiredZ);

            transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref currentVelocity, smoothTime);
            transform.LookAt(new Vector3(targetPosition.x, targetPosition.y - 0.5f, 0));
        }
    }
}
