using NUnit.Framework;
using UnityEngine;

namespace Truck.Tests
{
    public class InputGuardTests
    {
        [Test]
        public void ShouldProcessInput_Focused_IsTrue()
        {
            Assert.IsTrue(SimulatorController.ShouldProcessInput(true));
        }

        [Test]
        public void ShouldProcessInput_Unfocused_IsFalse()
        {
            Assert.IsFalse(SimulatorController.ShouldProcessInput(false));
        }

        [Test]
        public void ShouldClearSelection_OnlyWhenFocusIsJustLost()
        {
            Assert.IsTrue(SimulatorController.ShouldClearSelection(true, false));
            Assert.IsFalse(SimulatorController.ShouldClearSelection(false, false));
            Assert.IsFalse(SimulatorController.ShouldClearSelection(true, true));
            Assert.IsFalse(SimulatorController.ShouldClearSelection(false, true));
        }

        [Test]
        public void GetAxis_NullOrOutOfRange_ReturnsNull()
        {
            var go = new GameObject("Controller");
            try
            {
                var controller = go.AddComponent<SimulatorController>();
                controller.Initialize(new JointAxis[] { null });

                Assert.IsNull(controller.GetAxis(ControlId.Torre));
                Assert.IsNull(controller.GetAxis(ControlId.Caja));
                Assert.IsNull(controller.GetAxis((ControlId)99));
                Assert.IsNull(controller.GetAxis((ControlId)(-1)));
                controller.Initialize(null);
                Assert.IsNull(controller.GetAxis(ControlId.Torre));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
