using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Truck.Tests
{
    public class TruckDriveTests
    {
        private const float SettleSeconds = 4f;
        private const float StowSeconds = 3f;
        private const float ReachSettleSeconds = 6f;
        private const float AngularToleranceDegrees = 6f;
        private const float LinearToleranceMeters = 0.08f;
        private const float ReachToleranceMeters = 0.5f;
        private const float MaxStowedSpeed = 1f;
        private const float MinStowedGap = 0.1f;
        private const float GapSampleSeconds = 0.25f;

        private static readonly Vector3 GroundPoint = new Vector3(4.5f, 1.25f, 1.4f);
        private static readonly Vector3 BedPoint = new Vector3(0f, 2.05f, -1.75f);

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
        public IEnumerator Full_Build_HasSixAxesAndSevenDrives()
        {
            GameObject root = TruckBuilder.Build(BuildMode.Full);
            yield return null;
            SimulatorController controller = root.GetComponent<SimulatorController>();

            foreach (ControlSpec spec in TruckSpec.Controls)
            {
                JointAxis axis = controller.GetAxis(spec.Id);
                Assert.IsNotNull(axis, spec.Id.ToString());
                Assert.AreEqual(spec.Id, axis.Control);
            }

            int drives = 0;
            foreach (ArticulationBody body in root.GetComponentsInChildren<ArticulationBody>())
            {
                if (!body.isRoot && body.xDrive.stiffness > 0f)
                {
                    drives++;
                }
            }

            Assert.AreEqual(6, controller.Axes.Length);
            Assert.AreEqual(7, drives);

            ArticulationBody left = FindBody(root, TruckSpec.Get(BodyId.DedoIzquierdo).Name);
            ArticulationBody right = FindBody(root, TruckSpec.Get(BodyId.DedoDerecho).Name);
            controller.GetAxis(ControlId.Pinza).SetTarget(0.2f);
            yield return new WaitForSeconds(SettleSeconds);

            Assert.Greater(left.jointPosition[0], 0.1f);
            Assert.Less(right.jointPosition[0], -0.1f);
        }

        [UnityTest]
        public IEnumerator Axis_ReachesTarget([Values] ControlId id)
        {
            GameObject root = TruckBuilder.Build(BuildMode.Full);
            JointAxis axis = root.GetComponent<SimulatorController>().GetAxis(id);
            yield return Stow(root);
            axis.SetTarget(Midpoint(axis.Spec));
            yield return new WaitForSeconds(SettleSeconds);

            Assert.AreEqual(axis.Target, axis.Real, Tolerance(axis.Spec), id.ToString());
        }

        [UnityTest]
        public IEnumerator Axis_NeverExceedsLimits([Values] ControlId id)
        {
            GameObject root = TruckBuilder.Build(BuildMode.Full);
            JointAxis axis = root.GetComponent<SimulatorController>().GetAxis(id);
            yield return Stow(root);
            axis.SetTarget(axis.Spec.Max);
            yield return new WaitForSeconds(SettleSeconds);

            Assert.IsTrue(float.IsFinite(axis.Real), id.ToString());
            Assert.LessOrEqual(axis.Real, axis.Spec.Max + Tolerance(axis.Spec), id.ToString());
        }

        [UnityTest]
        public IEnumerator Passives_HaveNoDrive()
        {
            GameObject root = TruckBuilder.Build(BuildMode.Full);
            yield return null;

            ArticulationBody head = FindBody(root, TruckSpec.Get(BodyId.CabezalPinza).Name);
            ArticulationBody gate = FindBody(root, TruckSpec.Get(BodyId.CompuertaTrasera).Name);

            Assert.AreEqual(0f, head.yDrive.stiffness);
            Assert.AreEqual(0f, head.zDrive.stiffness);
            Assert.AreEqual(25f, head.yDrive.damping);
            Assert.AreEqual(25f, head.zDrive.damping);
            Assert.AreEqual(0f, gate.xDrive.stiffness);
            Assert.AreEqual(10f, gate.xDrive.damping);
        }

        [UnityTest]
        public IEnumerator ResetTargets_ReturnsToStowedPose()
        {
            GameObject root = TruckBuilder.Build(BuildMode.Full);
            SimulatorController controller = root.GetComponent<SimulatorController>();
            foreach (JointAxis axis in controller.Axes)
            {
                axis.SetTarget(Midpoint(axis.Spec));
            }

            yield return new WaitForSeconds(SettleSeconds);
            foreach (JointAxis axis in controller.Axes)
            {
                axis.ResetTarget();
            }

            yield return new WaitForSeconds(SettleSeconds);

            foreach (JointAxis axis in controller.Axes)
            {
                Assert.AreEqual(TruckSpec.Poses.Stowed[(int)axis.Control], axis.Real,
                    Tolerance(axis.Spec), axis.Control.ToString());
            }
        }

        [UnityTest]
        public IEnumerator Full_BuildTwice_NoDuplicates()
        {
            TruckBuilder.Build(BuildMode.Full);
            GameObject root = TruckBuilder.Build(BuildMode.Full);
            yield return null;

            Assert.AreEqual(10, root.GetComponentsInChildren<ArticulationBody>().Length);
            Assert.AreEqual(1, Object.FindObjectsByType<SimulatorController>(
                FindObjectsSortMode.None).Length);
            Assert.AreEqual(6, root.GetComponents<JointAxis>().Length);
        }

        [UnityTest]
        public IEnumerator Reach_GroundPoint_HeadWithinTolerance()
        {
            GameObject root = TruckBuilder.Build(BuildMode.Full);
            ApplyPose(root, TruckSpec.Poses.GroundReach);
            yield return new WaitForSeconds(ReachSettleSeconds);

            Vector3 head = FindBody(root, TruckSpec.Get(BodyId.CabezalPinza).Name)
                .transform.position;

            Assert.Less(Vector3.Distance(head, GroundPoint), ReachToleranceMeters, head.ToString());
        }

        [UnityTest]
        public IEnumerator Reach_BedCenter_HeadWithinTolerance()
        {
            GameObject root = TruckBuilder.Build(BuildMode.Full);
            ApplyPose(root, TruckSpec.Poses.BedReach);
            yield return new WaitForSeconds(ReachSettleSeconds);

            Vector3 head = FindBody(root, TruckSpec.Get(BodyId.CabezalPinza).Name)
                .transform.position;

            Assert.Less(Vector3.Distance(head, BedPoint), ReachToleranceMeters, head.ToString());
        }

        [UnityTest]
        public IEnumerator Stowed_BedTilt55_CraneStaysClearAndStable()
        {
            GameObject root = TruckBuilder.Build(BuildMode.Full);
            SimulatorController controller = root.GetComponent<SimulatorController>();
            yield return Stow(root);

            controller.GetAxis(ControlId.Caja).SetTarget(TruckSpec.Get(ControlId.Caja).Max);
            Collider[] crane = CollectColliders(root, BodyId.TorreBase, BodyId.BrazoPrincipal,
                BodyId.BrazoSecundario, BodyId.TramoTelescopico, BodyId.CabezalPinza,
                BodyId.DedoIzquierdo, BodyId.DedoDerecho);
            Collider[] bed = CollectColliders(root, BodyId.CajaVolcadora,
                BodyId.CompuertaTrasera);

            float minGap = float.MaxValue;
            for (float t = 0f; t < SettleSeconds; t += GapSampleSeconds)
            {
                yield return new WaitForSeconds(GapSampleSeconds);
                minGap = Mathf.Min(minGap, MinGap(crane, bed));
            }

            foreach (JointAxis axis in controller.Axes)
            {
                Assert.IsTrue(float.IsFinite(axis.Real), axis.Control.ToString());
                float margin = Tolerance(axis.Spec);
                Assert.GreaterOrEqual(axis.Real, axis.Spec.Min - margin, axis.Control.ToString());
                Assert.LessOrEqual(axis.Real, axis.Spec.Max + margin, axis.Control.ToString());
            }

            foreach (ArticulationBody body in root.GetComponentsInChildren<ArticulationBody>())
            {
                Assert.IsTrue(float.IsFinite(body.transform.position.x), body.name);
                if (!body.isRoot)
                {
                    Assert.Less(body.linearVelocity.magnitude, MaxStowedSpeed, body.name);
                }
            }

            Assert.Greater(minGap, MinStowedGap, "minimum crane to bed gap");
            Assert.AreEqual(TruckSpec.Get(ControlId.Caja).Max,
                controller.GetAxis(ControlId.Caja).Real, AngularToleranceDegrees);
        }

        // With every axis at zero the jib lies across the bed, so the other axes would collide
        // with it; each axis test starts from the stowed pose instead.
        private static IEnumerator Stow(GameObject root)
        {
            ApplyPose(root, TruckSpec.Poses.Stowed);
            yield return new WaitForSeconds(StowSeconds);
        }

        private static void ApplyPose(GameObject root, float[] pose)
        {
            SimulatorController controller = root.GetComponent<SimulatorController>();
            foreach (JointAxis axis in controller.Axes)
            {
                axis.SetTarget(pose[(int)axis.Control]);
            }
        }

        private static float Midpoint(ControlSpec spec)
        {
            return spec.Min + (spec.Max - spec.Min) * 0.5f;
        }

        private static float Tolerance(ControlSpec spec)
        {
            return spec.IsAngular ? AngularToleranceDegrees : LinearToleranceMeters;
        }

        private static Collider[] CollectColliders(GameObject root, params BodyId[] ids)
        {
            var colliders = new List<Collider>();
            foreach (BodyId id in ids)
            {
                ArticulationBody body = FindBody(root, TruckSpec.Get(id).Name);
                foreach (Collider collider in body.GetComponentsInChildren<Collider>())
                {
                    if (collider.GetComponentInParent<ArticulationBody>() == body)
                    {
                        colliders.Add(collider);
                    }
                }
            }

            return colliders.ToArray();
        }

        // Samples the corners and face centers of each crane box; exact for the convex boxes
        // used here as long as the closest feature is a corner or a face.
        private static float MinGap(Collider[] crane, Collider[] bed)
        {
            float min = float.MaxValue;
            foreach (Collider a in crane)
            {
                var box = (BoxCollider)a;
                foreach (Vector3 corner in SamplePoints(box))
                {
                    foreach (Collider b in bed)
                    {
                        Vector3 closest = Physics.ClosestPoint(corner, b, b.transform.position,
                            b.transform.rotation);
                        min = Mathf.Min(min, Vector3.Distance(corner, closest));
                    }
                }
            }

            return min;
        }

        private static IEnumerable<Vector3> SamplePoints(BoxCollider box)
        {
            Vector3 half = box.size * 0.5f;
            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    for (int z = -1; z <= 1; z++)
                    {
                        yield return box.transform.TransformPoint(
                            box.center + Vector3.Scale(half, new Vector3(x, y, z)));
                    }
                }
            }
        }

        private static ArticulationBody FindBody(GameObject root, string bodyName)
        {
            foreach (ArticulationBody body in root.GetComponentsInChildren<ArticulationBody>())
            {
                if (body.name == bodyName)
                {
                    return body;
                }
            }

            Assert.Fail("Body not found: " + bodyName);
            return null;
        }
    }
}
