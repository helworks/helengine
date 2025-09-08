namespace helengine {
    public class FontInfo {
        public string Name { get; set; }
        public int LineSpacing { get; set; }
        public float SpaceWidth { get; set; }

        public FontInfo(string name, int lineSpacing, float spaceWidth) {
            Name = name;
            LineSpacing = lineSpacing;
            SpaceWidth = spaceWidth;
        }
    }
}
