namespace helengine.editor {
    /// <summary>
    /// Resolves the world-presented 2D size for one scene viewport where one viewport pixel maps to one world unit.
    /// </summary>
    public static class EditorViewportDirect2DPresentationService {
        /// <summary>
        /// Resolves the direct 2D world-presentation size from the supplied viewport component.
        /// </summary>
        /// <param name="viewportOwner">Entity that owns the viewport component.</param>
        /// <param name="sceneViewportComponent">Viewport component that owns the authoritative scene viewport rectangle.</param>
        /// <returns>World-presented 2D size in world units.</returns>
        public static int2 ResolvePresentedWorldSize(Entity viewportOwner, ViewportComponent sceneViewportComponent) {
            if (viewportOwner == null) {
                throw new ArgumentNullException(nameof(viewportOwner));
            }
            if (sceneViewportComponent == null) {
                throw new ArgumentNullException(nameof(sceneViewportComponent));
            }

            if (TryGetReferenceCanvasFitComponent(viewportOwner, out ReferenceCanvasFitComponent fitComponent)) {
                return new int2(
                    Math.Max(1, fitComponent.ReferenceWidth),
                    Math.Max(1, fitComponent.ReferenceHeight));
            }

            return sceneViewportComponent.ResolvedViewportSize;
        }

        /// <summary>
        /// Resolves the editor-presented world position for one authored scene entity.
        /// </summary>
        /// <param name="entity">Authored scene entity to transform into editor world-preview space.</param>
        /// <returns>Editor-presented world position for the supplied entity.</returns>
        public static float3 ResolvePresentedWorldPosition(Entity entity) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }

            if (!TryResolveViewportOwner(entity, out Entity viewportOwner, out _)) {
                return entity.Position;
            }

            float3 viewportOrigin = ResolvePresentedViewportOwnerPosition(viewportOwner);
            if (ReferenceEquals(entity, viewportOwner)) {
                return viewportOrigin;
            }

