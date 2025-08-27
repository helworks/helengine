#if WINDOWS
using System;
using System.Drawing;
using System.Windows.Forms;

namespace Nucleus.Platform.Windows.Controls {
    /// <summary>
    /// Lists controls dynamically in a list
    /// </summary>
    public class ControlListBox : UserControl {
        public event Action<Control> SelectedChanged;

        public Control SelectedControl { get; protected set; }
        public Size Offset { get; set; }
        public bool CanSelectControls { get; set; } = true;
        public bool VerticalScrollEnabled { get; set; } = true;

        public int Border {
            get { return border; }
            set { border = value; }
        }

        public override bool AutoScroll {
            get { return base.AutoScroll; }
            set {
                base.AutoScroll = value;
                if (!value) {
                    this.HorizontalScroll.Visible = false;
                    this.HorizontalScroll.Enabled = false;
                    this.VerticalScroll.Visible = false;
                    this.VerticalScroll.Enabled = false;
                }
            }
        }

        private int border = 1;
        private int totalHeight;
        private int maxScrollHeight;
        private bool updatingSize;
        private Panel contentPanel;

        public ControlListBox() {
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScroll = false;

            AutoScaleMode = AutoScaleMode.None;

            this.Controls.Add(contentPanel);
        }

        public void Deselect() {
            SelectedControl = null;
            c_Click(this, EventArgs.Empty);
        }

        public void UpdateSizes() {
            if (updatingSize) {
                return;
            }

            updatingSize = true;

            totalHeight = 0;
            bool isVerticalVisible = VerticalScroll.Visible;
            int v = isVerticalVisible ? (1 + SystemInformation.VerticalScrollBarWidth) : 0;

            for (int i = 0; i < contentPanel.Controls.Count; i++) {
                var con = contentPanel.Controls[i];
                con.Width = this.Width - v;

                con.Location = new Point(0, totalHeight);
                totalHeight += con.Height + border;

                con.Invalidate();
            }

            updatingSize = false;

            HorizontalScroll.Visible = false;
            VerticalScroll.Visible = VerticalScrollEnabled ? (totalHeight > this.Height) : false;

            maxScrollHeight = totalHeight - this.Height;

            contentPanel.Size = new Size(this.Width, totalHeight);
            contentPanel.Location = new Point(0, 0);
            contentPanel.Invalidate();
            contentPanel.BringToFront();

            if (VerticalScroll.Visible != isVerticalVisible) {
                //UpdateSizes(); // need to update again
            }
        }

        protected override Control.ControlCollection CreateControlsInstance() {
            this.contentPanel = new Panel();
            return new ListBoxControlCollection(contentPanel, this);
        }

        protected override void OnSizeChanged(EventArgs e) {
            base.OnSizeChanged(e);
            UpdateSizes();
        }

        public void AddedControl(Control nControl) {
            //protected override void OnControlAdded(ControlEventArgs e) {
            //base.OnControlAdded(e);

            if (!this.DesignMode && nControl != null) {
                Control c = nControl;
                if (c == contentPanel) {
                    return;
                }

                c.ControlAdded += C_ControlAdded;
                c.SizeChanged += C_SizeChanged;
                if (c is IRadioControl) {
                    if (c is IMouseHoverControl) {
                        IMouseHoverControl mouse = (IMouseHoverControl)c;
                        mouse.Mouse.MouseEnter += c_MouseEnter;
                        mouse.Mouse.MouseMove += c_MouseMove;
                        mouse.Mouse.MouseWheel += c_MouseWheel;
                        mouse.Mouse.MouseLeave += c_MouseLeave;
                    } else {
                        c.Click += C_Click;
                        c.MouseEnter += c_MouseEnter;
                        c.MouseMove += c_MouseMove;
                        c.MouseWheel += c_MouseWheel;
                        c.MouseLeave += c_MouseLeave;
                    }
                } else {
                    c.MouseWheel += c_MouseWheel;
                }

                int index = contentPanel.Controls.IndexOf(c);
                Size s = c.Size;

                c.Location = new Point(0, totalHeight);
                totalHeight += s.Height + border;
            }
        }

        public void RemovedControl(Control nControl) {
            UpdateSizes();
        }

        private void C_ControlAdded(object sender, ControlEventArgs e) {
            Control c = e.Control;
            c.Click += c_Click;
        }

        private void c_MouseEnter(object sender, EventArgs e) {
            Control parent = (Control)sender;
            if (parent is TransparentControl) {
                parent = parent.Parent;
            }

            if (parent is IRadioControl) {
                IRadioControl high = (IRadioControl)parent;
                if (!high.EnableClicking || parent != SelectedControl) {
                    high.UserOver();
                }
            }
        }

        private void c_MouseMove(object sender, MouseEventArgs e) {
            // scroll bar
            //Debug.WriteLine(e.Delta);
            //VerticalScroll.Value += e.Delta;
        }

        private void c_MouseWheel(object sender, MouseEventArgs e) {
            // scroll bar
            Point loc = contentPanel.Location;
            Point nPoint = new Point(loc.X, (int)(loc.Y + (e.Delta * 0.5f)));
            nPoint.Y = MathUtil.Clamp(nPoint.Y, -maxScrollHeight, 0);
            contentPanel.Location = nPoint;
            contentPanel.Refresh();
        }

        private void c_MouseLeave(object sender, EventArgs e) {
            Control parent = (Control)sender;
            if (parent is TransparentControl) {
                parent = parent.Parent;
            }

            if (parent is IRadioControl) {
                IRadioControl high = (IRadioControl)parent;
                if (!high.EnableClicking || parent != SelectedControl) {
                    high.UserLeave();
                }
            }
        }

        private void C_SizeChanged(object sender, EventArgs e) {
            Control con = (Control)sender;
            // this has the potential of being incredibly slow
            UpdateSizes();
        }

        private void C_Click(object sender, EventArgs e) {
            OnClick(sender);
        }

        private void OnClick(object sender) {
            if (!CanSelectControls) {
                return;
            }

            Control parent = (Control)sender;
            if (parent is TransparentControl) {
                parent = parent.Parent;
            }

            if (parent is IRadioControl) {
                IRadioControl parentCon = (IRadioControl)parent;
                if (!parentCon.EnableClicking) {
                    return;
                }
            }

            for (int i = 0; i < contentPanel.Controls.Count; i++) {
                Control c = contentPanel.Controls[i];
                if (c is IRadioControl) {
                    IRadioControl high = (IRadioControl)c;
                    if (parent == c && high.EnableClicking) {
                        // highlight
                        high.RadioSelected();
                    } else {
                        high.RadioUnselected();
                    }
                }
            }

            if (parent != null &&
                parent != SelectedControl) {
                if (this.SelectedChanged != null) {
                    SelectedControl = parent;
                    this.SelectedChanged(SelectedControl);
                }
            }

            SelectedControl = parent;
        }

        private void c_Click(object sender, EventArgs e) {
            Control parent = (Control)sender;
            OnClick(parent);
        }
    }
}
#endif