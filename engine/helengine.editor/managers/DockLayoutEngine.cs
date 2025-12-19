using System;
using System.Collections.Generic;

namespace helengine.editor {
    /// <summary>
    /// Lays out dockable editor entities using a split-tree layout similar to Unity's docking system.
    /// </summary>
    public class DockLayoutEngine {
        const int EdgeMinThreshold = 16;
        const float EdgeBandFraction = 0.15f;
        const float CenterFraction = 0.4f;
        const int CenterMinSize = 24;
        const int PreviewMargin = 8;

        readonly List<DockableEntity> dockables;
        readonly int padding;
        readonly int gap;

        LayoutNode? root;

        /// <summary>
        /// Creates a new layout engine with optional padding and gap values.
        /// </summary>
        /// <param name="padding">Space to leave around the host bounds.</param>
        /// <param name="gap">Space inserted between docked panels.</param>
        public DockLayoutEngine(int padding = 0, int gap = 0) {
            this.padding = padding;
            this.gap = gap;
            dockables = new List<DockableEntity>(8);
        }

        /// <summary>
        /// Gets the dockable entities currently tracked by the layout engine.
        /// </summary>
        public IReadOnlyList<DockableEntity> Dockables => dockables;

        /// <summary>
        /// Gets a value indicating whether any panel is currently docked.
        /// </summary>
        public bool HasDocked => root != null;

        /// <summary>
        /// Adds a dockable entity to the layout list if it is not already tracked.
        /// </summary>
        /// <param name="entity">Entity to add.</param>
        public void Add(DockableEntity entity) {
            if (!dockables.Contains(entity)) {
                dockables.Add(entity);
            }
        }

        /// <summary>
        /// Removes a dockable entity from tracking and undocks it.
        /// </summary>
        /// <param name="entity">Entity to remove.</param>
        /// <returns>True if the entity was removed; otherwise false.</returns>
        public bool Remove(DockableEntity entity) {
            bool removed = dockables.Remove(entity);
            if (root != null) {
                root = root.Remove(entity);
            }

            entity.IsDocked = false;
            return removed;
        }

        /// <summary>
        /// Undocks the specified entity and reflows the layout tree.
        /// </summary>
        /// <param name="entity">Entity to undock.</param>
        public void Undock(DockableEntity entity) {
            if (root != null) {
                root = root.Remove(entity);
            }

            entity.IsDocked = false;
        }

        /// <summary>
        /// Docks an entity as the root panel, replacing any existing layout.
        /// </summary>
        /// <param name="entity">Entity to dock as root.</param>
        public void DockAsRoot(DockableEntity entity) {
            root = new PanelNode(entity);
            entity.IsDocked = true;
        }

        /// <summary>
        /// Docks an entity relative to an anchor panel in the specified direction.
        /// </summary>
        /// <param name="entity">Entity to dock.</param>
        /// <param name="anchor">Anchor panel to split.</param>
        /// <param name="direction">Direction to insert.</param>
        /// <param name="splitFraction">Fraction of the anchor allocated to the incoming panel.</param>
        public void DockRelative(DockableEntity entity, DockableEntity anchor, DockInsertDirection direction, float splitFraction = 0.5f) {
            Dock(entity, new DockHint(direction, anchor, float3.Zero, new int2(0, 0), splitFraction));
        }

        /// <summary>
        /// Docks an entity using a prepared hint generated from a pointer preview.
        /// </summary>
        /// <param name="entity">Entity to dock.</param>
        /// <param name="hint">Docking hint describing the target and direction.</param>
        public void Dock(DockableEntity entity, DockHint hint) {
            if (entity == null) {
                return;
            }

            if (root != null) {
                root = root.Remove(entity);
            }

            root = InsertDock(root, entity, hint);
            entity.IsDocked = true;
        }

        /// <summary>
        /// Performs layout using the host size at the origin.
        /// </summary>
        /// <param name="hostSize">Size of the host area.</param>
        public void Layout(int2 hostSize) {
            Layout(hostSize, float3.Zero);
        }

