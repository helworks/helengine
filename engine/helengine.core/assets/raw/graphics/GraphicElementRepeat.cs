namespace helengine {
    /// <summary>
    /// Selects which items (or gaps between items) a template element is instantiated for.
    /// </summary>
    public enum GraphicElementRepeat {
        /// <summary>
        /// One instance per item.
        /// </summary>
        EachItem = 0,

        /// <summary>
        /// One instance per gap between two consecutive items, attached to the item before the gap.
        /// </summary>
        BetweenItems = 1,

        /// <summary>
        /// One instance for the accent item chosen by the edit; nothing when no accent item is set.
        /// </summary>
        AccentItem = 2,

        /// <summary>
        /// One instance per item except the accent item.
        /// </summary>
        EachItemExceptAccent = 3,

        /// <summary>
        /// One instance for the first item.
        /// </summary>
        FirstItem = 4,

        /// <summary>
        /// One instance for the last item.
        /// </summary>
        LastItem = 5,

        /// <summary>
        /// One instance per item except the last one.
        /// </summary>
        EachItemExceptLast = 6
    }
}
