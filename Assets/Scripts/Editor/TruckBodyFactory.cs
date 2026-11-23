using System;
using UnityEngine;
using G = Truck.TruckSpec.Geometry;

namespace Truck
{
    public static class TruckBodyFactory
    {
        public const int SolverIterations = 16;
        public const int SolverVelocityIterations = 4;

        // Arm links point along their local +Z. Rotating 180 degrees around X aims +Z toward the
        // rear of the truck and keeps +X to the right, so a positive angle raises the arm.
        private static readonly Quaternion ArmRotation = Quaternion.Euler(180f, 0f, 0f);

        // The gripper is built tilted so that its swing cone is centered on the pitch the
        // telescopic section has when picking from the ground; gravity then hangs it vertical.
        private static readonly Quaternion GripperRotation =
            Quaternion.Euler(-G.HeadCenterElevation, 0f, 0f);

        // The bed hinge is at the rear and the bed extends forward. A positive rotation around
        // local +X must raise the front, which needs the local X axis to point to world -X.
        private static readonly Quaternion BedRotation = Quaternion.Euler(0f, 180f, 0f);

        private const float TowerWidth = 0.6f;
        private const float BoomThickness = 0.35f;
        private const float JibThickness = 0.3f;
        private const float TelescopeThickness = 0.22f;
        private const float WallThickness = 0.1f;
        private const float FloorThickness = 0.1f;
        private const float TailgateThickness = 0.08f;
        private const float WheelWidth = 0.3f;

        // Visual-only accents (no collider): a window band around the top of the cabin and a
        // hazard-striped front bumper.
        private const float WindowHeightFraction = 0.38f;
        private const float WindowDepthFraction = 0.6f;
        private const float WindowCenterFraction = 0.22f;
        private const float WindowInset = 0.03f;
        private const float BumperHeight = 0.26f;
        private const float BumperDepth = 0.16f;
        private const float BumperOverhang = 0.12f;
        private const float BumperDrop = 0.06f;

        public static ArticulationBody CreateBody(BodySpec spec, Transform parent, BuildMode mode,
            PhysicsMaterial grip, PhysicsMaterial steel)
        {
            var node = new GameObject(spec.Name);
            node.transform.SetParent(parent, false);
            ComputePose(spec.Id, parent, out Vector3 position, out Quaternion rotation);
            node.transform.SetPositionAndRotation(position, rotation);

            AddVisuals(spec.Id, node.transform, grip, steel);

            var body = node.AddComponent<ArticulationBody>();
            Configure(body, spec, parent, mode);
            return body;
        }

        public static GameObject CreateBox(string name, Transform parent, Vector3 localPosition,
            Vector3 size, Material look, PhysicsMaterial material, bool hasCollider = true)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = localPosition;
            box.transform.localScale = size;
            box.GetComponent<MeshRenderer>().sharedMaterial = look;

            var collider = box.GetComponent<BoxCollider>();
            if (hasCollider)
            {
                collider.sharedMaterial = material;
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }

            return box;
        }

