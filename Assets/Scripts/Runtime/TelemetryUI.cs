using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Truck
{
    /// <summary>
    /// Two-way link between one axis, its slider and its Target/Real text. Lives on the slider
    /// object so that it also receives the pointer events of the slider.
    /// </summary>
    public sealed class TelemetryUI : MonoBehaviour, IPointerUpHandler
    {
        private const string AngularFormat = "F1";
        private const string LinearFormat = "F2";
        private const float AngularStep = 0.1f;
        private const float LinearStep = 0.01f;
        private const string DegreeSymbol = "°";
        private const string MeterSymbol = " m";

        [SerializeField] private JointAxis _axis;
        [SerializeField] private Slider _slider;
        [SerializeField] private TMP_Text _label;
        private int _shownTargetSteps;
        private int _shownRealSteps;
        private bool _hasText;

        public static string Format(string label, float target, float real, bool isAngular)
        {
            string format = isAngular ? AngularFormat : LinearFormat;
            string unit = isAngular ? DegreeSymbol : MeterSymbol;
            return label + " | Target: " + Round(target, isAngular).ToString(
                format, CultureInfo.InvariantCulture) + unit + " | Real: "
                + Round(real, isAngular).ToString(format, CultureInfo.InvariantCulture) + unit;
        }

        public void Bind(JointAxis axis, Slider slider, TMP_Text label)
        {
            Unhook();
            _axis = axis;
            _slider = slider;
            _label = label;
            _hasText = false;
            _slider.minValue = axis.Spec.Min;
            _slider.maxValue = axis.Spec.Max;
            _slider.SetValueWithoutNotify(axis.Target);
            Hook();
            Refresh();
        }

        // Releasing the slider drops the UI selection so the arrow keys keep driving the bed.
        public void OnPointerUp(PointerEventData eventData)
        {
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }

        // Runtime listeners are not serialized, so a saved scene re-links them on load.
        private void Awake()
        {
            Hook();
        }

        private void OnDestroy()
        {
            Unhook();
        }

        private void Hook()
        {
            if (_slider != null)
            {
                _slider.onValueChanged.RemoveListener(OnSliderChanged);
                _slider.onValueChanged.AddListener(OnSliderChanged);
            }
        }

        private void Unhook()
        {
            if (_slider != null)
            {
                _slider.onValueChanged.RemoveListener(OnSliderChanged);
            }
        }

        private void OnSliderChanged(float value)
        {
            _axis.SetTarget(value);
        }

        private void Update()
        {
            if (_axis == null)
            {
                return;
            }

            if (_slider.value != _axis.Target)
            {
                _slider.SetValueWithoutNotify(_axis.Target);
            }

            Refresh();
        }

        // Strings are built only when a displayed (rounded) value changes.
        private void Refresh()
        {
            bool isAngular = _axis.Spec.IsAngular;
            float step = isAngular ? AngularStep : LinearStep;
            int targetSteps = Mathf.RoundToInt(_axis.Target / step);
            int realSteps = Mathf.RoundToInt(_axis.Real / step);
            if (_hasText && targetSteps == _shownTargetSteps && realSteps == _shownRealSteps)
            {
                return;
            }

            _shownTargetSteps = targetSteps;
            _shownRealSteps = realSteps;
            _hasText = true;
            _label.text = Format(_axis.Spec.Label, _axis.Target, _axis.Real, isAngular);
        }

        // Rounds first and adds zero so that a tiny negative value shows as 0.0, not -0.0.
        private static float Round(float value, bool isAngular)
        {
            float step = isAngular ? AngularStep : LinearStep;
            return Mathf.Round(value / step) * step + 0f;
        }
    }
}
