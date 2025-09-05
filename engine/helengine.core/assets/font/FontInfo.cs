namespace helengine {
    public class FontInfo {
        public string Name { get; set; }
        public float SpaceWidth { get; set; }
        
        // Comprehensive font metrics
        public float Ascent { get; set; }        // Distance from baseline to top of tallest character
        public float Descent { get; set; }       // Distance from baseline to bottom of lowest character
        public float LineHeight { get; set; }    // Total line height (ascent + descent + line gap)
        public float LineGap { get; set; }       // Additional spacing between lines
        public float BaselineOffset { get; set; } // Offset from top to baseline
        public float EmSize { get; set; }        // Font size in pixels
        
        // Legacy property for backward compatibility
        public int LineSpacing => (int)LineHeight;

        // New comprehensive constructor
        public FontInfo(string name, float spaceWidth, float ascent, float descent, 
                       float lineHeight, float lineGap, float baselineOffset, float emSize) {
            Name = name;
            SpaceWidth = spaceWidth;
            Ascent = ascent;
            Descent = descent;
            LineHeight = lineHeight;
            LineGap = lineGap;
            BaselineOffset = baselineOffset;
            EmSize = emSize;
        }
    }
}
