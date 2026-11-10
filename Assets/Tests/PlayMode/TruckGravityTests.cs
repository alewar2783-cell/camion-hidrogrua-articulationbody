using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Truck.Tests
{
    public class TruckGravityTests
    {
        private const float SettleSeconds = 5f;
        private const float AngularToleranceDegrees = 1f;
        private const float LinearToleranceMeters = 0.01f;
        private const float MaxLinearSpeed = 0.2f;
        private const float MaxAngularSpeed = 0.5f;
        private const float MaxHangAngleDegrees = 5f;

        [TearDown]
        public void TearDown()
        {
            GameObject root = GameObject.Find(TruckBuilder.RootName);
            if (root != null)
            {
                Object.Destroy(root);
            }
        }

        [UnityTest]
        public IEnumerator Build_CreatesTenArticulationBodies()
        {
            GameObject root = TruckBuilder.Build(BuildMode.NoDrives);
            yield return new WaitForSeconds(SettleSeconds);

            ArticulationBody[] bodies = root.GetComponentsInChildren<ArticulationBody>();

            Assert.AreEqual(10, bodies.Length);
            foreach (ArticulationBody body in bodies)
            {
                Assert.AreEqual(Vector3.one, body.transform.localScale, body.name);
                Assert.IsNull(body.GetComponent<MeshRenderer>(), body.name);
                Assert.IsNull(body.GetComponent<Collider>(), body.name);
            }
        }

        [UnityTest]
        public IEnumerator Build_Twice_LeavesSingleRoot()
        {
            TruckBuilder.Build(BuildMode.NoDrives);
            GameObject root = TruckBuilder.Build(BuildMode.NoDrives);
            yield return new WaitForSeconds(SettleSeconds);

            int roots = 0;
            foreach (GameObject candidate in Object.FindObjectsByType<GameObject>(
                FindObjectsSortMode.None))
            {
                if (candidate.name == TruckBuilder.RootName)
                {
                    roots++;
                }
            }

            Assert.AreEqual(1, roots);
            Assert.AreEqual(10, root.GetComponentsInChildren<ArticulationBody>().Length);
        }

        [UnityTest]
        public IEnumerator NoDrives_Settles_WithoutNaN()
        {
            GameObject root = TruckBuilder.Build(BuildMode.NoDrives);
            yield return new WaitForSeconds(SettleSeconds);

            foreach (ArticulationBody body in root.GetComponentsInChildren<ArticulationBody>())
            {
                AssertFinite(body.transform.position, body.name);
                for (int i = 0; i < body.dofCount; i++)
                {
                    Assert.IsTrue(float.IsFinite(body.jointPosition[i]), body.name);
                }
            }
        }

        [UnityTest]
        public IEnumerator NoDrives_RespectsLimits()
        {
            GameObject root = TruckBuilder.Build(BuildMode.NoDrives);
            yield return new WaitForSeconds(SettleSeconds);

            foreach (BodySpec spec in TruckSpec.Bodies)
            {
                if (spec.Joint == JointKind.Root || spec.Joint == JointKind.Spherical)
                {
                    continue;
                }

                ArticulationBody body = FindBody(root, spec.Name);
                bool isAngular = spec.Joint == JointKind.Revolute;
                float tolerance = isAngular ? AngularToleranceDegrees : LinearToleranceMeters;
                float position = JointAxis.ToDisplay(isAngular, body.jointPosition[0]);

                Assert.GreaterOrEqual(position, spec.Lower - tolerance, spec.Name);
                Assert.LessOrEqual(position, spec.Upper + tolerance, spec.Name);
            }
        }

        [UnityTest]
        public IEnumerator NoDrives_Settles_AtRest()
        {
            GameObject root = TruckBuilder.Build(BuildMode.NoDrives);
            yield return new WaitForSeconds(SettleSeconds);

            foreach (ArticulationBody body in root.GetComponentsInChildren<ArticulationBody>())
            {
                if (body.isRoot)
                {
                    continue;
                }

                Assert.Less(body.linearVelocity.magnitude, MaxLinearSpeed, body.name);
                Assert.Less(body.angularVelocity.magnitude, MaxAngularSpeed, body.name);
            }
        }

        [UnityTest]
        public IEnumerator NoDrives_GripperHangsVertical()
        {
            GameObject root = TruckBuilder.Build(BuildMode.NoDrives);
            yield return new WaitForSeconds(SettleSeconds);

            ArticulationBody head = FindBody(root, TruckSpec.Get(BodyId.CabezalPinza).Name);
            float angle = Vector3.Angle(head.transform.up, Vector3.up);

            Assert.Less(angle, MaxHangAngleDegrees);
        }

        [UnityTest]
        public IEnumerator Build_PhysicsMaterials_ArePersistedAssetsWithSpecValues()
        {
            GameObject root = TruckBuilder.Build(BuildMode.NoDrives);
            yield return null;

            foreach (string fingerName in new[] { "Visual_DedoIzquierdo", "Visual_DedoDerecho" })
            {
                AssertMaterial(FindChild(root, fingerName), TruckSpec.Materials.GripDynamicFriction,
                    TruckSpec.Materials.GripStaticFriction, PhysicsMaterialCombine.Maximum);
            }

            AssertMaterial(FindChild(root, "Visual_PisoCaja"),
                TruckSpec.Materials.SteelDynamicFriction, TruckSpec.Materials.SteelStaticFriction,
                PhysicsMaterialCombine.Average);
        }

        private static void AssertMaterial(Transform visual, float dynamicFriction,
            float staticFriction, PhysicsMaterialCombine combine)
        {
            PhysicsMaterial material = visual.GetComponent<Collider>().sharedMaterial;
            string path = AssetDatabase.GetAssetPath(material);

            Assert.IsNotEmpty(path, visual.name);
            StringAssert.StartsWith("Assets/PhysicsMaterials/", path);
            Assert.AreEqual(dynamicFriction, material.dynamicFriction, 1e-4f, visual.name);
            Assert.AreEqual(staticFriction, material.staticFriction, 1e-4f, visual.name);
            Assert.AreEqual(combine, material.frictionCombine, visual.name);
            Assert.AreEqual(0f, material.bounciness, visual.name);
        }

        private static Transform FindChild(GameObject root, string childName)
        {
            foreach (Transform candidate in root.GetComponentsInChildren<Transform>())
            {
                if (candidate.name == childName)
                {
                    return candidate;
                }
            }

            Assert.Fail("Child not found: " + childName);
            return null;
        }

        private static ArticulationBody FindBody(GameObject root, string bodyName)
        {
            var pending = new Queue<Transform>();
            pending.Enqueue(root.transform);
            while (pending.Count > 0)
            {
                Transform current = pending.Dequeue();
                if (current.name == bodyName)
                {
                    return current.GetComponent<ArticulationBody>();
                }

                foreach (Transform child in current)
                {
                    pending.Enqueue(child);
                }
            }

            Assert.Fail("Body not found: " + bodyName);
            return null;
        }

        private static void AssertFinite(Vector3 value, string label)
        {
            Assert.IsTrue(
                float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z),
                label);
        }
    }
}
