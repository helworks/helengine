namespace helengine {
    /// <summary>
    /// Forwards per-frame updates to a combo box component.
    /// </summary>
    class ComboBoxUpdateComponent : UpdateComponent {
        /// <summary>
        /// Combo box that receives forwarded update calls.
        /// </summary>
        readonly ComboBoxComponent comboBox;

        /// <summary>
        /// Creates an update component that drives the provided combo box.
        /// </summary>
        /// <param name="comboBox">Combo box to update.</param>
        public ComboBoxUpdateComponent(ComboBoxComponent comboBox) {
            if (comboBox == null) {
                throw new ArgumentNullException(nameof(comboBox));
            }

            this.comboBox = comboBox;
        }

        /// <summary>
        /// Forwards the update call to the combo box.
        /// </summary>
        public override void Update() {
            comboBox.Update();
        }
    }
}
