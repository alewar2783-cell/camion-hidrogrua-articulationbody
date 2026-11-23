using UnityEngine;

namespace Truck
{
    public static class TruckLoadsFactory
    {
        public const string EnvironmentName = "Environment";
        public const string LoadsRootName = "Loads";

        private const float YardHeight = 0.02f;
        private const float YardLift = 0.01f;
        // The hazard border sits just above the yard so the two flat boxes never z-fight.
        private const float YardBorderWidth = 0.35f;
        private const float YardBorderHeight = 0.03f;
        private const float YardBorderLift = 0.015f;

        public static string LoadName(int index)
        {
            return "Load_" + index;
        }

        /// <summary>Teleports a load and clears its motion.</summary>
        public static void Place(Rigidbody load, Vector3 position, Quaternion rotation)
        {
            load.transform.SetPositionAndRotation(position, rotation);
            load.position = position;
            load.rotation = rotation;
            load.linearVelocity = Vector3.zero;
            load.angularVelocity = Vector3.zero;
        }

        /// <summary>Moves the first loads onto the bed floor (TruckSpec.Loads.BedSpawn).</summary>
        public static void SpawnBedLoads()
        {
            GameObject loads = GameObject.Find(LoadsRootName);
            if (loads == null)
            {
                return;
            }

            Vector3[] spawn = TruckSpec.Loads.BedSpawn;
            for (int i = 0; i < spawn.Length; i++)
            {
                Transform load = loads.transform.Find(LoadName(i));
                if (load != null)
                {
                    Place(load.GetComponent<Rigidbody>(), spawn[i], Quaternion.identity);
                }
            }
        }

        /// <summary>Creates one cube per ground spawn point, each a plain rigid body.</summary>
        public static GameObject CreateLoads(PhysicsMaterial grip)
        {
            var loads = new GameObject(LoadsRootName);
            Vector3[] spawn = TruckSpec.Loads.GroundSpawn;
            for (int i = 0; i < spawn.Length; i++)
            {
                GameObject cube = TruckBodyFactory.CreateBox(LoadName(i), loads.transform,
                    spawn[i], Vector3.one * TruckSpec.Loads.CubeSize, TruckMaterials.Cargo(), grip);
                var body = cube.AddComponent<Rigidbody>();
                body.mass = TruckSpec.Loads.CubeMass;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                cube.AddComponent<LoadKillPlane>().Initialize(body, spawn[i]);
            }

            return loads;
        }

        public static (PhysicsMaterial Grip, PhysicsMaterial Steel) CreateMaterials()
        {
            PhysicsMaterial grip = TruckAssetStore.LoadOrCreatePhysicsMaterial("Mat_GomaPinza",
                TruckSpec.Materials.GripDynamicFriction, TruckSpec.Materials.GripStaticFriction,
                TruckSpec.Materials.GripCombine);
            PhysicsMaterial steel = TruckAssetStore.LoadOrCreatePhysicsMaterial("Mat_AceroCaja",
                TruckSpec.Materials.SteelDynamicFriction, TruckSpec.Materials.SteelStaticFriction,
                TruckSpec.Materials.SteelCombine);
            return (grip, steel);
        }

        /// <summary>Creates the static ground and a flat marker for the unloading yard.</summary>
        public static GameObject CreateGround()
        {
            var environment = new GameObject(EnvironmentName);
            PhysicsMaterial soil = TruckAssetStore.LoadOrCreatePhysicsMaterial("Mat_Suelo",
                TruckSpec.Materials.GroundDynamicFriction, TruckSpec.Materials.GroundStaticFriction,
                TruckSpec.Materials.GroundCombine);

            GameObject ground = TruckBodyFactory.CreateBox("Ground", environment.transform,
                new Vector3(0f, -0.5f, 0f),
                new Vector3(TruckSpec.Geometry.GroundSize, 1f, TruckSpec.Geometry.GroundSize),
                TruckMaterials.Ground(), soil);
            ground.isStatic = true;

            Vector3 yard = TruckSpec.Geometry.YardCenter;
            Vector2 yardSize = TruckSpec.Geometry.YardSize;
            TruckBodyFactory.CreateBox("Visual_Corralon", environment.transform,
                new Vector3(yard.x, YardLift, yard.z),
                new Vector3(yardSize.x, YardHeight, yardSize.y), TruckMaterials.Yard(), null,
                false);
            AddYardBorder(environment.transform, yard, yardSize);
            return environment;
        }

        // Visual only: a hazard-striped frame that marks the unloading yard.
        private static void AddYardBorder(Transform parent, Vector3 yard, Vector2 yardSize)
        {
            Material hazard = TruckMaterials.YardBorder();
            float halfX = (yardSize.x - YardBorderWidth) * 0.5f;
            float halfZ = (yardSize.y - YardBorderWidth) * 0.5f;
            var alongX = new Vector3(yardSize.x, YardBorderHeight, YardBorderWidth);
            var alongZ = new Vector3(YardBorderWidth, YardBorderHeight, yardSize.y);
            foreach (float side in new[] { -1f, 1f })
            {
                TruckBodyFactory.CreateBox("Visual_BordeCorralon", parent,
                    new Vector3(yard.x, YardBorderLift, yard.z + side * halfZ), alongX, hazard,
                    null, false);
                TruckBodyFactory.CreateBox("Visual_BordeCorralon", parent,
                    new Vector3(yard.x + side * halfX, YardBorderLift, yard.z), alongZ, hazard,
                    null, false);
            }
        }
    }
}
