namespace helengine.ui {
    /// <summary>Application-owned resources and actions borrowed by one declarative UI view.</summary>
    public interface IUiBindings {
        /// <summary>Returns literal bound text, throwing for an unknown key; null is invalid.</summary>
        string GetText(string key);
        /// <summary>Returns a borrowed image or an explicit null placeholder; unknown keys must throw.</summary>
        [NativeBorrowedReturn] RuntimeTexture GetTexture(string key);
        /// <summary>Resolves an application theme role without introducing app colors into the engine.</summary>
        byte4 GetColor(string key);
        /// <summary>Reports whether a callback key is available before any button is attached.</summary>
        bool HasAction(string key);
        /// <summary>Invokes an existing application action; the provider must outlive its view.</summary>
        void Invoke(string key);
    }
}
