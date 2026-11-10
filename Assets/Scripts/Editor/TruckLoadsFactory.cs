using UnityEngine;

namespace Truck
{
    public static class TruckLoadsFactory
    {
        public const string EnvironmentName = "Environment";

        public static (PhysicsMaterial Grip, PhysicsMaterial Steel) CreateMaterials()
        {
            var grip = new PhysicsMaterial("Mat_GomaPinza")
            {
                dynamicFriction = TruckSpec.Materials.GripDynamicFriction,
                staticFriction = TruckSpec.Materials.GripStaticFriction,
                bounciness = 0f,
                frictionCombine = PhysicsMaterialCombine.Maximum,
                bounceCombine = PhysicsMaterialCombine.Minimum
            };
            var steel = new PhysicsMaterial("Mat_AceroCaja")
            {
                dynamicFriction = TruckSpec.Materials.SteelDynamicFriction,
                staticFriction = TruckSpec.Materials.SteelStaticFriction,
                bounciness = 0f
            };
            return (grip, steel);
        }

        /// <summary>Creates the static ground and a flat marker for the unloading yard.</summary>
        public static GameObject CreateGround()
        {
            var environment = new GameObject(EnvironmentName);

            GameObject ground = TruckBodyFactory.CreateBox("Ground", environment.transform,
                new Vector3(0f, -0.5f, 0f),
                new Vector3(TruckSpec.Geometry.GroundSize, 1f, TruckSpec.Geometry.GroundSize),
                new Color(0.42f, 0.5f, 0.36f), null);
            ground.isStatic = true;

            Vector3 yard = TruckSpec.Geometry.YardCenter;
            Vector2 yardSize = TruckSpec.Geometry.YardSize;
            TruckBodyFactory.CreateBox("Visual_Corralon", environment.transform,
                new Vector3(yard.x, 0.01f, yard.z), new Vector3(yardSize.x, 0.02f, yardSize.y),
                new Color(0.55f, 0.45f, 0.3f), null, false);
            return environment;
        }
    }
}
