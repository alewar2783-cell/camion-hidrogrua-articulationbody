using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Truck.Tests
{
    public class TruckTaskTests
    {
        private const float StowSeconds = 3f;
        private const float RestSeconds = 10f;
        private const float ApproachSeconds = 5f;
        private const float DescentSeconds = 3f;
        private const float GraspSeconds = 1.5f;
        private const float LiftSeconds = 3f;
        private const float LiftDegrees = 15f;
        private const float MinLiftMeters = 0.2f;
        private const float MaxHeldDriftMeters = 0.15f;
        private const float MaxPenetrationMeters = 0.02f;
        private const float MinCubeHeight = -0.1f;
        private const float MaxCubeDistance = 30f;
        private const float GroundCubeMaxHeight = 0.35f;
        private const float ClosedSeconds = 3f;
        private const float MaxFingerSpeed = 5f;
        private const float LimitMargin = 0.01f;
        private const float DumpDegrees = 45f;
        private const float DumpSeconds = 10f;
        private const float BedSettleSeconds = 1f;
        private const float MinGateOpenDegrees = 30f;
        private const float MaxGateClosedDegrees = 5f;
        private const float GateCloseSeconds = 6f;

        // Where the first cube waits while the arm swings over its spawn point.
        private static readonly Vector3 ParkedPosition = new Vector3(-6f, 0.25f, 0.4f);

        [TearDown]
        public void TearDown()
        {
            foreach (string objectName in new[]
            {
                TruckBuilder.RootName, TruckLoadsFactory.EnvironmentName,
                TruckLoadsFactory.LoadsRootName
            })
            {
                GameObject existing = GameObject.Find(objectName);
                if (existing != null)
                {
                    Object.Destroy(existing);
                }
            }
        }

        [UnityTest]
        public IEnumerator Loads_AtRest_StayOnGround()
        {
            GameObject root = TruckBuilder.Build(BuildMode.Full);
            yield return Stow(root);
            yield return new WaitForSeconds(RestSeconds);

            Assert.AreEqual(TruckSpec.Loads.GroundSpawn.Length, CountLoads());
            for (int i = 0; i < TruckSpec.Loads.GroundSpawn.Length; i++)
            {
                Vector3 position = FindLoad(i).position;
                Assert.IsTrue(float.IsFinite(position.x + position.y + position.z), "Load_" + i);
                Assert.Greater(position.y, MinCubeHeight, "Load_" + i);
                Assert.Less(position.magnitude, MaxCubeDistance, "Load_" + i);
            }
        }

        [UnityTest]
        public IEnumerator Loads_HaveSpecProperties()
        {
            TruckBuilder.Build(BuildMode.Full);
            yield return null;

            Assert.AreEqual(TruckSpec.Loads.GroundSpawn.Length, CountLoads());
            for (int i = 0; i < TruckSpec.Loads.GroundSpawn.Length; i++)
            {
                Rigidbody load = FindLoad(i);
                string label = load.name;
                Assert.AreEqual(TruckSpec.Loads.CubeMass, load.mass, label);
                Assert.AreEqual(CollisionDetectionMode.ContinuousDynamic,
                    load.collisionDetectionMode, label);
                Assert.AreEqual(RigidbodyInterpolation.Interpolate, load.interpolation, label);
                Assert.AreEqual(Vector3.one * TruckSpec.Loads.CubeSize, load.transform.localScale,
                    label);
                Assert.AreEqual("Mat_GomaPinza", load.GetComponent<Collider>().sharedMaterial.name,
                    label);
            }
        }

        [UnityTest]
        public IEnumerator Grasp_Cube_LiftsWithGripper([Values(0, 1, 2)] int repetition)
        {
            GameObject root = TruckBuilder.Build(BuildMode.Full);
            SimulatorController controller = root.GetComponent<SimulatorController>();
            yield return Stow(root);
            Rigidbody cube = FindLoad(0);
            TruckLoadsFactory.Place(cube, ParkedPosition, Quaternion.identity);
            ApplyPose(root, TruckSpec.Poses.GraspApproach);
            yield return new WaitForSeconds(ApproachSeconds);
            ApplyPose(root, TruckSpec.Poses.GraspReach);
            yield return new WaitForSeconds(DescentSeconds);

            ArticulationBody head = FindBody(root, BodyId.CabezalPinza);
            ArticulationBody left = FindBody(root, BodyId.DedoIzquierdo);
            ArticulationBody right = FindBody(root, BodyId.DedoDerecho);
            Vector3 between = (left.transform.position + right.transform.position) * 0.5f;
            var yaw = Quaternion.Euler(0f, head.transform.eulerAngles.y, 0f);
            TruckLoadsFactory.Place(cube, between, yaw);

            controller.GetAxis(ControlId.Pinza).SetTarget(TruckSpec.Get(ControlId.Pinza).Max);
            yield return new WaitForSeconds(GraspSeconds);

            float startHeight = cube.position.y;
            float startDistance = HeadDistance(cube, head);
            JointAxis boom = controller.GetAxis(ControlId.Pluma);
            boom.SetTarget(boom.Target + LiftDegrees);
            yield return new WaitForSeconds(LiftSeconds);

            float gap = TruckSpec.Geometry.GripperOpening - left.jointPosition[0]
                + right.jointPosition[0];
            float lifted = cube.position.y - startHeight;
            float drift = Mathf.Abs(HeadDistance(cube, head) - startDistance);
            Assert.GreaterOrEqual(gap, TruckSpec.Loads.CubeSize - MaxPenetrationMeters,
                "finger gap while holding the cube");
            Assert.GreaterOrEqual(lifted, MinLiftMeters, "cube lift");
            Assert.Less(drift, MaxHeldDriftMeters, "cube to head distance change");
        }

        [UnityTest]
        public IEnumerator Gripper_ClosedOnNothing_IsStable()
        {
            GameObject root = TruckBuilder.Build(BuildMode.Full);
            yield return Stow(root);
            ArticulationBody left = FindBody(root, BodyId.DedoIzquierdo);
            ArticulationBody right = FindBody(root, BodyId.DedoDerecho);
            ArticulationBody[] bodies = root.GetComponentsInChildren<ArticulationBody>();

            root.GetComponent<SimulatorController>().GetAxis(ControlId.Pinza)
                .SetTarget(TruckSpec.Get(ControlId.Pinza).Max);

            float maxSpeed = 0f;
            float end = Time.time + ClosedSeconds;
            while (Time.time < end)
            {
                foreach (ArticulationBody body in bodies)
                {
                    if (!body.isRoot)
                    {
                        maxSpeed = Mathf.Max(maxSpeed, body.linearVelocity.magnitude);
                    }
                }

                yield return null;
            }

            float max = TruckSpec.Get(ControlId.Pinza).Max;
            Assert.IsTrue(float.IsFinite(left.jointPosition[0]), "left finger");
            Assert.IsTrue(float.IsFinite(right.jointPosition[0]), "right finger");
            Assert.That(left.jointPosition[0], Is.InRange(-LimitMargin, max + LimitMargin));
            Assert.That(right.jointPosition[0], Is.InRange(-max - LimitMargin, LimitMargin));
            Assert.Less(maxSpeed, MaxFingerSpeed, "max body speed");
        }

        [UnityTest]
        public IEnumerator Dump_OpensGate_AndEmptiesBed()
        {
            GameObject root = TruckBuilder.Build(BuildMode.Full);
            yield return Stow(root);
            ArticulationBody gate = FindBody(root, BodyId.CompuertaTrasera);

            TruckLoadsFactory.SpawnBedLoads();
            yield return new WaitForSeconds(BedSettleSeconds);
            root.GetComponent<SimulatorController>().GetAxis(ControlId.Caja)
                .SetTarget(DumpDegrees);

            float maxGate = 0f;
            float end = Time.time + DumpSeconds;
            while (Time.time < end)
            {
                maxGate = Mathf.Max(maxGate, gate.jointPosition[0] * Mathf.Rad2Deg);
                yield return null;
            }

            Assert.GreaterOrEqual(maxGate, MinGateOpenDegrees, "max gate angle");
            float rearEdge = TruckSpec.Geometry.BedHinge.z;
            for (int i = 0; i < TruckSpec.Loads.GroundSpawn.Length; i++)
            {
                Vector3 position = FindLoad(i).position;
                bool behind = position.z < rearEdge;
                bool onGround = position.y < GroundCubeMaxHeight;
                Assert.IsTrue(behind || onGround, "Load_" + i + " at " + position);
            }
        }

        [UnityTest]
        public IEnumerator Dump_ReturnsGateClosed()
        {
            GameObject root = TruckBuilder.Build(BuildMode.Full);
            yield return Stow(root);
            ArticulationBody gate = FindBody(root, BodyId.CompuertaTrasera);
            JointAxis bed = root.GetComponent<SimulatorController>().GetAxis(ControlId.Caja);

            TruckLoadsFactory.SpawnBedLoads();
            yield return new WaitForSeconds(BedSettleSeconds);
            bed.SetTarget(DumpDegrees);
            yield return new WaitForSeconds(DumpSeconds);
            bed.SetTarget(0f);
            yield return new WaitForSeconds(GateCloseSeconds);

            Assert.Less(gate.jointPosition[0] * Mathf.Rad2Deg, MaxGateClosedDegrees);
        }

        // With every axis at zero the jib lies across the bed, so each test starts from an
        // explicit pose.
        private static IEnumerator Stow(GameObject root)
        {
            ApplyPose(root, TruckSpec.Poses.Stowed);
            yield return new WaitForSeconds(StowSeconds);
        }

        private static void ApplyPose(GameObject root, float[] pose)
        {
            foreach (JointAxis axis in root.GetComponent<SimulatorController>().Axes)
            {
                axis.SetTarget(pose[(int)axis.Control]);
            }
        }

        private static float HeadDistance(Rigidbody cube, ArticulationBody head)
        {
            return Vector3.Distance(cube.position, head.transform.position);
        }

        private static int CountLoads()
        {
            GameObject loads = GameObject.Find(TruckLoadsFactory.LoadsRootName);
            Assert.IsNotNull(loads, "loads root");
            return loads.GetComponentsInChildren<Rigidbody>().Length;
        }

        private static Rigidbody FindLoad(int index)
        {
            GameObject load = GameObject.Find(TruckLoadsFactory.LoadName(index));
            Assert.IsNotNull(load, TruckLoadsFactory.LoadName(index));
            return load.GetComponent<Rigidbody>();
        }

        private static ArticulationBody FindBody(GameObject root, BodyId id)
        {
            string bodyName = TruckSpec.Get(id).Name;
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
