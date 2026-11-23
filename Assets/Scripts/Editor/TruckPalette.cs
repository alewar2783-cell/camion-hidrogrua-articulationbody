using UnityEngine;

namespace Truck
{
    /// <summary>
    /// The "industrial toon" palette shared by the truck, the environment and the interface.
    /// Every colour of the generated scene comes from here; see docs/ESTILO.md.
    /// </summary>
    public static class TruckPalette
    {
        // Machine.
        public static readonly Color SafetyYellow = FromHex(0xF2B91B);
        public static readonly Color SafetyOrange = FromHex(0xE8641E);
        public static readonly Color ChassisSteel = FromHex(0x3B424B);
        public static readonly Color BedSteel = FromHex(0x8C949E);
        public static readonly Color Graphite = FromHex(0x2A2D31);
        public static readonly Color Tyre = FromHex(0x1B1C1E);
        public static readonly Color Glass = FromHex(0x3E5566);
        public static readonly Color HazardBlack = FromHex(0x17181A);
        public static readonly Color CargoBlue = FromHex(0x2E6FB4);

        // Toon lighting.
        public static readonly Color Outline = FromHex(0x121417);
        public static readonly Color Shade = FromHex(0xA3ACBD);

        // Environment.
        public static readonly Color Ground = FromHex(0xC4C2BB);
        public static readonly Color Yard = FromHex(0x6F6C66);
        public static readonly Color SkyZenith = FromHex(0x6F9CC4);
        public static readonly Color SkyHorizon = FromHex(0xDCE4E8);
        public static readonly Color SkyBelow = FromHex(0xC4C2BB);
        public static readonly Color Sunlight = FromHex(0xFFFAF2);

        // Interface.
        public static readonly Color PanelSteel = FromHex(0x1C2127);
        public static readonly Color RowSteel = FromHex(0x272D35);
        public static readonly Color SliderTrack = FromHex(0x0E1114);
        public static readonly Color SliderHandle = FromHex(0xE9ECEF);
        public static readonly Color PanelText = FromHex(0xEEF1F4);
        public static readonly Color PanelMutedText = FromHex(0xB4BCC6);

        private const float ByteMax = 255f;
        private const int RedShift = 16;
        private const int GreenShift = 8;
        private const int ByteMask = 0xFF;

        private static Color FromHex(int rgb)
        {
            return new Color(((rgb >> RedShift) & ByteMask) / ByteMax,
                ((rgb >> GreenShift) & ByteMask) / ByteMax, (rgb & ByteMask) / ByteMax);
        }
    }
}
