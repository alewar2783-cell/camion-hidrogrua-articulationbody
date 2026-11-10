using UnityEngine;
using UnityEngine.InputSystem;

namespace Truck
{
    public sealed class SimulatorController : MonoBehaviour
    {
        [SerializeField] private JointAxis[] _axes = new JointAxis[0];

        public JointAxis[] Axes => _axes;

        public void Initialize(JointAxis[] axes)
        {
            _axes = axes;
        }

        public JointAxis GetAxis(ControlId id)
        {
            return _axes[(int)id];
        }

        /// <summary>Returns -1, 0 or 1; opposite keys held together cancel out.</summary>
        public static float ReadInput(bool decrease, bool increase)
        {
            return (increase ? 1f : 0f) - (decrease ? 1f : 0f);
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            foreach (JointAxis axis in _axes)
            {
                ControlSpec spec = axis.Spec;
                float input = ReadInput(IsAnyPressed(keyboard, spec.DecreaseKeys),
                    IsAnyPressed(keyboard, spec.IncreaseKeys));
                if (input != 0f)
                {
                    axis.SetTarget(JointAxis.Step(axis.Target, input, spec.Speed,
                        Time.deltaTime, spec.Min, spec.Max));
                }
            }
        }

        private static bool IsAnyPressed(Keyboard keyboard, Key[] keys)
        {
            foreach (Key key in keys)
            {
                if (keyboard[key].isPressed)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
