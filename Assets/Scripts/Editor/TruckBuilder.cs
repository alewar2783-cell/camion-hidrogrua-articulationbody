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
            Remove(TruckLoadsFactory.LoadsRootName);

            TruckLoadsFactory.CreateGround();
            (PhysicsMaterial grip, PhysicsMaterial steel) = TruckLoadsFactory.CreateMaterials();
            TruckLoadsFactory.CreateLoads(grip);

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

            if (mode == BuildMode.Full)
            {
                WireControls(root, nodes);
            }

            AssetDatabase.SaveAssets();
            root.SetActive(true);
            // Set after activation: the flag is ignored while the articulation does not exist yet.
            rootBody.immovable = true;
            return root;
        }

        // The first body of a control is the primary one; a second body is driven in mirror
        // (the gripper fingers move in opposite directions).
        private static void WireControls(GameObject root, Dictionary<BodyId, Transform> nodes)
        {
            var axes = new JointAxis[TruckSpec.Controls.Length];
            foreach (ControlSpec control in TruckSpec.Controls)
            {
                ArticulationBody primary = null;
                ArticulationBody mirror = null;
                foreach (BodySpec spec in TruckSpec.Bodies)
                {
                    if (spec.Control != control.Id)
                    {
                        continue;
                    }

                    var body = nodes[spec.Id].GetComponent<ArticulationBody>();
                    if (primary == null)
                    {
                        primary = body;
                    }
                    else
                    {
                        mirror = body;
                    }
                }

                JointAxis axis = root.AddComponent<JointAxis>();
                axis.Initialize(control, primary, mirror);
                axes[(int)control.Id] = axis;
            }

            root.AddComponent<SimulatorController>().Initialize(axes);
            root.AddComponent<SceneResetter>();
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
