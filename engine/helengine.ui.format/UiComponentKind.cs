namespace helengine.ui.format {
    /// <summary>Stable version-one wire identifiers; never renumber existing members.</summary>
    public enum UiComponentKind : ushort {
        /// <summary>Rectangle wire identifier.</summary>
        Rectangle = 1,
        /// <summary>Text wire identifier.</summary>
        Text = 2,
        /// <summary>Image wire identifier.</summary>
        Image = 3,
        /// <summary>Clip wire identifier.</summary>
        Clip = 4,
        /// <summary>Button wire identifier.</summary>
        Button = 5,
    }
}
