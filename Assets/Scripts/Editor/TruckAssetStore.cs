using System;
using UnityEditor;
using UnityEngine;

namespace Truck
{
    /// <summary>
    /// Persists the truck's materials and textures as assets so scene references survive a save
    /// and reload. An existing asset is reused and its values are re-applied from the spec and
    /// from TruckPalette.
    /// </summary>
    public static class TruckAssetStore
    {
        public const string PhysicsMaterialFolder = "Assets/PhysicsMaterials";
        public const string MaterialFolder = "Assets/Materials";
        public const string TextureFolder = "Assets/Textures";
        public const string ToonShaderName = "Truck/IndustrialToon";
        public const string SkyShaderName = "Truck/GradientSky";

        // Toon tuning: four light bands, a two-pixel outline capped at 6 cm so thin parts such
        // as the telescopic rod are not swallowed when the camera zooms out.
        public const float ToonBands = 4f;
        public const float OutlinePixels = 2f;
        public const float OutlineMaxWidth = 0.06f;
        public const float StripesPerMetre = 2.5f;
        public const float SkyExponent = 0.6f;
        public const int HazardTextureSize = 32;

        private const string OutlineLightMode = "SRPDefaultUnlit";
        private const string SkyMaterialName = "Mat_Cielo";
        private const string HazardTextureName = "Tex_FranjasPeligro";

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ShadeColorId = Shader.PropertyToID("_ShadeColor");
        private static readonly int BandsId = Shader.PropertyToID("_Bands");
        private static readonly int StripeColorId = Shader.PropertyToID("_StripeColor");
        private static readonly int StripeMixId = Shader.PropertyToID("_StripeMix");
        private static readonly int StripeFrequencyId = Shader.PropertyToID("_StripeFrequency");
        private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
        private static readonly int OutlinePixelsId = Shader.PropertyToID("_OutlinePixels");
        private static readonly int OutlineMaxWidthId = Shader.PropertyToID("_OutlineMaxWidth");
        private static readonly int ZenithColorId = Shader.PropertyToID("_ZenithColor");
        private static readonly int HorizonColorId = Shader.PropertyToID("_HorizonColor");
        private static readonly int GroundColorId = Shader.PropertyToID("_GroundColor");
        private static readonly int SkyExponentId = Shader.PropertyToID("_Exponent");

        public static PhysicsMaterial LoadOrCreatePhysicsMaterial(string materialName,
            float dynamicFriction, float staticFriction, PhysicsMaterialCombine frictionCombine)
        {
            string path = PhysicsMaterialFolder + "/" + materialName + ".physicMaterial";
            var material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if (material == null)
            {
                EnsureFolder(PhysicsMaterialFolder);
                material = new PhysicsMaterial(materialName);
                AssetDatabase.CreateAsset(material, path);
            }

            material.dynamicFriction = dynamicFriction;
            material.staticFriction = staticFriction;
            material.bounciness = 0f;
            material.frictionCombine = frictionCombine;
            material.bounceCombine = PhysicsMaterialCombine.Minimum;
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// Loads or creates a material of the industrial toon shader and re-applies its values.
        /// Ground-like surfaces pass hasOutline false: their outline pass is disabled.
        /// </summary>
        public static Material LoadOrCreateToonMaterial(string materialName, Color baseColor,
            bool hasOutline, bool hasStripes = false)
        {
            Material material = LoadOrCreateMaterial(materialName, ToonShaderName);
            material.SetColor(BaseColorId, baseColor);
            material.SetColor(ShadeColorId, TruckPalette.Shade);
            material.SetFloat(BandsId, ToonBands);
            material.SetColor(StripeColorId, TruckPalette.HazardBlack);
            material.SetFloat(StripeMixId, hasStripes ? 1f : 0f);
            material.SetFloat(StripeFrequencyId, StripesPerMetre);
            material.SetColor(OutlineColorId, TruckPalette.Outline);
            material.SetFloat(OutlinePixelsId, OutlinePixels);
            material.SetFloat(OutlineMaxWidthId, OutlineMaxWidth);
            material.SetShaderPassEnabled(OutlineLightMode, hasOutline);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>Loads or creates the gradient skybox material.</summary>
        public static Material LoadOrCreateSkyMaterial()
        {
            Material material = LoadOrCreateMaterial(SkyMaterialName, SkyShaderName);
            material.SetColor(ZenithColorId, TruckPalette.SkyZenith);
            material.SetColor(HorizonColorId, TruckPalette.SkyHorizon);
            material.SetColor(GroundColorId, TruckPalette.SkyBelow);
            material.SetFloat(SkyExponentId, SkyExponent);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// Loads or creates the tileable yellow and black hazard texture used by the interface.
        /// The pixels are generated here, so the project needs no image file.
        /// </summary>
        public static Texture2D LoadOrCreateHazardTexture()
        {
            string path = TextureFolder + "/" + HazardTextureName + ".asset";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null)
            {
                EnsureFolder(TextureFolder);
                texture = new Texture2D(HazardTextureSize, HazardTextureSize,
                    TextureFormat.RGBA32, false);
                AssetDatabase.CreateAsset(texture, path);
            }

            var pixels = new Color32[HazardTextureSize * HazardTextureSize];
            int stripeWidth = HazardTextureSize / 2;
            for (int y = 0; y < HazardTextureSize; y++)
            {
                for (int x = 0; x < HazardTextureSize; x++)
                {
                    bool isYellow = (x + y) / stripeWidth % 2 == 0;
                    pixels[y * HazardTextureSize + x] =
                        isYellow ? TruckPalette.SafetyYellow : TruckPalette.HazardBlack;
                }
            }

            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Bilinear;
            texture.SetPixels32(pixels);
            texture.Apply();
            EditorUtility.SetDirty(texture);
            return texture;
        }

        private static Material LoadOrCreateMaterial(string materialName, string shaderName)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                throw new InvalidOperationException("Shader '" + shaderName
                    + "' not found; check Assets/Shaders.");
            }

            string path = MaterialFolder + "/" + materialName + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                EnsureFolder(MaterialFolder);
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            return material;
        }

        private static void EnsureFolder(string folder)
        {
            if (!AssetDatabase.IsValidFolder(folder))
            {
                AssetDatabase.CreateFolder("Assets", folder.Substring("Assets/".Length));
            }
        }
    }
}