        /// <summary>
        /// Performs layout using the host size and a specified origin.
        /// </summary>
        /// <param name="hostSize">Size of the host area.</param>
        /// <param name="origin">Origin point of the host area.</param>
        public void Layout(int2 hostSize, float3 origin) {
            if (root == null) {
                return;
            }

            float left = origin.X + padding;
            float top = origin.Y + padding;
            float right = origin.X + hostSize.X - padding;
            float bottom = origin.Y + hostSize.Y - padding;

            if (right <= left || bottom <= top) {
                return;
            }

            root.Layout(left, top, right, bottom, origin.Z, gap);
        }

        /// <summary>
        /// Calculates a dock hint based on a pointer position within the host area.
        /// </summary>
        /// <param name="pointer">Pointer position in screen or host coordinates.</param>
        /// <param name="hostSize">Size of the host area.</param>
        /// <param name="origin">Origin of the host area.</param>
        /// <param name="fillOnly">True to only allow fill hints.</param>
        /// <param name="hint">Resulting docking hint.</param>
        /// <returns>True if a dock hint could be determined; otherwise false.</returns>
        public bool TryGetDockHint(int2 pointer, int2 hostSize, float3 origin, bool fillOnly, out DockHint hint) {
            hint = default;

            if (hostSize.X <= 0 || hostSize.Y <= 0) {
                return false;
            }

            float hostLeft = origin.X + padding;
            float hostTop = origin.Y + padding;
            float hostRight = origin.X + hostSize.X - padding;
            float hostBottom = origin.Y + hostSize.Y - padding;

            if (pointer.X < hostLeft || pointer.Y < hostTop || pointer.X > hostRight || pointer.Y > hostBottom) {
                return false;
            }

            PanelNode? anchorNode = root?.Hit(pointer.X, pointer.Y);
            DockableEntity? anchorEntity = anchorNode?.Entity;

            float targetLeft = anchorNode?.Bounds.X ?? hostLeft;
            float targetTop = anchorNode?.Bounds.Y ?? hostTop;
            float targetWidth = anchorNode?.Bounds.Z ?? (hostRight - hostLeft);
            float targetHeight = anchorNode?.Bounds.W ?? (hostBottom - hostTop);

            float localX = pointer.X - targetLeft;
            float localY = pointer.Y - targetTop;

            int centerWidth = Math.Max(CenterMinSize, (int)MathF.Round(targetWidth * CenterFraction));
            int centerHeight = Math.Max(CenterMinSize, (int)MathF.Round(targetHeight * CenterFraction));
            float centerStartX = targetLeft + (targetWidth - centerWidth) * 0.5f;
            float centerStartY = targetTop + (targetHeight - centerHeight) * 0.5f;
            float centerEndX = centerStartX + centerWidth;
            float centerEndY = centerStartY + centerHeight;

            if (fillOnly) {
                int previewWidth = Math.Max(1, (int)(targetWidth - PreviewMargin * 2));
                int previewHeight = Math.Max(1, (int)(targetHeight - PreviewMargin * 2));
                float3 pos = new float3(targetLeft + PreviewMargin, targetTop + PreviewMargin, origin.Z);
                hint = new DockHint(DockInsertDirection.Fill, anchorEntity, pos, new int2(previewWidth, previewHeight), 1f);
                return true;
            }

            int horizontalBand = Math.Max(EdgeMinThreshold, (int)MathF.Round(targetWidth * EdgeBandFraction));
            int verticalBand = Math.Max(EdgeMinThreshold, (int)MathF.Round(targetHeight * EdgeBandFraction));

            if (localX <= horizontalBand) {
                int width = Math.Max(1, (int)MathF.Round(targetWidth * 0.5f) - PreviewMargin * 2);
                int height = Math.Max(1, (int)(targetHeight - PreviewMargin * 2));
                float3 pos = new float3(targetLeft + PreviewMargin, targetTop + PreviewMargin, origin.Z);
                hint = new DockHint(DockInsertDirection.Left, anchorEntity, pos, new int2(width, height));
                return true;
            }

            if (localX >= targetWidth - horizontalBand) {
                int width = Math.Max(1, (int)MathF.Round(targetWidth * 0.5f) - PreviewMargin * 2);
                int height = Math.Max(1, (int)(targetHeight - PreviewMargin * 2));
                float3 pos = new float3(targetLeft + targetWidth - MathF.Round(targetWidth * 0.5f) + PreviewMargin, targetTop + PreviewMargin, origin.Z);
                hint = new DockHint(DockInsertDirection.Right, anchorEntity, pos, new int2(width, height));
                return true;
            }

            if (localY <= verticalBand) {
                int width = Math.Max(1, (int)(targetWidth - PreviewMargin * 2));
                int height = Math.Max(1, (int)MathF.Round(targetHeight * 0.5f) - PreviewMargin * 2);
                float3 pos = new float3(targetLeft + PreviewMargin, targetTop + PreviewMargin, origin.Z);
                hint = new DockHint(DockInsertDirection.Top, anchorEntity, pos, new int2(width, height));
                return true;
            }

            if (localY >= targetHeight - verticalBand) {
                int width = Math.Max(1, (int)(targetWidth - PreviewMargin * 2));
                int height = Math.Max(1, (int)MathF.Round(targetHeight * 0.5f) - PreviewMargin * 2);
                float3 pos = new float3(targetLeft + PreviewMargin, targetTop + targetHeight - MathF.Round(targetHeight * 0.5f) + PreviewMargin, origin.Z);
                hint = new DockHint(DockInsertDirection.Bottom, anchorEntity, pos, new int2(width, height));
                return true;
            }

            if (pointer.X >= centerStartX && pointer.X <= centerEndX &&
                pointer.Y >= centerStartY && pointer.Y <= centerEndY) {
                int width = Math.Max(1, (int)(centerWidth - PreviewMargin * 2));
                int height = Math.Max(1, (int)(centerHeight - PreviewMargin * 2));
                float3 pos = new float3(centerStartX + PreviewMargin, centerStartY + PreviewMargin, origin.Z);
                hint = new DockHint(DockInsertDirection.Fill, anchorEntity, pos, new int2(width, height));
                return true;
            }

            return false;
        }

