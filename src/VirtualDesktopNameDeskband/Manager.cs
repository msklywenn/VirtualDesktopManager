using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Windows.Forms;

class Manager : IDisposable
{
    public struct Rectangle
    {
        public int x, y, width, height;
        public bool IsInside(float _x, float _y)
        {
            return _x >= x && _y >= y && _x <= x + width && _y <= y + height;
        }
        public float Area { get { return width * height; } }
        public Vector2 Center
        {
            get
            {
                return new Vector2
                {
                    X = x + width * 0.5f,
                    Y = y + height * 0.5f
                };
            }
        }
    }

    class WindowInfo
    {
        public IntPtr handle;
        public Rectangle rectangle;
        public int desktop;
        public string name;

        public static WindowInfo Null
        {
            get
            {
                return new WindowInfo
                {
                    handle = IntPtr.Zero,
                    rectangle = new Rectangle { x = 0, y = 0, width = -1, height = -1 },
                    desktop = -1,
                    name = string.Empty
                };
            }
        }
    }

    // Outside references
    readonly PictureBox pictureBox;

    // Work variables
    readonly List<WindowInfo> windows = new List<WindowInfo>();
    public Rectangle screen { get; private set; }
    int desktopCount;
    WindowInfo pickedWindow = null;
    IntPtr hoveredWindow = IntPtr.Zero;
    int pickX, pickY;
    int pickMoveX, pickMoveY;
    //bool pickedFromAnotherDesktop;

    // Disposables
    readonly Pen foregroundWindowPen;
    readonly Pen otherWindowPen;
    readonly Pen pickedWindowPen;
    readonly Pen activeDesktopPen;
    readonly SolidBrush activeDesktopBrush;
    readonly SolidBrush windowBackgroundBrush;
    readonly SolidBrush hoveredWindowBrush;
    readonly SolidBrush textBrush;
    readonly Font font;

    // store delegates to avoid them being garbage collected in between calls from native
    readonly Win32.EnumWindowsProc filterWindows;
    readonly Win32.WinEventProc eventListener;

    ToolTip tooltip;

    public void Dispose()
    {
        foregroundWindowPen.Dispose();
        otherWindowPen.Dispose();
        pickedWindowPen.Dispose();
        activeDesktopPen.Dispose();
        activeDesktopBrush.Dispose();
        windowBackgroundBrush.Dispose();
        hoveredWindowBrush.Dispose();
        textBrush.Dispose();
        font.Dispose();
        tooltip.Dispose();
    }

    Color Lerp(Color lhs, Color rhs, float alpha)
    {
        return Color.FromArgb(
            (int)(lhs.A * alpha + rhs.A * (1f - alpha)),
            (int)(lhs.R * alpha + rhs.R * (1f - alpha)),
            (int)(lhs.G * alpha + rhs.G * (1f - alpha)),
            (int)(lhs.B * alpha + rhs.B * (1f - alpha)));
    }

    public Manager(PictureBox box)
    {
        pictureBox = box;

        if (pictureBox.Image == null)
            pictureBox.Image = new Bitmap(pictureBox.ClientRectangle.Width, pictureBox.ClientRectangle.Height);

        Color systemTint = Win32.GetSysColor(Win32.SysColor.COLOR_HIGHLIGHT);
        Color windowTint = Win32.GetSysColor(Win32.SysColor.COLOR_WINDOW);
        Color other = Color.FromArgb(96, windowTint.R, windowTint.G, windowTint.B);
        otherWindowPen = new Pen(other);
        foregroundWindowPen = new Pen(Lerp(other, windowTint, 0.5f));
        pickedWindowPen = new Pen(windowTint);
        activeDesktopPen = new Pen(systemTint);
        activeDesktopBrush = new SolidBrush(Color.FromArgb(64, systemTint.R, systemTint.G, systemTint.B));
        windowBackgroundBrush = new SolidBrush(Color.FromArgb(48, windowTint.R, windowTint.G, windowTint.B));
        hoveredWindowBrush = new SolidBrush(Color.FromArgb(96, windowTint.R, windowTint.G, windowTint.B));
        textBrush = new SolidBrush(Color.White);
        font = new Font("Segoe UI", 14);

        filterWindows = new Win32.EnumWindowsProc(FilterWindow);
        eventListener = new Win32.WinEventProc(EventListener);

        RefreshWindows();
        DrawWindows();

        pictureBox.MouseDown += TryPickWindow;
        pictureBox.MouseMove += MouseMove;
        pictureBox.MouseUp += ChangeDesktop;
        pictureBox.MouseLeave += MouseLeft;
        pictureBox.Resize += Resize;
        pictureBox.MouseWheel += SwitchDesktop;

        Win32.SetWinEventHook(interestingEvents.Min(), interestingEvents.Max(),
            IntPtr.Zero, eventListener, 0, 0, Win32.WinEventFlags.WINEVENT_OUTOFCONTEXT);

        tooltip = new ToolTip();
    }