        private static void ComputePose(BodyId id, Transform parent, out Vector3 position,
            out Quaternion rotation)
        {
            rotation = Quaternion.identity;
            switch (id)
            {
                case BodyId.ChasisRoot:
                    position = G.ChassisPivot;
                    break;
                case BodyId.TorreBase:
                    position = G.TowerPivot;
                    break;
                case BodyId.BrazoPrincipal:
                    position = parent.position + Vector3.up * G.TowerHeight;
                    rotation = ArmRotation;
                    break;
                case BodyId.BrazoSecundario:
                    position = parent.TransformPoint(
                        Vector3.forward * G.BoomLength);
                    rotation = ArmRotation;
                    break;
                case BodyId.TramoTelescopico:
                    position = parent.TransformPoint(
                        Vector3.forward * G.JibLength);
                    rotation = ArmRotation;
                    break;
                case BodyId.CabezalPinza:
                    position = parent.TransformPoint(
                        Vector3.forward * G.TelescopeLength);
                    rotation = GripperRotation;
                    break;
                case BodyId.DedoIzquierdo:
                    position = parent.TransformPoint(FingerOffset(-1f));
                    rotation = GripperRotation;
                    break;
                case BodyId.DedoDerecho:
                    position = parent.TransformPoint(FingerOffset(1f));
                    rotation = GripperRotation;
                    break;
                case BodyId.CajaVolcadora:
                    position = G.BedHinge;
                    rotation = BedRotation;
                    break;
                case BodyId.CompuertaTrasera:
                    position = G.TailgateHinge;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(id), id, null);
            }
        }

        private static Vector3 FingerOffset(float side)
        {
            Vector3 palm = G.PalmSize;
            Vector3 finger = G.FingerSize;
            float x = side * (G.GripperOpening * 0.5f + finger.x * 0.5f);
            return new Vector3(x, -(palm.y + finger.y * 0.5f), 0f);
        }

        private static void Configure(ArticulationBody body, BodySpec spec, Transform parent,
            BuildMode mode)
        {
            body.mass = spec.Mass;
            body.useGravity = true;
            body.solverIterations = SolverIterations;
            body.solverVelocityIterations = SolverVelocityIterations;

            if (spec.Joint == JointKind.Root)
            {
                return;
            }

            // Motion always happens around or along the anchor's local X axis. The spherical
            // joint uses local Y as its swing cone axis, so the gripper hangs along its -Y.
            Vector3 motionAxis = spec.Joint == JointKind.Spherical ? Vector3.up : spec.Axis;
            Quaternion anchor = Quaternion.FromToRotation(Vector3.right, motionAxis);
            body.anchorPosition = Vector3.zero;
            body.anchorRotation = anchor;
            body.parentAnchorPosition = parent.InverseTransformPoint(body.transform.position);
            body.parentAnchorRotation =
                Quaternion.Inverse(parent.rotation) * body.transform.rotation * anchor;

            float stiffness = mode == BuildMode.Full ? spec.Stiffness : 0f;
            // A zero force limit in the spec marks a passive joint; the drive still has to
            // apply its damping, so it must not be clamped to zero force.
            float forceLimit = spec.ForceLimit > 0f ? spec.ForceLimit : float.MaxValue;
            ArticulationDrive drive = CreateDrive(spec, stiffness, forceLimit);

            switch (spec.Joint)
            {
                case JointKind.Revolute:
                    body.jointType = ArticulationJointType.RevoluteJoint;
                    body.twistLock = ArticulationDofLock.LimitedMotion;
                    body.xDrive = drive;
                    break;
                case JointKind.Prismatic:
                    body.jointType = ArticulationJointType.PrismaticJoint;
                    body.linearLockX = ArticulationDofLock.LimitedMotion;
                    body.linearLockY = ArticulationDofLock.LockedMotion;
                    body.linearLockZ = ArticulationDofLock.LockedMotion;
                    body.xDrive = drive;
                    break;
                case JointKind.Spherical:
                    body.jointType = ArticulationJointType.SphericalJoint;
                    body.twistLock = ArticulationDofLock.LockedMotion;
                    body.swingYLock = ArticulationDofLock.LimitedMotion;
                    body.swingZLock = ArticulationDofLock.LimitedMotion;
                    body.yDrive = drive;
                    body.zDrive = drive;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(spec), spec.Joint, null);
            }
        }

        private static ArticulationDrive CreateDrive(BodySpec spec, float stiffness,
            float forceLimit)
        {
            return new ArticulationDrive
            {
                lowerLimit = spec.Lower,
                upperLimit = spec.Upper,
                stiffness = stiffness,
                damping = spec.Damping,
                forceLimit = forceLimit,
                target = 0f,
                targetVelocity = 0f
            };
        }

        private static void AddVisuals(BodyId id, Transform node, PhysicsMaterial grip,
            PhysicsMaterial steel)
        {
            switch (id)
            {
                case BodyId.ChasisRoot:
                    AddChassisVisuals(node);
                    break;
                case BodyId.TorreBase:
                    CreateBox("Visual_Torre", node,
                        new Vector3(0f, G.TowerHeight * 0.5f, 0f),
                        new Vector3(TowerWidth, G.TowerHeight, TowerWidth),
                        TruckMaterials.Crane(), null);
                    break;
                case BodyId.BrazoPrincipal:
                    AddArmVisual("Visual_Pluma", node, G.BoomLength, BoomThickness,
                        TruckMaterials.Crane());
                    break;
                case BodyId.BrazoSecundario:
                    AddArmVisual("Visual_Secundario", node, G.JibLength, JibThickness,
                        TruckMaterials.Crane());
                    break;
                case BodyId.TramoTelescopico:
                    AddArmVisual("Visual_Telescopico", node, G.TelescopeLength,
                        TelescopeThickness, TruckMaterials.Telescope());
                    AddTelescopeRod(node);
                    break;
                case BodyId.CabezalPinza:
                    CreateBox("Visual_Cabezal", node,
                        new Vector3(0f, -G.PalmSize.y * 0.5f, 0f), G.PalmSize,
                        TruckMaterials.Gripper(), null);
                    break;
                case BodyId.DedoIzquierdo:
                    CreateBox("Visual_DedoIzquierdo", node, Vector3.zero, G.FingerSize,
                        TruckMaterials.Gripper(), grip);
                    break;
                case BodyId.DedoDerecho:
                    CreateBox("Visual_DedoDerecho", node, Vector3.zero, G.FingerSize,
                        TruckMaterials.Gripper(), grip);
                    break;
                case BodyId.CajaVolcadora:
                    AddBedVisuals(node, steel);
                    break;
                case BodyId.CompuertaTrasera:
                    CreateBox("Visual_Compuerta", node,
                        new Vector3(0f, -G.TailgateHeight * 0.5f, 0f),
                        new Vector3(G.TailgateWidth, G.TailgateHeight, TailgateThickness),
                        TruckMaterials.Hazard(), null);
                    break;
            }
        }

        private static void AddChassisVisuals(Transform node)
        {
            Vector3 chassis = G.ChassisSize;
            Vector3 cabin = G.CabinSize;
            CreateBox("Visual_Chasis", node, Vector3.zero, chassis, TruckMaterials.Chassis(),
                null);
            var cabinCenter = new Vector3(0f, (chassis.y + cabin.y) * 0.5f,
                G.CabinCenterZ - G.ChassisPivot.z);
            CreateBox("Visual_Cabina", node, cabinCenter, cabin, TruckMaterials.Cabin(), null);
            AddCabinWindows(node, cabinCenter, cabin);
            AddBumper(node, chassis);

            float radius = G.ChassisPivot.y;
            float x = chassis.x * 0.5f + WheelWidth * 0.5f;
            float z = chassis.z * 0.5f - 1.3f;
            foreach (float side in new[] { -1f, 1f })
            {
                foreach (float axle in new[] { -1f, 1f })
                {
                    AddWheel(node, new Vector3(side * x, 0f, axle * z), radius);
                }
            }
        }

        private static void AddWheel(Transform node, Vector3 localPosition, float radius)
        {
            GameObject wheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            wheel.name = "Visual_Rueda";
            UnityEngine.Object.DestroyImmediate(wheel.GetComponent<Collider>());
            wheel.transform.SetParent(node, false);
            wheel.transform.localPosition = localPosition;
            wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            wheel.transform.localScale = new Vector3(radius * 2f, WheelWidth * 0.5f, radius * 2f);
            wheel.GetComponent<MeshRenderer>().sharedMaterial = TruckMaterials.Tyre();
        }

        // The window band pokes out of the cabin's front and sides, so it reads as glass from
        // any orbit angle. It has no collider: it only changes the look.
        private static void AddCabinWindows(Transform node, Vector3 cabinCenter, Vector3 cabin)
        {
            float depth = cabin.z * WindowDepthFraction;
            var center = new Vector3(0f, cabinCenter.y + cabin.y * WindowCenterFraction,
                cabinCenter.z + (cabin.z - depth) * 0.5f + WindowInset);
            var size = new Vector3(cabin.x + WindowInset * 2f, cabin.y * WindowHeightFraction,
                depth);
            CreateBox("Visual_Ventanas", node, center, size, TruckMaterials.Glass(), null, false);
        }

        private static void AddBumper(Transform node, Vector3 chassis)
        {
            var center = new Vector3(0f, -BumperDrop, (chassis.z + BumperDepth) * 0.5f);
            var size = new Vector3(chassis.x + BumperOverhang, BumperHeight, BumperDepth);
            CreateBox("Visual_Paragolpes", node, center, size, TruckMaterials.Hazard(), null,
                false);
        }

        private static void AddArmVisual(string name, Transform node, float length,
            float thickness, Material look)
        {
            CreateBox(name, node, new Vector3(0f, 0f, length * 0.5f),
                new Vector3(thickness, thickness, length), look, null);
        }

        // Visual only: keeps the extended section visually attached to the jib. It has no
        // collider because it would overlap the jib and boom, which are not its neighbors.
        private static void AddTelescopeRod(Transform node)
        {
            float travel = TruckSpec.Get(BodyId.TramoTelescopico).Upper;
            CreateBox("Visual_VarillaTelescopica", node,
                new Vector3(0f, 0f, -travel * 0.5f),
                new Vector3(TelescopeThickness * 0.8f, TelescopeThickness * 0.8f, travel),
                TruckMaterials.Telescope(), null, false);
        }

        private static void AddBedVisuals(Transform node, PhysicsMaterial steel)
        {
            float length = G.BedLength;
            float width = G.BedWidth;
            float wallHeight = G.BedWallHeight;
            float wallCenterY = FloorThickness * 0.5f + wallHeight * 0.5f;
            // The bed extends toward local -Z (see BedRotation).
            float centerZ = -length * 0.5f;

            CreateBox("Visual_PisoCaja", node, new Vector3(0f, 0f, centerZ),
                new Vector3(width, FloorThickness, length), TruckMaterials.Bed(), steel);
            float wallX = (width - WallThickness) * 0.5f;
            CreateBox("Visual_ParedIzquierda", node, new Vector3(-wallX, wallCenterY, centerZ),
                new Vector3(WallThickness, wallHeight, length), TruckMaterials.Bed(), null);
            CreateBox("Visual_ParedDerecha", node, new Vector3(wallX, wallCenterY, centerZ),
                new Vector3(WallThickness, wallHeight, length), TruckMaterials.Bed(), null);
            CreateBox("Visual_FrenteCaja", node,
                new Vector3(0f, wallCenterY, -length + WallThickness * 0.5f),
                new Vector3(width, wallHeight, WallThickness), TruckMaterials.Bed(), null);
        }
    }
}
