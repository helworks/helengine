namespace helengine.editor {
    /// <summary>
    /// Tracks editor keyboard focus across dock groups and focus targets.
    /// </summary>
    public static class EditorKeyboardFocusService {
        /// <summary>
        /// Registered focus groups known to the editor.
        /// </summary>
        static readonly List<IFocusGroup> RegisteredGroups = new List<IFocusGroup>();
        /// <summary>
        /// Registered focus targets known to the editor.
        /// </summary>
        static readonly List<IFocusTarget> RegisteredTargets = new List<IFocusTarget>();
        /// <summary>
        /// Stable registration sequence assigned to each focus target.
        /// </summary>
        static readonly Dictionary<IFocusTarget, int> TargetRegistrationOrder = new Dictionary<IFocusTarget, int>();
        /// <summary>
        /// Dock traversal order published by the current editor layout.
        /// </summary>
        static readonly List<DockableEntity> DockOrder = new List<DockableEntity>();
        /// <summary>
        /// Next registration sequence assigned to a new focus target.
        /// </summary>
        static int NextTargetRegistrationOrder;
        /// <summary>
        /// Root dock group that is currently active.
        /// </summary>
        static IFocusGroup ActiveRootGroup;
        /// <summary>
        /// Target that is currently focused.
        /// </summary>
        static IFocusTarget FocusedTarget;

        /// <summary>
        /// Registers one focus group if it has not already been registered.
        /// </summary>
        /// <param name="group">Group to register.</param>
        public static void RegisterGroup(IFocusGroup group) {
            if (group == null) {
                throw new ArgumentNullException(nameof(group));
            }

            if (!RegisteredGroups.Contains(group)) {
                RegisteredGroups.Add(group);
            }
        }

        /// <summary>
        /// Unregisters one focus group and clears active state if it owned the current activation.
        /// </summary>
        /// <param name="group">Group to unregister.</param>
        public static void UnregisterGroup(IFocusGroup group) {
            if (group == null) {
                throw new ArgumentNullException(nameof(group));
            }

            if (!RegisteredGroups.Remove(group)) {
                return;
            }

            if (ReferenceEquals(ResolveRootGroup(group), ActiveRootGroup)) {
                SetActiveRootGroup(null);
            }
        }

        /// <summary>
        /// Registers one focus target if it has not already been registered.
        /// </summary>
        /// <param name="target">Target to register.</param>
        public static void RegisterTarget(IFocusTarget target) {
            if (target == null) {
                throw new ArgumentNullException(nameof(target));
            }

            if (RegisteredTargets.Contains(target)) {
                return;
            }

            RegisteredTargets.Add(target);
            TargetRegistrationOrder[target] = NextTargetRegistrationOrder;
            NextTargetRegistrationOrder++;
        }

        /// <summary>
        /// Unregisters one focus target and clears focused state if it was active.
        /// </summary>
        /// <param name="target">Target to unregister.</param>
        public static void UnregisterTarget(IFocusTarget target) {
            if (target == null) {
                throw new ArgumentNullException(nameof(target));
            }

            if (!RegisteredTargets.Remove(target)) {
                return;
            }

            TargetRegistrationOrder.Remove(target);
            if (ReferenceEquals(FocusedTarget, target)) {
                target.SetTargetFocused(false);
                FocusedTarget = null;
            }
        }

        /// <summary>
        /// Publishes the current dock traversal order from the editor layout.
        /// </summary>
        /// <param name="dockOrder">Visible dock order from the current layout.</param>
        public static void SetDockOrder(IReadOnlyList<DockableEntity> dockOrder) {
            if (dockOrder == null) {
                throw new ArgumentNullException(nameof(dockOrder));
            }

            DockOrder.Clear();
            for (int i = 0; i < dockOrder.Count; i++) {
                DockableEntity dock = dockOrder[i];
                if (dock != null) {
                    DockOrder.Add(dock);
                }
            }
        }

        /// <summary>
        /// Applies focus to one target and activates its root dock.
        /// </summary>
        /// <param name="target">Target that should become focused.</param>
        public static void SetFocusedTarget(IFocusTarget target) {
            if (target == null) {
                throw new ArgumentNullException(nameof(target));
            }

            if (!IsTargetValid(target)) {
                return;
            }

            ApplyFocusedTarget(target);
        }

        /// <summary>
        /// Moves dock-local focus forward or backward inside the active root dock.
        /// </summary>
        /// <param name="forward">True to move forward; false to move backward.</param>
        public static void HandleTab(bool forward) {
            IFocusGroup rootGroup = ResolveTraversalRootGroup();
            if (rootGroup == null) {
                return;
            }

            List<IFocusTarget> targets = GetOrderedTargetsForRoot(rootGroup);
            if (targets.Count == 0) {
                return;
            }

            int targetIndex = targets.IndexOf(FocusedTarget);
            if (targetIndex < 0) {
                targetIndex = forward ? 0 : targets.Count - 1;
            } else if (forward) {
                targetIndex++;
                if (targetIndex >= targets.Count) {
                    targetIndex = 0;
                }
            } else {
                targetIndex--;
                if (targetIndex < 0) {
                    targetIndex = targets.Count - 1;
                }
            }

            ApplyFocusedTarget(targets[targetIndex]);
        }

        /// <summary>
        /// Moves active focus to the next or previous visible dock.
        /// </summary>
        /// <param name="forward">True to move forward; false to move backward.</param>
        public static void HandleCtrlTab(bool forward) {
            if (DockOrder.Count == 0) {
                return;
            }

            int currentIndex = GetCurrentDockIndex();
            if (currentIndex < 0) {
                currentIndex = forward ? -1 : 0;
            }

            for (int step = 0; step < DockOrder.Count; step++) {
                currentIndex = forward
                    ? (currentIndex + 1) % DockOrder.Count
                    : (currentIndex - 1 + DockOrder.Count) % DockOrder.Count;
                DockableEntity dock = DockOrder[currentIndex];
                if (dock == null || !dock.CanReceiveFocus) {
                    continue;
                }

                IFocusTarget target = FindPreferredTargetForRoot(dock);
                if (target != null) {
                    ApplyFocusedTarget(target);
                    return;
                }

                SetActiveRootGroup(dock);
                return;
            }
        }

        /// <summary>
        /// Routes an activation key into the currently focused target when supported.
        /// </summary>
        /// <param name="key">Activation key to route.</param>
        public static void HandleActivationKey(Keys key) {
            if (!IsTargetValid(FocusedTarget)) {
                return;
            }
            if (!FocusedTarget.CanActivateWithKey(key)) {
                return;
            }

            FocusedTarget.ActivateFromKey(key);
        }

        /// <summary>
        /// Synchronizes pointer presses into dock activation and target focus.
        /// </summary>
        /// <param name="point">Pointer location in screen coordinates.</param>
        /// <param name="isRightButton">True when the press came from the right mouse button.</param>
        public static void HandlePointerPressed(int2 point, bool isRightButton) {
            IFocusTarget target = FindTargetAtPoint(point);
            if (target != null) {
                ApplyFocusedTarget(target);
                return;
            }

            IFocusGroup rootGroup = FindRootGroupAtPoint(point);
            if (rootGroup == null) {
                return;
            }

            SetActiveRootGroup(rootGroup);
            if (!isRightButton &&
                FocusedTarget != null &&
                !ReferenceEquals(ResolveRootGroup(FocusedTarget.FocusGroup), rootGroup)) {
                FocusedTarget.SetTargetFocused(false);
                FocusedTarget = null;
            }
        }

        /// <summary>
        /// Repairs focus state after targets or groups change availability.
        /// </summary>
        public static void Update() {
            if (IsTargetValid(FocusedTarget)) {
                return;
            }

            if (FocusedTarget != null) {
                FocusedTarget.SetTargetFocused(false);
                FocusedTarget = null;
            }

            IFocusGroup rootGroup = ResolveTraversalRootGroup();
            if (rootGroup == null) {
                return;
            }

            IFocusTarget target = FindPreferredTargetForRoot(rootGroup);
            if (target != null) {
                ApplyFocusedTarget(target);
            } else {
                SetActiveRootGroup(rootGroup);
            }
        }

        /// <summary>
        /// Clears all registered focus state and visuals.
        /// </summary>
        public static void Reset() {
            if (FocusedTarget != null) {
                FocusedTarget.SetTargetFocused(false);
                FocusedTarget = null;
            }

            SetActiveRootGroup(null);
            RegisteredGroups.Clear();
            RegisteredTargets.Clear();
            TargetRegistrationOrder.Clear();
            DockOrder.Clear();
            NextTargetRegistrationOrder = 0;
        }

        /// <summary>
        /// Applies one target as the focused target and activates its root dock.
        /// </summary>
        /// <param name="target">Target that should become focused.</param>
        static void ApplyFocusedTarget(IFocusTarget target) {
            if (FocusedTarget != null && !ReferenceEquals(FocusedTarget, target)) {
                FocusedTarget.SetTargetFocused(false);
            }

            FocusedTarget = target;
            SetActiveRootGroup(ResolveRootGroup(target.FocusGroup));
            target.SetTargetFocused(true);
        }

        /// <summary>
        /// Sets the active root dock and clears active state on all other registered groups.
        /// </summary>
        /// <param name="rootGroup">Root group that should become active.</param>
        static void SetActiveRootGroup(IFocusGroup rootGroup) {
            ActiveRootGroup = rootGroup;
            for (int i = 0; i < RegisteredGroups.Count; i++) {
                IFocusGroup group = RegisteredGroups[i];
                IFocusGroup groupRoot = ResolveRootGroup(group);
                bool isActive = rootGroup != null &&
                    ReferenceEquals(group, groupRoot) &&
                    ReferenceEquals(groupRoot, rootGroup);
                group.SetGroupActive(isActive);
            }
        }

        /// <summary>
        /// Resolves the root group that should currently drive traversal.
        /// </summary>
        /// <returns>Root group used for traversal, or null when none is available.</returns>
        static IFocusGroup ResolveTraversalRootGroup() {
            if (ActiveRootGroup != null && ActiveRootGroup.CanReceiveFocus) {
                return ActiveRootGroup;
            }
            if (FocusedTarget != null && IsTargetValid(FocusedTarget)) {
                return ResolveRootGroup(FocusedTarget.FocusGroup);
            }

            for (int i = 0; i < DockOrder.Count; i++) {
                DockableEntity dock = DockOrder[i];
                if (dock != null && dock.CanReceiveFocus) {
                    return dock;
                }
            }

            return null;
        }

        /// <summary>
        /// Resolves the root dock group for one focus group.
        /// </summary>
        /// <param name="group">Group whose root should be resolved.</param>
        /// <returns>Resolved root group, or null when the input group is null.</returns>
        static IFocusGroup ResolveRootGroup(IFocusGroup group) {
            if (group == null) {
                return null;
            }

            return group.RootGroup ?? group;
        }

        /// <summary>
        /// Returns true when the provided target remains registered and eligible for focus.
        /// </summary>
        /// <param name="target">Target to validate.</param>
        /// <returns>True when the target remains focusable.</returns>
        static bool IsTargetValid(IFocusTarget target) {
            if (target == null) {
                return false;
            }
            if (!RegisteredTargets.Contains(target)) {
                return false;
            }
            if (!target.CanReceiveFocus) {
                return false;
            }
            if (target.FocusGroup == null) {
                return false;
            }
            if (!target.FocusGroup.CanReceiveFocus) {
                return false;
            }

            IFocusGroup rootGroup = ResolveRootGroup(target.FocusGroup);
            return rootGroup != null && rootGroup.CanReceiveFocus;
        }

        /// <summary>
        /// Collects all valid targets that belong to one root dock group in traversal order.
        /// </summary>
        /// <param name="rootGroup">Root dock group to inspect.</param>
        /// <returns>Ordered valid targets for the root group.</returns>
        static List<IFocusTarget> GetOrderedTargetsForRoot(IFocusGroup rootGroup) {
            List<IFocusTarget> targets = new List<IFocusTarget>();
            for (int i = 0; i < RegisteredTargets.Count; i++) {
                IFocusTarget target = RegisteredTargets[i];
                if (!IsTargetValid(target)) {
                    continue;
                }
                if (!ReferenceEquals(ResolveRootGroup(target.FocusGroup), rootGroup)) {
                    continue;
                }

                targets.Add(target);
            }

            targets.Sort(CompareTargets);
            return targets;
        }

        /// <summary>
        /// Compares two targets by group order, tab index, then stable registration order.
        /// </summary>
        /// <param name="left">Left target to compare.</param>
        /// <param name="right">Right target to compare.</param>
        /// <returns>Sort order comparing the two targets.</returns>
        static int CompareTargets(IFocusTarget left, IFocusTarget right) {
            int groupComparison = left.FocusGroup.GroupOrder.CompareTo(right.FocusGroup.GroupOrder);
            if (groupComparison != 0) {
                return groupComparison;
            }

            int tabIndexComparison = left.TabIndex.CompareTo(right.TabIndex);
            if (tabIndexComparison != 0) {
                return tabIndexComparison;
            }

            return GetTargetRegistrationOrder(left).CompareTo(GetTargetRegistrationOrder(right));
        }

        /// <summary>
        /// Returns the stable registration order assigned to one target.
        /// </summary>
        /// <param name="target">Target whose registration order should be returned.</param>
        /// <returns>Stable registration order for the target.</returns>
        static int GetTargetRegistrationOrder(IFocusTarget target) {
            if (target == null) {
                throw new ArgumentNullException(nameof(target));
            }

            if (!TargetRegistrationOrder.TryGetValue(target, out int order)) {
                return int.MaxValue;
            }

            return order;
        }

        /// <summary>
        /// Finds the preferred focus target for one root dock group.
        /// </summary>
        /// <param name="rootGroup">Root dock group to inspect.</param>
        /// <returns>Preferred target for the root group, or null when none are available.</returns>
        static IFocusTarget FindPreferredTargetForRoot(IFocusGroup rootGroup) {
            List<IFocusTarget> targets = GetOrderedTargetsForRoot(rootGroup);
            for (int i = 0; i < targets.Count; i++) {
                if (targets[i].IsDefaultTarget) {
                    return targets[i];
                }
            }

            return targets.Count == 0 ? null : targets[0];
        }

        /// <summary>
        /// Finds the topmost registered target at the provided point.
        /// </summary>
        /// <param name="point">Screen point to evaluate.</param>
        /// <returns>Target at the point, or null when none match.</returns>
        static IFocusTarget FindTargetAtPoint(int2 point) {
            IFocusTarget matchingTarget = null;
            int bestOrder = int.MinValue;
            for (int i = 0; i < RegisteredTargets.Count; i++) {
                IFocusTarget target = RegisteredTargets[i];
                if (!IsTargetValid(target)) {
                    continue;
                }
                if (!target.ContainsScreenPoint(point.X, point.Y)) {
                    continue;
                }

                int order = GetTargetRegistrationOrder(target);
                if (matchingTarget == null || order >= bestOrder) {
                    matchingTarget = target;
                    bestOrder = order;
                }
            }

            return matchingTarget;
        }

        /// <summary>
        /// Finds the first matching root dock group that contains the provided point.
        /// </summary>
        /// <param name="point">Screen point to evaluate.</param>
        /// <returns>Root dock group at the point, or null when none match.</returns>
        static IFocusGroup FindRootGroupAtPoint(int2 point) {
            for (int i = DockOrder.Count - 1; i >= 0; i--) {
                DockableEntity dock = DockOrder[i];
                if (dock == null || !dock.CanReceiveFocus) {
                    continue;
                }
                if (dock.ContainsScreenPoint(point)) {
                    return dock;
                }
            }

            for (int i = RegisteredGroups.Count - 1; i >= 0; i--) {
                IFocusGroup group = RegisteredGroups[i];
                IFocusGroup rootGroup = ResolveRootGroup(group);
                if (!ReferenceEquals(group, rootGroup)) {
                    continue;
                }
                if (!group.CanReceiveFocus) {
                    continue;
                }
                if (group.ContainsScreenPoint(point)) {
                    return group;
                }
            }

            return null;
        }

        /// <summary>
        /// Returns the current dock index inside the published dock order.
        /// </summary>
        /// <returns>Current dock index, or -1 when no active dock is published.</returns>
        static int GetCurrentDockIndex() {
            if (ActiveRootGroup == null) {
                return -1;
            }

            for (int i = 0; i < DockOrder.Count; i++) {
                if (ReferenceEquals(DockOrder[i], ActiveRootGroup)) {
                    return i;
                }
            }

            return -1;
        }
    }
}
