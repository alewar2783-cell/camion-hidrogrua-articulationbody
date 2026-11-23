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
    /// <summary>
    /// Builds the control panel with the industrial look: dark steel panel, safety-yellow edge
    /// and accents, hazard-stripe header and divider, and uppercase section labels.
    /// </summary>
    public static class TruckUiFactory
    {
        public const string RootName = "Interface";
        public const string ResetButtonLabel = "Reiniciar Escena";

        private static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

        private const float ScreenMatch = 0.5f;
        private const int PanelWidth = 640;
        private const int PanelPadding = 20;
        private const float PanelSpacing = 8f;
        private const float EdgeWidth = 6f;
        private const int RowPaddingX = 8;
        private const int RowPaddingY = 6;
        private const float RowSpacing = 2f;
        private const float RowLabelHeight = 32f;
        private const float SliderHeight = 28f;
        private const float ButtonHeight = 56f;
        private const float StripeHeaderHeight = 16f;
        private const float StripeDividerHeight = 8f;
        // One hazard texture tile spans this many reference pixels.
        private const float StripeTilePixels = 32f;
        private const float TitleHeight = 40f;
        private const float SectionHeight = 26f;
        private const float TitleFontSize = 34f;
        private const float SectionFontSize = 22f;
        private const float SectionCharacterSpacing = 12f;
        private const float LabelFontSize = 26f;
        private const float ButtonFontSize = 28f;
        private const float LegendFontSize = 23f;
        private const string TitleText = "PANEL DE MANDO";
        private const string AxesSection = "MANDOS";
        private const string KeyboardSection = "TECLADO";
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

            Texture2D hazard = TruckAssetStore.LoadOrCreateHazardTexture();
            RectTransform panel = CreatePanel(canvasObject.transform);
            CreateStripe("HazardHeader", panel, hazard, StripeHeaderHeight);
            CreateHeading("Title", panel, TitleText, TitleFontSize, TitleHeight,
                TruckPalette.SafetyYellow, 0f);
            CreateHeading("Section_Mandos", panel, AxesSection, SectionFontSize, SectionHeight,
                TruckPalette.PanelMutedText, SectionCharacterSpacing);
            foreach (JointAxis axis in axes)
            {
                CreateRow(panel, axis);
            }

            CreateResetButton(panel, resetter);
            CreateStripe("HazardDivider", panel, hazard, StripeDividerHeight);
            CreateHeading("Section_Teclado", panel, KeyboardSection, SectionFontSize,
                SectionHeight, TruckPalette.PanelMutedText, SectionCharacterSpacing);
            CreateLegend(panel);
            CreateEdge(panel);
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
            panelObject.GetComponent<Image>().color = TruckPalette.PanelSteel;

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

        // A safety-yellow strip along the right edge of the panel, outside the layout.
        private static void CreateEdge(RectTransform panel)
        {
            var edge = new GameObject("Edge", typeof(RectTransform), typeof(Image));
            edge.transform.SetParent(panel, false);
            var image = edge.GetComponent<Image>();
            image.color = TruckPalette.SafetyYellow;
            image.raycastTarget = false;
            edge.AddComponent<LayoutElement>().ignoreLayout = true;
            var rect = (RectTransform)edge.transform;
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(1f, 0.5f);
            rect.sizeDelta = new Vector2(EdgeWidth, 0f);
            rect.anchoredPosition = Vector2.zero;
        }

        // The hazard texture repeats, so the UV rect sets how many tiles fit in the bar.
        private static void CreateStripe(string name, RectTransform panel, Texture2D hazard,
            float height)
        {
            var stripe = new GameObject(name, typeof(RectTransform), typeof(RawImage));
            stripe.transform.SetParent(panel, false);
            var image = stripe.GetComponent<RawImage>();
            image.texture = hazard;
            image.raycastTarget = false;
            float width = PanelWidth - PanelPadding * 2f;
            image.uvRect = new Rect(0f, 0f, width / StripeTilePixels, height / StripeTilePixels);
            var element = stripe.AddComponent<LayoutElement>();
            element.minHeight = height;
            element.preferredHeight = height;
        }

        private static void CreateHeading(string name, RectTransform panel, string text,
            float fontSize, float height, Color color, float characterSpacing)
        {
            TMP_Text heading = CreateText(name, panel, fontSize, height);
            heading.text = text;
            heading.color = color;
            heading.fontStyle = FontStyles.Bold;
            heading.characterSpacing = characterSpacing;
            heading.alignment = TextAlignmentOptions.BottomLeft;
        }

        private static void ConfigureLayout(VerticalLayoutGroup layout)
        {
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
        }

        // The telemetry label must stay the first text of the row and the slider a direct
        // child of it: the tests and TelemetryUI find them that way.
        private static void CreateRow(RectTransform panel, JointAxis axis)
        {
            var row = new GameObject("Row_" + axis.Control, typeof(RectTransform), typeof(Image));
            row.transform.SetParent(panel, false);
            var background = row.GetComponent<Image>();
            background.color = TruckPalette.RowSteel;
            background.raycastTarget = false;
            var layout = row.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(RowPaddingX, RowPaddingX, RowPaddingY, RowPaddingY);
            layout.spacing = RowSpacing;
            ConfigureLayout(layout);

            TMP_Text label = CreateText("Label", row.transform, LabelFontSize, RowLabelHeight);
            // One line always: at the extreme values the text may run into the padding, but a
            // wrapped second line would be clipped by the row height.
            label.textWrappingMode = TextWrappingModes.NoWrap;
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
            PaintSlider(slider);
            return slider;
        }

        private static void PaintSlider(Slider slider)
        {
            slider.transform.Find("Background").GetComponent<Image>().color =
                TruckPalette.SliderTrack;
            slider.fillRect.GetComponent<Image>().color = TruckPalette.SafetyYellow;
            slider.handleRect.GetComponent<Image>().color = TruckPalette.SliderHandle;
            ColorBlock colors = slider.colors;
            colors.normalColor = Color.white;
            colors.selectedColor = Color.white;
            colors.highlightedColor = TruckPalette.SafetyYellow;
            colors.pressedColor = TruckPalette.SafetyOrange;
            slider.colors = colors;
        }

        private static void CreateResetButton(RectTransform panel, SceneResetter resetter)
        {
            var buttonObject = new GameObject("ResetButton", typeof(RectTransform), typeof(Image),
                typeof(Button));
            buttonObject.transform.SetParent(panel, false);
            var image = buttonObject.GetComponent<Image>();
            image.color = TruckPalette.SafetyOrange;
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
            // Drawn in capitals without changing the label text that the tests compare.
            text.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
            text.color = TruckPalette.HazardBlack;
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
            legend.color = TruckPalette.PanelMutedText;
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
            text.color = TruckPalette.PanelText;
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