            float3 viewportLocalOffset = ResolveViewportLocalOffset(entity, viewportOwner);
            viewportLocalOffset = UnscaleViewportLocalOffsetIfNeeded(viewportOwner, viewportLocalOffset);
            viewportLocalOffset = new float3(viewportLocalOffset.X, -viewportLocalOffset.Y, viewportLocalOffset.Z);
            return viewportOrigin + float4.RotateVector(viewportLocalOffset, viewportOwner.Orientation);
        }

        /// <summary>
        /// Resolves the stored world position that should be written back for one editor-presented world-space point.
        /// </summary>
        /// <param name="entity">Authored scene entity whose presented position is being edited.</param>
        /// <param name="presentedWorldPosition">Editor-presented world-space position chosen by scene-view interaction.</param>
        /// <returns>Stored world-space position that preserves the original authored/runtime coordinate contract.</returns>
        public static float3 ResolveStoredWorldPositionFromPresented(Entity entity, float3 presentedWorldPosition) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }

            if (!TryResolveViewportOwner(entity, out Entity viewportOwner, out _)) {
                return presentedWorldPosition;
            }

            if (ReferenceEquals(entity, viewportOwner)) {
                return ResolveStoredViewportOwnerWorldPositionFromPresented(viewportOwner, presentedWorldPosition);
            }

            float3 viewportOrigin = ResolvePresentedViewportOwnerPosition(viewportOwner);
            float3 presentedViewportOffset = presentedWorldPosition - viewportOrigin;
            float3 authoredViewportOffset = float4.RotateVector(presentedViewportOffset, float4.Inverse(viewportOwner.Orientation));
            authoredViewportOffset = new float3(authoredViewportOffset.X, -authoredViewportOffset.Y, authoredViewportOffset.Z);
            float3 storedViewportOffset = ScaleViewportLocalOffsetIfNeeded(viewportOwner, authoredViewportOffset);
            return viewportOwner.Position + float4.RotateVector(storedViewportOffset, viewportOwner.Orientation);
        }

        /// <summary>
        /// Resolves the editor-presented world scale for one authored 2D preview rectangle.
        /// </summary>
        /// <param name="entity">Authored scene entity mirrored by the preview rectangle.</param>
        /// <param name="currentSize">Current live preview size reported by the authored component.</param>
        /// <returns>Editor-presented world scale for a shared unit quad.</returns>
        public static float3 ResolvePresentedWorldScale(Entity entity, int2 currentSize) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }

            int2 presentedSize = ResolvePresentedComponentSize(entity, currentSize);
            return new float3(presentedSize.X, presentedSize.Y, 1f);
        }

        /// <summary>
        /// Resolves the editor-presented component size for one authored 2D preview source.
        /// </summary>
        /// <param name="entity">Authored scene entity mirrored by the preview rectangle.</param>
        /// <param name="currentSize">Current live preview size reported by the authored component.</param>
        /// <returns>Editor-presented component size after reversing viewport-fit scaling when necessary.</returns>
        public static int2 ResolvePresentedComponentSize(Entity entity, int2 currentSize) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }

            if (!TryResolveViewportOwner(entity, out Entity viewportOwner, out _)) {
                return currentSize;
            }

            if (ReferenceEquals(entity, viewportOwner)) {
                return currentSize;
            }

            if (!TryResolveViewportFitScales(viewportOwner, out double widthScale, out double heightScale)) {
                return currentSize;
            }

            int resolvedWidth = Math.Max(1, (int)Math.Round(currentSize.X / widthScale));
            int resolvedHeight = Math.Max(1, (int)Math.Round(currentSize.Y / heightScale));
            return new int2(resolvedWidth, resolvedHeight);
        }

        /// <summary>
        /// Resolves one viewport-local point into editor-presented world space using viewport right/down coordinates.
        /// </summary>
        /// <param name="viewportEntity">Viewport-owner entity that defines the local coordinate system.</param>
        /// <param name="localPoint">Viewport-local point expressed in right/down coordinates.</param>
        /// <returns>Editor-presented world-space point.</returns>
        public static float3 TransformPresentedViewportPoint(Entity viewportEntity, float3 localPoint) {
            if (viewportEntity == null) {
                throw new ArgumentNullException(nameof(viewportEntity));
            }

            float3 presentedOrigin = ResolvePresentedViewportOwnerPosition(viewportEntity);
            float3 presentedLocalPoint = new float3(localPoint.X, -localPoint.Y, localPoint.Z);
            return presentedOrigin + float4.RotateVector(presentedLocalPoint, viewportEntity.Orientation);
        }

        /// <summary>
        /// Resolves one entity-local point into editor-presented world space using the entity's own transform plus viewport right/down conversion when needed.
        /// </summary>
        /// <param name="entity">Entity that owns the supplied local point.</param>
        /// <param name="localPoint">Entity-local point to transform.</param>
        /// <returns>Editor-presented world-space point.</returns>
        public static float3 TransformPresentedEntityLocalPoint(Entity entity, float3 localPoint) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }

            float3 presentedOrigin = ResolvePresentedWorldPosition(entity);
            float3 presentedLocalPoint = localPoint;
            if (TryResolveViewportOwner(entity, out _, out _)) {
                presentedLocalPoint = new float3(localPoint.X, -localPoint.Y, localPoint.Z);
            }

            return presentedOrigin + float4.RotateVector(presentedLocalPoint, entity.Orientation);
        }

        /// <summary>
        /// Attempts to resolve the nearest ancestor viewport owner for one authored scene entity.
        /// </summary>
        /// <param name="entity">Entity whose viewport ancestry should be inspected.</param>
        /// <param name="viewportOwner">Receives the nearest viewport-owner ancestor when one exists.</param>
        /// <param name="viewportComponent">Receives the nearest viewport component when one exists.</param>
        /// <returns>True when a viewport owner was found; otherwise false.</returns>
        public static bool TryResolveViewportOwner(Entity entity, out Entity viewportOwner, out ViewportComponent viewportComponent) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }

            Entity current = entity;
            while (current != null) {
                for (int componentIndex = 0; componentIndex < current.Components.Count; componentIndex++) {
                    if (current.Components[componentIndex] is ViewportComponent resolvedViewportComponent) {
                        viewportOwner = current;
                        viewportComponent = resolvedViewportComponent;
                        return true;
                    }
                }

                current = current.Parent;
            }

            viewportOwner = null;
            viewportComponent = null;
            return false;
        }

        /// <summary>
        /// Rebuilds the scene camera 2D queue so only viewport-owned scene content remains on the screen-space path.
        /// </summary>
        /// <param name="sceneCamera">Scene camera whose 2D queue should retain only viewport-owned content.</param>
        public static void SynchronizeViewportOwnedSceneQueue(CameraComponent sceneCamera) {
            if (sceneCamera == null) {
                throw new ArgumentNullException(nameof(sceneCamera));
            }

            IRenderQueue2D renderQueue2D = sceneCamera.RenderQueue2D;
            renderQueue2D.Clear();

            List<IDrawable2D> drawables2D = Core.Instance.ObjectManager.Drawables2D;
            for (int drawableIndex = 0; drawableIndex < drawables2D.Count; drawableIndex++) {
                IDrawable2D drawable = drawables2D[drawableIndex];
                if (drawable == null || drawable.Parent == null || !drawable.Parent.Enabled) {
                    continue;
                }
                if ((drawable.Parent.LayerMask & sceneCamera.LayerMask) == 0) {
                    continue;
                }
                if (!ShouldKeepDrawableOnSceneCameraQueue(drawable.Parent)) {
                    continue;
                }

                renderQueue2D.Add(drawable);
            }
        }

        /// <summary>
        /// Determines whether one scene entity should keep viewport-lock behavior instead of receiving an editor world-space preview proxy.
        /// </summary>
        /// <param name="entity">Authored scene entity to evaluate.</param>
        /// <returns>True when the entity belongs to editor-owned viewport infrastructure that should stay on the screen-space path.</returns>
        public static bool ShouldKeepViewportLockBehavior(Entity entity) {
            if (!TryResolveViewportOwner(entity, out _, out _)) {
                return false;
            }

            return !EditorViewportSceneSelectionFilter.ShouldSelectEntity(entity);
        }

        /// <summary>
        /// Determines whether one 2D drawable should remain on the scene camera queue instead of being replaced by an editor world-space preview proxy.
        /// </summary>
        /// <param name="entity">Drawable parent entity to evaluate.</param>
        /// <returns>True when the drawable should keep its 2D scene-camera presentation.</returns>
        static bool ShouldKeepDrawableOnSceneCameraQueue(Entity entity) {
            if (entity == null) {
                return false;
            }
            if (ShouldKeepViewportLockBehavior(entity)) {
                return true;
            }
            if (!EditorViewportSceneSelectionFilter.ShouldSelectEntity(entity)) {
                return false;
            }
            if (EditorWorldSpace2DPreviewMapper.TryResolveSupportedSourceComponent(entity, out _)) {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Resolves the top-most selectable 2D scene entity at one viewport pointer position before any 3D fallback selection is considered.
        /// </summary>
        /// <param name="sceneCamera">Scene camera that renders the direct 2D viewport content.</param>
        /// <param name="viewport">Viewport rectangle in window coordinates.</param>
        /// <param name="pointer">Pointer position in window coordinates.</param>
        /// <returns>Selectable 2D scene entity when one is hit; otherwise null.</returns>
        public static Entity ResolveSelectableEntityAtPointer(CameraComponent sceneCamera, float4 viewport, int2 pointer) {
            if (sceneCamera == null) {
                throw new ArgumentNullException(nameof(sceneCamera));
            }

            if (!IsPointerInsideViewport(pointer, viewport)) {
                return null;
            }

            IInteractable2D interactable = PointerInteractableHitResolver.ResolveTopInteractableAt(
                Core.Instance.ObjectManager.Interactables,
                Core.Instance.ObjectManager.Drawables2D,
                sceneCamera,
                pointer.X,
                pointer.Y);
            if (interactable == null) {
                return null;
            }

            return EditorViewportSceneSelectionFilter.ResolveSelectableEntity(interactable.Parent);
        }

        /// <summary>
        /// Resolves the top-most selectable world-preview 2D scene entity at one viewport pointer position before generic 3D fallback selection is considered.
        /// </summary>
        /// <param name="sceneCamera">Scene camera that views the editor world-preview content.</param>
        /// <param name="viewport">Viewport rectangle in window coordinates.</param>
        /// <param name="pointer">Pointer position in window coordinates.</param>
        /// <returns>Selectable world-preview 2D scene entity when one is hit; otherwise null.</returns>
        public static Entity ResolveSelectableWorldPreviewEntityAtPointer(CameraComponent sceneCamera, float4 viewport, int2 pointer) {
            if (sceneCamera == null) {
                throw new ArgumentNullException(nameof(sceneCamera));
            }

            if (!IsPointerInsideViewport(pointer, viewport)) {
                return null;
            }

            if (!EditorViewportPointerRayBuilder.TryBuildPerspectiveCameraRay(sceneCamera, pointer, out float3 rayOrigin, out float3 rayDirection)) {
                return null;
            }

            Entity resolvedEntity = null;
            byte resolvedRenderOrder = 0;
            double resolvedDistance = double.MaxValue;

            List<IDrawable3D> drawables3D = Core.Instance.ObjectManager.Drawables3D;
            for (int drawableIndex = 0; drawableIndex < drawables3D.Count; drawableIndex++) {
                IDrawable3D drawable = drawables3D[drawableIndex];
                if (!TryResolveWorldPreviewHit(drawable, rayOrigin, rayDirection, out Entity sourceEntity, out byte renderOrder, out double distanceAlongRay)) {
                    continue;
                }

                if (resolvedEntity == null ||
                    renderOrder > resolvedRenderOrder ||
                    (renderOrder == resolvedRenderOrder && distanceAlongRay < resolvedDistance)) {
                    resolvedEntity = sourceEntity;
                    resolvedRenderOrder = renderOrder;
                    resolvedDistance = distanceAlongRay;
                }
            }

            return resolvedEntity;
        }

        /// <summary>
        /// Determines whether one pointer lies inside the supplied viewport rectangle.
        /// </summary>
        /// <param name="pointer">Pointer position in window coordinates.</param>
        /// <param name="viewport">Viewport rectangle in window coordinates.</param>
        /// <returns>True when the pointer lies within the viewport bounds.</returns>
        static bool IsPointerInsideViewport(int2 pointer, float4 viewport) {
            return pointer.X >= viewport.X &&
                   pointer.Y >= viewport.Y &&
                   pointer.X < viewport.X + viewport.Z &&
                   pointer.Y < viewport.Y + viewport.W;
        }

        /// <summary>
        /// Attempts to resolve one ray hit against an editor world-preview proxy drawable.
        /// </summary>
        /// <param name="drawable">Drawable candidate to test.</param>
        /// <param name="rayOrigin">World-space ray origin.</param>
        /// <param name="rayDirection">Normalized world-space ray direction.</param>
        /// <param name="sourceEntity">Receives the authored source entity when the ray hits the preview rectangle.</param>
        /// <param name="renderOrder">Receives the authored render order used to prioritize overlapping 2D previews.</param>
        /// <param name="distanceAlongRay">Receives the hit distance along the ray.</param>
        /// <returns>True when the ray hits one selectable world-preview rectangle.</returns>
        static bool TryResolveWorldPreviewHit(
            IDrawable3D drawable,
            float3 rayOrigin,
            float3 rayDirection,
            out Entity sourceEntity,
            out byte renderOrder,
            out double distanceAlongRay) {
            sourceEntity = null;
            renderOrder = 0;
            distanceAlongRay = 0.0;
            if (drawable == null || drawable.Parent is not EditorEntity previewEntity || !previewEntity.Enabled) {
                return false;
            }

            Entity resolvedSourceEntity = EditorWorldSpace2DPreviewRegistry.ResolveSourceEntity(previewEntity);
            if (resolvedSourceEntity == null) {
                return false;
            }
            if (!EditorWorldSpace2DPreviewMapper.TryResolveSupportedSourceComponent(resolvedSourceEntity, out Component sourceComponent)) {
                return false;
            }

            int2 previewSize = ResolvePreviewComponentSize(sourceComponent, resolvedSourceEntity);
            if (previewSize.X <= 0 || previewSize.Y <= 0) {
                return false;
            }

            float3 rectangleOrigin = ResolvePresentedWorldPosition(resolvedSourceEntity);
            float3 rectangleXAxis = TransformPresentedEntityLocalPoint(resolvedSourceEntity, new float3(previewSize.X, 0f, 0f)) - rectangleOrigin;
            float3 rectangleYAxis = TransformPresentedEntityLocalPoint(resolvedSourceEntity, new float3(0f, previewSize.Y, 0f)) - rectangleOrigin;
            if (!TryIntersectRectangle(rayOrigin, rayDirection, rectangleOrigin, rectangleXAxis, rectangleYAxis, out double resolvedDistanceAlongRay)) {
                return false;
            }

            sourceEntity = resolvedSourceEntity;
            renderOrder = ResolveRenderOrder(sourceComponent);
            distanceAlongRay = resolvedDistanceAlongRay;
            return true;
        }

        /// <summary>
        /// Resolves the presented preview size for one authored 2D source component.
        /// </summary>
        /// <param name="sourceComponent">Authored 2D source component mirrored by the preview proxy.</param>
        /// <param name="sourceEntity">Authored source entity that owns the component.</param>
        /// <returns>Presented preview size in world units.</returns>
        static int2 ResolvePreviewComponentSize(Component sourceComponent, Entity sourceEntity) {
            if (sourceComponent == null) {
                throw new ArgumentNullException(nameof(sourceComponent));
            }
            if (sourceEntity == null) {
                throw new ArgumentNullException(nameof(sourceEntity));
            }

            if (sourceComponent is SpriteComponent spriteComponent) {
                int2 rawSize = spriteComponent.Size;
                if ((rawSize.X <= 0 || rawSize.Y <= 0) && spriteComponent.Texture != null) {
                    rawSize = new int2(
                        Math.Max(1, spriteComponent.Texture.Width),
                        Math.Max(1, spriteComponent.Texture.Height));
                }

                return ResolvePresentedComponentSize(sourceEntity, rawSize);
            } else if (sourceComponent is TextComponent textComponent) {
                return ResolvePresentedComponentSize(sourceEntity, textComponent.Size);
            } else if (sourceComponent is RoundedRectComponent roundedRectComponent) {
                return ResolvePresentedComponentSize(sourceEntity, roundedRectComponent.Size);
            }

            throw new InvalidOperationException($"Unsupported 2D preview source component type '{sourceComponent.GetType().FullName}'.");
        }

        /// <summary>
        /// Resolves the authored render order used to prioritize overlapping 2D preview hits.
        /// </summary>
        /// <param name="sourceComponent">Authored 2D source component mirrored by the preview proxy.</param>
        /// <returns>Render-order value used by the authored source component.</returns>
        static byte ResolveRenderOrder(Component sourceComponent) {
            if (sourceComponent == null) {
                throw new ArgumentNullException(nameof(sourceComponent));
            }

            if (sourceComponent is SpriteComponent spriteComponent) {
                return spriteComponent.RenderOrder2D;
            } else if (sourceComponent is TextComponent textComponent) {
                return textComponent.RenderOrder2D;
            } else if (sourceComponent is RoundedRectComponent roundedRectComponent) {
                return roundedRectComponent.RenderOrder2D;
            }

            throw new InvalidOperationException($"Unsupported 2D preview source component type '{sourceComponent.GetType().FullName}'.");
        }

        /// <summary>
        /// Attempts to intersect one world-space ray with one oriented rectangle.
        /// </summary>
        /// <param name="rayOrigin">World-space ray origin.</param>
        /// <param name="rayDirection">Normalized world-space ray direction.</param>
        /// <param name="rectangleOrigin">World-space rectangle origin.</param>
        /// <param name="rectangleXAxis">World-space rectangle X edge vector.</param>
        /// <param name="rectangleYAxis">World-space rectangle Y edge vector.</param>
        /// <param name="distanceAlongRay">Receives the positive hit distance along the ray.</param>
        /// <returns>True when the ray intersects the rectangle interior.</returns>
        static bool TryIntersectRectangle(
            float3 rayOrigin,
            float3 rayDirection,
            float3 rectangleOrigin,
            float3 rectangleXAxis,
            float3 rectangleYAxis,
            out double distanceAlongRay) {
            float3 planeNormal = float3.Cross(rectangleXAxis, rectangleYAxis);
            double normalLengthSquared =
                (planeNormal.X * planeNormal.X) +
                (planeNormal.Y * planeNormal.Y) +
                (planeNormal.Z * planeNormal.Z);
            if (normalLengthSquared <= 0.000000000001) {
                distanceAlongRay = 0.0;
                return false;
            }

            double denominator = float3.Dot(planeNormal, rayDirection);
            if (Math.Abs(denominator) <= 0.000000000001) {
                distanceAlongRay = 0.0;
                return false;
            }

            float3 planeDelta = rectangleOrigin - rayOrigin;
            distanceAlongRay = float3.Dot(planeDelta, planeNormal) / denominator;
            if (distanceAlongRay < 0.0) {
                return false;
            }

            float3 hitPoint = rayOrigin + (rayDirection * (float)distanceAlongRay);
            float3 hitOffset = hitPoint - rectangleOrigin;
            double xLengthSquared = float3.Dot(rectangleXAxis, rectangleXAxis);
            double yLengthSquared = float3.Dot(rectangleYAxis, rectangleYAxis);
            if (xLengthSquared <= 0.000000000001 || yLengthSquared <= 0.000000000001) {
                return false;
            }

            double xParameter = float3.Dot(hitOffset, rectangleXAxis) / xLengthSquared;
            double yParameter = float3.Dot(hitOffset, rectangleYAxis) / yLengthSquared;
            return xParameter >= 0.0 &&
                   xParameter <= 1.0 &&
                   yParameter >= 0.0 &&
                   yParameter <= 1.0;
        }

        /// <summary>
        /// Resolves the nearest ancestor entity that owns one viewport component.
        /// </summary>
        /// <param name="entity">Entity whose viewport-owner ancestry should be inspected.</param>
        /// <returns>Viewport-owner ancestor when present; otherwise null.</returns>
        /// <summary>
        /// Resolves the current viewport-owner world position after reversing any live reference-canvas fit offset.
        /// </summary>
        /// <param name="viewportOwner">Viewport-owner entity to resolve.</param>
        /// <returns>Editor-presented world position for the viewport origin.</returns>
        static float3 ResolvePresentedViewportOwnerPosition(Entity viewportOwner) {
            if (viewportOwner == null) {
                throw new ArgumentNullException(nameof(viewportOwner));
            }

            if (!TryGetReferenceCanvasFitComponent(viewportOwner, out ReferenceCanvasFitComponent fitComponent) ||
                !TryResolveViewportFitScales(viewportOwner, out double widthScale, out double heightScale)) {
                return viewportOwner.Position;
            }

            float2 canvasOrigin = ResolveCurrentCanvasOrigin(fitComponent);
            float3 authoredLocalPosition = viewportOwner.LocalPosition;
            authoredLocalPosition.X = (float)((authoredLocalPosition.X - canvasOrigin.X) / widthScale);
            authoredLocalPosition.Y = (float)((authoredLocalPosition.Y - canvasOrigin.Y) / heightScale);

            if (viewportOwner.Parent == null) {
                return authoredLocalPosition;
            }

            float3 rotatedLocalPosition = float4.RotateVector(authoredLocalPosition, viewportOwner.Parent.Orientation);
            return viewportOwner.Parent.Position + rotatedLocalPosition;
        }

        /// <summary>
        /// Resolves the stored world position for one viewport-owner entity from its editor-presented viewport origin.
        /// </summary>
        /// <param name="viewportOwner">Viewport-owner entity being translated.</param>
        /// <param name="presentedWorldPosition">Editor-presented world-space viewport origin.</param>
        /// <returns>Stored world-space position for the viewport-owner entity.</returns>
        static float3 ResolveStoredViewportOwnerWorldPositionFromPresented(Entity viewportOwner, float3 presentedWorldPosition) {
            if (viewportOwner == null) {
                throw new ArgumentNullException(nameof(viewportOwner));
            }

            if (!TryGetReferenceCanvasFitComponent(viewportOwner, out ReferenceCanvasFitComponent fitComponent) ||
                !TryResolveViewportFitScales(viewportOwner, out double widthScale, out double heightScale)) {
                return presentedWorldPosition;
            }

            float3 parentLocalPresentedPosition = presentedWorldPosition;
            if (viewportOwner.Parent != null) {
                float3 parentWorldOffset = presentedWorldPosition - viewportOwner.Parent.Position;
                parentLocalPresentedPosition = float4.RotateVector(parentWorldOffset, float4.Inverse(viewportOwner.Parent.Orientation));
            }

            float2 canvasOrigin = ResolveCurrentCanvasOrigin(fitComponent);
            float3 storedLocalPosition = new float3(
                (float)((parentLocalPresentedPosition.X * widthScale) + canvasOrigin.X),
                (float)((parentLocalPresentedPosition.Y * heightScale) + canvasOrigin.Y),
                parentLocalPresentedPosition.Z);
            if (viewportOwner.Parent == null) {
                return storedLocalPosition;
            }

            return viewportOwner.Parent.Position + float4.RotateVector(storedLocalPosition, viewportOwner.Parent.Orientation);
        }

        /// <summary>
        /// Resolves the live viewport-local offset between one entity and its viewport owner.
        /// </summary>
        /// <param name="entity">Authored scene entity whose offset should be resolved.</param>
        /// <param name="viewportOwner">Nearest viewport-owner ancestor.</param>
        /// <returns>Viewport-local offset before editor axis conversion.</returns>
        static float3 ResolveViewportLocalOffset(Entity entity, Entity viewportOwner) {
            float3 worldOffset = entity.Position - viewportOwner.Position;
            return float4.RotateVector(worldOffset, float4.Inverse(viewportOwner.Orientation));
        }

        /// <summary>
        /// Reverses live reference-canvas fit scaling from one viewport-local offset when the viewport owner uses fitted layout.
        /// </summary>
        /// <param name="viewportOwner">Viewport-owner entity that may own a fit component.</param>
        /// <param name="viewportLocalOffset">Viewport-local offset to adjust.</param>
        /// <returns>Viewport-local offset expressed in authored reference-canvas coordinates.</returns>
        static float3 UnscaleViewportLocalOffsetIfNeeded(Entity viewportOwner, float3 viewportLocalOffset) {
            if (!TryResolveViewportFitScales(viewportOwner, out double widthScale, out double heightScale)) {
                return viewportLocalOffset;
            }

            return new float3(
                (float)(viewportLocalOffset.X / widthScale),
                (float)(viewportLocalOffset.Y / heightScale),
                viewportLocalOffset.Z);
        }

        /// <summary>
        /// Reapplies live reference-canvas fit scaling to one authored viewport-local offset before it is stored on the scene entity.
        /// </summary>
        /// <param name="viewportOwner">Viewport-owner entity that may own a fit component.</param>
        /// <param name="viewportLocalOffset">Authored viewport-local offset expressed in reference-canvas coordinates.</param>
        /// <returns>Stored viewport-local offset in live runtime world units.</returns>
        static float3 ScaleViewportLocalOffsetIfNeeded(Entity viewportOwner, float3 viewportLocalOffset) {
            if (!TryResolveViewportFitScales(viewportOwner, out double widthScale, out double heightScale)) {
                return viewportLocalOffset;
            }

            return new float3(
                (float)(viewportLocalOffset.X * widthScale),
                (float)(viewportLocalOffset.Y * heightScale),
                viewportLocalOffset.Z);
        }

        /// <summary>
        /// Attempts to resolve one reference-canvas fit component from the supplied viewport owner.
        /// </summary>
        /// <param name="viewportOwner">Viewport-owner entity to inspect.</param>
        /// <param name="fitComponent">Receives the resolved fit component when one exists.</param>
        /// <returns>True when the viewport owner exposes one fit component; otherwise false.</returns>
        static bool TryGetReferenceCanvasFitComponent(Entity viewportOwner, out ReferenceCanvasFitComponent fitComponent) {
            for (int componentIndex = 0; componentIndex < viewportOwner.Components.Count; componentIndex++) {
                if (viewportOwner.Components[componentIndex] is ReferenceCanvasFitComponent resolvedFitComponent) {
                    fitComponent = resolvedFitComponent;
                    return true;
                }
            }

            fitComponent = null;
            return false;
        }

        /// <summary>
        /// Attempts to resolve the live fit scales currently applied by one viewport-owner fit component.
        /// </summary>
        /// <param name="viewportOwner">Viewport-owner entity to inspect.</param>
        /// <param name="widthScale">Receives the live horizontal fit scale.</param>
        /// <param name="heightScale">Receives the live vertical fit scale.</param>
        /// <returns>True when valid live fit scales were resolved; otherwise false.</returns>
        static bool TryResolveViewportFitScales(Entity viewportOwner, out double widthScale, out double heightScale) {
            if (!TryGetReferenceCanvasFitComponent(viewportOwner, out ReferenceCanvasFitComponent fitComponent)) {
                widthScale = 1.0;
                heightScale = 1.0;
                return false;
            }

            widthScale = fitComponent.ReferenceWidth > 0
                ? (double)fitComponent.AnchorSpace.Size.X / fitComponent.ReferenceWidth
                : 1.0;
            heightScale = fitComponent.ReferenceHeight > 0
                ? (double)fitComponent.AnchorSpace.Size.Y / fitComponent.ReferenceHeight
                : 1.0;
            if (widthScale <= 0.0 || heightScale <= 0.0) {
                widthScale = 1.0;
                heightScale = 1.0;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Resolves the current fitted canvas origin applied by one reference-canvas fit component.
        /// </summary>
        /// <param name="fitComponent">Fit component whose live canvas origin should be reconstructed.</param>
        /// <returns>Current fitted canvas origin in local viewport-owner space.</returns>
        static float2 ResolveCurrentCanvasOrigin(ReferenceCanvasFitComponent fitComponent) {
            int2 mainWindowSize = Core.Instance.RenderManager3D.MainWindowSize;
            return new float2(
                (float)((mainWindowSize.X - fitComponent.AnchorSpace.Size.X) * 0.5d),
                (float)((mainWindowSize.Y - fitComponent.AnchorSpace.Size.Y) * 0.5d));
        }
    }
}
