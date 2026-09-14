namespace helengine.editor {
    /// <summary>
    /// Owns the workspace-layout rules that stand behind the editor's panel and dock chrome:
    /// translating between the live dock snapshot and its persisted document form, pruning a
    /// restored layout down to the panel instances that actually came back, mapping workspace slot
    /// menu actions to slot numbers, and placing newly floated panels. The editor session still owns
    /// the panel instances and the docking manager and applies these decisions to them.
    /// </summary>
    public static class EditorWorkspacePanelCoordinator {
        /// <summary>
        /// Filters one persisted dock tree so it contains only workspace panel instances restored in the current session.
        /// A branch whose panels all failed to restore collapses away instead of leaving an empty dock region behind.
        /// </summary>
        /// <param name="node">Persisted dock node to filter.</param>
        /// <param name="restoredInstanceIds">Stable panel instance identifiers restored in the current session.</param>
        /// <returns>Filtered dock node, or null when the node no longer contains any restored panel instances.</returns>
        public static EditorWorkspaceDockNodeDocument FilterDockDocumentNode(EditorWorkspaceDockNodeDocument node, HashSet<string> restoredInstanceIds) {
            if (node == null) {
                return null;
            }
            if (restoredInstanceIds == null) {
                throw new ArgumentNullException(nameof(restoredInstanceIds));
            }

            if (node is EditorWorkspaceDockLeafNodeDocument leaf) {
                List<string> filteredInstanceIds = new List<string>();
                for (int index = 0; index < leaf.InstanceIds.Count; index++) {
                    string instanceId = leaf.InstanceIds[index];
                    if (restoredInstanceIds.Contains(instanceId)) {
                        filteredInstanceIds.Add(instanceId);
                    }
                }

                if (filteredInstanceIds.Count == 0) {
                    return null;
                }

                string activeInstanceId;
                if (filteredInstanceIds.Contains(leaf.ActiveInstanceId)) {
                    activeInstanceId = leaf.ActiveInstanceId;
                } else {
                    activeInstanceId = filteredInstanceIds[0];
                }

                return new EditorWorkspaceDockLeafNodeDocument {
                    InstanceIds = filteredInstanceIds,
                    ActiveInstanceId = activeInstanceId
                };
            }

            if (node is EditorWorkspaceDockSplitNodeDocument split) {
                EditorWorkspaceDockNodeDocument first = FilterDockDocumentNode(split.First, restoredInstanceIds);
                EditorWorkspaceDockNodeDocument second = FilterDockDocumentNode(split.Second, restoredInstanceIds);
                if (first == null && second == null) {
                    return null;
                }
                if (first == null) {
                    return second;
                }
                if (second == null) {
                    return first;
                }

                return new EditorWorkspaceDockSplitNodeDocument {
                    IsVertical = split.IsVertical,
                    SplitFraction = split.SplitFraction,
                    First = first,
                    Second = second
                };
            }

            throw new InvalidOperationException("Unsupported workspace dock document node type.");
        }

        /// <summary>
        /// Converts one captured dock snapshot node into its persisted document counterpart.
        /// </summary>
        /// <param name="node">Captured dock snapshot node.</param>
        /// <returns>Persisted dock node document.</returns>
        public static EditorWorkspaceDockNodeDocument ConvertDockSnapshotNodeToDocument(EditorWorkspaceDockNodeSnapshot node) {
            if (node == null) {
                return null;
            }
            if (node is EditorWorkspaceDockLeafSnapshot leafSnapshot) {
                return new EditorWorkspaceDockLeafNodeDocument {
                    ActiveInstanceId = leafSnapshot.ActiveInstanceId,
                    InstanceIds = new List<string>(leafSnapshot.InstanceIds)
                };
            }

            EditorWorkspaceDockSplitSnapshot splitSnapshot = node as EditorWorkspaceDockSplitSnapshot;
            if (splitSnapshot == null) {
                throw new InvalidOperationException("Unsupported dock snapshot node type.");
            }

            return new EditorWorkspaceDockSplitNodeDocument {
                IsVertical = splitSnapshot.IsVertical,
                SplitFraction = splitSnapshot.SplitFraction,
                First = ConvertDockSnapshotNodeToDocument(splitSnapshot.First),
                Second = ConvertDockSnapshotNodeToDocument(splitSnapshot.Second)
            };
        }

        /// <summary>
        /// Converts one persisted dock node document back into its runtime snapshot counterpart.
        /// </summary>
        /// <param name="node">Persisted dock node document.</param>
        /// <returns>Runtime dock snapshot node.</returns>
        public static EditorWorkspaceDockNodeSnapshot ConvertDockDocumentNodeToSnapshot(EditorWorkspaceDockNodeDocument node) {
            if (node == null) {
                return null;
            }
            if (node is EditorWorkspaceDockLeafNodeDocument leafDocument) {
                return new EditorWorkspaceDockLeafSnapshot {
                    ActiveInstanceId = leafDocument.ActiveInstanceId,
                    InstanceIds = new List<string>(leafDocument.InstanceIds)
                };
            }

            EditorWorkspaceDockSplitNodeDocument splitDocument = node as EditorWorkspaceDockSplitNodeDocument;
            if (splitDocument == null) {
                throw new InvalidOperationException("Unsupported dock document node type.");
            }

            return new EditorWorkspaceDockSplitSnapshot {
                IsVertical = splitDocument.IsVertical,
                SplitFraction = splitDocument.SplitFraction,
                First = ConvertDockDocumentNodeToSnapshot(splitDocument.First),
                Second = ConvertDockDocumentNodeToSnapshot(splitDocument.Second)
            };
        }

        /// <summary>
        /// Resolves the workspace slot number associated with one UI menu action.
        /// </summary>
        /// <param name="action">Workspace UI menu action.</param>
        /// <param name="slotNumber">Resolved one-based slot number when successful.</param>
        /// <returns>True when the action maps to a slot; otherwise false.</returns>
        public static bool TryResolveWorkspaceSlotNumber(EditorTitleBarUiMenuAction action, out int slotNumber) {
            slotNumber = 0;
            if (action == EditorTitleBarUiMenuAction.SaveSlot1 || action == EditorTitleBarUiMenuAction.LoadSlot1) {
                slotNumber = 1;
                return true;
            }
            if (action == EditorTitleBarUiMenuAction.SaveSlot2 || action == EditorTitleBarUiMenuAction.LoadSlot2) {
                slotNumber = 2;
                return true;
            }
            if (action == EditorTitleBarUiMenuAction.SaveSlot3 || action == EditorTitleBarUiMenuAction.LoadSlot3) {
                slotNumber = 3;
                return true;
            }
            if (action == EditorTitleBarUiMenuAction.SaveSlot4 || action == EditorTitleBarUiMenuAction.LoadSlot4) {
                slotNumber = 4;
                return true;
            }
            if (action == EditorTitleBarUiMenuAction.SaveSlot5 || action == EditorTitleBarUiMenuAction.LoadSlot5) {
                slotNumber = 5;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Determines whether one workspace UI menu action saves a slot instead of loading it.
        /// </summary>
        /// <param name="action">Workspace UI menu action.</param>
        /// <returns>True when the action saves a slot; otherwise false.</returns>
        public static bool IsWorkspaceSaveAction(EditorTitleBarUiMenuAction action) {
            return action == EditorTitleBarUiMenuAction.SaveSlot1 ||
                   action == EditorTitleBarUiMenuAction.SaveSlot2 ||
                   action == EditorTitleBarUiMenuAction.SaveSlot3 ||
                   action == EditorTitleBarUiMenuAction.SaveSlot4 ||
                   action == EditorTitleBarUiMenuAction.SaveSlot5;
        }

        /// <summary>
        /// Resolves the default centered floating position for one newly created panel instance.
        /// The panel is centered inside the workspace area below the editor title bar; before the
        /// host has reported a layout size there is no area to center in, so the origin is used.
        /// </summary>
        /// <param name="layoutWidth">Most recent host layout width in pixels.</param>
        /// <param name="layoutHeight">Most recent host layout height in pixels.</param>
        /// <param name="titleBarHeight">Height of the editor title bar in pixels.</param>
        /// <param name="panelSize">Requested panel size.</param>
        /// <returns>Centered floating origin inside the available workspace area.</returns>
        public static float3 ResolveCenteredFloatingPanelPosition(int layoutWidth, int layoutHeight, int titleBarHeight, int2 panelSize) {
            if (layoutWidth <= 0 || layoutHeight <= 0) {
                return float3.Zero;
            }

            int availableHeight = Math.Max(0, layoutHeight - titleBarHeight);
            int x = Math.Max(0, (layoutWidth - panelSize.X) / 2);
            int y = titleBarHeight + Math.Max(0, (availableHeight - panelSize.Y) / 2);
            return new float3(x, y, 0f);
        }
    }
}