    private void Resize(object sender, EventArgs e)
    {
        pictureBox.ClientSize = pictureBox.Parent.ClientSize;
        int w = Math.Max(pictureBox.ClientSize.Width, 1);
        int h = Math.Max(pictureBox.ClientSize.Height, 1);
        pictureBox.Image = new Bitmap(w, h);
        pictureBox.Width = pictureBox.ClientSize.Width;
        pictureBox.Height = pictureBox.ClientSize.Height;
    }

    static readonly Win32.WinEvents[] interestingEvents =
    {
        Win32.WinEvents.EVENT_SYSTEM_SWITCHEND, // alt-tab
        Win32.WinEvents.EVENT_SYSTEM_MOVESIZEEND,
        Win32.WinEvents.EVENT_SYSTEM_MINIMIZESTART,
        Win32.WinEvents.EVENT_SYSTEM_MINIMIZEEND,
        Win32.WinEvents.EVENT_SYSTEM_FOREGROUND,
        Win32.WinEvents.EVENT_OBJECT_LOCATIONCHANGE,
        Win32.WinEvents.EVENT_OBJECT_CREATE,
        Win32.WinEvents.EVENT_OBJECT_DESTROY,
        Win32.WinEvents.EVENT_OBJECT_UNCLOAKED,
        Win32.WinEvents.EVENT_OBJECT_CLOAKED,
        Win32.WinEvents.EVENT_OBJECT_HIDE,
        Win32.WinEvents.EVENT_OBJECT_SHOW,
        Win32.WinEvents.EVENT_SYSTEM_DESKTOPSWITCH,
    };

    void EventListener(IntPtr hWinEventHook, uint eventType,
        IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        if ((Win32.WinObjects)idObject == Win32.WinObjects.OBJID_WINDOW && interestingEvents.Contains((Win32.WinEvents)eventType))
        {
            RefreshWindows();
            DrawWindows();
        }
    }

    private void MouseLeft(object sender, EventArgs e)
    {
        pickedWindow = null;
        DrawWindows();
    }

    WindowInfo PickWindow(int desktop, int x, int y)
    {
        float smallestArea = float.MaxValue;
        WindowInfo best = WindowInfo.Null;
        foreach (var window in windows)
        {
            if (desktop == window.desktop && window.rectangle.IsInside(x, y))
            {
                float d = window.rectangle.Area;
                if (d < smallestArea)
                {
                    smallestArea = d;
                    best = window;
                }
            }
        }
        return best;
    }

    void PictureBoxToDesktop(int x, int y, out int dx, out int dy, out int desktop)
    {
        desktop = (int)Math.Floor(x * desktopCount / (float)pictureBox.Width);
        dx = (int)(x * (screen.width * desktopCount) / (float)pictureBox.Width + screen.x - desktop * screen.width);
        dy = (int)(y * screen.height / (float)pictureBox.Height + screen.y);
    }

    private void TryPickWindow(object sender, MouseEventArgs e)
    {
        if (sender == pictureBox)
        {
            PictureBoxToDesktop(e.X, e.Y, out int x, out int y, out int desktop);
            WindowInfo window = PickWindow(desktop, x, y);
            pickedWindow = window;
            if (window.handle != IntPtr.Zero)
            {
                pickX = e.X;
                pickY = e.Y;
                //int current = VirtualDesktop.Desktop.FromDesktop(VirtualDesktop.Desktop.Current);
                //pickedFromAnotherDesktop = current != window.desktop;
            }
        }
    }

    private void MouseMove(object sender, MouseEventArgs e)
    {
        if (pickedWindow != null)
        {
            pickMoveX = e.X - pickX;
            pickMoveY = e.Y - pickY;
            DrawWindows();
        }
        else
        {
            PictureBoxToDesktop(e.X, e.Y, out int x, out int y, out int desktop);
            var hovered = PickWindow(desktop, x, y);
            if (hovered.handle != hoveredWindow)
                tooltip.SetToolTip(pictureBox, hovered.name);
            hoveredWindow = hovered.handle;
            DrawWindows();
        }
    }

    int Clip(int x, int width, int area)
    {
        if (x < 0) return 0;
        if (x + width > area) return area - width;
        return x;
    }

    private void SwitchDesktop(object sender, MouseEventArgs e)
    {
        if (e.Delta < 0f)
        {
            var left = VirtualDesktop.Desktop.Current.Left;
            if (left != null)
                left.MakeVisible();
            else
                VirtualDesktop.Desktop.FromIndex(VirtualDesktop.Desktop.Count - 1).MakeVisible();
        } 
        else if (e.Delta > 0f)
        {
            var right = VirtualDesktop.Desktop.Current.Right;
            if (right != null)
                right.MakeVisible();
            else
                VirtualDesktop.Desktop.FromIndex(0).MakeVisible();
        }
    }

