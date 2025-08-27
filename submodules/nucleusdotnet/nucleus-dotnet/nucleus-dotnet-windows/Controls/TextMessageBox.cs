#if WINDOWS
using System;
using System.Windows.Forms;

namespace Nucleus.Platform.Windows.Controls {
    public partial class TextMessageBox : Form {
        public string UserText {
            get { return this.textBox1.Text; }
        }

        public TextMessageBox() {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e) {
            if (!string.IsNullOrEmpty(textBox1.Text)) {
                this.DialogResult = System.Windows.Forms.DialogResult.OK;
                this.Close();
            }
        }
    }
}
#endif