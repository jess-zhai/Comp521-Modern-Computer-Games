using UnityEngine;

namespace KalponicGames
{
    /// <summary>
    /// Orbits the camera smoothly around a target GameObject for preview/demonstration scenes.
    /// </summary>
    public class SimpleCameraOrbit : MonoBehaviour
    {
        [Header("Orbit Target")]
        public Transform Target;         // The object to orbit around

        [Header("Orbit Settings")]
        public float Distance = 10f;     // How far from the target
        public float Height = 3f;        // Height above the target
        public float Speed = 15f;        // Degrees per second

        private float angle;             // Current orbit angle

        void LateUpdate()
        {
            if (Target == null)
                return;

            angle += Speed * Time.deltaTime;
            float rad = angle * Mathf.Deg2Rad;

            // Calculate orbit position
            Vector3 offset = new Vector3(Mathf.Sin(rad), 0, Mathf.Cos(rad)) * Distance;
            offset.y = Height;

            transform.position = Target.position + offset;
            transform.LookAt(Target.position + Vector3.up * (Height * 0.25f)); // Slight downward tilt
        }
    }
}
