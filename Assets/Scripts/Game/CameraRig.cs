using UnityEngine;

namespace ShapeShifter.Game
{
    /// <summary>Keeps the runner at the left quarter of a 432x240-pixel view and adds screen shake.</summary>
    public sealed class CameraRig : MonoBehaviour
    {
        /// <summary>Half the view height in units: 240 pixels at 16 pixels per unit.</summary>
        public const float HalfHeight = 7.5f;
        public const float HalfWidth = HalfHeight * 16f / 9f;
        /// <summary>How far ahead of the runner the camera centre sits.</summary>
        public const float Lead = 7f;
        public const float CenterY = 3.4f;

        float trauma;

        public bool ShakeEnabled { get; set; } = true;
        public float FocusX { get; private set; }

        public void Follow(float runnerX)
        {
            FocusX = runnerX + Lead;
        }

        public void Shake(float amount)
        {
            if (ShakeEnabled) trauma = Mathf.Min(1f, trauma + amount);
        }

        void LateUpdate()
        {
            trauma = Mathf.Max(0f, trauma - Time.unscaledDeltaTime * 2.2f);
            float s = trauma * trauma * 0.35f;
            float t = Time.unscaledTime * 40f;
            float x = FocusX + s * (Mathf.PerlinNoise(t, 0.3f) - 0.5f) * 2f;
            float y = CenterY + s * (Mathf.PerlinNoise(0.7f, t) - 0.5f) * 2f;
            // Snap to the pixel grid so the pixel art stays crisp.
            x = Mathf.Round(x * 16f) / 16f;
            y = Mathf.Round(y * 16f) / 16f;
            transform.position = new Vector3(x, y, -10f);
        }
    }
}