        LayoutNode InsertDock(LayoutNode? currentRoot, DockableEntity entity, DockHint hint) {
            PanelNode newPanel = new PanelNode(entity);

            if (currentRoot == null) {
                return newPanel;
            }

            PanelNode? anchorNode = hint.Anchor != null ? currentRoot.Find(hint.Anchor) : currentRoot.FirstLeaf();
            if (anchorNode == null) {
                anchorNode = currentRoot.FirstLeaf();
            }

            if (anchorNode == null) {
                return newPanel;
            }

            LayoutNode replacement;
            switch (hint.Direction) {
                case DockInsertDirection.Left:
                    replacement = new SplitNode(isVertical: true, hint.SplitFraction, newPanel, anchorNode);
                    break;
                case DockInsertDirection.Right:
                    replacement = new SplitNode(isVertical: true, hint.SplitFraction, anchorNode, newPanel);
                    break;
                case DockInsertDirection.Top:
                    replacement = new SplitNode(isVertical: false, hint.SplitFraction, newPanel, anchorNode);
                    break;
                case DockInsertDirection.Bottom:
                    replacement = new SplitNode(isVertical: false, hint.SplitFraction, anchorNode, newPanel);
                    break;
                case DockInsertDirection.Fill:
                default:
                    anchorNode.Entity.IsDocked = false;
                    anchorNode.Entity = entity;
                    anchorNode.Entity.IsDocked = true;
                    return currentRoot;
            }

            return Replace(currentRoot, anchorNode, replacement);
        }

        static LayoutNode Replace(LayoutNode node, PanelNode target, LayoutNode replacement) {
            if (ReferenceEquals(node, target)) {
                return replacement;
            }

            if (node is SplitNode split) {
                split.First = Replace(split.First, target, replacement);
                split.Second = Replace(split.Second, target, replacement);
            }

            return node;
        }

        /// <summary>
        /// Represents a node in the docking layout tree.
        /// </summary>
        abstract class LayoutNode {
            public abstract void Layout(float left, float top, float right, float bottom, float z, int gap);
            public abstract PanelNode? Hit(float x, float y);
            public abstract LayoutNode? Remove(DockableEntity entity);
            public abstract PanelNode? Find(DockableEntity entity);
            public abstract PanelNode? FirstLeaf();
        }

