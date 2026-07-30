namespace helengine {
    /// <summary>
    /// Creates real engine entity hierarchies used to verify public HelPhysics scene binding behavior.
    /// </summary>
    public static class HelPhysicsTestSceneFactory3D {
        /// <summary>
        /// Creates one root containing a static ground and four dynamic boxes distributed across nested child levels.
        /// </summary>
        /// <returns>A fully authored hierarchy whose five physics entities are ready for runtime binding.</returns>
        public static Entity CreateNestedGroundAndFourBoxScene() {
            Entity root = CreateEntity(float3.Zero);
            Entity ground = CreateBoxEntity(
                new float3(0f, -0.5f, 0f),
                new float3(10f, 1f, 10f),
                BodyKind3D.Static);
            Entity firstBox = CreateBoxEntity(
                new float3(0f, 0.5f, 0f),
                float3.One,
                BodyKind3D.Dynamic);
            Entity nestedGroup = CreateEntity(float3.Zero);
            Entity secondBox = CreateBoxEntity(
                new float3(0f, 1.5f, 0f),
                float3.One,
                BodyKind3D.Dynamic);
            Entity deeperGroup = CreateEntity(float3.Zero);
            Entity thirdBox = CreateBoxEntity(
                new float3(0f, 2.5f, 0f),
                float3.One,
                BodyKind3D.Dynamic);
            Entity fourthBox = CreateBoxEntity(
                new float3(0f, 3.5f, 0f),
                float3.One,
                BodyKind3D.Dynamic);

            root.AddChild(ground);
            root.AddChild(firstBox);
            root.AddChild(nestedGroup);
            nestedGroup.AddChild(secondBox);
            nestedGroup.AddChild(deeperGroup);
            deeperGroup.AddChild(thirdBox);
            deeperGroup.AddChild(fourthBox);
            return root;
        }

        /// <summary>
        /// Creates one entity with initialized component and child collections at the supplied local position.
        /// </summary>
        /// <param name="localPosition">Local position authored before optional parenting.</param>
        /// <returns>An empty entity ready to receive components and children.</returns>
        public static Entity CreateEntity(float3 localPosition) {
            Entity entity = new Entity {
                LocalPosition = localPosition
            };
            entity.InitComponents();
            entity.InitChildren();
            return entity;
        }

        /// <summary>
        /// Creates one entity carrying exactly one rigid body and one box collider.
        /// </summary>
        /// <param name="localPosition">Local position authored before optional parenting.</param>
        /// <param name="boxSize">Full unscaled collider size.</param>
        /// <param name="bodyKind">Physics participation mode authored on the rigid body.</param>
        /// <returns>An entity that satisfies the supported HelPhysics translation shape.</returns>
        public static Entity CreateBoxEntity(float3 localPosition, float3 boxSize, BodyKind3D bodyKind) {
            Entity entity = CreateEntity(localPosition);
            entity.AddComponent(new RigidBody3DComponent {
                BodyKind = bodyKind
            });
            entity.AddComponent(new BoxCollider3DComponent {
                Size = boxSize
            });
            return entity;
        }

        /// <summary>
        /// Creates one malformed physics entity for a named component-composition validation case.
        /// </summary>
        /// <param name="invalidCase">Stable test case name selecting the malformed component set.</param>
        /// <returns>An entity whose component composition must be rejected by strict HelPhysics translation.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the case name is unknown.</exception>
        public static Entity CreateInvalidPhysicsEntity(string invalidCase) {
            Entity entity = CreateEntity(float3.Zero);
            if (invalidCase == "collider-without-body") {
                entity.AddComponent(new BoxCollider3DComponent());
            } else if (invalidCase == "body-without-collider") {
                entity.AddComponent(new RigidBody3DComponent());
            } else if (invalidCase == "multiple-colliders") {
                entity.AddComponent(new RigidBody3DComponent());
                entity.AddComponent(new BoxCollider3DComponent());
                entity.AddComponent(new BoxCollider3DComponent());
            } else if (invalidCase == "multiple-rigid-bodies") {
                entity.AddComponent(new RigidBody3DComponent());
                entity.AddComponent(new RigidBody3DComponent());
                entity.AddComponent(new BoxCollider3DComponent());
            } else if (invalidCase == "static-mesh") {
                entity.AddComponent(new RigidBody3DComponent {
                    BodyKind = BodyKind3D.Static
                });
                entity.AddComponent(new StaticMeshCollider3DComponent());
            } else {
                throw new ArgumentOutOfRangeException(nameof(invalidCase), invalidCase, "Unknown invalid physics entity case.");
            }

            return entity;
        }

        /// <summary>
        /// Creates a hierarchy with one valid box followed by a box whose effective world scale is invalid.
        /// </summary>
        /// <param name="invalidScale">Scale applied to the second box after parenting.</param>
        /// <returns>A hierarchy that verifies full preflight validation prevents partial binding.</returns>
        public static Entity CreateHierarchyWithInvalidScaledBox(float3 invalidScale) {
            Entity root = CreateEntity(float3.Zero);
            Entity validBox = CreateBoxEntity(float3.Zero, float3.One, BodyKind3D.Dynamic);
            Entity invalidBox = CreateBoxEntity(float3.Zero, float3.One, BodyKind3D.Dynamic);
            invalidBox.LocalScale = invalidScale;
            root.AddChild(validBox);
            root.AddChild(invalidBox);
            return root;
        }
    }
}
