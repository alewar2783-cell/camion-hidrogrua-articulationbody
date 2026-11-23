using UnityEngine;
using UnityEngine.Rendering;

namespace Truck
{
    /// <summary>
    /// Scene-wide look: the gradient sky, a flat ambient colour and the sun colour. It changes
    /// only rendering settings, never the simulation.
    /// </summary>
    public static class TruckSceneStyle
    {
        public const float SunIntensity = 1f;
        public const float SunShadowStrength = 1f;

        public static void Apply()
        {
            RenderSettings.skybox = TruckAssetStore.LoadOrCreateSkyMaterial();
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = TruckPalette.Shade;

            foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type != LightType.Directional)
                {
                    continue;
                }

                light.useColorTemperature = false;
                light.color = TruckPalette.Sunlight;
                light.intensity = SunIntensity;
                light.shadows = LightShadows.Hard;
                light.shadowStrength = SunShadowStrength;
            }
        }
    }
}
