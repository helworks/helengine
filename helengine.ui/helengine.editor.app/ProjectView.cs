namespace helengine.editor.app {
    public partial class ProjectView : Form {
        public ProjectView() {
            InitializeComponent();
        }

        private void btn_addProject_Click(object sender, EventArgs e) {
        }

        private void btn_newProject_Click(object sender, EventArgs e) {
            panel_newProject.Visible = true;
            panel_newProject.BringToFront();
        }

        private void btnSaveProject_Click(object sender, EventArgs e) {
            string folder = FolderDialog.OpenFolderDialog();
            txt_projectFile.Text = folder;
        }

        private void txt_projectName_TextChanged(object sender, EventArgs e) {
            updateEnabledCreate();
        }

        private void txt_projectFile_TextChanged(object sender, EventArgs e) {
            updateEnabledCreate();
        }

        private void btn_cancel_Click(object sender, EventArgs e) {
            txt_projectFile.Text = "";
            btn_createProject.Enabled = false;
            panel_newProject.Visible = false;
        }

        private void updateEnabledCreate() {
            if (!string.IsNullOrEmpty(txt_projectFile.Text) &&
                !string.IsNullOrEmpty(txt_projectName.Text)) {
                btn_createProject.Enabled = true;
            } else {
                btn_createProject.Enabled = false;
            }
        }

        private void btn_createProject_Click(object sender, EventArgs e) {

        }
    }
}
