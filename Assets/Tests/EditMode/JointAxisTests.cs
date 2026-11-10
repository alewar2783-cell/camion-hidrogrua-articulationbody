using NUnit.Framework;
using UnityEngine;

namespace Truck.Tests
{
    public class JointAxisTests
    {
        [Test]
        public void Step_BothKeys_NoChange()
        {
            float input = SimulatorController.ReadInput(true, true);

            Assert.AreEqual(0f, input);
            Assert.AreEqual(10f, JointAxis.Step(10f, 0f, 45f, 0.02f, -170f, 170f));
        }

        [Test]
        public void ReadInput_SingleKey_ReturnsSign()
        {
            Assert.AreEqual(-1f, SimulatorController.ReadInput(true, false));
            Assert.AreEqual(1f, SimulatorController.ReadInput(false, true));
            Assert.AreEqual(0f, SimulatorController.ReadInput(false, false));
        }

        [Test]
        public void Step_LargeDeltaTime_IsCapped()
        {
            float result = JointAxis.Step(0f, 1f, 45f, 5f, -170f, 170f);

            Assert.AreEqual(45f * 0.05f, result, 1e-4f);
        }

        [Test]
        public void Step_ClampsToRange()
        {
            Assert.AreEqual(75f, JointAxis.Step(74.9f, 1f, 30f, 0.05f, -15f, 75f));
        }

        [Test]
        public void ToDisplay_Angular_ConvertsRadiansToDegrees()
        {
            Assert.AreEqual(180f, JointAxis.ToDisplay(true, Mathf.PI), 1e-3f);
            Assert.AreEqual(1.25f, JointAxis.ToDisplay(false, 1.25f));
        }

        [Test]
        public void ClampTarget_OutOfRange_Clamps()
        {
            Assert.AreEqual(75f, JointAxis.ClampTarget(100f, -15f, 75f));
            Assert.AreEqual(-15f, JointAxis.ClampTarget(-40f, -15f, 75f));
            Assert.AreEqual(30f, JointAxis.ClampTarget(30f, -15f, 75f));
        }
    }
}
