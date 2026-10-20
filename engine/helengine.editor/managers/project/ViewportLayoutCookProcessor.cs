using helengine.baseplatform.Builders;

namespace helengine.editor {
    /// <summary>Resolves packaged UI with detached participants, sharing runtime math without running scene callbacks.</summary>
    internal sealed class ViewportLayoutCookProcessor {
        /// <summary>Version included in cook identity and checked by the player.</summary>
        public const int AlgorithmVersion = ViewportCookedLayout.CurrentAlgorithmVersion;
        /// <summary>Uses the existing packaging resolver for effective overrides and saved references.</summary>
        readonly Func<SceneComponentAssetRecord, IReadOnlyList<SceneOverrideScopeStepKind>, Component> ComponentResolver;
        /// <summary>Captures the immutable target contract for all detached computations.</summary>
        readonly NumericPrecisionPolicy NumericPolicy;
        /// <summary>Arithmetic adapter reused across the complete cook pass.</summary>
        readonly CookNumericOperations NumericMath;


        /// <summary>Connects detached layout processing to the official component persistence and override pipeline.</summary>
        public ViewportLayoutCookProcessor(Func<SceneComponentAssetRecord, IReadOnlyList<SceneOverrideScopeStepKind>, Component> componentResolver, NumericPrecisionPolicy numericPolicy = null) {
            NumericPolicy = numericPolicy ?? RealRepresentation.Policy;
            NumericMath = new CookNumericOperations(NumericPolicy);
            ComponentResolver = componentResolver ?? throw new ArgumentNullException(nameof(componentResolver));
        }

        /// <summary>Prepares only Cook owners in the already expanded and pruned packaged scene.</summary>
        public void Process(SceneAsset packagedScene, PlatformLayoutTarget target) {
            if (packagedScene == null) { throw new ArgumentNullException(nameof(packagedScene)); }
            foreach (SceneEntityAsset entity in packagedScene.RootEntities ?? Array.Empty<SceneEntityAsset>()) {
                if (entity != null) { ProcessNode(BuildNode(entity, null), null, null, target); }
            }
        }

        /// <summary>Builds an editor-only hierarchy without constructing a Core, live Entity or gameplay component.</summary>
        ViewportLayoutCookNode BuildNode(SceneEntityAsset entity, ViewportLayoutCookNode parent) {
            ViewportLayoutCookNode node = new ViewportLayoutCookNode { Asset = entity, Parent = parent };
            foreach (SceneComponentAssetRecord record in entity.Components ?? Array.Empty<SceneComponentAssetRecord>()) {
                if (record != null && IsGeneratedControl(record.ComponentTypeId)) { node.UnsupportedGeneratedControlTypeId ??= record.ComponentTypeId; }
                if (record == null || !IsParticipant(record.ComponentTypeId)) { continue; }
                Component component = ComponentResolver(record, entity.HasOverrideLevelOrder ? entity.OverrideLevelOrder : null);
                node.Components.Add(component);
                if (component is ViewportComponent viewport) { node.Viewport = viewport; }
                else if (component is ReferenceCanvasFitComponent fit) { node.Fit = fit; }
            }
            foreach (SceneEntityAsset child in entity.Children ?? Array.Empty<SceneEntityAsset>()) {
                if (child != null) { node.Children.Add(BuildNode(child, node)); }
            }
            return node;
        }

