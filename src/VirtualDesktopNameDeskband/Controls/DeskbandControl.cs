using System;
using System.Windows.Forms;

namespace VirtualDesktopNameDeskband
{
    public partial class DeskbandControl : UserControl
    {
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
        }

        public DeskbandControl()
        {
            InitializeComponent();
        }

        internal void Close()
        {
        }
    }
}
