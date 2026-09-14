namespace helengine {
    /// <summary>
    /// Selects the wording used when a versioned HELE binary payload fails strict version validation, so every serializer migrated onto <see cref="VersionedBinaryPayload"/> keeps publishing the exact diagnostic its callers and tests already expect.
    /// </summary>
    public enum VersionedBinaryVersionMismatchStyle {
        /// <summary>
        /// Reports the received version, names the current version, and appends the regenerate instruction, as in "Unsupported texture asset import settings binary version received '1'; current version is '2'. Regenerate the sidecar.".
        /// </summary>
        CurrentVersionSuffix = 0,

        /// <summary>
        /// Reports only the received version and omits both the current version and the regenerate instruction, as in "Unsupported shader cache metadata binary version '1'.".
        /// </summary>
        ReceivedVersionOnly = 1,

        /// <summary>
        /// Leads with the payload subject, states the version that is required, and appends the regenerate instruction, as in "Editor asset version '1' is unsupported; version '2' is required. Regenerate the authored asset.".
        /// </summary>
        RequiredVersionSubjectFirst = 2
    }
}
