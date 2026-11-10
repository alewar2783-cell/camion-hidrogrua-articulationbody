using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Truck
{
    public sealed class SimulatorController : MonoBehaviour
    {
        [SerializeField] private JointAxis[] _axes = new JointAxis[0];

        private bool _wasFocused = true;

        public JointAxis[] Axes => _axes;

        public void Initialize(JointAxis[] axes)
        {
            _axes = axes;
        }

        /// <summary>Returns null when the id has no axis, instead of throwing.</summary>
        public JointAxis GetAxis(ControlId id)
        {
            int index = (int)id;
            if (_axes == null || index < 0 || index >= _axes.Length)
            {
                return null;
            }

            return _axes[index];
        }

        /// <summary>Returns -1, 0 or 1; opposite keys held together cancel out.</summary>
        public static float ReadInput(bool decrease, bool increase)
        {
            return (increase ? 1f : 0f) - (decrease ? 1f : 0f);
        }

        /// <summary>
        /// A window without focus (for example a WebGL tab in the background) can miss the key
        /// release, so held keys are ignored until focus returns.
        /// </summary>
        public static bool ShouldProcessInput(bool isFocused)
        {
            return isFocused;
        }

        /// <summary>The UI selection is dropped once, at the moment focus is lost.</summary>
        public static bool ShouldClearSelection(bool wasFocused, bool isFocused)
        {
            return wasFocused && !isFocused;
        }

        private void Update()
        {
            bool isFocused = Application.isFocused;
            if (ShouldClearSelection(_wasFocused, isFocused) && EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }

            _wasFocused = isFocused;
            if (!ShouldProcessInput(isFocused))
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || _axes == null)
            {
                return;
            }

            foreach (JointAxis axis in _axes)
            {
                if (axis == null)
                {
                    continue;
                }

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
