using UnityEngine;

namespace Truck
{
    public sealed class JointAxis : MonoBehaviour
    {
        public const float MaxDeltaTime = 0.05f;

        // Only the id is serialized; the spec is always read from TruckSpec so that a saved
        // scene keeps working.
        [SerializeField] private ControlId _control;
        [SerializeField] private ArticulationBody _primary;
        [SerializeField] private ArticulationBody _mirror;
        [SerializeField] private float _target;
        [SerializeField] private float _defaultTarget;

        public ControlId Control => _control;

        public ControlSpec Spec => TruckSpec.Get(_control);

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

                return ToDisplay(Spec.IsAngular, _primary.jointPosition[0]);
            }
        }

        /// <summary>Target the axis starts with and returns to on ResetTarget.</summary>
        public float DefaultTarget => _defaultTarget;

        public void Initialize(ControlSpec spec, ArticulationBody primary,
            ArticulationBody mirror = null, float defaultTarget = 0f)
        {
            _control = spec.Id;
            _primary = primary;
            _mirror = mirror;
            _defaultTarget = ClampTarget(defaultTarget, spec.Min, spec.Max);
            _target = _defaultTarget;
        }

        public void SetTarget(float value)
        {
            if (float.IsNaN(value))
            {
                return;
            }

            ControlSpec spec = Spec;
            _target = ClampTarget(value, spec.Min, spec.Max);
        }

        public void ResetTarget()
        {
            _target = _defaultTarget;
        }

        /// <summary>
        /// Teleports the joints to the current target. Only valid once the articulation exists
        /// (after its root is active); it avoids starting in a pose that collides.
        /// </summary>
        public void SnapToTarget()
        {
            SnapBody(_primary, _target);
            SnapBody(_mirror, -_target);
        }

        public static float ClampTarget(float value, float min, float max)
        {
            return Mathf.Clamp(value, min, max);
        }

        public static float Step(float target, float input, float speed, float deltaTime,
            float min, float max)
        {
            if (float.IsNaN(input) || float.IsNaN(speed) || float.IsNaN(deltaTime))
            {
                return target;
            }

            float dt = Mathf.Min(deltaTime, MaxDeltaTime);
            return ClampTarget(target + input * speed * dt, min, max);
        }

        /// <summary>Revolute jointPosition is in radians; drive targets are degrees.</summary>
        public static float ToDisplay(bool isAngular, float jointPosition)
        {
            return isAngular ? jointPosition * Mathf.Rad2Deg : jointPosition;
        }

        private void SnapBody(ArticulationBody body, float value)
        {
            if (body == null)
            {
                return;
            }

            float joint = Spec.IsAngular ? value * Mathf.Deg2Rad : value;
            body.jointPosition = new ArticulationReducedSpace(joint);
            body.jointVelocity = new ArticulationReducedSpace(0f);
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