    private void ChangeDesktop(object sender, MouseEventArgs e)
    {
        var box = sender as PictureBox;

        if (e.Button != MouseButtons.Left)
            return;

        pickMoveX = e.X - pickX;
        pickMoveY = e.Y - pickY;

        if (pickedWindow == null || pickedWindow.handle == IntPtr.Zero || (pickMoveX == 0 && pickMoveY == 0))
        {
            int desktopIndex = (int)Math.Floor(e.X * desktopCount / (float)box.Width);
            var desktop = VirtualDesktop.DesktopManager.GetDesktop(desktopIndex);
            VirtualDesktop.DesktopManager.VirtualDesktopManagerInternal.SwitchDesktop(desktop);
        }
        else if (pickedWindow.handle != IntPtr.Zero)
        {
            int desktopIndex = (int)Math.Floor(e.X * desktopCount / (float)box.Width);

            var desktop = VirtualDesktop.Desktop.FromIndex(desktopIndex);
            desktop.MoveWindow(pickedWindow.handle);

            int x = Clip(pickedWindow.rectangle.x + (int)(pickMoveX / (float)box.Width * screen.width * desktopCount) % screen.width, pickedWindow.rectangle.width, screen.width);
            int y = Clip(pickedWindow.rectangle.y + (int)(pickMoveY / (float)box.Height * screen.height) % screen.height, pickedWindow.rectangle.height, screen.height);
            Win32.MoveWindow(pickedWindow.handle, x, y, pickedWindow.rectangle.width, pickedWindow.rectangle.height, false);

            RefreshWindows();

            //int current = VirtualDesktop.Desktop.FromDesktop(VirtualDesktop.Desktop.Current);
            //if (desktopIndex == current && pickedFromAnotherDesktop)
            //{
            //    Win32.SetForegroundWindow(pickedWindow);
            //}
        }

        pickedWindow = null;

        DrawWindows();
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
        try
        {
            if (IsInterestingWindow(window))
            {
                var desktopID = VirtualDesktop.DesktopManager.VirtualDesktopManager.GetWindowDesktopId(window);
                if (desktopID != Guid.Empty)
                {
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
                        desktop = index,
                        name = Win32.GetWindowText(window)
                    });
                }
            }
        }
        catch { }
        return true; // continue enumeration
    }

    delegate void Paint(IntPtr window, int x, int y, int w, int h);
    void PaintWindows(float scaleX, float scaleY, Paint paint)
    {
        // windows seems to list from front to back
        // we want to paint from back to front
        for (int i = windows.Count - 1; i >= 0; i--)
        {
            var window = windows[i];

            float x = 1 + (int)((window.rectangle.x - screen.x + screen.width * window.desktop) * scaleX);
            float y = 1 + (int)((window.rectangle.y - screen.y) * scaleY);
            float width = (int)(window.rectangle.width * scaleX);
            float height = (int)(window.rectangle.height * scaleY);

            if (pickedWindow == window)
            {
                x += pickMoveX;
                y += pickMoveY;
            }

            paint(window.handle, (int)x, (int)y, (int)width, (int)height);
        }
    }

    void DrawWindows()
    {
        try
        {
            using (var graphics = Graphics.FromImage(pictureBox.Image))
            {
                graphics.Clear(Color.Transparent);

                float scaleX = pictureBox.Image.Width / (float)screen.width / desktopCount;
                float scaleY = pictureBox.Image.Height / (float)screen.height;

                var currentDesktop = VirtualDesktop.Desktop.Current;
                var foreground = Win32.GetForegroundWindow();
                int activeDesktop = VirtualDesktop.Desktop.FromDesktop(VirtualDesktop.Desktop.Current);

                // highlight active desktop
                graphics.FillRectangle(activeDesktopBrush, screen.width * activeDesktop * scaleX, 0, screen.width * scaleX, pictureBox.Image.Height);

                scaleX = (pictureBox.Image.Width - 2) / (float)screen.width / desktopCount;
                scaleY = (pictureBox.Image.Height - 2) / (float)screen.height;

                // draw translucent window previews
                PaintWindows(scaleX, scaleY, delegate (IntPtr window, int x, int y, int width, int height)
                {
                    SolidBrush brush = ((pickedWindow != null && window == pickedWindow.handle) || window == hoveredWindow)
                        ? hoveredWindowBrush
                        : windowBackgroundBrush;
                    graphics.FillRectangle(brush, x, y, width, height);
                });

                // draw window borders
                PaintWindows(scaleX, scaleY, delegate (IntPtr window, int x, int y, int width, int height)
                {
                    var pen = otherWindowPen;
                    if (pickedWindow != null && pickedWindow.handle == window)
                        pen = pickedWindowPen;
                    else if (foreground == window)
                        pen = foregroundWindowPen;
                    graphics.DrawRectangle(pen, x, y, width, height);
                });

                graphics.DrawRectangle(activeDesktopPen, screen.width * activeDesktop * scaleX, 0,
                    screen.width * scaleX - 1, pictureBox.Image.Height - 1);

                // display virtual desktop number
                graphics.TextRenderingHint |= System.Drawing.Text.TextRenderingHint.AntiAlias;
                StringFormat format = new StringFormat()
                {
                    LineAlignment = StringAlignment.Center,
                    Alignment = StringAlignment.Center,
                };
                for (int i = 1; i <= desktopCount; i++)
                    graphics.DrawString(i.ToString(), font, textBrush,
                        new RectangleF(screen.width * (i - 1) * scaleX, 0, screen.width * scaleX, pictureBox.Image.Height), format);
            }
        }
        catch { }
        pictureBox.Refresh();
    }
}