        /// <summary>
        /// Represents a docked panel leaf node.
        /// </summary>
        sealed class PanelNode : LayoutNode {
            public PanelNode(DockableEntity entity) {
                Entity = entity;
                Bounds = new float4(0, 0, 0, 0);
            }

            public DockableEntity Entity { get; set; }

            public float4 Bounds { get; private set; }

            public override void Layout(float left, float top, float right, float bottom, float z, int gap) {
                float width = Math.Max(1, right - left);
                float height = Math.Max(1, bottom - top);
                Bounds = new float4(left, top, width, height);

                int targetWidth = Math.Max(1, (int)MathF.Round(width));
                int targetHeight = Math.Max(1, (int)MathF.Round(height - DockableEntity.TitleBarHeight));

                Entity.Position = new float3(left, top, z);
                Entity.Size = new int2(targetWidth, targetHeight);
                Entity.IsDocked = true;
            }

            public override PanelNode? Hit(float x, float y) {
                if (x >= Bounds.X && x <= Bounds.X + Bounds.Z &&
                    y >= Bounds.Y && y <= Bounds.Y + Bounds.W) {
                    return this;
                }

                return null;
            }

            public override LayoutNode? Remove(DockableEntity entity) {
                if (ReferenceEquals(Entity, entity)) {
                    Entity.IsDocked = false;
                    return null;
                }

                return this;
            }

            public override PanelNode? Find(DockableEntity entity) {
                return ReferenceEquals(Entity, entity) ? this : null;
            }

            public override PanelNode? FirstLeaf() {
                return this;
            }
        }

        /// <summary>
        /// Represents a split between two child layout nodes.
        /// </summary>
        sealed class SplitNode : LayoutNode {
            readonly bool isVertical;

            public SplitNode(bool isVertical, float splitFraction, LayoutNode first, LayoutNode second) {
                this.isVertical = isVertical;
                SplitFraction = Math.Clamp(splitFraction, 0.05f, 0.95f);
                First = first;
                Second = second;
            }

            public float SplitFraction { get; private set; }

            public LayoutNode First { get; set; }

            public LayoutNode Second { get; set; }

            public override void Layout(float left, float top, float right, float bottom, float z, int gap) {
                if (isVertical) {
                    float availableWidth = right - left;
                    float splitWidth = MathF.Max(1f, availableWidth * SplitFraction);
                    float firstRight = left + splitWidth - gap * 0.5f;
                    float secondLeft = left + splitWidth + gap * 0.5f;

                    First.Layout(left, top, firstRight, bottom, z, gap);
                    Second.Layout(secondLeft, top, right, bottom, z, gap);
                } else {
                    float availableHeight = bottom - top;
                    float splitHeight = MathF.Max(1f, availableHeight * SplitFraction);
                    float firstBottom = top + splitHeight - gap * 0.5f;
                    float secondTop = top + splitHeight + gap * 0.5f;

                    First.Layout(left, top, right, firstBottom, z, gap);
                    Second.Layout(left, secondTop, right, bottom, z, gap);
                }
            }

            public override PanelNode? Hit(float x, float y) {
                PanelNode? firstHit = First.Hit(x, y);
                if (firstHit != null) {
                    return firstHit;
                }

                return Second.Hit(x, y);
            }

            public override LayoutNode? Remove(DockableEntity entity) {
                LayoutNode? newFirst = First.Remove(entity);
                LayoutNode? newSecond = Second.Remove(entity);

                if (newFirst == null && newSecond == null) {
                    return null;
                }

                if (newFirst == null) {
                    return newSecond;
                }

                if (newSecond == null) {
                    return newFirst;
                }

                First = newFirst;
                Second = newSecond;
                return this;
            }

            public override PanelNode? Find(DockableEntity entity) {
                PanelNode? first = First.Find(entity);
                if (first != null) {
                    return first;
                }

                return Second.Find(entity);
            }

            public override PanelNode? FirstLeaf() {
                return First.FirstLeaf() ?? Second.FirstLeaf();
            }
        }
    }
}
