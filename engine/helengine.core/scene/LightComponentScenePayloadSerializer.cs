namespace helengine {
    /// <summary>
    /// Reads and writes scene payloads for authored light components.
    /// </summary>
    public static class LightComponentScenePayloadSerializer {
        /// <summary>
        /// Current payload version shared by all serialized light component records.
        /// </summary>
        public const byte CurrentVersion = 2;
        /// <summary>
        /// Writes the shared directional-light payload fields into the supplied writer.
        /// </summary>
        /// <param name="writer">Destination writer receiving the payload.</param>
        /// <param name="lightComponent">Directional light whose authored values should be serialized.</param>
        public static void WriteDirectionalLight(EngineBinaryWriter writer, DirectionalLightComponent lightComponent) {
            if (writer == null) {
                throw new ArgumentNullException(nameof(writer));
            } else if (lightComponent == null) {
                throw new ArgumentNullException(nameof(lightComponent));
            }

            WriteCommonLightFields(writer, lightComponent);
            writer.WriteSingle(lightComponent.ShadowDistance);
        }

        /// <summary>
        /// Reads the shared directional-light payload fields from the supplied reader.
        /// </summary>
        /// <param name="reader">Source reader positioned at the directional-light payload.</param>
        /// <returns>Directional light reconstructed from the payload.</returns>
        public static DirectionalLightComponent ReadDirectionalLight(EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            DirectionalLightComponent lightComponent = new DirectionalLightComponent();
            ReadCommonLightFields(reader, lightComponent);
            lightComponent.ShadowDistance = reader.ReadSingle();
            return lightComponent;
        }

        /// <summary>
        /// Reads one legacy directional-light payload that predates the explicit shadow-distance field.
        /// </summary>
        /// <param name="reader">Source reader positioned at the legacy directional-light payload.</param>
        /// <returns>Directional light reconstructed from the legacy payload.</returns>
        public static DirectionalLightComponent ReadDirectionalLightVersion1(EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            DirectionalLightComponent lightComponent = new DirectionalLightComponent();
            ReadCommonLightFields(reader, lightComponent);
            return lightComponent;
        }

        /// <summary>
        /// Writes the point-light payload fields into the supplied writer.
        /// </summary>
        /// <param name="writer">Destination writer receiving the payload.</param>
        /// <param name="lightComponent">Point light whose authored values should be serialized.</param>
        public static void WritePointLight(EngineBinaryWriter writer, PointLightComponent lightComponent) {
            if (writer == null) {
                throw new ArgumentNullException(nameof(writer));
            } else if (lightComponent == null) {
                throw new ArgumentNullException(nameof(lightComponent));
            }

            WriteCommonLightFields(writer, lightComponent);
            writer.WriteSingle(lightComponent.Range);
        }

        /// <summary>
        /// Reads the point-light payload fields from the supplied reader.
        /// </summary>
        /// <param name="reader">Source reader positioned at the point-light payload.</param>
        /// <returns>Point light reconstructed from the payload.</returns>
        public static PointLightComponent ReadPointLight(EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            PointLightComponent lightComponent = new PointLightComponent();
            ReadCommonLightFields(reader, lightComponent);
            lightComponent.Range = reader.ReadSingle();
            return lightComponent;
        }

        /// <summary>
        /// Reads one legacy point-light payload that uses runtime payload version 1.
        /// </summary>
        /// <param name="reader">Source reader positioned at the legacy point-light payload.</param>
        /// <returns>Point light reconstructed from the legacy payload.</returns>
        public static PointLightComponent ReadPointLightVersion1(EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            PointLightComponent lightComponent = new PointLightComponent();
            ReadCommonLightFields(reader, lightComponent);
            lightComponent.Range = reader.ReadSingle();
            return lightComponent;
        }

        /// <summary>
        /// Writes the spot-light payload fields into the supplied writer.
        /// </summary>
        /// <param name="writer">Destination writer receiving the payload.</param>
        /// <param name="lightComponent">Spot light whose authored values should be serialized.</param>
        public static void WriteSpotLight(EngineBinaryWriter writer, SpotLightComponent lightComponent) {
            if (writer == null) {
                throw new ArgumentNullException(nameof(writer));
            } else if (lightComponent == null) {
                throw new ArgumentNullException(nameof(lightComponent));
            }

            WriteCommonLightFields(writer, lightComponent);
            writer.WriteSingle(lightComponent.Range);
            writer.WriteSingle(lightComponent.InnerConeAngleDegrees);
            writer.WriteSingle(lightComponent.OuterConeAngleDegrees);
        }

        /// <summary>
        /// Reads the spot-light payload fields from the supplied reader.
        /// </summary>
        /// <param name="reader">Source reader positioned at the spot-light payload.</param>
        /// <returns>Spot light reconstructed from the payload.</returns>
        public static SpotLightComponent ReadSpotLight(EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            SpotLightComponent lightComponent = new SpotLightComponent();
            ReadCommonLightFields(reader, lightComponent);
            lightComponent.Range = reader.ReadSingle();
            lightComponent.InnerConeAngleDegrees = reader.ReadSingle();
            lightComponent.OuterConeAngleDegrees = reader.ReadSingle();
            return lightComponent;
        }

        /// <summary>
        /// Reads one legacy spot-light payload that uses runtime payload version 1.
        /// </summary>
        /// <param name="reader">Source reader positioned at the legacy spot-light payload.</param>
        /// <returns>Spot light reconstructed from the legacy payload.</returns>
        public static SpotLightComponent ReadSpotLightVersion1(EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            SpotLightComponent lightComponent = new SpotLightComponent();
            ReadCommonLightFields(reader, lightComponent);
            lightComponent.Range = reader.ReadSingle();
            lightComponent.InnerConeAngleDegrees = reader.ReadSingle();
            lightComponent.OuterConeAngleDegrees = reader.ReadSingle();
            return lightComponent;
        }

        /// <summary>
        /// Writes the shared light payload fields consumed by all concrete light families.
        /// </summary>
        /// <param name="writer">Destination writer receiving the payload.</param>
        /// <param name="lightComponent">Light whose common authored values should be serialized.</param>
        static void WriteCommonLightFields(EngineBinaryWriter writer, LightComponent lightComponent) {
            if (writer == null) {
                throw new ArgumentNullException(nameof(writer));
            } else if (lightComponent == null) {
                throw new ArgumentNullException(nameof(lightComponent));
            }

            WriteFloat4(writer, lightComponent.Color);
            writer.WriteSingle(lightComponent.Intensity);
            writer.WriteByte(lightComponent.ShadowsEnabled ? (byte)1 : (byte)0);
            writer.WriteByte((byte)lightComponent.ShadowMapMode);
            writer.WriteSingle(lightComponent.ShadowStrength);
        }

        /// <summary>
        /// Reads the shared light payload fields consumed by all concrete light families.
        /// </summary>
        /// <param name="reader">Source reader positioned at the common light payload.</param>
        /// <param name="lightComponent">Light instance that should receive the decoded values.</param>
        static void ReadCommonLightFields(EngineBinaryReader reader, LightComponent lightComponent) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            } else if (lightComponent == null) {
                throw new ArgumentNullException(nameof(lightComponent));
            }

            lightComponent.Color = ReadFloat4(reader);
            lightComponent.Intensity = reader.ReadSingle();
            lightComponent.ShadowsEnabled = reader.ReadByte() != 0;
            lightComponent.ShadowMapMode = (ShadowMapMode)reader.ReadByte();
            lightComponent.ShadowStrength = reader.ReadSingle();
        }

        /// <summary>
        /// Writes one <see cref="float4"/> value into the supplied writer.
        /// </summary>
        /// <param name="writer">Destination writer receiving the vector payload.</param>
        /// <param name="value">Vector value that should be serialized.</param>
        static void WriteFloat4(EngineBinaryWriter writer, float4 value) {
            if (writer == null) {
                throw new ArgumentNullException(nameof(writer));
            }

            writer.WriteSingle(value.X);
            writer.WriteSingle(value.Y);
            writer.WriteSingle(value.Z);
            writer.WriteSingle(value.W);
        }

        /// <summary>
        /// Reads one <see cref="float4"/> value from the supplied reader.
        /// </summary>
        /// <param name="reader">Source reader positioned at the vector payload.</param>
        /// <returns>Decoded vector value.</returns>
        static float4 ReadFloat4(EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            return new float4(
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle());
        }

    }
}
