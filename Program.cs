/*
***************************************************************************
Project          :  Wintermelon (modernized rebuild, .NET 8)
Original author  :  arun-goud  (https://github.com/arun-goud/Wintermelon)
                    (WIN)dows (T)en & (E)leven (R)endering of (M)irrored
                    (E)vent-triggered (L)ockscreens (O)n (N)on-main displays
Description      :  Console-style Win 10 & 11 helper that toggles the display
                    projection mode between Duplicate (Clone) and Extend.
                    Accepts a single argument:
                      save    -> record positions of open windows, then CLONE
                                 displays (so the lock screen mirrors onto all
                                 monitors).
                      restore -> EXTEND displays, then move the windows that
                                 were on non-primary monitors back to where
                                 they were before the clone.
                    Window positions are stored at
                    %LOCALAPPDATA%\Wintermelon\winpos.txt
Use case         :  Registered as two Task Scheduler tasks triggered by
                    workstation lock / unlock, this makes the Windows lock
                    screen appear on every monitor instead of just the primary.

Changes vs. the original:
  - Retargeted from .NET Core 3.1 to net8.0-windows.
  - OutputType WinExe so no console window flashes on lock/unlock.
  - Hardened the window-enumeration callback with try/catch so a single
    inaccessible (e.g. elevated) process cannot abort the whole pass.
***************************************************************************
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace Wintermelon
{
    class Program
    {
        // PInvoke imports for controlling window placement
        [DllImport("user32.dll")]
        private static extern bool SetWindowPlacement(IntPtr hWnd, [In] ref WINDOWPLACEMENT lpwndpl);
        [DllImport("user32.dll")]
        private static extern bool GetWindowPlacement(IntPtr hWnd, out WINDOWPLACEMENT lpwndpl);

        // PInvoke imports to enumerate top-level windows
        public delegate bool EnumDelegate(IntPtr hWnd, int lParam);
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll")]
        public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpWindowText, int nMaxCount);
        [DllImport("user32.dll")]
        public static extern bool EnumDesktopWindows(IntPtr hDesktop, EnumDelegate lpEnumCallbackFunction, IntPtr lParam);
        [DllImport("User32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
        [DllImport("dwmapi.dll")]
        public static extern int DwmGetWindowAttribute(IntPtr hwnd, DwmWindowAttribute dwAttribute, out bool pvAttribute, int cbAttribute);

        [Flags]
        public enum DwmWindowAttribute
        {
            DWMWA_NCRENDERING_ENABLED = 1,
            DWMWA_NCRENDERING_POLICY,
            DWMWA_TRANSITIONS_FORCEDISABLED,
            DWMWA_ALLOW_NCPAINT,
            DWMWA_CAPTION_BUTTON_BOUNDS,
            DWMWA_NONCLIENT_RTL_LAYOUT,
            DWMWA_FORCE_ICONIC_REPRESENTATION,
            DWMWA_FLIP3D_POLICY,
            DWMWA_EXTENDED_FRAME_BOUNDS,
            DWMWA_HAS_ICONIC_BITMAP,
            DWMWA_DISALLOW_PEEK,
            DWMWA_EXCLUDED_FROM_PEEK,
            DWMWA_CLOAK,
            DWMWA_CLOAKED,
            DWMWA_FREEZE_REPRESENTATION,
            DWMWA_LAST
        }

        // Display projection switch
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern long SetDisplayConfig(uint numPathArrayElements,
            IntPtr pathArray, uint numModeArrayElements, IntPtr modeArray, uint flags);

        public enum SDC_FLAGS
        {
            SDC_TOPOLOGY_INTERNAL = 1,
            SDC_TOPOLOGY_CLONE = 2,
            SDC_TOPOLOGY_EXTEND = 4,
            SDC_TOPOLOGY_EXTERNAL = 8,
            SDC_APPLY = 128
        }

        public static void CloneDisplays()
        {
            SetDisplayConfig(0, IntPtr.Zero, 0, IntPtr.Zero, ((uint)SDC_FLAGS.SDC_APPLY | (uint)SDC_FLAGS.SDC_TOPOLOGY_CLONE));
        }

        public static void ExtendDisplays()
        {
            SetDisplayConfig(0, IntPtr.Zero, 0, IntPtr.Zero, ((uint)SDC_FLAGS.SDC_APPLY | (uint)SDC_FLAGS.SDC_TOPOLOGY_EXTEND));
        }

        public static void ExternalDisplay()
        {
            SetDisplayConfig(0, IntPtr.Zero, 0, IntPtr.Zero, ((uint)SDC_FLAGS.SDC_APPLY | (uint)SDC_FLAGS.SDC_TOPOLOGY_EXTERNAL));
        }

        public static void InternalDisplay()
        {
            SetDisplayConfig(0, IntPtr.Zero, 0, IntPtr.Zero, ((uint)SDC_FLAGS.SDC_APPLY | (uint)SDC_FLAGS.SDC_TOPOLOGY_INTERNAL));
        }

        // RECT structure required by WINDOWPLACEMENT
        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
            public RECT(int left, int top, int right, int bottom)
            {
                this.Left = left;
                this.Top = top;
                this.Right = right;
                this.Bottom = bottom;
            }
            public override string ToString()
            {
                return "(" + this.Left.ToString() + "," + this.Top.ToString() + "," +
                    this.Right.ToString() + "," + this.Bottom.ToString() + ")";
            }
        }

        // POINT structure required by WINDOWPLACEMENT
        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
            public POINT(int x, int y)
            {
                this.X = x;
                this.Y = y;
            }
            public override string ToString()
            {
                return "(" + this.X.ToString() + "," + this.Y.ToString() + ")";
            }
        }

        // WINDOWPLACEMENT stores the position, size, and state of a window
        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        private struct WINDOWPLACEMENT
        {
            public int length;
            public int flags;
            public int showCmd;
            public POINT minPosition;
            public POINT maxPosition;
            public RECT normalPosition;
            public override string ToString()
            {
                return "Length=" + length.ToString() + "\tFlags=" + flags.ToString() + "\tCmd=" + showCmd.ToString() +
                    "\tMin=" + minPosition.ToString() + "\tMax=" + maxPosition.ToString() + "\tRect=" + normalPosition.ToString();
            }
        }

        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                // Usage: Wintermelon [save | restore]
                return;
            }

            string appDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Wintermelon");
            if (!Directory.Exists(appDir))
                Directory.CreateDirectory(appDir);
            string settingsFile = Path.Combine(appDir, "winpos.txt");

            Screen[] screens = Screen.AllScreens;

            // Collect visible top-level .exe windows and their placements.
            var procSettings = new List<string>();
            var handleInfo = new List<IntPtr>();
            EnumDelegate filter = delegate (IntPtr hWnd, int lParam)
            {
                try
                {
                    StringBuilder strbTitle = new StringBuilder(255);
                    GetWindowText(hWnd, strbTitle, strbTitle.Capacity + 1);
                    string strTitle = strbTitle.ToString();

                    if (IsWindowVisible(hWnd) && string.IsNullOrEmpty(strTitle) == false)
                    {
                        WINDOWPLACEMENT wp = new WINDOWPLACEMENT();
                        wp.length = Marshal.SizeOf(wp);
                        GetWindowPlacement(hWnd, out wp);

                        uint pid = 0;
                        GetWindowThreadProcessId(hWnd, out pid);

                        // Skip cloaked windows (e.g. background WinRT/UWP apps like the Store)
                        DwmGetWindowAttribute(hWnd, DwmWindowAttribute.DWMWA_CLOAKED, out bool isCloaked, Marshal.SizeOf(typeof(bool)));
                        if (!isCloaked)
                        {
                            string exeName = Process.GetProcessById((int)pid).MainModule.FileName;
                            if (exeName.ToLower().EndsWith(".exe")) // Save only .exe-generated window positions
                            {
                                var screen = Screen.FromHandle(hWnd);
                                procSettings.Add("Title=" + strTitle + "\tPID=" + pid.ToString() + "\tID=" + hWnd.ToString() + "\tName=" + exeName +
                                    "\t" + wp.ToString() + "\tPrimary=" + screen.Primary.ToString() + "\tDisplay=" + screen.DeviceName);
                                handleInfo.Add(hWnd);
                            }
                        }
                    }
                }
                catch
                {
                    // Inaccessible process (e.g. elevated/protected) — skip this window and continue.
                }
                return true;
            };

            // Enumerate visible windows using the filter defined above
            if (!EnumDesktopWindows(IntPtr.Zero, filter, IntPtr.Zero))
            {
                int nErrorCode = Marshal.GetLastWin32Error();
                string strErrMsg = String.Format("EnumDesktopWindows failed with code {0}.", nErrorCode);
                throw new Exception(strErrMsg);
            }

            // 'save' --> save window positions, then clone displays
            if (string.Equals(args[0], "save", StringComparison.OrdinalIgnoreCase))
            {
                File.WriteAllText(settingsFile, "");
                foreach (var item in procSettings)
                {
                    File.AppendAllText(settingsFile, item + "\n");
                }

                CloneDisplays();
            }
            // 'restore' --> extend displays, then restore windows
            else if (string.Equals(args[0], "restore", StringComparison.OrdinalIgnoreCase))
            {
                ExtendDisplays();

                if (!File.Exists(settingsFile))
                    return;

                // Read settings file into a dictionary keyed by pid_id_title
                var processInfo = new Dictionary<string, WINDOWPLACEMENT>();
                using (System.IO.StreamReader infile = new System.IO.StreamReader(@settingsFile))
                {
                    string line;
                    while ((line = infile.ReadLine()) != null)
                    {
                        if (string.IsNullOrWhiteSpace(line))
                            continue;

                        var dict = line.Split('\t').Select(x => x.Split('=')).ToDictionary(x => x[0], x => x[1]);
                        string pid = dict["PID"];
                        string id = dict["ID"];
                        string ptitle = dict["Title"];
                        bool isPrimary = string.Equals(dict["Primary"], "True", StringComparison.OrdinalIgnoreCase);

                        // Only windows that were NOT on the primary screen need repositioning
                        if (!isPrimary)
                        {
                            WINDOWPLACEMENT wp = new WINDOWPLACEMENT();
                            wp.length = Marshal.SizeOf(wp);
                            wp.flags = int.Parse(dict["Flags"]);
                            wp.showCmd = int.Parse(dict["Cmd"]);
                            var minxy = dict["Min"].Replace("(", "").Replace(")", "").Split(",");
                            wp.minPosition = new POINT(int.Parse(minxy[0]), int.Parse(minxy[1]));
                            var maxxy = dict["Max"].Replace("(", "").Replace(")", "").Split(",");
                            wp.maxPosition = new POINT(int.Parse(maxxy[0]), int.Parse(maxxy[1]));
                            var pqrs = dict["Rect"].Replace("(", "").Replace(")", "").Split(",");
                            wp.normalPosition = new RECT(int.Parse(pqrs[0]), int.Parse(pqrs[1]), int.Parse(pqrs[2]), int.Parse(pqrs[3]));

                            processInfo.Add(pid + "_" + id + "_" + ptitle, wp);
                        }
                    }
                }

                foreach (var handle in handleInfo)
                {
                    StringBuilder strbTitle = new StringBuilder(255);
                    GetWindowText(handle, strbTitle, strbTitle.Capacity + 1);
                    string ptitle = strbTitle.ToString();
                    uint procid = 0;
                    GetWindowThreadProcessId(handle, out procid);

                    string pid = procid.ToString();
                    string id = handle.ToString();
                    string key = pid + "_" + id + "_" + ptitle;

                    if (processInfo.ContainsKey(key))
                    {
                        // First restore as a normal (non-maximized) window...
                        WINDOWPLACEMENT wp = processInfo[key];
                        wp.length = Marshal.SizeOf(wp);
                        wp.showCmd = 1;
                        SetWindowPlacement(handle, ref wp);

                        // ...then apply the saved state (handles maximized windows correctly)
                        WINDOWPLACEMENT wp2 = processInfo[key];
                        wp2.length = Marshal.SizeOf(wp2);
                        SetWindowPlacement(handle, ref wp2);
                    }
                }
            }
            else
            {
                // Usage: Wintermelon [save | restore]
                return;
            }
        }
    }
}
