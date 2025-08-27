#if WINFORMS
using System.Windows.Forms;
using static System.Windows.Forms.Control;

namespace Nucleus.Platform.Windows.Controls {
    public partial class ListBoxControlCollection : ControlCollection {
        public Panel Panel { get; private set; }
        public ControlListBox ListBox { get; private set; }

        public ListBoxControlCollection(Panel sub, ControlListBox owner)
            : base(owner) {
            Panel = sub;
            ListBox = owner;
        }

        public override void Add(Control value) {
            if (value == Panel) {
                base.Add(value);
            } else {
                Panel.Controls.Add(value);
                ListBox.AddedControl(value);
                Panel.ControlRemoved += Panel_ControlRemoved;
            }
        }

        private void Panel_ControlRemoved(object sender, ControlEventArgs e) {
            ListBox.RemovedControl(e.Control);
        }

        public override void Clear() {
            Panel.Controls.Clear();
            //base.Clear(); // disable base clear
        }
    }
}
#endif