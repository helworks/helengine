namespace helengine {
    /// <summary>
    /// Describes the deterministic minimum-penetration axis shared by two overlapping oriented boxes.
    /// </summary>
    readonly struct HelPhysicsBoxSatResult3D {
        /// <summary>
        /// Stores the unit world-space collision normal directed from query box A toward query box B.
        /// </summary>
        public readonly PhysicsVector3 Normal;

        /// <summary>
        /// Stores the ordered SAT axis family that supplied the minimum penetration.
        /// </summary>
        public readonly HelPhysicsBoxSatAxisKind3D AxisKind;

        /// <summary>
        /// Stores the local A-axis index for an A face or edge pair, or negative one when the winning axis uses only a B face.
        /// </summary>
        public readonly int AxisAIndex;

        /// <summary>
        /// Stores the local B-axis index for a B face or edge pair, or negative one when the winning axis uses only an A face.
        /// </summary>
        public readonly int AxisBIndex;

        /// <summary>
        /// Stores the non-negative overlap distance measured along the normalized winning axis.
        /// </summary>
        public readonly PhysicsScalar PenetrationDepth;

        /// <summary>
        /// Initializes one successful oriented-box SAT result with its manifold-generation metadata.
        /// </summary>
        /// <param name="normal">Unit world-space normal directed from box A toward box B.</param>
        /// <param name="axisKind">Ordered SAT axis family that supplied the minimum penetration.</param>
        /// <param name="axisAIndex">A local axis index, or negative one when the winning axis uses only a B face.</param>
        /// <param name="axisBIndex">B local axis index, or negative one when the winning axis uses only an A face.</param>
        /// <param name="penetrationDepth">Non-negative overlap distance along <paramref name="normal"/>.</param>
        public HelPhysicsBoxSatResult3D(
            PhysicsVector3 normal,
            HelPhysicsBoxSatAxisKind3D axisKind,
            int axisAIndex,
            int axisBIndex,
            PhysicsScalar penetrationDepth) {
            Normal = normal;
            AxisKind = axisKind;
            AxisAIndex = axisAIndex;
            AxisBIndex = axisBIndex;
            PenetrationDepth = penetrationDepth;
        }
    }
}
