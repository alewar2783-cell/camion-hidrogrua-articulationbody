using System.Globalization;
using NUnit.Framework;
using UnityEngine;

namespace Truck.Tests
{
    public class TelemetryAndCameraTests
    {
        private const float Tolerance = 1e-4f;

        [Test]
        public void Format_Angular_UsesDegreesAndOneDecimal()
        {
            Assert.AreEqual("Brazo 1 | Target: 45.0° | Real: 43.8°",
                TelemetryUI.Format("Brazo 1", 45f, 43.84f, true));
        }

        [Test]
        public void Format_Linear_UsesMetersAndTwoDecimals()
        {
            Assert.AreEqual("Telescópico | Target: 1.00 m | Real: 0.99 m",
                TelemetryUI.Format("Telescópico", 1f, 0.987f, false));
        }

        [Test]
        public void Format_UsesInvariantCulture()
        {
            CultureInfo previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("es-AR");
                Assert.AreEqual("Torre | Target: -12.5° | Real: -12.4°",
                    TelemetryUI.Format("Torre", -12.5f, -12.44f, true));
                Assert.AreEqual("Pinza | Target: 0.35 m | Real: 0.20 m",
                    TelemetryUI.Format("Pinza", 0.35f, 0.2f, false));
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        }

        [Test]
        public void ClampPitch_OutOfRange_Clamps()
        {
            Assert.AreEqual(OrbitCamera.MinPitch, OrbitCamera.ClampPitch(-50f));
            Assert.AreEqual(OrbitCamera.MaxPitch, OrbitCamera.ClampPitch(120f));
            Assert.AreEqual(30f, OrbitCamera.ClampPitch(30f));
            Assert.AreEqual(-10f, OrbitCamera.MinPitch);
            Assert.AreEqual(85f, OrbitCamera.MaxPitch);
        }

        [Test]
        public void ClampDistance_OutOfRange_Clamps()
        {
            Assert.AreEqual(OrbitCamera.MinDistance, OrbitCamera.ClampDistance(0.5f));
            Assert.AreEqual(OrbitCamera.MaxDistance, OrbitCamera.ClampDistance(100f));
            Assert.AreEqual(10f, OrbitCamera.ClampDistance(10f));
            Assert.AreEqual(3f, OrbitCamera.MinDistance);
            Assert.AreEqual(25f, OrbitCamera.MaxDistance);
        }

        [Test]
        public void SetTarget_NaN_IsIgnored()
        {
            JointAxis axis = CreateAxis(ControlId.Pluma, 0f);
            axis.SetTarget(20f);

            axis.SetTarget(float.NaN);

            Assert.AreEqual(20f, axis.Target);
            Object.DestroyImmediate(axis.gameObject);
        }

        [Test]
        public void Step_NaNInput_ReturnsCurrentTarget()
        {
            Assert.AreEqual(10f, JointAxis.Step(10f, float.NaN, 45f, 0.02f, -170f, 170f));
            Assert.AreEqual(10f, JointAxis.Step(10f, 1f, float.NaN, 0.02f, -170f, 170f));
        }

        [Test]
        public void Initialize_WithDefault_StartsAtDefaultAndResetsToIt()
        {
            JointAxis axis = CreateAxis(ControlId.Pluma, 70f);
            Assert.AreEqual(70f, axis.Target, Tolerance);

            axis.SetTarget(10f);
            axis.ResetTarget();

            Assert.AreEqual(70f, axis.Target, Tolerance);
            Object.DestroyImmediate(axis.gameObject);
        }

        [Test]
        public void Initialize_WithoutDefault_StartsAtZero()
        {
            var host = new GameObject("axis");
            JointAxis axis = host.AddComponent<JointAxis>();
            axis.Initialize(TruckSpec.Get(ControlId.Torre), null);

            Assert.AreEqual(0f, axis.Target);
            Object.DestroyImmediate(host);
        }

        private static JointAxis CreateAxis(ControlId id, float defaultTarget)
        {
            var host = new GameObject("axis");
            JointAxis axis = host.AddComponent<JointAxis>();
            axis.Initialize(TruckSpec.Get(id), null, null, defaultTarget);
            return axis;
        }
    }
}
