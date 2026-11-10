using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Truck
{
    public static class TruckUiFactory
    {
        public const string RootName = "Interface";
        public const string ResetButtonLabel = "Reiniciar Escena";

        private static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);
        private static readonly Color PanelColor = new Color(0.05f, 0.07f, 0.1f, 0.78f);
        private static readonly Color TextColor = new Color(0.95f, 0.95f, 0.95f);
        private static readonly Color ButtonColor = new Color(0.8f, 0.3f, 0.15f);

        private const float ScreenMatch = 0.5f;
        private const int PanelWidth = 560;
        private const int PanelPadding = 20;
        private const float PanelSpacing = 10f;
        private const float RowSpacing = 4f;
        private const float RowLabelHeight = 34f;
        private const float SliderHeight = 26f;
        private const float ButtonHeight = 56f;
        private const float LabelFontSize = 24f;
        private const float ButtonFontSize = 26f;
        private const float LegendFontSize = 20f;
        private const string LegendTitle = "Controles (- / +)";
        private const string LegendFooter =
            "Reiniciar: " + SceneResetter.KeysLabel
            + "\nCámara: clic derecho + arrastrar, rueda para zoom";

        private const string BackgroundSprite = "UI/Skin/Background.psd";
        private const string SliderSprite = "UI/Skin/UISprite.psd";
        private const string KnobSprite = "UI/Skin/Knob.psd";

        /// <summary>Creates the Canvas, the event system and the panel bound to the axes.</summary>
        public static GameObject Create(JointAxis[] axes, SceneResetter resetter)
        {
            var root = new GameObject(RootName);
            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                CreateEventSystem(root.transform);
            }

            var canvasObject = new GameObject("Canvas");
            canvasObject.transform.SetParent(root.transform, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = ScreenMatch;
            canvasObject.AddComponent<GraphicRaycaster>();

            RectTransform panel = CreatePanel(canvasObject.transform);
            foreach (JointAxis axis in axes)
            {
                CreateRow(panel, axis);
            }

            CreateResetButton(panel, resetter);
            CreateLegend(panel);
            return root;
        }

        private static void CreateEventSystem(Transform parent)
        {
            var eventSystem = new GameObject("EventSystem");
            eventSystem.transform.SetParent(parent, false);
            eventSystem.AddComponent<EventSystem>();
            var module = eventSystem.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
        }

        private static RectTransform CreatePanel(Transform canvas)
        {
            var panelObject = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panelObject.transform.SetParent(canvas, false);
            panelObject.GetComponent<Image>().color = PanelColor;

            var rect = (RectTransform)panelObject.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = new Vector2(PanelWidth, 0f);
            rect.anchoredPosition = Vector2.zero;

            var layout = panelObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(PanelPadding, PanelPadding, PanelPadding, PanelPadding);
            layout.spacing = PanelSpacing;
            layout.childAlignment = TextAnchor.UpperLeft;
            ConfigureLayout(layout);
            return rect;
        }

        private static void ConfigureLayout(VerticalLayoutGroup layout)
        {
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
        }

        private static void CreateRow(RectTransform panel, JointAxis axis)
        {
            var row = new GameObject("Row_" + axis.Control, typeof(RectTransform));
            row.transform.SetParent(panel, false);
            var layout = row.AddComponent<VerticalLayoutGroup>();
            layout.spacing = RowSpacing;
            ConfigureLayout(layout);

            TMP_Text label = CreateText("Label", row.transform, LabelFontSize, RowLabelHeight);
            Slider slider = CreateSlider(row.transform);
            slider.gameObject.AddComponent<TelemetryUI>().Bind(axis, slider, label);
        }

        private static Slider CreateSlider(Transform parent)
        {
            var resources = new DefaultControls.Resources
            {
                standard = AssetDatabase.GetBuiltinExtraResource<Sprite>(SliderSprite),
                background = AssetDatabase.GetBuiltinExtraResource<Sprite>(BackgroundSprite),
                knob = AssetDatabase.GetBuiltinExtraResource<Sprite>(KnobSprite)
            };
            GameObject sliderObject = DefaultControls.CreateSlider(resources);
            sliderObject.name = "Slider";
            sliderObject.transform.SetParent(parent, false);
            var element = sliderObject.AddComponent<LayoutElement>();
            element.minHeight = SliderHeight;
            element.preferredHeight = SliderHeight;

            // Navigation is off and the selection is dropped on release (see TelemetryUI) so
            // that the arrow keys, which also drive the bed, are never consumed by the UI.
            var slider = sliderObject.GetComponent<Slider>();
            slider.navigation = new Navigation { mode = Navigation.Mode.None };
            return slider;
        }

        private static void CreateResetButton(RectTransform panel, SceneResetter resetter)
        {
            var buttonObject = new GameObject("ResetButton", typeof(RectTransform), typeof(Image),
                typeof(Button));
            buttonObject.transform.SetParent(panel, false);
            var image = buttonObject.GetComponent<Image>();
            image.color = ButtonColor;
            var element = buttonObject.AddComponent<LayoutElement>();
            element.minHeight = ButtonHeight;
            element.preferredHeight = ButtonHeight;

            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            UnityEventTools.AddPersistentListener(button.onClick, resetter.ReloadScene);

            TMP_Text text = CreateText("Text", buttonObject.transform, ButtonFontSize, 0f);
            text.text = ResetButtonLabel;
            text.alignment = TextAlignmentOptions.Center;
            var rect = (RectTransform)text.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void CreateLegend(RectTransform panel)
        {
            var builder = new StringBuilder(LegendTitle);
            foreach (ControlSpec control in TruckSpec.Controls)
            {
                builder.Append('\n').Append(control.Label).Append(": ")
                    .Append(KeyNames(control.DecreaseKeys)).Append(" / ")
                    .Append(KeyNames(control.IncreaseKeys));
            }

            builder.Append('\n').Append(LegendFooter);
            TMP_Text legend = CreateText("Legend", panel, LegendFontSize, 0f);
            legend.text = builder.ToString();
            legend.textWrappingMode = TextWrappingModes.Normal;
        }

        private static string KeyNames(Key[] keys)
        {
            var builder = new StringBuilder();
            for (int i = 0; i < keys.Length; i++)
            {
                builder.Append(i > 0 ? " o " : string.Empty).Append(KeyName(keys[i]));
            }

            return builder.ToString();
        }

        private static string KeyName(Key key)
        {
            switch (key)
            {
                case Key.UpArrow:
                    return "Arriba";
                case Key.DownArrow:
                    return "Abajo";
                default:
                    return key.ToString();
            }
        }

        private static TMP_Text CreateText(string name, Transform parent, float fontSize,
            float height)
        {
            var textObject = new GameObject(name, typeof(RectTransform));
            textObject.transform.SetParent(parent, false);
            var text = textObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.color = TextColor;
            text.raycastTarget = false;
            if (height > 0f)
            {
                var element = textObject.AddComponent<LayoutElement>();
                element.minHeight = height;
                element.preferredHeight = height;
            }

            return text;
        }
    }
}
