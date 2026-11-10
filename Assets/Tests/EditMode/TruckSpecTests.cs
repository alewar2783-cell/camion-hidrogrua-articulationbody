using System.Linq;
using NUnit.Framework;

namespace Truck.Tests
{
    public class TruckSpecTests
    {
        [Test]
        public void Bodies_Count_IsTen()
        {
            Assert.AreEqual(10, TruckSpec.Bodies.Length);
        }

        [Test]
        public void Bodies_UseAtLeastThreeJointKinds()
        {
            int kinds = TruckSpec.Bodies.Select(b => b.Joint).Distinct().Count();

            Assert.GreaterOrEqual(kinds, 3);
        }

        [Test]
        public void Bodies_DrivenCount_IsSeven()
        {
            int driven = TruckSpec.Bodies.Count(b => b.Control != null);

            Assert.AreEqual(7, driven);
            Assert.AreEqual(6, TruckSpec.Controls.Length);
        }

        [Test]
        public void Bodies_PassiveCount_IsTwo()
        {
            var passive = TruckSpec.Bodies.Where(b => b.IsPassive).ToArray();

            Assert.AreEqual(2, passive.Length);
            Assert.IsTrue(passive.All(b => b.Stiffness == 0f));
        }

        [Test]
        public void Bodies_HaveSingleRoot()
        {
            var roots = TruckSpec.Bodies.Where(b => b.Parent == null).ToArray();

            Assert.AreEqual(1, roots.Length);
            Assert.AreEqual(BodyId.ChasisRoot, roots[0].Id);
        }

        [Test]
        public void Masses_NonIncreasingAlongCraneChain()
        {
            var expected = new[] { 5f, 15f, 35f, 60f, 100f, 150f };
            var chain = new System.Collections.Generic.List<float>();
            BodyId? current = BodyId.DedoIzquierdo;
            while (current != null && current != BodyId.ChasisRoot)
            {
                chain.Add(TruckSpec.Get(current.Value).Mass);
                current = TruckSpec.Get(current.Value).Parent;
            }

            Assert.AreEqual(expected.Length, chain.Count);
            CollectionAssert.AreEqual(expected, chain);
            for (int i = 1; i < chain.Count; i++)
            {
                Assert.GreaterOrEqual(chain[i], chain[i - 1]);
            }
        }

        [Test]
        public void Controls_RangesMatchSpec()
        {
            AssertControl(ControlId.Torre, -170f, 170f, 45f);
            AssertControl(ControlId.Pluma, -15f, 75f, 30f);
            AssertControl(ControlId.BrazoSecundario, -125f, 15f, 35f);
            AssertControl(ControlId.Telescopico, 0f, 2.5f, 0.8f);
            AssertControl(ControlId.Pinza, 0f, 0.35f, 0.4f);
            AssertControl(ControlId.Caja, 0f, 55f, 20f);
        }

        [Test]
        public void Loads_CubeCount_BetweenFourAndSix()
        {
            Assert.That(TruckSpec.Loads.GroundSpawn.Length, Is.InRange(4, 6));
        }

        private static void AssertControl(ControlId id, float min, float max, float speed)
        {
            var control = TruckSpec.Get(id);

            Assert.AreEqual(min, control.Min, 1e-4f);
            Assert.AreEqual(max, control.Max, 1e-4f);
            Assert.AreEqual(speed, control.Speed, 1e-4f);
        }

        [Test]
        public void Get_EveryBodyId_ReturnsMatchingSpec()
        {
            foreach (BodyId id in System.Enum.GetValues(typeof(BodyId)))
            {
                Assert.AreEqual(id, TruckSpec.Get(id).Id);
            }
        }

        [Test]
        public void Get_EveryControlId_ReturnsMatchingSpec()
        {
            foreach (ControlId id in System.Enum.GetValues(typeof(ControlId)))
            {
                Assert.AreEqual(id, TruckSpec.Get(id).Id);
            }
        }
    }
}
