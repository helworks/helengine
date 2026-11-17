namespace helengine {
    /// <summary>Constrains descendant pointer hits using an application-defined visible region, including nonrectangular occlusion.</summary>
    public interface IPointerHitRegion2D {
        /// <summary>Tests a pointer in the same resolved drawable coordinates used by descendant controls. Each pointer is evaluated independently.</summary>
        bool ContainsPointer(int pointerX, int pointerY);
    }
}
