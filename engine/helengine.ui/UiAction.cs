namespace helengine.ui {
    /// <summary>Receives native callbacks without capturing temporary loop values.</summary>
    public sealed class UiAction {
        /// <summary>Provider remains owned by the application for the entire view lifetime.</summary>
        readonly IUiBindings Bindings;
        /// <summary>Exact typed action key, without special string syntax.</summary>
        readonly string Key;
        /// <summary>Retains the borrowed provider and immutable callback key.</summary>
        public UiAction(IUiBindings bindings, string key) { Bindings = bindings; Key = key; }
        /// <summary>Dispatches through the application's existing controller boundary.</summary>
        public void Invoke() { Bindings.Invoke(Key); }
    }
}
