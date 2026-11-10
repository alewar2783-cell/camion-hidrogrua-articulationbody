using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Truck
{
    public enum BuildMode
    {
        NoDrives,
        Full
    }

    public static class TruckBuilder
    {
        public const string RootName = "Truck";

        /// <summary>Destroys any previous truck and environment, then builds a new truck.</summary>
        public static GameObject Build(BuildMode mode)
        {
            Remove(RootName);
            Remove(TruckLoadsFactory.EnvironmentName);

            TruckLoadsFactory.CreateGround();
            (PhysicsMaterial grip, PhysicsMaterial steel) = TruckLoadsFactory.CreateMaterials();

            // The articulation is created in one go when the root is activated, so the solver
            // never sees a half-built hierarchy.
            var root = new GameObject(RootName);
            root.SetActive(false);

            var nodes = new Dictionary<BodyId, Transform>();
            ArticulationBody rootBody = null;
            foreach (BodySpec spec in TruckSpec.Bodies)
            {
                Transform parent = spec.Parent == null ? root.transform : nodes[spec.Parent.Value];
                ArticulationBody body =
                    TruckBodyFactory.CreateBody(spec, parent, mode, grip, steel);
                nodes[spec.Id] = body.transform;
                if (spec.Joint == JointKind.Root)
                {
                    rootBody = body;
                }
            }

            root.SetActive(true);
            // Set after activation: the flag is ignored while the articulation does not exist yet.
            rootBody.immovable = true;
            return root;
        }

        [MenuItem("Tools/Truck/Build Scene (No Drives)")]
        private static void BuildNoDrivesFromMenu()
        {
            BuildFromMenu(BuildMode.NoDrives);
        }

        [MenuItem("Tools/Truck/Build Scene (Full)")]
        private static void BuildFullFromMenu()
        {
            BuildFromMenu(BuildMode.Full);
        }

        private static void BuildFromMenu(BuildMode mode)
        {
            GameObject root = Build(mode);
            Selection.activeGameObject = root;
            if (!Application.isPlaying)
            {
                EditorSceneManager.MarkSceneDirty(root.scene);
            }
        }

        private static void Remove(string objectName)
        {
            GameObject existing;
            while ((existing = GameObject.Find(objectName)) != null)
            {
                if (Application.isPlaying)
                {
                    // Destroy is deferred to the end of the frame; deactivating first removes the
                    // old bodies from the simulation and from name lookups immediately.
                    existing.SetActive(false);
                    Object.Destroy(existing);
                }
                else
                {
                    Object.DestroyImmediate(existing);
                }
            }
        }
    }
}
