using System;
using System.Runtime.InteropServices;
using System.Text;

public static class Win32
{
    public delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsWindowVisible(IntPtr hwnd);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct WINDOWINFO
    {
        uint cbSize;
        public RECT rcWindow;
        public RECT rcClient;
        public uint dwStyle;
        public uint dwExStyle;
        public uint dwWindowStatus;
        public uint cxWindowBorders;
        public uint cyWindowBorders;
        public ushort atomWindowType;
        public ushort wCreatorVersion;

        public WINDOWINFO(Boolean? filler) : this()   // Allows automatic initialization of "cbSize" with "new WINDOWINFO(null/true/false)".
        {
            cbSize = (UInt32)(Marshal.SizeOf(typeof(WINDOWINFO)));
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetWindowInfo(IntPtr hwnd, out WINDOWINFO pwi);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    public static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

    [DllImport("user32.dll")]
    public static extern IntPtr GetActiveWindow();

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetForegroundWindow(IntPtr hwnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder strText, int maxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    /// <summary> Get the text for the window pointed to by hWnd </summary>
    public static string GetWindowText(IntPtr hWnd)
    {
        int size = GetWindowTextLength(hWnd);
        if (size > 0)
        {
            var builder = new StringBuilder(size + 1);
            GetWindowText(hWnd, builder, builder.Capacity);
            return builder.ToString();
        }

        return String.Empty;
    }

    public enum SystemMetric : uint
    {
        SM_XVIRTUALSCREEN = 76,
        SM_YVIRTUALSCREEN = 77,
        SM_CXVIRTUALSCREEN = 78,
        SM_CYVIRTUALSCREEN = 79,
    };

    [DllImport("user32.dll")]
    public static extern int GetSystemMetrics(SystemMetric smIndex);

    public enum WinEventFlags : uint
    {
        WINEVENT_OUTOFCONTEXT = 0x0000, // Events are ASYNC
        WINEVENT_SKIPOWNTHREAD = 0x0001, // Don't call back for events on installer's thread
        WINEVENT_SKIPOWNPROCESS = 0x0002, // Don't call back for events on installer's process
        WINEVENT_INCONTEXT = 0x0004, // Events are SYNC, this causes your dll to be injected into every process
    }

    public enum WinEvents : uint
    {
        EVENT_AIA_START = 0xA000,
        EVENT_AIA_END = 0xAFFF,
        EVENT_MIN = 0x00000001,
        EVENT_MAX = 0x7FFFFFFF,
        EVENT_OBJECT_ACCELERATORCHANGE = 0x8012,
        EVENT_OBJECT_CLOAKED = 0x8017,
        EVENT_OBJECT_CONTENTSCROLLED = 0x8015,
        EVENT_OBJECT_CREATE = 0x8000,
        EVENT_OBJECT_DEFACTIONCHANGE = 0x8011,
        EVENT_OBJECT_DESCRIPTIONCHANGE = 0x800D,
        EVENT_OBJECT_DESTROY = 0x8001,
        EVENT_OBJECT_DRAGSTART = 0x8021,
        EVENT_OBJECT_DRAGCANCEL = 0x8022,
        EVENT_OBJECT_DRAGCOMPLETE = 0x8023,
        EVENT_OBJECT_DRAGENTER = 0x8024,
        EVENT_OBJECT_DRAGLEAVE = 0x8025,
        EVENT_OBJECT_DRAGDROPPED = 0x8026,
        EVENT_OBJECT_END = 0x80FF,
        EVENT_OBJECT_FOCUS = 0x8005,
        EVENT_OBJECT_HELPCHANGE = 0x8010,
        EVENT_OBJECT_HIDE = 0x8003,
        EVENT_OBJECT_HOSTEDOBJECTSINVALIDATED = 0x8020,
        EVENT_OBJECT_IME_HIDE = 0x8028,
        EVENT_OBJECT_IME_SHOW = 0x8027,
        EVENT_OBJECT_IME_CHANGE = 0x8029,
        EVENT_OBJECT_INVOKED = 0x8013,
        EVENT_OBJECT_LIVEREGIONCHANGED = 0x8019,
        EVENT_OBJECT_LOCATIONCHANGE = 0x800B,
        EVENT_OBJECT_NAMECHANGE = 0x800C,
        EVENT_OBJECT_PARENTCHANGE = 0x800F,
        EVENT_OBJECT_REORDER = 0x8004,
        EVENT_OBJECT_SELECTION = 0x8006,
        EVENT_OBJECT_SELECTIONADD = 0x8007,
        EVENT_OBJECT_SELECTIONREMOVE = 0x8008,
        EVENT_OBJECT_SELECTIONWITHIN = 0x8009,
        EVENT_OBJECT_SHOW = 0x8002,
        EVENT_OBJECT_STATECHANGE = 0x800A,
        EVENT_OBJECT_TEXTEDIT_CONVERSIONTARGETCHANGED = 0x8030,
        EVENT_OBJECT_TEXTSELECTIONCHANGED = 0x8014,
        EVENT_OBJECT_UNCLOAKED = 0x8018,
        EVENT_OBJECT_VALUECHANGE = 0x800E,
        EVENT_OEM_DEFINED_START = 0x0101,
        EVENT_OEM_DEFINED_END = 0x01FF,
        EVENT_SYSTEM_ALERT = 0x0002,
        EVENT_SYSTEM_ARRANGMENTPREVIEW = 0x8016,
        EVENT_SYSTEM_CAPTUREEND = 0x0009,
        EVENT_SYSTEM_CAPTURESTART = 0x0008,
        EVENT_SYSTEM_CONTEXTHELPEND = 0x000D,
        EVENT_SYSTEM_CONTEXTHELPSTART = 0x000C,
        EVENT_SYSTEM_DESKTOPSWITCH = 0x0020,
        EVENT_SYSTEM_DIALOGEND = 0x0011,
        EVENT_SYSTEM_DIALOGSTART = 0x0010,
        EVENT_SYSTEM_DRAGDROPEND = 0x000F,
        EVENT_SYSTEM_DRAGDROPSTART = 0x000E,
        EVENT_SYSTEM_END = 0x00FF,
        EVENT_SYSTEM_FOREGROUND = 0x0003,
        EVENT_SYSTEM_MENUPOPUPEND = 0x0007,
        EVENT_SYSTEM_MENUPOPUPSTART = 0x0006,
        EVENT_SYSTEM_MENUEND = 0x0005,
        EVENT_SYSTEM_MENUSTART = 0x0004,
        EVENT_SYSTEM_MINIMIZEEND = 0x0017,
        EVENT_SYSTEM_MINIMIZESTART = 0x0016,
        EVENT_SYSTEM_MOVESIZEEND = 0x000B,
        EVENT_SYSTEM_MOVESIZESTART = 0x000A,
        EVENT_SYSTEM_SCROLLINGEND = 0x0013,
        EVENT_SYSTEM_SCROLLINGSTART = 0x0012,
        EVENT_SYSTEM_SOUND = 0x0001,
        EVENT_SYSTEM_SWITCHEND = 0x0015,
        EVENT_SYSTEM_SWITCHSTART = 0x0014,
        EVENT_UIA_EVENTID_START = 0x4E00,
        EVENT_UIA_EVENTID_END = 0x4EFF,
        EVENT_UIA_PROPID_START = 0x7500,
        EVENT_UIA_PROPID_END = 0x75FF
    }

    public delegate void WinEventProc(IntPtr hWinEventHook, uint eventType,
        IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

    [DllImport("user32.dll")]
    public static extern IntPtr SetWinEventHook(WinEvents eventMin, WinEvents eventMax, IntPtr
        hmodWinEventProc, WinEventProc lpfnWinEventProc, uint idProcess,
        uint idThread, WinEventFlags dwFlags);
}
