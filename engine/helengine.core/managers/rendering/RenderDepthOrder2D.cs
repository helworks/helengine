namespace helengine {
    /// <summary>Orders screen-space content by composed depth, then scene hierarchy and component order.</summary>
    public static class RenderDepthOrder2D {
        /// <summary>Compares two drawables in back-to-front drawing order without a separate priority value.</summary>
        /// <param name="left">First drawable.</param>
        /// <param name="right">Second drawable.</param>
        /// <returns>A negative value when the first drawable should be drawn earlier.</returns>
        public static int CompareDrawables(IDrawable2D left, IDrawable2D right) {
            if (ReferenceEquals(left, right)) {
                return 0;
            }
            Entity leftEntity = left != null ? left.Parent : null;
            Entity rightEntity = right != null ? right.Parent : null;
            int comparison = CompareEntities(leftEntity, rightEntity);
            if (comparison != 0 || leftEntity == null || !ReferenceEquals(leftEntity, rightEntity)) {
                return comparison;
            }
            if (left is Component leftComponent && right is Component rightComponent) {
                return leftEntity.Components.IndexOf(leftComponent).CompareTo(leftEntity.Components.IndexOf(rightComponent));
            }
            return 0;
        }

        /// <summary>Compares composed Z first, with parents and earlier siblings behind later descendants.</summary>
        /// <param name="left">First entity.</param>
        /// <param name="right">Second entity.</param>
        /// <returns>A negative value when the first entity lies behind the second.</returns>
        public static int CompareEntities(Entity left, Entity right) {
            if (ReferenceEquals(left, right)) {
                return 0;
            }
            if (left == null) {
                return -1;
            } else if (right == null) {
                return 1;
            }
            int comparison = left.Position.Z.CompareTo(right.Position.Z);
            return comparison != 0 ? comparison : CompareHierarchy(left, right);
        }

        /// <summary>Compares hierarchy preorder independently of registration order or depth.</summary>
        /// <param name="left">First entity in the scene hierarchy.</param>
        /// <param name="right">Second entity in the scene hierarchy.</param>
        /// <returns>Hierarchy order, with root ties resolved by the owning object manager's entity order.</returns>
        public static int CompareHierarchy(Entity left, Entity right) {
            if (ReferenceEquals(left, right)) {
                return 0;
            }
            if (left == null) {
                return -1;
            } else if (right == null) {
                return 1;
            }
            int leftDepth = GetHierarchyDepth(left);
            int rightDepth = GetHierarchyDepth(right);
            int depthDifference = leftDepth.CompareTo(rightDepth);
            while (leftDepth > rightDepth) {
                left = left.Parent;
                leftDepth--;
            }
            while (rightDepth > leftDepth) {
                right = right.Parent;
                rightDepth--;
            }
            if (ReferenceEquals(left, right)) {
                return depthDifference;
            }
            while (!ReferenceEquals(left.Parent, right.Parent)) {
                left = left.Parent;
                right = right.Parent;
            }
            if (left.Parent != null) {
                return left.Parent.Children.IndexOf(left).CompareTo(left.Parent.Children.IndexOf(right));
            }
            return left.OwnerCore.ObjectManager.Entities.IndexOf(left).CompareTo(right.OwnerCore.ObjectManager.Entities.IndexOf(right));
        }

        /// <summary>Counts ancestors without allocating a temporary hierarchy path.</summary>
        /// <param name="entity">Entity whose ancestry is counted.</param>
        /// <returns>Number of parents between the entity and its root.</returns>
        static int GetHierarchyDepth(Entity entity) {
            int depth = 0;
            while (entity.Parent != null) {
                entity = entity.Parent;
                depth++;
            }
            return depth;
        }
    }
}
