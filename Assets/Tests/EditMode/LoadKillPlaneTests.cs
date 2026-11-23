using NUnit.Framework;

namespace Truck.Tests
{
    public class LoadKillPlaneTests
    {
        [Test]
        public void IsOutOfWorld_BelowPlane_IsTrue()
        {
            Assert.IsTrue(LoadKillPlane.IsOutOfWorld(TruckSpec.Loads.KillPlaneY - 0.1f));
        }

        [Test]
        public void IsOutOfWorld_OnGroundOrAbovePlane_IsFalse()
        {
            Assert.IsFalse(LoadKillPlane.IsOutOfWorld(0.25f));
            Assert.IsFalse(LoadKillPlane.IsOutOfWorld(TruckSpec.Loads.KillPlaneY));
        }

        [Test]
        public void IsOutOfWorld_NanOrInfinity_IsTrue()
        {
            Assert.IsTrue(LoadKillPlane.IsOutOfWorld(float.NaN));
            Assert.IsTrue(LoadKillPlane.IsOutOfWorld(float.NegativeInfinity));
        }
    }
}
