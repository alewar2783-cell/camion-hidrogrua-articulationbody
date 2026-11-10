using NUnit.Framework;

namespace Truck.Tests
{
    public class OrbitCameraGuardTests
    {
        [Test]
        public void IsPointerFree_OverUi_IsFalse()
        {
            Assert.IsFalse(OrbitCamera.IsPointerFree(true));
        }

        [Test]
        public void IsPointerFree_NotOverUi_IsTrue()
        {
            Assert.IsTrue(OrbitCamera.IsPointerFree(false));
        }
    }
}
