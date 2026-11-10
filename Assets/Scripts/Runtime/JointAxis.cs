using UnityEngine;

namespace Truck
{
    public sealed class JointAxis : MonoBehaviour
    {
        public const float MaxDeltaTime = 0.05f;

        private ControlSpec _spec;
        private ArticulationBody _primary;
        private ArticulationBody _mirror;
        private float _target;

        public ControlId Control => _spec.Id;

        public ControlSpec Spec => _spec;

        /// <summary>Commanded position: degrees for angular axes, meters otherwise.</summary>
        public float Target => _target;

        /// <summary>Measured position of the primary body, in the same unit as Target.</summary>
        public float Real
        {
            get
            {
                if (_primary == null)
                {
                    return 0f;
                }

                return ToDisplay(_spec.IsAngular, _primary.jointPosition[0]);
            }
        }

        public void Initialize(ControlSpec spec, ArticulationBody primary,
            ArticulationBody mirror = null)
        {
            _spec = spec;
            _primary = primary;
            _mirror = mirror;
            _target = 0f;
        }

        public void SetTarget(float value)
        {
            _target = ClampTarget(value, _spec.Min, _spec.Max);
        }

        public void ResetTarget()
        {
            _target = 0f;
        }

        public static float ClampTarget(float value, float min, float max)
        {
            return Mathf.Clamp(value, min, max);
        }

        public static float Step(float target, float input, float speed, float deltaTime,
            float min, float max)
        {
            float dt = Mathf.Min(deltaTime, MaxDeltaTime);
            return ClampTarget(target + input * speed * dt, min, max);
        }

        /// <summary>Revolute jointPosition is in radians; drive targets are degrees.</summary>
        public static float ToDisplay(bool isAngular, float jointPosition)
        {
            return isAngular ? jointPosition * Mathf.Rad2Deg : jointPosition;
        }

        private void FixedUpdate()
        {
            ApplyTarget(_primary, _target);
            ApplyTarget(_mirror, -_target);
        }

        private static void ApplyTarget(ArticulationBody body, float value)
        {
            if (body == null)
            {
                return;
            }

            ArticulationDrive drive = body.xDrive;
            drive.target = value;
            body.xDrive = drive;
        }
    }
}
