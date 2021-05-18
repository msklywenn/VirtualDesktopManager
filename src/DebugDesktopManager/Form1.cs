
using System.Windows.Forms;

namespace DebugDesktopManager
{
    public partial class Form1 : Form
    {
        Manager manager;

        public Form1()
        {
            InitializeComponent();

            SuspendLayout();
            manager = new Manager(pictureBox1);
            ResumeLayout();
        }

        ~Form1()
        {
            manager.Dispose();
        }
    }
}
