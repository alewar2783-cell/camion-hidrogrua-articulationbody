using System;
using UnityEditor;
using UnityEngine;

namespace Truck
{
    /// <summary>
    /// Persists the truck's materials as assets so scene references survive a save and reload.
    /// An existing asset is reused and its values are re-applied from the spec.
    /// </summary>
    public static class TruckAssetStore
    {
        public const string PhysicsMaterialFolder = "Assets/PhysicsMaterials";
        public const string MaterialFolder = "Assets/Materials";

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

        public static Material LoadOrCreateVisualMaterial(Color color)
        {
            string path = MaterialFolder + "/Mat_Visual_" + ColorUtility.ToHtmlStringRGB(color)
                + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                {
                    throw new InvalidOperationException(
                        "Shader 'Universal Render Pipeline/Lit' not found; check that URP is installed.");
                }

                EnsureFolder(MaterialFolder);
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            if (material.color != color)
            {
                material.color = color;
                EditorUtility.SetDirty(material);
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
