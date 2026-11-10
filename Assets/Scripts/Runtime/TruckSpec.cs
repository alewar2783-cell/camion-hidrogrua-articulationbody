using UnityEngine;
using UnityEngine.InputSystem;

namespace Truck
{
    public enum BodyId
    {
        ChasisRoot = 1,
        TorreBase,
        BrazoPrincipal,
        BrazoSecundario,
        TramoTelescopico,
        CabezalPinza,
        DedoIzquierdo,
        DedoDerecho,
        CajaVolcadora,
        CompuertaTrasera
    }

    public enum JointKind
    {
        Root,
        Revolute,
        Prismatic,
        Spherical
    }

    public enum ControlId
    {
        Torre,
        Pluma,
        BrazoSecundario,
        Telescopico,
        Pinza,
        Caja
    }

    /// <summary>Lower/Upper are degrees for angular joints and meters for prismatic ones.</summary>
    public readonly struct BodySpec
    {
        public BodySpec(
            BodyId id, string name, BodyId? parent, JointKind joint, float mass, Vector3 axis,
            float lower, float upper, float stiffness, float damping, float forceLimit,
            bool isPassive, ControlId? control)
        {
            Id = id;
            Name = name;
            Parent = parent;
            Joint = joint;
            Mass = mass;
            Axis = axis;
            Lower = lower;
            Upper = upper;
            Stiffness = stiffness;
            Damping = damping;
            ForceLimit = forceLimit;
            IsPassive = isPassive;
            Control = control;
        }

        public BodyId Id { get; }
        public string Name { get; }
        public BodyId? Parent { get; }
        public JointKind Joint { get; }
        public float Mass { get; }
        public Vector3 Axis { get; }
        public float Lower { get; }
        public float Upper { get; }
        public float Stiffness { get; }
        public float Damping { get; }
        public float ForceLimit { get; }
        public bool IsPassive { get; }
        public ControlId? Control { get; }
    }

    public readonly struct ControlSpec
    {
        public ControlSpec(
            ControlId id, string label, float min, float max, float speed, bool isAngular,
            Key[] decreaseKeys, Key[] increaseKeys)
        {
            Id = id;
            Label = label;
            Min = min;
            Max = max;
            Speed = speed;
            IsAngular = isAngular;
            DecreaseKeys = decreaseKeys;
            IncreaseKeys = increaseKeys;
        }

        public ControlId Id { get; }
        public string Label { get; }
        public float Min { get; }
        public float Max { get; }
        public float Speed { get; }
        public bool IsAngular { get; }
        public Key[] DecreaseKeys { get; }
        public Key[] IncreaseKeys { get; }
    }

    /// <summary>
    /// Single source of truth for the truck. Axes: X right, Y up, Z forward (cab at +Z),
    /// ground at y = 0. Distances in meters, angles in degrees.
    /// </summary>
    public static class TruckSpec
    {
        public static readonly BodySpec[] Bodies =
        {
            new BodySpec(BodyId.ChasisRoot, "Chasis_Root", null, JointKind.Root, 1000f,
                Vector3.zero, 0f, 0f, 0f, 0f, 0f, false, null),
            new BodySpec(BodyId.TorreBase, "Torre_Base", BodyId.ChasisRoot, JointKind.Revolute,
                150f, Vector3.up, -170f, 170f, 60000f, 6000f, 120000f, false, ControlId.Torre),
            new BodySpec(BodyId.BrazoPrincipal, "Brazo_Principal", BodyId.TorreBase,
                JointKind.Revolute, 100f, Vector3.right, -15f, 75f, 90000f, 9000f, 180000f,
                false, ControlId.Pluma),
            new BodySpec(BodyId.BrazoSecundario, "Brazo_Secundario", BodyId.BrazoPrincipal,
                JointKind.Revolute, 60f, Vector3.right, -125f, 15f, 65000f, 6500f, 110000f,
                false, ControlId.BrazoSecundario),
            new BodySpec(BodyId.TramoTelescopico, "Tramo_Telescopico", BodyId.BrazoSecundario,
                JointKind.Prismatic, 35f, Vector3.forward, 0f, 2.5f, 45000f, 4500f, 60000f,
                false, ControlId.Telescopico),
            new BodySpec(BodyId.CabezalPinza, "Cabezal_Pinza", BodyId.TramoTelescopico,
                JointKind.Spherical, 15f, Vector3.zero, -40f, 40f, 0f, 25f, 0f, true, null),
            new BodySpec(BodyId.DedoIzquierdo, "Dedo_Izquierdo", BodyId.CabezalPinza,
                JointKind.Prismatic, 5f, Vector3.right, 0f, 0.35f, 25000f, 2500f, 20000f,
                false, ControlId.Pinza),
            new BodySpec(BodyId.DedoDerecho, "Dedo_Derecho", BodyId.CabezalPinza,
                JointKind.Prismatic, 5f, Vector3.right, -0.35f, 0f, 25000f, 2500f, 20000f,
                false, ControlId.Pinza),
            new BodySpec(BodyId.CajaVolcadora, "Caja_Volcadora", BodyId.ChasisRoot,
                JointKind.Revolute, 250f, Vector3.right, 0f, 55f, 120000f, 12000f, 250000f,
                false, ControlId.Caja),
            new BodySpec(BodyId.CompuertaTrasera, "Compuerta_Trasera", BodyId.CajaVolcadora,
                JointKind.Revolute, 20f, Vector3.right, 0f, 95f, 0f, 10f, 0f, true, null)
        };

