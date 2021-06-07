using System;
using System.Drawing;
using System.Windows.Forms;

namespace VirtualDesktopNameDeskband
{
    public partial class DeskbandControl : UserControl
    {
        Manager manager;

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
        }

        public DeskbandControl()
        {
            InitializeComponent();
            manager = new Manager(pictureBox1);
        }

        internal void Close()
        {
            manager.Dispose();
        }

        public Size GetPreferredSize()
        {
            float taskbarHeight = Screen.PrimaryScreen.Bounds.Height - Screen.PrimaryScreen.WorkingArea.Height;
            float screenRatio = manager.screen.width / (float)manager.screen.height;
            float height = taskbarHeight * 0.8f;
            Size pref = new Size()
            {
                Width = (int)(height * screenRatio * VirtualDesktop.Desktop.Count),
                Height = (int)height
            };
            pictureBox1.Parent.Size = pref;
            return pref;
        }
    }
}
