using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

class Manager : IDisposable
{
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

    // Outside references
    readonly PictureBox pictureBox;

    // Work variables
    readonly List<WindowInfo> windows = new List<WindowInfo>();
    Rectangle screen;
    int desktopCount;
    IntPtr pickedWindow = IntPtr.Zero;
    int pickX, pickY;
    int pickMoveX, pickMoveY;
    bool pickedFromAnotherDesktop;

    // Disposables
    readonly Pen foregroundWindowPen;
    readonly Pen otherWindowPen;
    readonly Pen pickedWindowPen;
    readonly SolidBrush activeDesktopBrush;

    Win32.EnumWindowsProc filterWindows;
    Win32.WinEventProc eventListener;

    public void Dispose()
    {
        foregroundWindowPen.Dispose();
        otherWindowPen.Dispose();
        pickedWindowPen.Dispose();
        activeDesktopBrush.Dispose();
    }

    public Manager(PictureBox box)
    {
        pictureBox = box;

        if (pictureBox.Image == null)
            pictureBox.Image = new Bitmap(pictureBox.ClientRectangle.Width, pictureBox.ClientRectangle.Height);

        foregroundWindowPen = new Pen(Color.DarkGray);
        otherWindowPen = new Pen(Color.DimGray);
        pickedWindowPen = new Pen(Color.White);
        activeDesktopBrush = new SolidBrush(Color.FromArgb(32, 255, 255, 255));

        filterWindows = new Win32.EnumWindowsProc(FilterWindow);
        eventListener = new Win32.WinEventProc(EventListener);

        RefreshWindows();
        DrawWindows();

        pictureBox.MouseDown += TryPickWindow;
        pictureBox.MouseMove += MovePicked;
        pictureBox.MouseUp += ChangeDesktop;
        pictureBox.MouseLeave += MouseLeft;

        Win32.SetWinEventHook(interestingEvents.Min(), interestingEvents.Max(),
            IntPtr.Zero, eventListener, 0, 0, Win32.WinEventFlags.WINEVENT_OUTOFCONTEXT);
    }

    static readonly Win32.WinEvents[] interestingEvents = 
    {
        Win32.WinEvents.EVENT_SYSTEM_SWITCHEND, // alt-tab
        Win32.WinEvents.EVENT_SYSTEM_MOVESIZEEND,
        Win32.WinEvents.EVENT_SYSTEM_MINIMIZESTART,
        Win32.WinEvents.EVENT_SYSTEM_MINIMIZEEND,
        Win32.WinEvents.EVENT_SYSTEM_FOREGROUND,
        Win32.WinEvents.EVENT_OBJECT_LOCATIONCHANGE,
        Win32.WinEvents.EVENT_OBJECT_CREATE
    };

    void EventListener(IntPtr hWinEventHook, uint eventType,
        IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        const int OBJID_CURSOR = -9;
        if (idObject != OBJID_CURSOR && interestingEvents.Contains((Win32.WinEvents)eventType))
        {
            RefreshWindows();
            DrawWindows();
            pictureBox.Refresh();
        }
    }

    private void MouseLeft(object sender, EventArgs e)
    {
        pickedWindow = IntPtr.Zero;
        DrawWindows();
        pictureBox.Refresh();
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
                int current = VirtualDesktop.Desktop.FromDesktop(VirtualDesktop.Desktop.Current);
                pickedFromAnotherDesktop = current != window.desktop;
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
            DrawWindows();
            pictureBox.Refresh();
        }
    }

    private void ChangeDesktop(object sender, MouseEventArgs e)
    {
        var box = sender as PictureBox;

        if (e.Button != MouseButtons.Left)
            return;

        pickMoveX = e.X - pickX;
        pickMoveY = e.Y - pickY;

        if (pickedWindow == IntPtr.Zero || (pickMoveX == 0 && pickMoveY == 0))
        {
            int desktopIndex = (int)Math.Floor(e.X * desktopCount / (float)box.Width);
            var desktop = VirtualDesktop.DesktopManager.GetDesktop(desktopIndex);
            VirtualDesktop.DesktopManager.VirtualDesktopManagerInternal.SwitchDesktop(desktop);
        }
        else if (pickedWindow != IntPtr.Zero)
        {
            int desktopIndex = (int)Math.Floor(e.X * desktopCount / (float)box.Width);

            var desktop = VirtualDesktop.Desktop.FromIndex(desktopIndex);
            desktop.MoveWindow(pickedWindow);
            RefreshWindows();

            int current = VirtualDesktop.Desktop.FromDesktop(VirtualDesktop.Desktop.Current);
            if (desktopIndex == current && pickedFromAnotherDesktop)
            {
                Win32.SetForegroundWindow(pickedWindow);
            }
        }

        pickedWindow = IntPtr.Zero;

        DrawWindows();
        pictureBox.Refresh();
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
        screen = new Rectangle
        {
            x = Win32.GetSystemMetrics(Win32.SystemMetric.SM_XVIRTUALSCREEN),
            y = Win32.GetSystemMetrics(Win32.SystemMetric.SM_YVIRTUALSCREEN),
            width = Win32.GetSystemMetrics(Win32.SystemMetric.SM_CXVIRTUALSCREEN),
            height = Win32.GetSystemMetrics(Win32.SystemMetric.SM_CYVIRTUALSCREEN)
        };

        desktopCount = VirtualDesktop.Desktop.Count;

        windows.Clear();
        Win32.EnumWindows(filterWindows, IntPtr.Zero);
    }

    bool FilterWindow(IntPtr window, IntPtr lParam)
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
    }

    void DrawWindows()
    {
        using (var graphics = Graphics.FromImage(pictureBox.Image))
        {
            graphics.Clear(Color.Black);

            float scaleX = pictureBox.ClientRectangle.Width / (float)screen.width / desktopCount;
            float scaleY = pictureBox.ClientRectangle.Height / (float)screen.height;

            var currentDesktop = VirtualDesktop.Desktop.Current;
            var foreground = Win32.GetForegroundWindow();
            int activeDesktop = VirtualDesktop.Desktop.FromDesktop(VirtualDesktop.Desktop.Current);

            graphics.FillRectangle(activeDesktopBrush, screen.width * activeDesktop * scaleX, 0, screen.width * scaleX, pictureBox.Image.Height);

            // windows seems to list from front to back
            // we want to paint from back to front
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
                    pen = foregroundWindowPen;
                graphics.DrawRectangle(pen, x, y, width, height);
            }
        }
    }
}
