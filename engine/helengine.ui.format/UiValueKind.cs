namespace helengine.ui.format {
    /// <summary>Stable version-one wire identifiers; never renumber existing members.</summary>
    public enum UiValueKind : byte {
        /// <summary>Number wire identifier.</summary>
        Number = 1,
        /// <summary>Integer wire identifier.</summary>
        Integer = 2,
        /// <summary>Boolean wire identifier.</summary>
        Boolean = 3,
        /// <summary>String wire identifier.</summary>
        String = 4,
        /// <summary>Color wire identifier.</summary>
        Color = 5,
        /// <summary>TextBinding wire identifier.</summary>
        TextBinding = 6,
        /// <summary>ResourceBinding wire identifier.</summary>
        ResourceBinding = 7,
        /// <summary>ActionBinding wire identifier.</summary>
        ActionBinding = 8,
        /// <summary>Theme wire identifier.</summary>
        Theme = 9,
    }
}
