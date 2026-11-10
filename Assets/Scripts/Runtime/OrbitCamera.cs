using UnityEngine;
using UnityEngine.InputSystem;

namespace Truck
{
    /// <summary>Right mouse button + drag orbits around a point; the wheel zooms.</summary>
    public sealed class OrbitCamera : MonoBehaviour
    {
        public const float MinPitch = -10f;
        public const float MaxPitch = 85f;
        public const float MinDistance = 3f;
        public const float MaxDistance = 25f;

        private const float DegreesPerPixel = 0.25f;
        private const float ZoomFraction = 0.1f;

        // Serialized so that a framing set at build time in the Editor survives into Play.
        [SerializeField] private Vector3 _focus;
        [SerializeField] private float _yaw;
        [SerializeField] private float _pitch;
        [SerializeField] private float _distance = MinDistance;

        public static float ClampPitch(float degrees)
        {
            return Mathf.Clamp(degrees, MinPitch, MaxPitch);
        }

        public static float ClampDistance(float distance)
        {
            return Mathf.Clamp(distance, MinDistance, MaxDistance);
        }

        public void Frame(Vector3 focus, float yaw, float pitch, float distance)
        {
            _focus = focus;
            _yaw = yaw;
            _pitch = ClampPitch(pitch);
            _distance = ClampDistance(distance);
            Apply();
        }

        private void Update()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            if (mouse.rightButton.isPressed)
            {
                Vector2 delta = mouse.delta.ReadValue();
                _yaw += delta.x * DegreesPerPixel;
                _pitch = ClampPitch(_pitch - delta.y * DegreesPerPixel);
            }

            float scroll = mouse.scroll.ReadValue().y;
            if (scroll != 0f)
            {
                _distance = ClampDistance(_distance * (1f - Mathf.Sign(scroll) * ZoomFraction));
            }
        }

        private void LateUpdate()
        {
            Apply();
        }

        private void Apply()
        {
            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            transform.SetPositionAndRotation(
                _focus + rotation * (Vector3.back * _distance), rotation);
        }
    }
}
