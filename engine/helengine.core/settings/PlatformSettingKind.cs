namespace helengine {
    /// <summary>Identifies the portable typed value accepted by a platform runtime setting.</summary>
    public enum PlatformRuntimeSettingKind {
        /// <summary>An enabled or disabled feature.</summary>
        Boolean,
        /// <summary>A finite whole number within the definition's bounds.</summary>
        Integer,
        /// <summary>A finite numeric value within the definition's bounds.</summary>
        Number,
        /// <summary>One entry from the definition's closed list of choices.</summary>
        Choice,
        /// <summary>A platform-defined text value.</summary>
        Text
    }
}
