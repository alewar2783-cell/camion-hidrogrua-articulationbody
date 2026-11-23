// Copiar a Assets/Scripts/Editor/ExportUiRects.cs (quitar el sufijo .txt) para activarlo.
// Menu: Tools/Truck/Export UI Rects (solo en Play Mode). Escribe tools/video/ui_rects.json
// con los rectangulos en pixeles de la Game view, origen arriba a la izquierda.
// Formato documentado en tools/video/README.md.
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Truck
{
    public static class ExportUiRects
    {
        private const string PanelPath = "Interface/Canvas/Panel";

        [Serializable]
        private class RectData
        {
            public float x;
            public float y;
            public float width;
            public float height;
        }

        [Serializable]
        private class SliderData
        {
            public string name;       // ControlId: Torre, Pluma, BrazoSecundario, ...
            public RectData track;    // RectTransform del Slider (pista completa)
            public RectData handle;   // tirador en el momento de exportar
            public float min;
            public float max;
            public float value;
            public float value01;     // fraccion 0..1 del tirador sobre la pista
        }

        [Serializable]
        private class ScreenData
        {
            public int width;
            public int height;
        }

        [Serializable]
        private class RectsFile
        {
            public int version = 1;
            public ScreenData screen = new ScreenData();
            public RectData panel;
            public RectData reset_button;
            public List<SliderData> sliders = new List<SliderData>();
        }

        [MenuItem("Tools/Truck/Export UI Rects")]
        public static void Export()
        {
            GameObject panelObject = GameObject.Find(PanelPath);
            if (panelObject == null)
            {
                Debug.LogError("ExportUiRects: no se encontro " + PanelPath
                    + ". Entre en Play Mode con la escena del camion.");
                return;
            }

            var file = new RectsFile();
            file.screen.width = Screen.width;
            file.screen.height = Screen.height;
            file.panel = ToRect((RectTransform)panelObject.transform);

            Transform panel = panelObject.transform;
            Transform reset = panel.Find("ResetButton");
            if (reset != null)
            {
                file.reset_button = ToRect((RectTransform)reset);
            }

            foreach (Transform child in panel)
            {
                if (!child.name.StartsWith("Row_", StringComparison.Ordinal))
                {
                    continue;
                }

                var slider = child.GetComponentInChildren<Slider>();
                if (slider == null)
                {
                    continue;
                }

                file.sliders.Add(new SliderData
                {
                    name = child.name.Substring("Row_".Length),
                    track = ToRect((RectTransform)slider.transform),
                    handle = ToRect(slider.handleRect),
                    min = slider.minValue,
                    max = slider.maxValue,
                    value = slider.value,
                    value01 = slider.normalizedValue
                });
            }

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string path = Path.Combine(projectRoot, "tools", "video", "ui_rects.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(file, true));
            Debug.Log("ExportUiRects: " + file.sliders.Count + " sliders, pantalla "
                + Screen.width + "x" + Screen.height + " -> " + path);
        }

        [MenuItem("Tools/Truck/Export UI Rects", true)]
        private static bool Validate()
        {
            return Application.isPlaying;
        }

        // Canvas en Screen Space Overlay: las esquinas del mundo ya estan en pixeles de pantalla
        // con origen abajo a la izquierda; se pasa a origen arriba a la izquierda.
        private static RectData ToRect(RectTransform rt)
        {
            var c = new Vector3[4];
            rt.GetWorldCorners(c); // 0 abajo-izq, 1 arriba-izq, 2 arriba-der, 3 abajo-der
            float left = c[0].x;
            float right = c[2].x;
            float top = Screen.height - c[1].y;
            float bottom = Screen.height - c[0].y;
            return new RectData { x = left, y = top, width = right - left, height = bottom - top };
        }
    }
}
