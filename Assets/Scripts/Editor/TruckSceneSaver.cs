using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Truck
{
    /// <summary>Builds the truck into Main and saves it, so the player needs no setup.</summary>
    public static class TruckSceneSaver
    {
        public const string MainScenePath = "Assets/Scenes/Main.unity";

        [MenuItem("Tools/Truck/Build And Save Main Scene")]
        public static void BuildAndSave()
        {
            if (Application.isPlaying)
            {
                throw new InvalidOperationException("Stop Play mode before saving the scene.");
            }

            Scene scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
            GameObject root = TruckBuilder.Build(BuildMode.Full);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new InvalidOperationException("Could not save " + MainScenePath);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("Saved " + MainScenePath + " with " + root.name);
        }
    }
}
