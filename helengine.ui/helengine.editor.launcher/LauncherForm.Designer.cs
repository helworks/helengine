namespace helengine.editor.launcher {
    partial class LauncherForm {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing) {
            if (disposing && (components != null)) {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent() {
            titleBarControl1 = new Nucleus.Platform.Windows.Controls.TitleBarControl();
            SuspendLayout();
            // 
            // titleBarControl1
            // 
            titleBarControl1.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            titleBarControl1.BackColor = Color.Purple;
            titleBarControl1.EnableMaximize = true;
            titleBarControl1.ForeColor = Color.White;
            titleBarControl1.Icon = null;
            titleBarControl1.Location = new Point(0, 0);
            titleBarControl1.Margin = new Padding(0);
            titleBarControl1.Name = "titleBarControl1";
            titleBarControl1.ShowIcon = false;
            titleBarControl1.Size = new Size(986, 21);
            titleBarControl1.StripWidth = 30;
            titleBarControl1.TabIndex = 0;
            // 
            // LauncherForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(986, 705);
            Controls.Add(titleBarControl1);
            FormBorderStyle = FormBorderStyle.None;
            Name = "LauncherForm";
            Text = "Form1";
            ResumeLayout(false);
        }

        #endregion

        private Nucleus.Platform.Windows.Controls.TitleBarControl titleBarControl1;
    }
}
