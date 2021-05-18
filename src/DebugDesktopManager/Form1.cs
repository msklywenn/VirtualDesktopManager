using System;
using System.Collections.Generic;
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

            if (pictureBox1.Image == null)
                pictureBox1.Image = new Bitmap(pictureBox1.ClientRectangle.Width, pictureBox1.ClientRectangle.Height);

            IntPtr hInstance = Marshal.GetHINSTANCE(typeof(Program).Module);
            Win32.SetWindowsHookEx(Win32.HookType.WH_SHELL,
                delegate (int code, IntPtr wParam, IntPtr lParam) {
                    if (code < 0) return Win32.CallNextHookEx(IntPtr.Zero, code, wParam, lParam);
                    // wParam contains window handle in both cases
                    const uint HSHELL_WINDOWCREATED = 1; 
                    const uint HSHELL_WINDOWDESTROYED = 2;
                    if (code == HSHELL_WINDOWCREATED || code == HSHELL_WINDOWDESTROYED)
                    {
                        RefreshWindows();
                        DrawWindows(pictureBox1.Image);
                    }
                    return IntPtr.Zero; 
                }, hInstance, 0);

            SuspendLayout();
            RefreshWindows();
            DrawWindows(pictureBox1.Image);
            ResumeLayout();

            pictureBox1.MouseDown += TryPickWindow;
            pictureBox1.MouseMove += MovePicked;
            pictureBox1.MouseUp += ChangeDesktop;
            pictureBox1.MouseLeave += MouseLeft;
        }

        struct Rectangle
        {
            public int x, y, width, height;
            public bool IsInside(float _x, float _y)
            {
                return _x >= x && _y >= y && _x <= x + width && _y <= y + height;
            }
        }

        struct WindowInfo
        {
            public IntPtr handle;
            public Rectangle rectangle;
            public int desktop;
        }

        List<WindowInfo> windows = new List<WindowInfo>();
        Rectangle screen;
        int desktopCount;
        IntPtr pickedWindow = IntPtr.Zero;
        int pickX, pickY;
        int pickMoveX, pickMoveY;

        private void MouseLeft(object sender, EventArgs e)
        {
            pickedWindow = IntPtr.Zero;
            DrawWindows(pictureBox1.Image);
            pictureBox1.Refresh();
        }

        private void TryPickWindow(object sender, MouseEventArgs e)
        {
            var box = sender as PictureBox;
            pickedWindow = IntPtr.Zero;
            int desktopIndex = (int)Math.Floor(e.X * desktopCount / (float)box.Width);
            float x = e.X * (screen.width * desktopCount) / (float)box.Width + screen.x - desktopIndex * screen.width;
            float y = e.Y * screen.height / (float)box.Height + screen.y;
            foreach (var window in windows)
            {
                if (desktopIndex == window.desktop && window.rectangle.IsInside(x, y))
                {
                    pickedWindow = window.handle;
                    pickX = e.X;
                    pickY = e.Y;
                    DrawWindows(pictureBox1.Image);
                    pictureBox1.Refresh();
                    break;
                }
            }
        }

        private void MovePicked(object sender, MouseEventArgs e)
        {
            if (pickedWindow != IntPtr.Zero)
            {
                pickMoveX = e.X - pickX;
                pickMoveY = e.Y - pickY;
                DrawWindows(pictureBox1.Image);
                pictureBox1.Refresh();
            }
        }

        private void ChangeDesktop(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
                return;

            pickMoveX = e.X - pickX;
            pickMoveY = e.Y - pickY;

            if (pickedWindow == IntPtr.Zero || (pickMoveX == 0 && pickMoveY == 0))
            {
                var box = sender as PictureBox;
                int desktopIndex = (int)Math.Floor(e.X * desktopCount / (float)box.Width);
                var desktop = VirtualDesktop.DesktopManager.GetDesktop(desktopIndex);
                VirtualDesktop.DesktopManager.VirtualDesktopManagerInternal.SwitchDesktop(desktop);
            }
            else if (pickedWindow != IntPtr.Zero)
            {
                var box = sender as PictureBox;
                int desktopIndex = (int)Math.Floor(e.X * desktopCount / (float)box.Width);

                var desktop = VirtualDesktop.Desktop.FromIndex(desktopIndex);
                desktop.MoveWindow(pickedWindow);
                RefreshWindows();

                if (VirtualDesktop.Desktop.FromIndex(desktopIndex).IsVisible)
                {
                    Win32.SetForegroundWindow(pickedWindow);
                }
            }
            
            pickedWindow = IntPtr.Zero;

            DrawWindows(pictureBox1.Image);
            pictureBox1.Refresh();
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

        void RefreshWindows()
        {
            screen = new Rectangle {
                x = Win32.GetSystemMetrics(Win32.SystemMetric.SM_XVIRTUALSCREEN),
                y = Win32.GetSystemMetrics(Win32.SystemMetric.SM_YVIRTUALSCREEN),
                width = Win32.GetSystemMetrics(Win32.SystemMetric.SM_CXVIRTUALSCREEN),
                height = Win32.GetSystemMetrics(Win32.SystemMetric.SM_CYVIRTUALSCREEN)
            };

            desktopCount = VirtualDesktop.Desktop.Count;

            windows.Clear();
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

                    windows.Add(new WindowInfo()
                    {
                        handle = window,
                        rectangle = new Rectangle()
                        {
                            x = topLeft.X,
                            y = topLeft.Y,
                            width = clientWidth,
                            height = clientHeight,
                        },
                        desktop = index
                    });
                }
                return true; // continue enumeration
            }, IntPtr.Zero);
        }

        void DrawWindows(Image image)
        {
            using (var graphics = Graphics.FromImage(image))
            using (var activeWindowPen = new Pen(Color.White))                               // todo: create once
            using (var otherWindowPen = new Pen(Color.Gray))                                 // todo: create once
            using (var pickedWindowPen = new Pen(Color.Cyan))                                // todo: create once
            using (var activeDesktopPen = new SolidBrush(Color.FromArgb(32, 255, 255, 255))) // todo: create once
            {
                graphics.Clear(Color.Black);

                float scaleX = pictureBox1.ClientRectangle.Width / (float)screen.width / desktopCount;
                float scaleY = pictureBox1.ClientRectangle.Height / (float)screen.height;

                var currentDesktop = VirtualDesktop.Desktop.Current;
                var foreground = Win32.GetForegroundWindow();
                int activeDesktop = VirtualDesktop.Desktop.FromDesktop(VirtualDesktop.Desktop.Current);

                graphics.FillRectangle(activeDesktopPen, screen.width * activeDesktop * scaleX, 0, screen.width * scaleX, image.Height);

                // windows seems to list from front to back
                // we want to paint in reverse order
                for (int i = windows.Count - 1; i >= 0; i--)
                {
                    var window = windows[i];

                    float x = (int)((window.rectangle.x - screen.x + screen.width * window.desktop) * scaleX);
                    float y = (int)((window.rectangle.y - screen.y) * scaleY);
                    float width = (int)(window.rectangle.width * scaleX);
                    float height = (int)(window.rectangle.height * scaleY);

                    if (pickedWindow == window.handle)
                    {
                        x += pickMoveX;
                        y += pickMoveY;
                    }

                    var pen = otherWindowPen;
                    if (pickedWindow == window.handle)
                        pen = pickedWindowPen;
                    else if (foreground == window.handle)
                        pen = activeWindowPen;
                    graphics.DrawRectangle(pen, x, y, width, height);
                }
            }
        }
    }
}
