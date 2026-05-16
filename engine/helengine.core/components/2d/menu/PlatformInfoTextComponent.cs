namespace helengine {
    /// <summary>
    /// Applies the current platform name and version to the baked demo-menu overlay text lines.
    /// </summary>
    public sealed class PlatformInfoTextComponent : UpdateComponent {
        /// <summary>
        /// Stable serialized component type id used by packaged runtime scenes.
        /// </summary>
        public const string SerializedComponentTypeId = "helengine.PlatformInfoTextComponent";

        /// <summary>
        /// Cached child entity that renders the platform name.
        /// </summary>
        Entity PlatformNameTextEntity;

        /// <summary>
        /// Cached child entity that renders the platform version.
        /// </summary>
        Entity PlatformVersionTextEntity;

        /// <summary>
        /// Cached text component that renders the platform name.
        /// </summary>
        TextComponent PlatformNameTextComponent;

        /// <summary>
        /// Cached text component that renders the platform version.
        /// </summary>
        TextComponent PlatformVersionTextComponent;

        /// <summary>
        /// Resolves the child text components and applies platform info once the overlay hierarchy is initialized.
        /// </summary>
        /// <param name="entity">Owning overlay entity.</param>
        public override void ComponentInitialized(Entity entity) {
            base.ComponentInitialized(entity);
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }

            BindTextEntities(entity);
            string platformName = Core.Instance.PlatformInfo.Name;
            string platformVersion = Core.Instance.PlatformInfo.Version;
            ApplyText(PlatformNameTextEntity, PlatformNameTextComponent, platformName, 0f);
            ApplyText(PlatformVersionTextEntity, PlatformVersionTextComponent, platformVersion, PlatformNameTextComponent.Size.Y + 6f);
        }

        /// <summary>
        /// Resolves the two direct child text entities that render the platform overlay.
        /// </summary>
        /// <param name="entity">Owning overlay entity.</param>
        void BindTextEntities(Entity entity) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            } else if (entity.Children == null || entity.Children.Count < 2) {
                throw new InvalidOperationException("Platform-info overlay requires two child text entities.");
            }

            PlatformNameTextEntity = FindRequiredChildEntity(entity, 0);
            PlatformVersionTextEntity = FindRequiredChildEntity(entity, 1);
            PlatformNameTextComponent = FindTextComponent(PlatformNameTextEntity);
            PlatformVersionTextComponent = FindTextComponent(PlatformVersionTextEntity);
        }

        /// <summary>
        /// Applies one text line to the supplied overlay child entity.
        /// </summary>
        /// <param name="entity">Child entity whose position should be updated.</param>
        /// <param name="textComponent">Text component that should render the line.</param>
        /// <param name="text">Resolved text value.</param>
        /// <param name="topOffset">Vertical offset from the overlay origin.</param>
        void ApplyText(Entity entity, TextComponent textComponent, string text, float topOffset) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            } else if (textComponent == null) {
                throw new ArgumentNullException(nameof(textComponent));
            }

            textComponent.Text = text;
            float2 measuredSize = textComponent.Font.MeasureString(text);
            textComponent.Size = new int2((int)Math.Ceiling(measuredSize.X), (int)Math.Ceiling(measuredSize.Y));
            entity.LocalPosition = new float3(-textComponent.Size.X, topOffset, 0f);
        }

        /// <summary>
        /// Resolves one required direct child entity by index.
        /// </summary>
        /// <param name="parentEntity">Parent overlay entity.</param>
        /// <param name="childIndex">Direct child index to resolve.</param>
        /// <returns>Resolved child entity.</returns>
        Entity FindRequiredChildEntity(Entity parentEntity, int childIndex) {
            if (parentEntity == null) {
                throw new ArgumentNullException(nameof(parentEntity));
            } else if (parentEntity.Children == null) {
                throw new InvalidOperationException("Platform-info overlay requires child entities.");
            } else if (childIndex < 0 || childIndex >= parentEntity.Children.Count) {
                throw new InvalidOperationException($"Platform-info overlay is missing child entity at index {childIndex}.");
            }

            return parentEntity.Children[childIndex];
        }

        /// <summary>
        /// Resolves the text component attached to one overlay child entity.
        /// </summary>
        /// <param name="entity">Child entity whose text component should be returned.</param>
        /// <returns>Attached text component.</returns>
        TextComponent FindTextComponent(Entity entity) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }

            for (int index = 0; index < entity.Components.Count; index++) {
                if (entity.Components[index] is TextComponent textComponent) {
                    return textComponent;
                }
            }

            throw new InvalidOperationException("Platform-info overlay child must include a text component.");
        }
    }
}
