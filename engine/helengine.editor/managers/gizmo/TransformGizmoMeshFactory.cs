namespace helengine.editor {
    /// <summary>
    /// Generates primitive meshes used by editor transform gizmos.
    /// </summary>
    public static class TransformGizmoMeshFactory {
        /// <summary>
        /// Builds a cylinder mesh aligned to +Y with its base at Y=0.
        /// </summary>
        /// <param name="radius">Cylinder radius in world units.</param>
        /// <param name="height">Cylinder height in world units.</param>
        /// <param name="segments">Segment count used for roundness.</param>
        /// <returns>Generated cylinder model asset.</returns>
        public static ModelAsset CreateCylinder(float radius, float height, int segments) {
            ValidatePrimitiveArguments(radius, height, segments);

            List<float3> positions = new List<float3>(segments * 6 + 2);
            List<float3> normals = new List<float3>(segments * 6 + 2);
            List<float2> texCoords = new List<float2>(segments * 6 + 2);
            List<int> indices = new List<int>(segments * 24);

            for (int i = 0; i < segments; i++) {
                double fraction = (double)i / segments;
                double angle = fraction * Math.PI * 2.0;
                float x = (float)(Math.Cos(angle) * radius);
                float z = (float)(Math.Sin(angle) * radius);
                float3 radialNormal = NormalizeXZ(x, z);

                positions.Add(new float3(x, 0f, z));
                normals.Add(radialNormal);
                texCoords.Add(new float2((float)fraction, 0f));

                positions.Add(new float3(x, height, z));
                normals.Add(radialNormal);
                texCoords.Add(new float2((float)fraction, 1f));
            }

            for (int i = 0; i < segments; i++) {
                int currentBottom = i * 2;
                int currentTop = currentBottom + 1;
                int nextBottom = ((i + 1) % segments) * 2;
                int nextTop = nextBottom + 1;

                AddDoubleSidedTriangle(indices, currentBottom, currentTop, nextTop);
                AddDoubleSidedTriangle(indices, currentBottom, nextTop, nextBottom);
            }

            int bottomCenterIndex = positions.Count;
            positions.Add(new float3(0f, 0f, 0f));
            normals.Add(new float3(0f, -1f, 0f));
            texCoords.Add(new float2(0.5f, 0.5f));
            int bottomRingStart = positions.Count;
            for (int i = 0; i < segments; i++) {
                double fraction = (double)i / segments;
                double angle = fraction * Math.PI * 2.0;
                float x = (float)(Math.Cos(angle) * radius);
                float z = (float)(Math.Sin(angle) * radius);
                positions.Add(new float3(x, 0f, z));
                normals.Add(new float3(0f, -1f, 0f));
                texCoords.Add(new float2((x / radius + 1f) * 0.5f, (z / radius + 1f) * 0.5f));
            }

            for (int i = 0; i < segments; i++) {
                int current = bottomRingStart + i;
                int next = bottomRingStart + ((i + 1) % segments);
                AddDoubleSidedTriangle(indices, bottomCenterIndex, next, current);
            }

            int topCenterIndex = positions.Count;
            positions.Add(new float3(0f, height, 0f));
            normals.Add(new float3(0f, 1f, 0f));
            texCoords.Add(new float2(0.5f, 0.5f));
            int topRingStart = positions.Count;
            for (int i = 0; i < segments; i++) {
                double fraction = (double)i / segments;
                double angle = fraction * Math.PI * 2.0;
                float x = (float)(Math.Cos(angle) * radius);
                float z = (float)(Math.Sin(angle) * radius);
                positions.Add(new float3(x, height, z));
                normals.Add(new float3(0f, 1f, 0f));
                texCoords.Add(new float2((x / radius + 1f) * 0.5f, (z / radius + 1f) * 0.5f));
            }

            for (int i = 0; i < segments; i++) {
                int current = topRingStart + i;
                int next = topRingStart + ((i + 1) % segments);
                AddDoubleSidedTriangle(indices, topCenterIndex, current, next);
            }

            return CreateModelAsset(positions, normals, texCoords, indices);
        }

        /// <summary>
        /// Builds a cone mesh aligned to +Y with its base at Y=0.
        /// </summary>
        /// <param name="radius">Cone base radius in world units.</param>
        /// <param name="height">Cone height in world units.</param>
        /// <param name="segments">Segment count used for roundness.</param>
        /// <returns>Generated cone model asset.</returns>
        public static ModelAsset CreateCone(float radius, float height, int segments) {
            ValidatePrimitiveArguments(radius, height, segments);

            List<float3> positions = new List<float3>(segments * 2 + 2);
            List<float3> normals = new List<float3>(segments * 2 + 2);
            List<float2> texCoords = new List<float2>(segments * 2 + 2);
            List<int> indices = new List<int>(segments * 12);

            double slope = radius / height;
            for (int i = 0; i < segments; i++) {
                double fraction = (double)i / segments;
                double angle = fraction * Math.PI * 2.0;
                float x = (float)(Math.Cos(angle) * radius);
                float z = (float)(Math.Sin(angle) * radius);
                float3 sideNormal = Normalize3(new float3(x, (float)slope, z));

                positions.Add(new float3(x, 0f, z));
                normals.Add(sideNormal);
                texCoords.Add(new float2((float)fraction, 0f));
            }

            int tipIndex = positions.Count;
            positions.Add(new float3(0f, height, 0f));
            normals.Add(new float3(0f, 1f, 0f));
            texCoords.Add(new float2(0.5f, 1f));

            for (int i = 0; i < segments; i++) {
                int current = i;
                int next = (i + 1) % segments;
                AddDoubleSidedTriangle(indices, current, tipIndex, next);
            }

            int baseCenterIndex = positions.Count;
            positions.Add(new float3(0f, 0f, 0f));
            normals.Add(new float3(0f, -1f, 0f));
            texCoords.Add(new float2(0.5f, 0.5f));

            int baseRingStart = positions.Count;
            for (int i = 0; i < segments; i++) {
                double fraction = (double)i / segments;
                double angle = fraction * Math.PI * 2.0;
                float x = (float)(Math.Cos(angle) * radius);
                float z = (float)(Math.Sin(angle) * radius);
                positions.Add(new float3(x, 0f, z));
                normals.Add(new float3(0f, -1f, 0f));
                texCoords.Add(new float2((x / radius + 1f) * 0.5f, (z / radius + 1f) * 0.5f));
            }

            for (int i = 0; i < segments; i++) {
                int current = baseRingStart + i;
                int next = baseRingStart + ((i + 1) % segments);
                AddDoubleSidedTriangle(indices, baseCenterIndex, current, next);
            }

            return CreateModelAsset(positions, normals, texCoords, indices);
        }

        /// <summary>
        /// Builds a box mesh aligned to +Y with its base resting at Y=0.
        /// </summary>
        /// <param name="width">Box width along local X.</param>
        /// <param name="height">Box height along local Y.</param>
        /// <param name="depth">Box depth along local Z.</param>
        /// <returns>Generated box model asset.</returns>
        public static ModelAsset CreateBox(float width, float height, float depth) {
            ValidateBoxArguments(width, height, depth);

            float halfWidth = width * 0.5f;
            float halfDepth = depth * 0.5f;

            List<float3> positions = new List<float3>(24);
            List<float3> normals = new List<float3>(24);
            List<float2> texCoords = new List<float2>(24);
            List<int> indices = new List<int>(36);

            AddBoxFace(
                positions,
                normals,
                texCoords,
                indices,
                new float3(-halfWidth, 0f, -halfDepth),
                new float3(halfWidth, 0f, -halfDepth),
                new float3(halfWidth, height, -halfDepth),
                new float3(-halfWidth, height, -halfDepth),
                new float3(0f, 0f, -1f));
            AddBoxFace(
                positions,
                normals,
                texCoords,
                indices,
                new float3(-halfWidth, 0f, halfDepth),
                new float3(-halfWidth, height, halfDepth),
                new float3(halfWidth, height, halfDepth),
                new float3(halfWidth, 0f, halfDepth),
                new float3(0f, 0f, 1f));
            AddBoxFace(
                positions,
                normals,
                texCoords,
                indices,
                new float3(halfWidth, 0f, -halfDepth),
                new float3(halfWidth, 0f, halfDepth),
                new float3(halfWidth, height, halfDepth),
                new float3(halfWidth, height, -halfDepth),
                new float3(1f, 0f, 0f));
            AddBoxFace(
                positions,
                normals,
                texCoords,
                indices,
                new float3(-halfWidth, 0f, halfDepth),
                new float3(-halfWidth, 0f, -halfDepth),
                new float3(-halfWidth, height, -halfDepth),
                new float3(-halfWidth, height, halfDepth),
                new float3(-1f, 0f, 0f));
            AddBoxFace(
                positions,
                normals,
                texCoords,
                indices,
                new float3(-halfWidth, height, -halfDepth),
                new float3(halfWidth, height, -halfDepth),
                new float3(halfWidth, height, halfDepth),
                new float3(-halfWidth, height, halfDepth),
                new float3(0f, 1f, 0f));
            AddBoxFace(
                positions,
                normals,
                texCoords,
                indices,
                new float3(-halfWidth, 0f, halfDepth),
                new float3(halfWidth, 0f, halfDepth),
                new float3(halfWidth, 0f, -halfDepth),
                new float3(-halfWidth, 0f, -halfDepth),
                new float3(0f, -1f, 0f));

            return CreateModelAsset(positions, normals, texCoords, indices);
        }

        /// <summary>
        /// Builds a square plane mesh aligned to local XY with its lower-left corner at the origin.
        /// </summary>
        /// <param name="size">Side length in world units.</param>
        /// <returns>Generated square plane model asset.</returns>
        public static ModelAsset CreatePlaneSquare(float size) {
            if (size <= 0f) {
                throw new ArgumentOutOfRangeException(nameof(size), "Plane size must be greater than zero.");
            }

            List<float3> positions = new List<float3>(8);
            List<float3> normals = new List<float3>(8);
            List<float2> texCoords = new List<float2>(8);
            List<int> indices = new List<int>(12);

            positions.Add(new float3(0f, 0f, 0f));
            positions.Add(new float3(size, 0f, 0f));
            positions.Add(new float3(size, size, 0f));
            positions.Add(new float3(0f, size, 0f));
            normals.Add(new float3(0f, 0f, 1f));
            normals.Add(new float3(0f, 0f, 1f));
            normals.Add(new float3(0f, 0f, 1f));
            normals.Add(new float3(0f, 0f, 1f));
            texCoords.Add(new float2(0f, 0f));
            texCoords.Add(new float2(1f, 0f));
            texCoords.Add(new float2(1f, 1f));
            texCoords.Add(new float2(0f, 1f));

            positions.Add(new float3(0f, 0f, 0f));
            positions.Add(new float3(0f, size, 0f));
            positions.Add(new float3(size, size, 0f));
            positions.Add(new float3(size, 0f, 0f));
            normals.Add(new float3(0f, 0f, -1f));
            normals.Add(new float3(0f, 0f, -1f));
            normals.Add(new float3(0f, 0f, -1f));
            normals.Add(new float3(0f, 0f, -1f));
            texCoords.Add(new float2(0f, 0f));
            texCoords.Add(new float2(0f, 1f));
            texCoords.Add(new float2(1f, 1f));
            texCoords.Add(new float2(1f, 0f));

            indices.Add(0);
            indices.Add(1);
            indices.Add(2);
            indices.Add(0);
            indices.Add(2);
            indices.Add(3);

            indices.Add(4);
            indices.Add(5);
            indices.Add(6);
            indices.Add(4);
            indices.Add(6);
            indices.Add(7);

            return CreateModelAsset(positions, normals, texCoords, indices);
        }

        /// <summary>
        /// Builds a square plane mesh aligned to local XY and centered on the local origin.
        /// </summary>
        /// <param name="size">Side length in local units.</param>
        /// <returns>Generated centered square plane model asset.</returns>
        public static ModelAsset CreateCenteredPlaneSquare(float size) {
            if (size <= 0f) {
                throw new ArgumentOutOfRangeException(nameof(size), "Plane size must be greater than zero.");
            }

            float halfSize = size * 0.5f;
            List<float3> positions = new List<float3>(8);
            List<float3> normals = new List<float3>(8);
            List<float2> texCoords = new List<float2>(8);
            List<int> indices = new List<int>(12);

            positions.Add(new float3(-halfSize, -halfSize, 0f));
            positions.Add(new float3(halfSize, -halfSize, 0f));
            positions.Add(new float3(halfSize, halfSize, 0f));
            positions.Add(new float3(-halfSize, halfSize, 0f));
            normals.Add(new float3(0f, 0f, 1f));
            normals.Add(new float3(0f, 0f, 1f));
            normals.Add(new float3(0f, 0f, 1f));
            normals.Add(new float3(0f, 0f, 1f));
            texCoords.Add(new float2(0f, 0f));
            texCoords.Add(new float2(1f, 0f));
            texCoords.Add(new float2(1f, 1f));
            texCoords.Add(new float2(0f, 1f));

            positions.Add(new float3(-halfSize, -halfSize, 0f));
            positions.Add(new float3(-halfSize, halfSize, 0f));
            positions.Add(new float3(halfSize, halfSize, 0f));
            positions.Add(new float3(halfSize, -halfSize, 0f));
            normals.Add(new float3(0f, 0f, -1f));
            normals.Add(new float3(0f, 0f, -1f));
            normals.Add(new float3(0f, 0f, -1f));
            normals.Add(new float3(0f, 0f, -1f));
            texCoords.Add(new float2(0f, 0f));
            texCoords.Add(new float2(0f, 1f));
            texCoords.Add(new float2(1f, 1f));
            texCoords.Add(new float2(1f, 0f));

            indices.Add(0);
            indices.Add(1);
            indices.Add(2);
            indices.Add(0);
            indices.Add(2);
            indices.Add(3);

            indices.Add(4);
            indices.Add(5);
            indices.Add(6);
            indices.Add(4);
            indices.Add(6);
            indices.Add(7);

            return CreateModelAsset(positions, normals, texCoords, indices);
        }

        /// <summary>
        /// Builds a centered square plane mesh whose vertices all share the same texture coordinate.
        /// </summary>
        /// <param name="size">Side length in local units.</param>
        /// <param name="uniformTexCoord">Texture coordinate written to every generated vertex.</param>
        /// <returns>Generated centered square plane model asset.</returns>
        public static ModelAsset CreateCenteredPlaneSquare(float size, float2 uniformTexCoord) {
            if (size <= 0f) {
                throw new ArgumentOutOfRangeException(nameof(size), "Plane size must be greater than zero.");
            }

            float halfSize = size * 0.5f;
            List<float3> positions = new List<float3>(8);
            List<float3> normals = new List<float3>(8);
            List<float2> texCoords = new List<float2>(8);
            List<int> indices = new List<int>(12);

            positions.Add(new float3(-halfSize, -halfSize, 0f));
            positions.Add(new float3(halfSize, -halfSize, 0f));
            positions.Add(new float3(halfSize, halfSize, 0f));
            positions.Add(new float3(-halfSize, halfSize, 0f));
            normals.Add(new float3(0f, 0f, 1f));
            normals.Add(new float3(0f, 0f, 1f));
            normals.Add(new float3(0f, 0f, 1f));
            normals.Add(new float3(0f, 0f, 1f));
            texCoords.Add(uniformTexCoord);
            texCoords.Add(uniformTexCoord);
            texCoords.Add(uniformTexCoord);
            texCoords.Add(uniformTexCoord);

            positions.Add(new float3(-halfSize, -halfSize, 0f));
            positions.Add(new float3(-halfSize, halfSize, 0f));
            positions.Add(new float3(halfSize, halfSize, 0f));
            positions.Add(new float3(halfSize, -halfSize, 0f));
            normals.Add(new float3(0f, 0f, -1f));
            normals.Add(new float3(0f, 0f, -1f));
            normals.Add(new float3(0f, 0f, -1f));
            normals.Add(new float3(0f, 0f, -1f));
            texCoords.Add(uniformTexCoord);
            texCoords.Add(uniformTexCoord);
            texCoords.Add(uniformTexCoord);
            texCoords.Add(uniformTexCoord);

            indices.Add(0);
            indices.Add(1);
            indices.Add(2);
            indices.Add(0);
            indices.Add(2);
            indices.Add(3);

            indices.Add(4);
            indices.Add(5);
            indices.Add(6);
            indices.Add(4);
            indices.Add(6);
            indices.Add(7);

            return CreateModelAsset(positions, normals, texCoords, indices);
        }

        /// <summary>
        /// Builds a hollow tube ring centered at the origin around the local Y axis.
        /// </summary>
        /// <param name="innerRadius">Inner radius of the ring hole.</param>
        /// <param name="outerRadius">Outer radius of the ring body.</param>
        /// <param name="height">Axial thickness of the ring along local Y.</param>
        /// <param name="segments">Segment count around the circular ring.</param>
        /// <returns>Generated hollow tube-ring model asset.</returns>
        public static ModelAsset CreateTubeRing(float innerRadius, float outerRadius, float height, int segments) {
            ValidateTubeRingArguments(innerRadius, outerRadius, height, segments);

            List<float3> positions = new List<float3>(segments * 8);
            List<float3> normals = new List<float3>(segments * 8);
            List<float2> texCoords = new List<float2>(segments * 8);
            List<int> indices = new List<int>(segments * 48);
            float halfHeight = height * 0.5f;

            int outerSideStart = positions.Count;
            for (int segmentIndex = 0; segmentIndex < segments; segmentIndex++) {
                double fraction = (double)segmentIndex / segments;
                double angle = fraction * Math.PI * 2.0;
                float x = (float)(Math.Cos(angle) * outerRadius);
                float z = (float)(Math.Sin(angle) * outerRadius);
                float3 normal = NormalizeXZ(x, z);

                positions.Add(new float3(x, -halfHeight, z));
                positions.Add(new float3(x, halfHeight, z));
                normals.Add(normal);
                normals.Add(normal);
                texCoords.Add(new float2((float)fraction, 0f));
                texCoords.Add(new float2((float)fraction, 1f));
            }

            for (int segmentIndex = 0; segmentIndex < segments; segmentIndex++) {
                int currentBottom = outerSideStart + (segmentIndex * 2);
                int currentTop = currentBottom + 1;
                int nextBottom = outerSideStart + (((segmentIndex + 1) % segments) * 2);
                int nextTop = nextBottom + 1;
                AddDoubleSidedTriangle(indices, currentBottom, currentTop, nextTop);
                AddDoubleSidedTriangle(indices, currentBottom, nextTop, nextBottom);
            }

            int innerSideStart = positions.Count;
            for (int segmentIndex = 0; segmentIndex < segments; segmentIndex++) {
                double fraction = (double)segmentIndex / segments;
                double angle = fraction * Math.PI * 2.0;
                float x = (float)(Math.Cos(angle) * innerRadius);
                float z = (float)(Math.Sin(angle) * innerRadius);
                float3 normal = NormalizeXZ(-x, -z);

                positions.Add(new float3(x, -halfHeight, z));
                positions.Add(new float3(x, halfHeight, z));
                normals.Add(normal);
                normals.Add(normal);
                texCoords.Add(new float2((float)fraction, 0f));
                texCoords.Add(new float2((float)fraction, 1f));
            }

            for (int segmentIndex = 0; segmentIndex < segments; segmentIndex++) {
                int currentBottom = innerSideStart + (segmentIndex * 2);
                int currentTop = currentBottom + 1;
                int nextBottom = innerSideStart + (((segmentIndex + 1) % segments) * 2);
                int nextTop = nextBottom + 1;
                AddDoubleSidedTriangle(indices, currentBottom, nextBottom, nextTop);
                AddDoubleSidedTriangle(indices, currentBottom, nextTop, currentTop);
            }

            int topCapStart = positions.Count;
            for (int segmentIndex = 0; segmentIndex < segments; segmentIndex++) {
                double fraction = (double)segmentIndex / segments;
                double angle = fraction * Math.PI * 2.0;
                float outerX = (float)(Math.Cos(angle) * outerRadius);
                float outerZ = (float)(Math.Sin(angle) * outerRadius);
                float innerX = (float)(Math.Cos(angle) * innerRadius);
                float innerZ = (float)(Math.Sin(angle) * innerRadius);

                positions.Add(new float3(outerX, halfHeight, outerZ));
                positions.Add(new float3(innerX, halfHeight, innerZ));
                normals.Add(new float3(0f, 1f, 0f));
                normals.Add(new float3(0f, 1f, 0f));
                texCoords.Add(new float2((float)fraction, 0f));
                texCoords.Add(new float2((float)fraction, 1f));
            }

            for (int segmentIndex = 0; segmentIndex < segments; segmentIndex++) {
                int currentOuter = topCapStart + (segmentIndex * 2);
                int currentInner = currentOuter + 1;
                int nextOuter = topCapStart + (((segmentIndex + 1) % segments) * 2);
                int nextInner = nextOuter + 1;
                AddDoubleSidedTriangle(indices, currentOuter, currentInner, nextInner);
                AddDoubleSidedTriangle(indices, currentOuter, nextInner, nextOuter);
            }

            int bottomCapStart = positions.Count;
            for (int segmentIndex = 0; segmentIndex < segments; segmentIndex++) {
                double fraction = (double)segmentIndex / segments;
                double angle = fraction * Math.PI * 2.0;
                float outerX = (float)(Math.Cos(angle) * outerRadius);
                float outerZ = (float)(Math.Sin(angle) * outerRadius);
                float innerX = (float)(Math.Cos(angle) * innerRadius);
                float innerZ = (float)(Math.Sin(angle) * innerRadius);

                positions.Add(new float3(outerX, -halfHeight, outerZ));
                positions.Add(new float3(innerX, -halfHeight, innerZ));
                normals.Add(new float3(0f, -1f, 0f));
                normals.Add(new float3(0f, -1f, 0f));
                texCoords.Add(new float2((float)fraction, 0f));
                texCoords.Add(new float2((float)fraction, 1f));
            }

            for (int segmentIndex = 0; segmentIndex < segments; segmentIndex++) {
                int currentOuter = bottomCapStart + (segmentIndex * 2);
                int currentInner = currentOuter + 1;
                int nextOuter = bottomCapStart + (((segmentIndex + 1) % segments) * 2);
                int nextInner = nextOuter + 1;
                AddDoubleSidedTriangle(indices, currentOuter, nextOuter, nextInner);
                AddDoubleSidedTriangle(indices, currentOuter, nextInner, currentInner);
            }

            return CreateModelAsset(positions, normals, texCoords, indices);
        }

        /// <summary>
        /// Validates basic primitive generation arguments.
        /// </summary>
        /// <param name="radius">Primitive radius.</param>
        /// <param name="height">Primitive height.</param>
        /// <param name="segments">Primitive segment count.</param>
        static void ValidatePrimitiveArguments(float radius, float height, int segments) {
            if (radius <= 0f) {
                throw new ArgumentOutOfRangeException(nameof(radius), "Radius must be greater than zero.");
            }

            if (height <= 0f) {
                throw new ArgumentOutOfRangeException(nameof(height), "Height must be greater than zero.");
            }

            if (segments < 3) {
                throw new ArgumentOutOfRangeException(nameof(segments), "At least three segments are required.");
            }
        }

        /// <summary>
        /// Validates hollow tube-ring generation arguments.
        /// </summary>
        /// <param name="innerRadius">Inner radius of the ring hole.</param>
        /// <param name="outerRadius">Outer radius of the ring body.</param>
        /// <param name="height">Axial thickness of the ring along local Y.</param>
        /// <param name="segments">Segment count around the circular ring.</param>
        static void ValidateTubeRingArguments(float innerRadius, float outerRadius, float height, int segments) {
            if (innerRadius <= 0f) {
                throw new ArgumentOutOfRangeException(nameof(innerRadius), "Inner radius must be greater than zero.");
            }

            if (outerRadius <= innerRadius) {
                throw new ArgumentOutOfRangeException(nameof(outerRadius), "Outer radius must be greater than inner radius.");
            }

            if (height <= 0f) {
                throw new ArgumentOutOfRangeException(nameof(height), "Ring height must be greater than zero.");
            }

            if (segments < 3) {
                throw new ArgumentOutOfRangeException(nameof(segments), "At least three ring segments are required.");
            }
        }

        /// <summary>
        /// Validates box generation arguments.
        /// </summary>
        /// <param name="width">Box width along local X.</param>
        /// <param name="height">Box height along local Y.</param>
        /// <param name="depth">Box depth along local Z.</param>
        static void ValidateBoxArguments(float width, float height, float depth) {
            if (width <= 0f) {
                throw new ArgumentOutOfRangeException(nameof(width), "Box width must be greater than zero.");
            }

            if (height <= 0f) {
                throw new ArgumentOutOfRangeException(nameof(height), "Box height must be greater than zero.");
            }

            if (depth <= 0f) {
                throw new ArgumentOutOfRangeException(nameof(depth), "Box depth must be greater than zero.");
            }
        }

        /// <summary>
        /// Creates a model asset from generated vertex streams and triangle indices.
        /// </summary>
        /// <param name="positions">Generated vertex positions.</param>
        /// <param name="normals">Generated vertex normals.</param>
        /// <param name="texCoords">Generated vertex texture coordinates.</param>
        /// <param name="indices">Generated triangle indices.</param>
        /// <returns>Generated model asset.</returns>
        static ModelAsset CreateModelAsset(
            List<float3> positions,
            List<float3> normals,
            List<float2> texCoords,
            List<int> indices) {
            if (positions == null) {
                throw new ArgumentNullException(nameof(positions));
            }

            if (normals == null) {
                throw new ArgumentNullException(nameof(normals));
            }

            if (texCoords == null) {
                throw new ArgumentNullException(nameof(texCoords));
            }

            if (indices == null) {
                throw new ArgumentNullException(nameof(indices));
            }

            if (positions.Count == 0) {
                throw new InvalidOperationException("Mesh generation produced no vertices.");
            }

            if (positions.Count != normals.Count) {
                throw new InvalidOperationException("Vertex position and normal counts must match.");
            }

            if (positions.Count != texCoords.Count) {
                throw new InvalidOperationException("Vertex position and UV counts must match.");
            }

            if (positions.Count > ushort.MaxValue) {
                throw new InvalidOperationException("Mesh uses more vertices than 16-bit indices support.");
            }

            ModelAsset modelAsset = new ModelAsset();
            modelAsset.Id = Guid.NewGuid().ToString("N");
            modelAsset.Positions = positions.ToArray();
            modelAsset.Normals = normals.ToArray();
            modelAsset.TexCoords = texCoords.ToArray();
            modelAsset.Indices16 = ConvertIndices(indices);
            return modelAsset;
        }

        /// <summary>
        /// Converts integer triangle indices to 16-bit indices with range validation.
        /// </summary>
        /// <param name="indices">Triangle indices to convert.</param>
        /// <returns>Converted 16-bit index buffer.</returns>
        static ushort[] ConvertIndices(List<int> indices) {
            if (indices == null) {
                throw new ArgumentNullException(nameof(indices));
            }

            ushort[] converted = new ushort[indices.Count];
            for (int i = 0; i < indices.Count; i++) {
                int index = indices[i];
                if (index < 0 || index > ushort.MaxValue) {
                    throw new InvalidOperationException("Mesh index exceeds 16-bit range.");
                }

                converted[i] = (ushort)index;
            }

            return converted;
        }

        /// <summary>
        /// Adds one outward-facing box face to the supplied mesh streams.
        /// </summary>
        /// <param name="positions">Vertex-position stream to append to.</param>
        /// <param name="normals">Vertex-normal stream to append to.</param>
        /// <param name="texCoords">Vertex-UV stream to append to.</param>
        /// <param name="indices">Triangle-index stream to append to.</param>
        /// <param name="bottomLeft">Lower-left corner of the face.</param>
        /// <param name="bottomRight">Lower-right corner of the face.</param>
        /// <param name="topRight">Upper-right corner of the face.</param>
        /// <param name="topLeft">Upper-left corner of the face.</param>
        /// <param name="normal">Outward face normal.</param>
        static void AddBoxFace(
            List<float3> positions,
            List<float3> normals,
            List<float2> texCoords,
            List<int> indices,
            float3 bottomLeft,
            float3 bottomRight,
            float3 topRight,
            float3 topLeft,
            float3 normal) {
            if (positions == null) {
                throw new ArgumentNullException(nameof(positions));
            }

            if (normals == null) {
                throw new ArgumentNullException(nameof(normals));
            }

            if (texCoords == null) {
                throw new ArgumentNullException(nameof(texCoords));
            }

            if (indices == null) {
                throw new ArgumentNullException(nameof(indices));
            }

            int startIndex = positions.Count;
            positions.Add(bottomLeft);
            positions.Add(bottomRight);
            positions.Add(topRight);
            positions.Add(topLeft);

            normals.Add(normal);
            normals.Add(normal);
            normals.Add(normal);
            normals.Add(normal);

            texCoords.Add(new float2(0f, 0f));
            texCoords.Add(new float2(1f, 0f));
            texCoords.Add(new float2(1f, 1f));
            texCoords.Add(new float2(0f, 1f));

            indices.Add(startIndex + 0);
            indices.Add(startIndex + 1);
            indices.Add(startIndex + 2);
            indices.Add(startIndex + 0);
            indices.Add(startIndex + 2);
            indices.Add(startIndex + 3);
        }

        /// <summary>
        /// Adds a triangle to the index list in both winding orders.
        /// </summary>
        /// <param name="indices">Index list to append to.</param>
        /// <param name="a">First vertex index.</param>
        /// <param name="b">Second vertex index.</param>
        /// <param name="c">Third vertex index.</param>
        static void AddDoubleSidedTriangle(List<int> indices, int a, int b, int c) {
            if (indices == null) {
                throw new ArgumentNullException(nameof(indices));
            }

            indices.Add(a);
            indices.Add(b);
            indices.Add(c);
            indices.Add(a);
            indices.Add(c);
            indices.Add(b);
        }

        /// <summary>
        /// Normalizes an XZ vector into a unit vector on the XZ plane.
        /// </summary>
        /// <param name="x">X component.</param>
        /// <param name="z">Z component.</param>
        /// <returns>Normalized XZ vector.</returns>
        static float3 NormalizeXZ(float x, float z) {
            double length = Math.Sqrt(x * x + z * z);
            if (length <= 0.0) {
                throw new InvalidOperationException("Cannot normalize a zero-length XZ vector.");
            }

            return new float3((float)(x / length), 0f, (float)(z / length));
        }

        /// <summary>
        /// Normalizes a 3D vector.
        /// </summary>
        /// <param name="value">Vector to normalize.</param>
        /// <returns>Normalized vector.</returns>
        static float3 Normalize3(float3 value) {
            double length = Math.Sqrt(value.X * value.X + value.Y * value.Y + value.Z * value.Z);
            if (length <= 0.0) {
                throw new InvalidOperationException("Cannot normalize a zero-length vector.");
            }

            return new float3((float)(value.X / length), (float)(value.Y / length), (float)(value.Z / length));
        }
    }
}
