using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Truck.Tests
{
    /// <summary>Loads the scene saved on disk, the way the player build opens it.</summary>
    public class TruckSavedSceneTests
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";
        private const float SettleSeconds = 2f;
        private const float FramingTolerance = 0.01f;
        private const float MaxFramingAngleDegrees = 1f;
        private const float AngularToleranceDegrees = 3f;
        private const float LinearToleranceMeters = 0.03f;
        private const string GripMaterial = "Mat_GomaPinza";
        private const string SteelMaterial = "Mat_AceroCaja";

        [UnityTest]
        public IEnumerator MainScene_Loaded_HasTruckUiCameraAndLoads()
        {
            yield return LoadMain();

            GameObject root = GameObject.Find(TruckBuilder.RootName);
            Assert.IsNotNull(root, "truck root");
            ArticulationBody[] bodies = root.GetComponentsInChildren<ArticulationBody>();
            Assert.AreEqual(TruckSpec.Bodies.Length, bodies.Length, "articulation bodies");
            foreach (ArticulationBody body in bodies)
            {
                Assert.AreEqual(Vector3.one, body.transform.localScale, body.name);
            }

            AssertUi();
            AssertCamera();
            AssertLoads();
            AssertMaterials(root);
            AssertControllerWiring(root);
        }

        [UnityTest]
        public IEnumerator MainScene_Loaded_SnapsToStowedPose()
        {
            yield return LoadMain();
            yield return new WaitForSeconds(SettleSeconds);

            GameObject root = GameObject.Find(TruckBuilder.RootName);
            Assert.IsNotNull(root, "truck root");
            foreach (JointAxis axis in root.GetComponent<SimulatorController>().Axes)
            {
                float tolerance = axis.Spec.IsAngular ? AngularToleranceDegrees
                    : LinearToleranceMeters;
                Assert.AreEqual(TruckSpec.Poses.Stowed[(int)axis.Control], axis.Real, tolerance,
                    axis.Control.ToString());
            }
        }

        private static IEnumerator LoadMain()
        {
            yield return EditorSceneManager.LoadSceneInPlayMode(ScenePath,
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            yield return null;
        }

        private static void AssertUi()
        {
            Assert.AreEqual(1, Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Length,
                "canvas");
            Assert.AreEqual(1,
                Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length,
                "event system");
            GameObject ui = GameObject.Find(TruckUiFactory.RootName);
            Assert.IsNotNull(ui, "ui root");
            Assert.AreEqual(TruckSpec.Controls.Length, ui.GetComponentsInChildren<Slider>().Length,
                "sliders");
            Button[] buttons = ui.GetComponentsInChildren<Button>();
            Assert.AreEqual(1, buttons.Length, "buttons");
            Assert.AreEqual(1, buttons[0].onClick.GetPersistentEventCount());
            Assert.IsInstanceOf<SceneResetter>(buttons[0].onClick.GetPersistentTarget(0));
        }

        private static void AssertCamera()
        {
            Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            Assert.AreEqual(1, cameras.Length, "cameras");
            Transform camera = cameras[0].transform;
            Assert.IsNotNull(camera.GetComponent<OrbitCamera>(), "orbit camera");
            Vector3 toFocus = TruckBuilder.CameraFocus - camera.position;
            Assert.Less(Vector3.Angle(camera.forward, toFocus), MaxFramingAngleDegrees,
                "camera looks at the focus");
            Assert.AreEqual(TruckBuilder.CameraDistance, toFocus.magnitude, FramingTolerance,
                "camera distance");
            Assert.That(toFocus.magnitude,
                Is.InRange(OrbitCamera.MinDistance, OrbitCamera.MaxDistance));
        }

        private static void AssertLoads()
        {
            GameObject loads = GameObject.Find(TruckLoadsFactory.LoadsRootName);
            Assert.IsNotNull(loads, "loads root");
            Assert.AreEqual(TruckSpec.Loads.GroundSpawn.Length,
                loads.GetComponentsInChildren<Rigidbody>().Length, "loads");
            for (int i = 0; i < TruckSpec.Loads.GroundSpawn.Length; i++)
            {
                Transform load = loads.transform.Find(TruckLoadsFactory.LoadName(i));
                Assert.IsNotNull(load, TruckLoadsFactory.LoadName(i));
                var body = load.GetComponent<Rigidbody>();
                string label = load.name;
                Assert.AreEqual(TruckSpec.Loads.CubeMass, body.mass, label);
                Assert.AreEqual(CollisionDetectionMode.ContinuousDynamic,
                    body.collisionDetectionMode, label);
                Assert.AreEqual(RigidbodyInterpolation.Interpolate, body.interpolation, label);
                Assert.AreEqual(Vector3.one * TruckSpec.Loads.CubeSize, load.localScale, label);
                AssertPersistedMaterial(load.GetComponent<Collider>(), GripMaterial, label);
            }
        }

        private static void AssertMaterials(GameObject root)
        {
            foreach (string finger in new[] { "Visual_DedoIzquierdo", "Visual_DedoDerecho" })
            {
                Transform visual = FindDeep(root.transform, finger);
                Assert.IsNotNull(visual, finger);
                AssertPersistedMaterial(visual.GetComponent<Collider>(), GripMaterial, finger);
            }

            Transform floor = FindDeep(root.transform, "Visual_PisoCaja");
            Assert.IsNotNull(floor, "bed floor");
            AssertPersistedMaterial(floor.GetComponent<Collider>(), SteelMaterial, "bed floor");
        }

        private static void AssertPersistedMaterial(Collider collider, string expected,
            string label)
        {
            Assert.IsNotNull(collider, label + " collider");
            Assert.IsNotNull(collider.sharedMaterial, label + " material");
            Assert.AreEqual(expected, collider.sharedMaterial.name, label);
            Assert.IsFalse(
                string.IsNullOrEmpty(AssetDatabase.GetAssetPath(collider.sharedMaterial)),
                label + " material is an asset");
        }

        private static void AssertControllerWiring(GameObject root)
        {
            var controller = root.GetComponent<SimulatorController>();
            Assert.IsNotNull(controller, "controller");
            Assert.AreEqual(TruckSpec.Controls.Length, controller.Axes.Length, "axes");
            for (int i = 0; i < controller.Axes.Length; i++)
            {
                JointAxis axis = controller.Axes[i];
                Assert.IsNotNull(axis, "axis " + i);
                Assert.AreEqual((ControlId)i, axis.Control);
                Assert.AreEqual(TruckSpec.Poses.Stowed[i], axis.Target, axis.Control.ToString());
                Assert.AreEqual(TruckSpec.Poses.Stowed[i], axis.DefaultTarget,
                    axis.Control.ToString());
            }

            Assert.AreEqual(TruckSpec.Controls.Length, root.GetComponents<JointAxis>().Length);
            Assert.IsNotNull(root.GetComponent<SceneResetter>(), "resetter");
        }

        private static Transform FindDeep(Transform parent, string objectName)
        {
            foreach (Transform child in parent.GetComponentsInChildren<Transform>())
            {
                if (child.name == objectName)
                {
                    return child;
                }
            }

            return null;
        }
    }
}
