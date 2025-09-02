namespace helengine.editor.app {
    partial class ProjectView {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
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
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent() {
            btn_addProject = new Button();
            controlListBox1 = new Nucleus.Platform.Windows.Controls.ControlListBox();
            btn_newProject = new Button();
            panel_newProject = new Panel();
            txt_projectFile = new Nucleus.Platform.Windows.Controls.BorderTextBox();
            txt_projectName = new Nucleus.Platform.Windows.Controls.BorderTextBox();
            btnSaveProject = new Button();
            label2 = new Label();
            btn_createProject = new Button();
            label1 = new Label();
            btn_cancel = new Button();
            panel_newProject.SuspendLayout();
            SuspendLayout();
            // 
            // btn_addProject
            // 
            btn_addProject.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btn_addProject.BackColor = Color.FromArgb(194, 49, 175);
            btn_addProject.FlatAppearance.BorderSize = 2;
            btn_addProject.FlatStyle = FlatStyle.Flat;
            btn_addProject.Font = new Font("Consolas", 18F, FontStyle.Regular, GraphicsUnit.Point, 0);
            btn_addProject.Location = new Point(417, 45);
            btn_addProject.Name = "btn_addProject";
            btn_addProject.Size = new Size(344, 52);
            btn_addProject.TabIndex = 0;
            btn_addProject.Text = "add project";
            btn_addProject.UseVisualStyleBackColor = false;
            btn_addProject.Click += btn_addProject_Click;
            // 
            // controlListBox1
            // 
            controlListBox1.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            controlListBox1.Border = 2;
            controlListBox1.BorderStyle = BorderStyle.FixedSingle;
            controlListBox1.CanSelectControls = true;
            controlListBox1.Location = new Point(12, 70);
            controlListBox1.Name = "controlListBox1";
            controlListBox1.Offset = new Size(0, 0);
            controlListBox1.Size = new Size(1099, 826);
            controlListBox1.TabIndex = 1;
            controlListBox1.VerticalScrollEnabled = true;
            // 
            // btn_newProject
            // 
            btn_newProject.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btn_newProject.BackColor = Color.FromArgb(194, 49, 175);
            btn_newProject.FlatAppearance.BorderSize = 2;
            btn_newProject.FlatStyle = FlatStyle.Flat;
            btn_newProject.Font = new Font("Consolas", 18F, FontStyle.Regular, GraphicsUnit.Point, 0);
            btn_newProject.Location = new Point(767, 45);
            btn_newProject.Name = "btn_newProject";
            btn_newProject.Size = new Size(344, 52);
            btn_newProject.TabIndex = 2;
            btn_newProject.Text = "new project";
            btn_newProject.UseVisualStyleBackColor = false;
            btn_newProject.Click += btn_newProject_Click;
            // 
            // panel_newProject
            // 
            panel_newProject.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            panel_newProject.BackColor = Color.FromArgb(141, 49, 194);
            panel_newProject.Controls.Add(txt_projectFile);
            panel_newProject.Controls.Add(txt_projectName);
            panel_newProject.Controls.Add(btnSaveProject);
            panel_newProject.Controls.Add(label2);
            panel_newProject.Controls.Add(btn_createProject);
            panel_newProject.Controls.Add(label1);
            panel_newProject.Controls.Add(btn_cancel);
            panel_newProject.Location = new Point(1, 48);
            panel_newProject.Name = "panel_newProject";
            panel_newProject.Size = new Size(1122, 859);
            panel_newProject.TabIndex = 3;
            panel_newProject.Visible = false;
            // 
            // txt_projectFile
            // 
            txt_projectFile.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            txt_projectFile.BackColor = Color.FromArgb(194, 49, 102);
            txt_projectFile.BorderColor = Color.White;
            txt_projectFile.BorderSize = 3;
            txt_projectFile.BorderStyle = BorderStyle.None;
            txt_projectFile.Font = new Font("Consolas", 24F);
            txt_projectFile.ForeColor = Color.White;
            txt_projectFile.Location = new Point(11, 186);
            txt_projectFile.Name = "txt_projectFile";
            txt_projectFile.Size = new Size(991, 38);
            txt_projectFile.TabIndex = 9;
            txt_projectFile.TextChanged += txt_projectFile_TextChanged;
            // 
            // txt_projectName
            // 
            txt_projectName.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            txt_projectName.BackColor = Color.FromArgb(194, 49, 102);
            txt_projectName.BorderColor = Color.White;
            txt_projectName.BorderSize = 3;
            txt_projectName.BorderStyle = BorderStyle.None;
            txt_projectName.Font = new Font("Consolas", 24F);
            txt_projectName.ForeColor = Color.White;
            txt_projectName.Location = new Point(11, 94);
            txt_projectName.Name = "txt_projectName";
            txt_projectName.Size = new Size(1099, 38);
            txt_projectName.TabIndex = 8;
            txt_projectName.TextChanged += txt_projectName_TextChanged;
            // 
            // btnSaveProject
            // 
            btnSaveProject.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnSaveProject.BackColor = Color.FromArgb(194, 49, 175);
            btnSaveProject.FlatAppearance.BorderSize = 2;
            btnSaveProject.FlatStyle = FlatStyle.Flat;
            btnSaveProject.Font = new Font("Consolas", 18F, FontStyle.Regular, GraphicsUnit.Point, 0);
            btnSaveProject.ForeColor = Color.White;
            btnSaveProject.Location = new Point(1008, 186);
            btnSaveProject.Name = "btnSaveProject";
            btnSaveProject.Size = new Size(102, 38);
            btnSaveProject.TabIndex = 7;
            btnSaveProject.Text = "...";
            btnSaveProject.UseVisualStyleBackColor = false;
            btnSaveProject.Click += btnSaveProject_Click;
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Font = new Font("Consolas", 14F);
            label2.ForeColor = Color.White;
            label2.Location = new Point(11, 150);
            label2.Name = "label2";
            label2.Size = new Size(150, 22);
            label2.TabIndex = 5;
            label2.Text = "project folder";
            // 
            // btn_createProject
            // 
            btn_createProject.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btn_createProject.BackColor = Color.FromArgb(194, 49, 175);
            btn_createProject.Enabled = false;
            btn_createProject.FlatAppearance.BorderSize = 2;
            btn_createProject.FlatStyle = FlatStyle.Flat;
            btn_createProject.Font = new Font("Consolas", 18F, FontStyle.Regular, GraphicsUnit.Point, 0);
            btn_createProject.ForeColor = Color.White;
            btn_createProject.Location = new Point(876, 796);
            btn_createProject.Name = "btn_createProject";
            btn_createProject.Size = new Size(234, 52);
            btn_createProject.TabIndex = 4;
            btn_createProject.Text = "create";
            btn_createProject.UseVisualStyleBackColor = false;
            btn_createProject.Click += btn_createProject_Click;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Font = new Font("Consolas", 14F);
            label1.ForeColor = Color.White;
            label1.Location = new Point(11, 69);
            label1.Name = "label1";
            label1.Size = new Size(130, 22);
            label1.TabIndex = 2;
            label1.Text = "project name";
            // 
            // btn_cancel
            // 
            btn_cancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btn_cancel.BackColor = Color.FromArgb(194, 49, 175);
            btn_cancel.FlatAppearance.BorderSize = 2;
            btn_cancel.FlatStyle = FlatStyle.Flat;
            btn_cancel.Font = new Font("Consolas", 18F, FontStyle.Regular, GraphicsUnit.Point, 0);
            btn_cancel.ForeColor = Color.White;
            btn_cancel.Location = new Point(636, 796);
            btn_cancel.Name = "btn_cancel";
            btn_cancel.Size = new Size(234, 52);
            btn_cancel.TabIndex = 1;
            btn_cancel.Text = "cancel";
            btn_cancel.UseVisualStyleBackColor = false;
            btn_cancel.Click += btn_cancel_Click;
            // 
            // ProjectView
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.FromArgb(68, 49, 194);
            ClientSize = new Size(1123, 908);
            Controls.Add(btn_newProject);
            Controls.Add(controlListBox1);
            Controls.Add(btn_addProject);
            Controls.Add(panel_newProject);
            Name = "ProjectView";
            Text = "helengine";
            panel_newProject.ResumeLayout(false);
            panel_newProject.PerformLayout();
            ResumeLayout(false);
        }

        #endregion

        private Button btn_addProject;
        private Nucleus.Platform.Windows.Controls.ControlListBox controlListBox1;
        private Button btn_newProject;
        private Panel panel_newProject;
        private Button btn_cancel;
        private Label label1;
        private Button btn_createProject;
        private Label label2;
        private Button btnSaveProject;
        private Nucleus.Platform.Windows.Controls.BorderTextBox txt_projectName;
        private Nucleus.Platform.Windows.Controls.BorderTextBox txt_projectFile;
    }
}