        /// <summary>Identifies built-in hosts whose visuals are created only by live component callbacks.</summary>
        static bool IsGeneratedControl(string typeId) {
            string name = (typeId ?? string.Empty).Split(',')[0].Trim();
            return name.Equals("helengine.CheckBoxComponent", StringComparison.OrdinalIgnoreCase)
                || name.Equals("helengine.TextBoxComponent", StringComparison.OrdinalIgnoreCase)
                || name.Equals("helengine.ComboBoxComponent", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Limits detached construction to the built-in layout and camera types understood by this pass.</summary>
        static bool IsParticipant(string typeId) {
            string name = (typeId ?? string.Empty).Split(',')[0].Trim();
            return name.Equals("helengine.ViewportComponent", StringComparison.OrdinalIgnoreCase)
                || name.Equals("helengine.ReferenceCanvasFitComponent", StringComparison.OrdinalIgnoreCase)
                || name.Equals("helengine.CameraComponent", StringComparison.OrdinalIgnoreCase)
                || name.Equals("helengine.LayoutComponent", StringComparison.OrdinalIgnoreCase)
                || name.Equals("helengine.RoundedRectComponent", StringComparison.OrdinalIgnoreCase)
                || name.Equals("helengine.TextComponent", StringComparison.OrdinalIgnoreCase)
                || name.Equals("helengine.SpriteComponent", StringComparison.OrdinalIgnoreCase)
                || name.Equals("helengine.ClipRectComponent", StringComparison.OrdinalIgnoreCase)
                || name.Equals("helengine.InteractableComponent", StringComparison.OrdinalIgnoreCase)
                || name.Equals("helengine.ScrollComponent", StringComparison.OrdinalIgnoreCase)
                || name.Equals("helengine.FPSComponent", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Resolves owner boundaries, then geometry and anchors in parent-before-child order.</summary>
        void ProcessNode(ViewportLayoutCookNode node, ViewportLayoutCookNode geometryOwner, ViewportLayoutCookNode viewportOwner, PlatformLayoutTarget target) {
            if (node.Viewport != null) {
                viewportOwner = node;
                node.ViewportTarget = ResolveViewportTarget(node, target);
                bool known = TryResolveMetrics(node, node.Viewport.ScalingMode, node.Viewport.ReferenceWidth, node.Viewport.ReferenceHeight,
                    NumericMath.Read(node.Viewport.UiScale), NumericMath.Multiply(NumericMath.Read(node.Viewport.PixelsPerCentimeter), NumericMath.Number(2.54d)), node.ViewportTarget, out ViewportLayoutMetrics viewportMetrics);
                node.ViewportMetrics = viewportMetrics;
                if (node.Viewport.ScalingMode != ViewportComponent.NoScalingMode) {
                    geometryOwner = node;
                    node.Metrics = viewportMetrics;
                    node.MetricsKnown = known;
                }
                if (node.Viewport.LayoutProcessingMode == ViewportLayoutProcessingMode.Cook) {
                    RequireKnown(node, known, node.Viewport.ScalingMode == ViewportComponent.DensityScalingMode ? "DPI" : "target");
                    node.Viewport.CookedLayout = CreateMetadata(node.ViewportTarget, viewportMetrics, (double)(node.Viewport.UiScale));
                }
            }
            node.ViewportOwner = viewportOwner;
            if (node.Fit != null) {
                if (node.Viewport != null && node.Viewport.ScalingMode != ViewportComponent.NoScalingMode) {
                    throw Failure(node, "two scaling owners on the same entity");
                }
                geometryOwner = node;
                PlatformLayoutTarget fitTarget = viewportOwner == null ? ResolveCameraTarget(node, target, false) : viewportOwner.ViewportTarget;
                node.MetricsKnown = TryResolveMetrics(node, ViewportComponent.ReferenceCanvasScalingMode, node.Fit.ReferenceWidth,
                    node.Fit.ReferenceHeight, 1d, 0d, fitTarget, out ViewportLayoutMetrics fitMetrics);
                node.Metrics = fitMetrics;
                if (GetMode(node) == ViewportLayoutProcessingMode.Cook) {
                    RequireKnown(node, node.MetricsKnown, "target");
                    node.Fit.CookedLayout = CreateMetadata(fitTarget, fitMetrics, 1d);
                }
            }
            node.GeometryOwner = geometryOwner;
            if (!ReferenceEquals(node, geometryOwner)) {
                node.MetricsKnown = geometryOwner == null || geometryOwner.MetricsKnown;
                node.Metrics = geometryOwner == null ? new ViewportLayoutMetrics { ScaleX = 1, ScaleY = 1, GlyphScale = 1 } : geometryOwner.Metrics;
            }
            node.BakeGeometry = geometryOwner != null && GetMode(geometryOwner) == ViewportLayoutProcessingMode.Cook;
            if (node.BakeGeometry) {
                if (node.UnsupportedGeneratedControlTypeId != null) {
                    throw new InvalidOperationException($"Viewport '{geometryOwner.Asset.Name}' cannot Cook runtime-generated UI component '{node.UnsupportedGeneratedControlTypeId}' on entity '{node.Asset.Name}'. Use OnLoad or Dynamic until a detached control cook adapter is available.");
                }
                RequireKnown(node, node.MetricsKnown, "layout factors");
                foreach (Component component in node.Components) { ViewportLayoutApplier.ApplyComponent(component, node.Metrics, NumericPolicy); }
            }
            node.ResolvedSize = ResolveOwnSize(node);
            ViewportLayoutMetrics local = node.Metrics;
            if (!ReferenceEquals(node, geometryOwner)) { local.Origin = new float2(0f, 0f); }
            node.ResolvedPosition = node.MetricsKnown ? ViewportLayoutApplier.ResolvePosition(node.Asset.LocalPosition, local, NumericPolicy) : node.Asset.LocalPosition;
            foreach (Component component in node.Components) {
                if (component is LayoutComponent layout && layout.AnchorFlags != 0
                    && !(ReferenceEquals(node, geometryOwner) && node.Viewport != null && node.Fit == null)) {
                    ResolveAnchors(node, layout, target);
                }
            }
            if (node.BakeGeometry) { node.Asset.LocalPosition = node.ResolvedPosition; }
            foreach (ViewportLayoutCookNode child in node.Children) { ProcessNode(child, geometryOwner, viewportOwner, target); }
        }

        /// <summary>Gets the effective policy of a viewport or a legacy fit inheriting its nearest viewport.</summary>
        static ViewportLayoutProcessingMode GetMode(ViewportLayoutCookNode node) {
            if (node.Fit != null) { return node.ViewportOwner == null ? ViewportLayoutProcessingMode.Dynamic : node.ViewportOwner.Viewport.LayoutProcessingMode; }
            return node.Viewport == null ? ViewportLayoutProcessingMode.Dynamic : node.Viewport.LayoutProcessingMode;
        }

        /// <summary>Resolves viewport binding without falling back from unknown camera or output metrics.</summary>
        PlatformLayoutTarget ResolveViewportTarget(ViewportLayoutCookNode node, PlatformLayoutTarget target) {
            ViewportComponent viewport = node.Viewport;
            if (viewport.BindingMode == ViewportComponent.FixedBindingMode) {
                return new PlatformLayoutTarget(viewport.FixedSize.X, viewport.FixedSize.Y, 1d, target != null && target.HasDpi,
                    target == null ? 0d : target.Dpi, target == null ? 1d : target.UiScale);
            } else if (viewport.BindingMode == ViewportComponent.ScreenBindingMode) {
                return target;
            } else if (viewport.BindingMode == ViewportComponent.AncestorCameraBindingMode) {
                return ResolveCameraTarget(node, target, viewport.LayoutProcessingMode == ViewportLayoutProcessingMode.Cook);
            }
            if (viewport.LayoutProcessingMode == ViewportLayoutProcessingMode.Cook) { throw Failure(node, "explicit camera binding cannot be resolved statically"); }
            return null;
        }

        /// <summary>Resolves the ancestor camera rectangle using the same normalized-viewport helper as the player.</summary>
        PlatformLayoutTarget ResolveCameraTarget(ViewportLayoutCookNode node, PlatformLayoutTarget target, bool required) {
            ViewportLayoutCookNode current = node;
            while (current != null) {
                foreach (Component component in current.Components) {
                    if (component is CameraComponent camera) {
                        if (!string.IsNullOrEmpty(camera.OutputWindowTag) && !string.Equals(camera.OutputWindowTag, "main", StringComparison.OrdinalIgnoreCase)) {
                            if (required) { throw Failure(node, "named camera output target"); }
                            return null;
                        }
                        if (camera.RenderTarget != null) {
                            return new PlatformLayoutTarget(camera.RenderTarget.Width, camera.RenderTarget.Height, 1d, false, 0d, 1d);
                        }
                        if (target == null) { return null; }
                        float4 bounds = CameraViewportResolver.ResolveViewport(camera.Viewport, target.Width, target.Height, NumericPolicy);
                        return new PlatformLayoutTarget(Math.Max(1, NumericMath.RoundToInt(NumericMath.Read(bounds.Z))), Math.Max(1, NumericMath.RoundToInt(NumericMath.Read(bounds.W))),
                            target.PixelAspect, target.HasDpi, target.Dpi, target.UiScale);
                    }
                }
                current = current.Parent;
            }
            if (required) { throw Failure(node, "ancestor camera target"); }
            return target;
        }

        /// <summary>Leaves unresolved Dynamic/OnLoad metrics unknown; Cook callers require them explicitly.</summary>
        bool TryResolveMetrics(ViewportLayoutCookNode node, int scalingMode, int width, int height, double uiScale, double referenceDpi,
            PlatformLayoutTarget target, out ViewportLayoutMetrics metrics) {
            metrics = new ViewportLayoutMetrics();
            if (target == null || (scalingMode == ViewportComponent.DensityScalingMode && !target.HasDpi)) { return false; }
            metrics = ViewportLayoutMath.Resolve(scalingMode, new int2(width, height), new int2(target.Width, target.Height),
                target.PixelAspect, NumericMath.Multiply(NumericMath.Number(uiScale), NumericMath.Number(target.UiScale)), target.Dpi, referenceDpi, NumericPolicy);
            return true;
        }

        /// <summary>Creates shared viewport metadata rather than per-entity original-value data.</summary>
        ViewportCookedLayout CreateMetadata(PlatformLayoutTarget target, ViewportLayoutMetrics metrics, double uiScale) {
            return new ViewportCookedLayout { AlgorithmVersion = AlgorithmVersion, TargetWidth = target.Width, TargetHeight = target.Height,
                PixelAspect = target.PixelAspect, HasDpi = target.HasDpi, Dpi = target.Dpi, UiScale = NumericMath.Multiply(NumericMath.Number(uiScale), NumericMath.Number(target.UiScale)),
                ScaleX = (double)(metrics.ScaleX), ScaleY = (double)(metrics.ScaleY), GlyphScale = (double)(metrics.GlyphScale), Origin = metrics.Origin, AnchorSize = metrics.AnchorSize };
        }

        /// <summary>Computes actual or virtual post-scale dimensions using the runtime's largest-area size-provider rule.</summary>
        int2 ResolveOwnSize(ViewportLayoutCookNode node) {
            int2 size = new int2(0, 0);
            int bestArea = -1;
            foreach (Component component in node.Components) {
                if (component is IAnchorSizeProvider provider) {
                    int2 candidate = provider.AnchorSize;
                    if (!node.BakeGeometry && node.MetricsKnown) { candidate = ViewportLayoutApplier.ScaleSize(candidate, node.Metrics.ScaleX, node.Metrics.ScaleY, NumericPolicy); }
                    int area = candidate.X < 0 || candidate.Y < 0 ? -1 : candidate.X * candidate.Y;
                    if (area > bestArea) { bestArea = area; size = candidate; }
                }
            }
            return size;
        }

        /// <summary>Resolves anchors against virtual prepared providers, writing only cooked participants.</summary>
        void ResolveAnchors(ViewportLayoutCookNode node, LayoutComponent layout, PlatformLayoutTarget target) {
            if (!node.MetricsKnown) { return; }
            int2 spaceSize = target == null ? new int2(0, 0) : new int2(target.Width, target.Height);
            float2 origin = new float2(0f, 0f);
            ViewportLayoutCookNode current = node.Parent;
            bool found = false;
            while (current != null && !found) {
                if (layout.LayoutSpace == LayoutComponent.ParentLayoutRectSpace) {
                    RequireKnown(node, current.MetricsKnown || !node.BakeGeometry, "parent layout bounds");
                    spaceSize = current.ResolvedSize;
                    found = true;
                } else {
                    for (int index = current.Components.Count - 1; index >= 0 && !found; index--) {
                        Component provider = current.Components[index];
                        if (provider is LayoutComponent && layout.LayoutSpace == LayoutComponent.InheritedLayoutSpace) {
                            RequireKnown(node, current.MetricsKnown || !node.BakeGeometry, "parent layout bounds");
                            spaceSize = current.ResolvedSize; found = true;
                        } else if (provider is ReferenceCanvasFitComponent && layout.LayoutSpace != LayoutComponent.CameraViewportLayoutSpace) {
                            RequireKnown(node, current.MetricsKnown || !node.BakeGeometry, "reference canvas bounds");
                            spaceSize = current.Metrics.AnchorSize; found = true;
                        } else if (provider is ViewportComponent && layout.LayoutSpace != LayoutComponent.ReferenceCanvasLayoutSpace) {
                            if (current.ViewportTarget == null) { RequireKnown(node, !node.BakeGeometry, "viewport anchor target"); return; }
                            if (layout.LayoutSpace == LayoutComponent.CameraViewportLayoutSpace || current.Viewport.ScalingMode == ViewportComponent.NoScalingMode) {
                                spaceSize = new int2(current.ViewportTarget.Width, current.ViewportTarget.Height);
                                if (current.Fit != null) {
                                    RequireKnown(node, current.MetricsKnown || !node.BakeGeometry, "reference canvas origin");
                                    origin = new float2(-current.Metrics.Origin.X, -current.Metrics.Origin.Y);
                                }
                            } else {
                                RequireKnown(node, current.MetricsKnown || !node.BakeGeometry, "viewport canvas bounds");
                                spaceSize = current.ViewportMetrics.AnchorSize;
                            }
                            found = true;
                        }
                    }
                }
                current = current.Parent;
            }
            if (!found && target == null) { RequireKnown(node, !node.BakeGeometry, "anchor target"); return; }
            float4 distances = layout.AnchorDistances;
            if (!node.BakeGeometry) {
                distances = new float4(LayoutNumericKernel<double, CookNumericOperations>.Scale(distances.X, node.Metrics.ScaleX, NumericMath), LayoutNumericKernel<double, CookNumericOperations>.Scale(distances.Y, node.Metrics.ScaleX, NumericMath),
                    LayoutNumericKernel<double, CookNumericOperations>.Scale(distances.Z, node.Metrics.ScaleY, NumericMath), LayoutNumericKernel<double, CookNumericOperations>.Scale(distances.W, node.Metrics.ScaleY, NumericMath));
            }
            float3 position = node.ResolvedPosition;
            node.ResolvedSize = AnchorLayoutMath.Resolve(layout.AnchorFlags, distances, spaceSize, origin, node.ResolvedSize, ref position, NumericPolicy);
            node.ResolvedPosition = position;
            if (node.BakeGeometry) {
                foreach (Component component in node.Components) { ViewportLayoutApplier.ApplyResolvedSize(component, node.ResolvedSize); }
            }
        }

        /// <summary>Turns unresolved input into an owner-specific diagnostic rather than a fallback.</summary>
        static void RequireKnown(ViewportLayoutCookNode node, bool known, string parameter) {
            if (!known) { throw Failure(node, parameter); }
        }

        /// <summary>Identifies the viewport and unresolved presentation input in cook failures.</summary>
        static InvalidOperationException Failure(ViewportLayoutCookNode node, string parameter) {
            return new InvalidOperationException($"Viewport '{node.Asset.Name}' (entity {node.Asset.Id}) cannot Cook unresolved {parameter}.");
        }
    }
}