        public static readonly ControlSpec[] Controls =
        {
            new ControlSpec(ControlId.Torre, "Torre", -170f, 170f, 45f, true,
                new[] { Key.A }, new[] { Key.D }),
            new ControlSpec(ControlId.Pluma, "Pluma principal", -15f, 75f, 30f, true,
                new[] { Key.S }, new[] { Key.W }),
            new ControlSpec(ControlId.BrazoSecundario, "Brazo secundario", -125f, 15f, 35f, true,
                new[] { Key.F }, new[] { Key.R }),
            new ControlSpec(ControlId.Telescopico, "Telescópico", 0f, 2.5f, 0.8f, false,
                new[] { Key.G }, new[] { Key.T }),
            new ControlSpec(ControlId.Pinza, "Pinza", 0f, 0.35f, 0.4f, false,
                new[] { Key.Q }, new[] { Key.E }),
            new ControlSpec(ControlId.Caja, "Caja volcadora", 0f, 55f, 20f, true,
                new[] { Key.DownArrow, Key.Z }, new[] { Key.UpArrow, Key.X })
        };

        public static BodySpec Get(BodyId id)
        {
            return Bodies[(int)id - 1];
        }

        public static ControlSpec Get(ControlId id)
        {
            return Controls[(int)id];
        }

        public static class Geometry
        {
            public static readonly Vector3 ChassisSize = new Vector3(2.4f, 0.4f, 7f);
            public static readonly Vector3 ChassisPivot = new Vector3(0f, 0.5f, 0f);
            public static readonly Vector3 CabinSize = new Vector3(2.2f, 1.6f, 1.6f);
            public const float CabinCenterZ = 2.6f;

            public static readonly Vector3 BedHinge = new Vector3(0f, 0.8f, -3.5f);
            public const float BedLength = 3.5f;
            public const float BedWidth = 2.2f;
            public const float BedWallHeight = 0.9f;

            public static readonly Vector3 TowerPivot = new Vector3(0f, 0.7f, 1.4f);
            public const float TowerHeight = 1.5f;
            public const float BoomLength = 3f;
            public const float JibLength = 1.5f;
            public const float TelescopeLength = 0.5f;
            public const float GripperOpening = 0.8f;

            /// <summary>
            /// Pitch of the telescopic section, in degrees above horizontal, at which the gripper
            /// hangs at the center of its swing cone.
            /// </summary>
            public const float HeadCenterElevation = -30f;
            public static readonly Vector3 PalmSize = new Vector3(1.1f, 0.3f, 0.5f);
            public static readonly Vector3 FingerSize = new Vector3(0.1f, 0.6f, 0.4f);

            public const float GroundSize = 80f;
            public static readonly Vector3 YardCenter = new Vector3(0f, 0f, -12f);
            public static readonly Vector2 YardSize = new Vector2(14f, 8f);

            public const float TailgateWidth = 2.2f;
            public const float TailgateHeight = 0.9f;
            public static readonly Vector3 TailgateHinge =
                new Vector3(0f, BedHinge.y + BedWallHeight, BedHinge.z);
        }

        /// <summary>
        /// Target sets in ControlId order (Torre, Pluma, BrazoSecundario, Telescopico, Pinza,
        /// Caja). Angles in degrees, lengths in meters. Found by forward kinematics and checked
        /// in play mode; see docs/CALIBRACION.md.
        /// </summary>
        public static class Poses
        {
            /// <summary>Gripper head over the ground, about 4.5 m right of the truck.</summary>
            public static readonly float[] GroundReach = { -90f, 32f, -81f, 1f, 0f, 0f };

            /// <summary>Gripper head about 1 m over the center of the bed floor.</summary>
            public static readonly float[] BedReach = { 0f, 38f, -105f, 0f, 0f, 0f };

            /// <summary>Boom raised and jib folded back: clears the bed while it tilts.</summary>
            public static readonly float[] Stowed = { 0f, 70f, -45f, 0f, 0f, 0f };
        }

        public static class Materials
        {
            public const float GripDynamicFriction = 0.9f;
            public const float GripStaticFriction = 0.95f;
            public const float SteelDynamicFriction = 0.25f;
            public const float SteelStaticFriction = 0.3f;
        }

        public static class Loads
        {
            public const float CubeSize = 0.5f;
            public const float CubeMass = 8f;

            public static readonly Vector3[] GroundSpawn =
            {
                new Vector3(4.2f, 0.25f, 0.4f),
                new Vector3(5.2f, 0.25f, 0.9f),
                new Vector3(4.2f, 0.25f, 1.4f),
                new Vector3(5.2f, 0.25f, 1.9f),
                new Vector3(4.2f, 0.25f, 2.4f)
            };

            public static readonly Vector3[] BedSpawn =
            {
                new Vector3(-0.55f, 1.1f, -2.8f),
                new Vector3(0.55f, 1.1f, -2.8f),
                new Vector3(-0.55f, 1.1f, -1.8f),
                new Vector3(0.55f, 1.1f, -1.8f)
            };
        }
    }
}
