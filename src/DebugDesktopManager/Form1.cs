using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace DebugDesktopManager
{
    // must be first in file for designer to work...
    // must be first in file for designer to work...
    // must be first in file for designer to work...
    // must be first in file for designer to work...
    // must be first in file for designer to work...
    // must be first in file for designer to work...
    // must be first in file for designer to work...
    // must be first in file for designer to work...
    // must be first in file for designer to work...
    // must be first in file for designer to work...
    // must be first in file for designer to work...
    // must be first in file for designer to work...
    // must be first in file for designer to work...
    // must be first in file for designer to work...
    // must be first in file for designer to work...
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();

            IntPtr hInstance = Marshal.GetHINSTANCE(typeof(Program).Module);
            Win32.SetWindowsHookEx(Win32.HookType.WH_SHELL,
                delegate (int code, IntPtr wParam, IntPtr lParam) {
                    if (code < 0) return Win32.CallNextHookEx(IntPtr.Zero, code, wParam, lParam);
                    // wParam contains window handle in both cases
                    const uint HSHELL_WINDOWCREATED = 1; 
                    const uint HSHELL_WINDOWDESTROYED = 2;
                    if (code == HSHELL_WINDOWCREATED || code == HSHELL_WINDOWDESTROYED)
                        RefreshWindowList();
                    return IntPtr.Zero; 
                }, hInstance, 0);

            SuspendLayout();
            RefreshWindowList();
            ResumeLayout();

            pictureBox1.MouseClick += TryChangeDesktop;
        }

        private void TryChangeDesktop(object sender, MouseEventArgs e)
        {
            var box = sender as PictureBox;
            if (e.Button == MouseButtons.Left)
            {
                int desktopCount = VirtualDesktop.Desktop.Count;
                int desktopIndex = (int)Math.Floor(e.X * desktopCount / (float)box.Width);
                var desktop = VirtualDesktop.DesktopManager.GetDesktop(desktopIndex);
                VirtualDesktop.DesktopManager.VirtualDesktopManagerInternal.SwitchDesktop(desktop);
            }
        }

        bool IsInterestingWindow(IntPtr window)
        {
            if (!Win32.IsWindowVisible(window))
                return false;

            // skip untitled stuff
            string title = Win32.GetWindowText(window);
            if (string.IsNullOrWhiteSpace(title))
                return false;

            // skip overlays and shit
            const uint WS_POPUP = 0x80000000;
            Win32.GetWindowInfo(window, out Win32.WINDOWINFO info);
            if ((info.dwStyle & WS_POPUP) != 0)
                return false;

            // skip extra small
            Win32.GetClientRect(window, out Win32.RECT rect);
            int clientWidth = rect.Right;
            int clientHeight = rect.Bottom;
            if (clientWidth <= 1 || clientHeight <= 1)
                return false;

            return true;
        }

        private void RefreshWindowList()
        {
            if (pictureBox1.Image == null)
                pictureBox1.Image = new Bitmap(pictureBox1.ClientRectangle.Width, pictureBox1.ClientRectangle.Height);

            DrawWindows(pictureBox1.Image);
        }

        void DrawWindows(Image image)
        {
            using (var graphics = Graphics.FromImage(image))
            using (var activeWindowPen = new Pen(Color.White))
            using (var otherWindowPen = new Pen(Color.Gray))
            using (var activeDesktopPen = new SolidBrush(Color.FromArgb(16, 255, 255, 255)))
            {
                graphics.Clear(Color.Black);

                int screenWidth = Win32.GetSystemMetrics(Win32.SystemMetric.SM_CXVIRTUALSCREEN);
                int screenHeight = Win32.GetSystemMetrics(Win32.SystemMetric.SM_CYVIRTUALSCREEN);
                int screenStartX = Win32.GetSystemMetrics(Win32.SystemMetric.SM_XVIRTUALSCREEN);
                int screenStartY = Win32.GetSystemMetrics(Win32.SystemMetric.SM_YVIRTUALSCREEN);

                int desktopCount = VirtualDesktop.Desktop.Count;

                float scaleX = pictureBox1.ClientRectangle.Width / (float)screenWidth / desktopCount;
                float scaleY = pictureBox1.ClientRectangle.Height / (float)screenHeight;

                var currentDesktop = VirtualDesktop.Desktop.Current;
                var foreground = Win32.GetForegroundWindow();
                int activeDesktop = VirtualDesktop.Desktop.FromDesktop(VirtualDesktop.Desktop.Current);

                graphics.FillRectangle(activeDesktopPen, screenWidth * activeDesktop * scaleX, 0, screenWidth * scaleX, image.Height);

                int total = VirtualDesktop.DesktopManager.VirtualDesktopManagerInternal2.GetCount();
                Win32.EnumWindows(delegate (IntPtr window, IntPtr lParam)
                {
                    if (IsInterestingWindow(window))
                    {
                        var desktopID = VirtualDesktop.DesktopManager.VirtualDesktopManager.GetWindowDesktopId(window);
                        var desktop = VirtualDesktop.DesktopManager.VirtualDesktopManagerInternal.FindDesktop(ref desktopID);
                        int index = VirtualDesktop.DesktopManager.GetDesktopIndex(desktop);

                        Win32.POINT topLeft = new Win32.POINT { X = 0, Y = 0 };
                        Win32.ClientToScreen(window, ref topLeft);

                        Win32.GetClientRect(window, out Win32.RECT rect);
                        int clientWidth = rect.Right;
                        int clientHeight = rect.Bottom;

                        float x = ((topLeft.X - screenStartX) + screenWidth * index) * scaleX;
                        float y = (topLeft.Y - screenStartY) * scaleY;
                        float width = clientWidth * scaleX;
                        float height = clientHeight * scaleY;
                        var pen = window == foreground ? activeWindowPen : otherWindowPen;
                        graphics.DrawRectangle(pen, x, y, width, height);
                    }
                    return true; // continue enumeration
                }, IntPtr.Zero);
            }
        }
    }
}
