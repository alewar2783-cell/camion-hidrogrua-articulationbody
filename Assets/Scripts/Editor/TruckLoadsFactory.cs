using UnityEngine;

namespace Truck
{
    public static class TruckLoadsFactory
    {
        public const string EnvironmentName = "Environment";
        public const string LoadsRootName = "Loads";

        private static readonly Color LoadColor = new Color(0.8f, 0.25f, 0.15f);

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
                    spawn[i], Vector3.one * TruckSpec.Loads.CubeSize, LoadColor, grip);
                var body = cube.AddComponent<Rigidbody>();
                body.mass = TruckSpec.Loads.CubeMass;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                body.interpolation = RigidbodyInterpolation.Interpolate;
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
