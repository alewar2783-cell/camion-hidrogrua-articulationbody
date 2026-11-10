using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

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
            AssertSingleRoots();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new InvalidOperationException("Could not save " + MainScenePath);
            }

            Debug.Log("Saved " + MainScenePath + " with " + root.name);
        }

        // Build removes the previous roots by name; a duplicate here would mean it did not.
        private static void AssertSingleRoots()
        {
            foreach (string rootName in new[]
            {
                TruckBuilder.RootName, TruckUiFactory.RootName, TruckLoadsFactory.LoadsRootName,
                TruckLoadsFactory.EnvironmentName, TruckBuilder.CameraName
            })
            {
                int count = 0;
                foreach (GameObject candidate in Object.FindObjectsByType<GameObject>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (candidate.name == rootName)
                    {
                        count++;
                    }
                }

                if (count != 1)
                {
                    throw new InvalidOperationException(rootName + " appears " + count
                        + " times in the scene");
                }
            }
        }
    }
}
