namespace helengine {
    /// <summary>
    /// Moment a track's keyframe offsets are measured from.
    /// </summary>
    public enum GraphicTimeAnchor {
        /// <summary>
        /// The moment of the item the element belongs to.
        /// </summary>
        Item = 0,

        /// <summary>
        /// The moment of the item after the element's item; the track is skipped for the last item.
        /// </summary>
        NextItem = 1
    }
}
