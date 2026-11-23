using UnityEngine;

namespace Truck
{
    /// <summary>
    /// Render materials of the generated scene, one per surface role. They only change how the
    /// scene looks; physics materials live in TruckLoadsFactory and TruckSpec.
    /// </summary>
    public static class TruckMaterials
    {
        public static Material Crane()
        {
            return Toon("Mat_Toon_Grua", TruckPalette.SafetyYellow);
        }

        public static Material Telescope()
        {
            return Toon("Mat_Toon_Telescopico", TruckPalette.SafetyOrange);
        }

        public static Material Cabin()
        {
            return Toon("Mat_Toon_Cabina", TruckPalette.SafetyYellow);
        }

        public static Material Glass()
        {
            return Toon("Mat_Toon_Vidrio", TruckPalette.Glass);
        }

        public static Material Chassis()
        {
            return Toon("Mat_Toon_Chasis", TruckPalette.ChassisSteel);
        }

        public static Material Bed()
        {
            return Toon("Mat_Toon_Caja", TruckPalette.BedSteel);
        }

        public static Material Gripper()
        {
            return Toon("Mat_Toon_Pinza", TruckPalette.Graphite);
        }

        public static Material Tyre()
        {
            return Toon("Mat_Toon_Rueda", TruckPalette.Tyre);
        }

        public static Material Hazard()
        {
            return TruckAssetStore.LoadOrCreateToonMaterial("Mat_Toon_Peligro",
                TruckPalette.SafetyYellow, true, true);
        }

        public static Material Cargo()
        {
            return Toon("Mat_Toon_Carga", TruckPalette.CargoBlue);
        }

        public static Material Ground()
        {
            return TruckAssetStore.LoadOrCreateToonMaterial("Mat_Toon_Suelo", TruckPalette.Ground,
                false);
        }

        public static Material Yard()
        {
            return TruckAssetStore.LoadOrCreateToonMaterial("Mat_Toon_Corralon",
                TruckPalette.Yard, false);
        }

        public static Material YardBorder()
        {
            return TruckAssetStore.LoadOrCreateToonMaterial("Mat_Toon_BordeCorralon",
                TruckPalette.SafetyYellow, false, true);
        }

        private static Material Toon(string materialName, Color color)
        {
            return TruckAssetStore.LoadOrCreateToonMaterial(materialName, color, true);
        }
    }
}
