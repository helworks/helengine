namespace helengine.editor {
    /// <summary>
    /// Turns a concrete build target (platform, environment) into its override scope path under an entity's level order,
    /// and selects the deepest authored prefix of that path. Used by the packager and the viewport so both agree.
    /// </summary>
    public sealed class EditorOverrideScopeResolver {
        readonly EditorProjectPlatformGroupsDocument Groups;

        /// <summary>
        /// Initializes the resolver and validates the group tree against the supported platforms; inconsistent settings throw here.
        /// </summary>
        public EditorOverrideScopeResolver(EditorProjectPlatformGroupsDocument groups, IReadOnlyList<string> supportedPlatformIds) {
            if (groups == null) {
                throw new ArgumentNullException(nameof(groups));
            }
            if (supportedPlatformIds == null) {
                throw new ArgumentNullException(nameof(supportedPlatformIds));
            }

            Groups = groups;
            EditorProjectPlatformGroupsService.Validate(Groups, supportedPlatformIds);
        }

        /// <summary>
        /// Initializes a resolver over an already validated group tree.
        /// </summary>
        EditorOverrideScopeResolver(EditorProjectPlatformGroupsDocument groups) {
            Groups = groups;
        }

        /// <summary>
        /// Reads <c>settings/platform-groups.json</c> and <c>settings/platforms.json</c> from a project and builds a
        /// resolver. Never writes to disk, so packaging and other read-only callers can build a resolver concurrently
        /// without racing to seed default settings files. Inconsistent settings throw here.
        /// </summary>
        public static EditorOverrideScopeResolver Load(string projectRootPath) {
            if (!TryLoad(projectRootPath, out EditorOverrideScopeResolver resolver, out string error)) {
                throw new InvalidOperationException(error);
            }

            return resolver;
        }

        /// <summary>
        /// Builds the same resolver as <see cref="Load"/> and reports inconsistent settings instead of throwing, so
        /// callers on an interactive path, such as the viewport, can report the problem and carry on. Validation never
        /// throws internally on this path.
        /// </summary>
        /// <param name="projectRootPath">Absolute project root path whose settings should be read.</param>
        /// <param name="resolver">Resolver built over the project's group tree when the settings are consistent.</param>
        /// <param name="error">Message naming the offending settings entry when the settings are inconsistent; empty otherwise.</param>
        /// <returns>True when a resolver was built; otherwise false.</returns>
        public static bool TryLoad(string projectRootPath, out EditorOverrideScopeResolver resolver, out string error) {
            EditorProjectPlatformGroupsDocument groups = new EditorProjectPlatformGroupsService(projectRootPath).Read();
            IReadOnlyList<string> platforms = new EditorProjectPlatformsService(projectRootPath).Read().SupportedPlatforms;
            if (!EditorProjectPlatformGroupsService.TryValidate(groups, platforms, out error)) {
                resolver = null;
                return false;
            }

            resolver = new EditorOverrideScopeResolver(groups);
            return true;
        }

        /// <summary>
        /// Returns the entity's order when authored, else the project default, else <see cref="EditorOverrideLevelOrder.Default"/>.
        /// </summary>
        public IReadOnlyList<SceneOverrideScopeStepKind> ResolveLevelOrder(IReadOnlyList<SceneOverrideScopeStepKind> entityOrder) {
            if (entityOrder != null) {
                return entityOrder;
            }
            if (Groups.DefaultLevelOrder != null && Groups.DefaultLevelOrder.Count > 0) {
                return Groups.DefaultLevelOrder;
            }

            return EditorOverrideLevelOrder.Default;
        }

        /// <summary>
        /// Builds the path for one target: per level, the group chain containing the platform, the platform, or the
        /// environment. A blank environment ends the path at the Build Config level.
        /// </summary>
        public EditorOverrideScope BuildTargetPath(IReadOnlyList<SceneOverrideScopeStepKind> levelOrder, string platformId, string environmentId) {
            if (levelOrder == null) {
                throw new ArgumentNullException(nameof(levelOrder));
            }
            if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }

            EditorOverrideLevelOrder.Validate(levelOrder);
            EditorOverrideScope path = EditorOverrideScope.Common;
            for (int level = 0; level < levelOrder.Count; level++) {
                SceneOverrideScopeStepKind kind = levelOrder[level];
                if (kind == SceneOverrideScopeStepKind.Group) {
                    IReadOnlyList<string> chain = EditorProjectPlatformGroupsService.FindGroupChain(Groups, platformId);
                    for (int index = 0; index < chain.Count; index++) {
                        path = path.Append(new EditorOverrideScopeStep(SceneOverrideScopeStepKind.Group, chain[index]));
                    }
                } else if (kind == SceneOverrideScopeStepKind.Platform) {
                    path = path.Append(new EditorOverrideScopeStep(SceneOverrideScopeStepKind.Platform, platformId));
                } else {
                    if (string.IsNullOrWhiteSpace(environmentId)) {
                        break;
                    }
                    path = path.Append(new EditorOverrideScopeStep(SceneOverrideScopeStepKind.BuildConfig, environmentId));
                }
            }

            return path;
        }

        /// <summary>
        /// Selects the item whose scope is the longest prefix of <paramref name="target"/>; Common counts as a prefix.
        /// </summary>
        public static bool TrySelectDeepest<T>(IReadOnlyList<T> items, Func<T, EditorOverrideScope> scopeSelector, EditorOverrideScope target, out T selected) {
            if (items == null) {
                throw new ArgumentNullException(nameof(items));
            }
            if (scopeSelector == null) {
                throw new ArgumentNullException(nameof(scopeSelector));
            }

            selected = default;
            int bestDepth = -1;
            for (int index = 0; index < items.Count; index++) {
                if (items[index] == null) {
                    continue;
                }

                EditorOverrideScope scope = scopeSelector(items[index]);
                if (scope.Depth > bestDepth && scope.IsPrefixOf(target)) {
                    bestDepth = scope.Depth;
                    selected = items[index];
                }
            }

            return bestDepth >= 0;
        }
    }
}
