using helengine.ui;
namespace helengine.ui.tests {
    /// <summary>Supplies deterministic data and records actions without an app or graphics device.</summary>
    public sealed class Bindings : IUiBindings {
        /// <summary>Number of native button invocations.</summary>
        public int Calls;
        /// <summary>Texture borrowed by the fixture, optionally absent.</summary>
        public RuntimeTexture Texture;
        /// <summary>Injects a theme failure to check transactional view construction.</summary>
        public bool RejectTheme;
        /// <summary>Resolves only the fixture's display-name binding.</summary>
        public string GetText(string key) { if (key != "name") { throw new FormatException("Unknown text key."); } return "<Arizona>"; }
        /// <summary>Resolves only the fixture's optional thumbnail.</summary>
        public RuntimeTexture GetTexture(string key) { if (key != "thumbnail") { throw new FormatException("Unknown image key."); } return Texture; }
        /// <summary>Uses an opaque color so layout checks are independent of the app theme.</summary>
        public byte4 GetColor(string key) { if (RejectTheme) { throw new FormatException("Unknown theme key."); } return new byte4(1, 2, 3, 255); }
        /// <summary>Admits the one explicit fixture action.</summary>
        public bool HasAction(string key) { return key == "open"; }
        /// <summary>Counts calls after checking the same action key.</summary>
        public void Invoke(string key) { if (!HasAction(key)) { throw new FormatException("Unknown action key."); } Calls++; }
    }
}
