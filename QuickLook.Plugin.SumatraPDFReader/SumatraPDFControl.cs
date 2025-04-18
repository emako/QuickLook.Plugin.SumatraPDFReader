// Copyright © 2025 QL-Win Contributors
//
// This file is part of QuickLook program.
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace QuickLook.Plugin.SumatraPDFReader;

/// <summary>
/// Windows Forms Control which embeds modifed version of SumatraPDF to read and view Portable Document Files (PDF)
/// </summary>
/// <remarks>
/// This control allows you to open and read PDF files with most features present in great SumatraPDF reader (https://www.sumatrapdfreader.org/).
/// It requires an specific compiled Sumatra code version (https://github.com/marcoscmonteiro/sumatrapdf) which enables SumatraPDF
/// working in an enhanced plugin mode.
/// It's forked from original SumatraPDF code (https://github.com/sumatrapdfreader/sumatrapdf)
/// </remarks>
[Guid("E5FDA170-ACF6-4C4D-AAF9-C8F9C70EE09C")]
public partial class SumatraPDFControl : Control
{
    private struct COPYDATASTRUCT
    {
        public nint dwData;
        public int cbData;
        public nint lpData;
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern nint FindWindowEx(nint parentHandle, nint childAfter, string lclassName, string windowTitle);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern int MoveWindow(nint hWnd, int X, int Y, int nWidth, int nHeight, int bRepaint);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern nint SendMessage(nint hWnd, uint Msg, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    private static extern int SendNotifyMessage(nint hWnd, uint Msg, nint wParam, nint lParam);

    // Windows constants and default pointers
    private const int WM_COPYDATA = 0x004A;

    private const int WM_DESTROY = 0x0002;
    private const int WM_SETFOCUS = 0x0007;
    private const int WM_PARENTNOTIFY = 0x0210;
    private const int WM_LBUTTONDOWN = 0x0201;
    private const int WM_LBUTTONDBLCLK = 0x0203;
    private const int WM_RBUTTONDOWN = 0x0204;
    private const int WM_RBUTTONDBLCLK = 0x0206;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_CHAR = 0x0102; // for use by event KeyPress

    private readonly nint SUMATRAPLUGIN = 0x44646558;

    private enum SumatraPDFCommandEmum
    {
        CmdPrint,
        CmdCopySelection,
        CmdSelectAll,
        CmdGoToNextPage,
        CmdGoToPrevPage,
        CmdGoToFirstPage,
        CmdGoToLastPage,
        CmdRefresh
    }

    private Process SumatraProcess;
    private string sCurrentFile = string.Empty;
    private nint pSumatraWindowHandle;
    private nint SUMATRAMESSAGETYPE;

    private static List<IntPtr> pSumatraWindowHandleList = [];

    private void SumatraPDFCopyDataMsg(string strMessage, params object[] parr)
    {
        // Without define current file commands cannot be send to sumatra
        if (sCurrentFile == string.Empty) return;

        if (SumatraWindowHandle == IntPtr.Zero && pSumatraWindowHandleList.Count == 0) return;
        nint SumatraCurrentHandle = SumatraWindowHandle == IntPtr.Zero ? pSumatraWindowHandleList[0] : SumatraWindowHandle;

        string DDEMessage = string.Empty;
        if (strMessage.StartsWith("[")) DDEMessage = String.Format(strMessage, parr);
        else
        {
            DDEMessage = "[" + strMessage + "("; //\"" + sCurrentFile + "\"";
            bool FirstParam = true;
            foreach (object p in parr)
            {
                if (!FirstParam) DDEMessage += ","; else FirstParam = false;
                DDEMessage += p.GetType().FullName switch
                {
                    "System.String" => "\"" + p.ToString() + "\"",
                    "System.Double" => ((double)p).ToString(new System.Globalization.CultureInfo("en-US")),
                    _ => p.ToString(),
                };
            }
            DDEMessage += ")]";
        }

        COPYDATASTRUCT DataStruct = default;
        DDEMessage += "\0";
        DataStruct.dwData = SUMATRAMESSAGETYPE;
        DataStruct.cbData = checked(DDEMessage.Length * Marshal.SystemDefaultCharSize);
        DataStruct.lpData = Marshal.StringToHGlobalAuto(DDEMessage);
        nint pDataStruct = Marshal.AllocHGlobal(Marshal.SizeOf(DataStruct));
        Marshal.StructureToPtr(DataStruct, pDataStruct, fDeleteOld: false);
        SendMessage(SumatraCurrentHandle, WM_COPYDATA, (IntPtr)0, pDataStruct);
        Marshal.FreeHGlobal(DataStruct.lpData);
        Marshal.FreeHGlobal(pDataStruct);
    }

    private void SumatraPDFMsg(uint Msg, int Cmd = 0, bool Async = false)
    {
        if (SumatraWindowHandle != (nint)0)
        {
            if (!Async) SendMessage(SumatraWindowHandle, Msg, Cmd, 0);
            else SendNotifyMessage(SumatraWindowHandle, Msg, Cmd, 0);
        }
    }

    private void SumatraPDFFrameCmd(SumatraPDFCommandEmum cmd, bool Async = false)
    {
        SumatraPDFCopyDataMsg("SetProperty", "SendCommand" + (Async ? "Async" : string.Empty), cmd.ToString());
    }

    private nint SumatraWindowHandle
    {
        get
        {
            if (pSumatraWindowHandle == IntPtr.Zero)
            {
                pSumatraWindowHandle = FindWindowEx(base.Handle, default, null, null);
                if (pSumatraWindowHandle != IntPtr.Zero)
                    pSumatraWindowHandleList.Add(pSumatraWindowHandle);
            }
            return pSumatraWindowHandle;
        }
    }

    private nint ParseSumatraMsg(string sMsg, nint dwData)
    {
        string sMsg0 = sMsg.Substring(0, sMsg.Length - 1);
        int CallBackReturn = 0;

        // dwData = 0x4C5255 it's a message to internet browser when SumatraPDF is operating in plugin mode, so raise LinkClickedMessage event
        if (dwData == 0x4C5255)
        {
            LinkClick?.Invoke(this, new LinkClickedEventArgs(sMsg0));
            return CallBackReturn;
        }
        else
            if (dwData != SUMATRAPLUGIN) sMsg0 = string.Format("[UnknownMessage(\"{0}\")]", sMsg0);

        Match m = Regex.Match(sMsg0, @"\[(?<message>\w+)\((?<args>.*)\)\]");
        if (m.Success)
        {
            string mmsg = m.Result("${message}");
            switch (mmsg)
            {
                case "PageChanged":
                case "Page":
                    Match mPG = Regex.Match(m.Result("${args}"), @"(?<pageNo>.+)\,\s*\u0022(?<namedDest>.*)\u0022");
                    nPage = int.Parse(mPG.Result("${pageNo}"));
                    sNamedDest = mPG.Result("${namedDest}");
                    if (mmsg.Contains("Changed"))
                        PageChanged?.Invoke(this, new PageChangedEventArgs(nPage, sNamedDest));
                    break;

                case "ContextMenuOpening":
                case "TocContextMenuOpening":

                    Match m2 = Regex.Match(m.Result("${args}"), @"(?<x>.+)\,\s*(?<y>.+)");
                    var cmoe = new ContextMenuOpeningEventArgs(int.Parse(m2.Result("${x}")), int.Parse(m2.Result("${y}")));

                    ContextMenuOpening?.Invoke(this, cmoe);

                    if (ContextMenuStrip != null && !cmoe.Handled)
                    {
                        cmoe.Handled = true;
                        if (!ContextMenuStrip.Visible)
                        {
                            ContextMenuStrip.LostFocus -= ContextMenuStrip_LostFocus;
                            ContextMenuStrip.Show(this, cmoe.X, cmoe.Y);
                            ContextMenuStrip.Focus();
                            ContextMenuStrip.LostFocus += ContextMenuStrip_LostFocus;
                        }
                    }
                    CallBackReturn = cmoe.Handled ? 1 : 0;
                    break;

                case "ZoomChanged":
                case "ZoomChangedMouseWeel":
                case "Zoom":

                    Match mZoom = Regex.Match(m.Result("${args}"), @"(?<x>.+)\,\s*(?<y>.+)");
                    fZoom = float.Parse(mZoom.Result("${x}"), new System.Globalization.CultureInfo("en-US"));
                    float ZoomVirtual0 = float.Parse(mZoom.Result("${y}"), new System.Globalization.CultureInfo("en-US"));
                    if (ZoomVirtual0 < 0) fZoomVirtual = (ZoomVirtualEnum)ZoomVirtual0; else fZoomVirtual = ZoomVirtualEnum.None;
                    if (mmsg.Contains("Changed"))
                        ZoomChanged?.Invoke(this, new ZoomChangedEventArgs(fZoom, fZoomVirtual, (mmsg != "ZoomChanged")));
                    break;

                case "Rotation":
                case "RotationChanged":
                    Match mRotation = Regex.Match(m.Result("${args}"), @"(?<rot>.+)");
                    eRotation = (RotationEnum)int.Parse(mRotation.Result("${rot}"));
                    break;

                case "DisplayModeChanged":
                case "DisplayMode":
                    eDisplayMode = (DisplayModeEnum)int.Parse(m.Result("${args}"));
                    if (mmsg.Contains("Changed")) DisplayModeChanged?.Invoke(this, new DisplayModeChangedEventArgs(eDisplayMode));
                    break;

                case "EnableAccelerators":
                    bKeyAccelerators = (int.Parse(m.Result("${args}")) == 1);
                    break;

                case "StartupFinished":
                    StartupFinished?.Invoke(this, new EventArgs());
                    StartupFinished?.Invoke(this, new EventArgs());
                    StartupFinished?.Invoke(this, new EventArgs());
                    break;

                case "FileOpened":
                    GetPage();
                    SumatraPDFCopyDataMsg("SetProperty", "AllowEditAnnotations", "0");
                    FileOpened?.Invoke(this, new EventArgs());
                    break;

                case "ToolbarVisible":
                    bToolbarVisible = (int.Parse(m.Result("${args}")) == 1);
                    break;

                case "TocVisible":
                    bTocVisible = (int.Parse(m.Result("${args}")) == 1);
                    break;

                case "ScrollState":
                case "ScrollStateChanged":
                    Match mSP = Regex.Match(m.Result("${args}"), @"(?<page>.+)\,(?<x>.+)\,\s*(?<y>.+)");
                    pScrollState = new ScrollStateStruct(
                        int.Parse(mSP.Result("${page}")),
                        double.Parse(mSP.Result("${x}"), new System.Globalization.CultureInfo("en-US")),
                        double.Parse(mSP.Result("${y}"), new System.Globalization.CultureInfo("en-US"))
                    );
                    if (mmsg.Contains("Changed")) ScrollStateChanged?.Invoke(this, new ScrollStateEventArgs(pScrollState));
                    break;

                case "PageCount":
                    nPageCount = int.Parse(m.Result("${args}"));
                    break;

                default:
                    CallBackReturn = RaiseDefaultSumatraEvent(sMsg0, dwData);
                    break;
            }
        }

        return (nint)CallBackReturn;
    }

    /// <summary>
    /// Processes Windows Messages.
    /// </summary>
    /// <remarks>
    /// This method has many customizations in order to process SumatraPDF messages. So, if there is need to override, remember to call base.WndProc.
    /// </remarks>
    /// <param name="m">The Windows <see cref="Message"/> to process.</param>
    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);

        if (m.Msg == WM_SETFOCUS)
        {
            OnEnter(new EventArgs());
        }
        else if (m.Msg == WM_PARENTNOTIFY && (m.WParam.ToInt32() == WM_LBUTTONDOWN || m.WParam.ToInt32() == WM_RBUTTONDOWN))
        {
            if (!base.ContainsFocus)
            {
                OnEnter(new EventArgs());
            }
        }
        else if (m.Msg == WM_DESTROY && !base.IsDisposed && !base.Disposing)
        {
            Dispose();
        }
        else if (m.Msg == WM_COPYDATA)
        {
            object lParam = m.GetLParam(typeof(COPYDATASTRUCT));
            COPYDATASTRUCT x = (lParam != null) ? ((COPYDATASTRUCT)lParam) : default;
            byte[] strb = new byte[checked(x.cbData)];
            Marshal.Copy(x.lpData, strb, 0, x.cbData);
            string sMsg = Encoding.Default.GetString(strb);
            m.Result = ParseSumatraMsg(sMsg, x.dwData);
        }
        else if (m.Msg == WM_KEYDOWN) // Control maps as MouseDown event
        {
            m.Result = LastKeyDownEventArgs.Handled ? (nint)0 : (nint)1;
        }
        else if (m.Msg == WM_CHAR) // Control maps as KeyPress event
        {
            m.Result = LastKeyPressEventArgs.Handled ? (nint)0 : (nint)1;
        }
        else if (m.Msg == WM_KEYUP) // Control maps as MouseDown event
        {
            m.Result = LastKeyUpEventArgs.Handled ? (nint)0 : (nint)1;
        }
        else if (m.Msg == WM_RBUTTONDBLCLK || m.Msg == WM_LBUTTONDBLCLK)
        {
            nint xy = m.LParam;
            int x = unchecked((short)(long)xy);
            int y = unchecked((short)((long)xy >> 16));

            OnMouseDoubleClick(new MouseEventArgs(m.Msg == WM_LBUTTONDBLCLK ? MouseButtons.Left : MouseButtons.Right, 2, x, y, 0));
        }
    }

    // Hide but not destroy BackgroundImage. Because this image do not need to be substituted.
    /// <summary>
    /// SumatraPDF logo
    /// </summary>
    [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
    public new System.Drawing.Image BackgroundImage
    {
        get => base.BackgroundImage;
        set { }
    }

    private KeyEventArgs LastKeyDownEventArgs;

    private void SumatraPDFControl_KeyDown(object sender, KeyEventArgs e)
    {
        this.KeyDown?.Invoke(this, e);
        LastKeyDownEventArgs = e;
    }

    private KeyEventArgs LastKeyUpEventArgs;

    private void SumatraPDFControl_KeyUp(object sender, KeyEventArgs e)
    {
        this.KeyUp?.Invoke(this, e);
        LastKeyUpEventArgs = e;
    }

    private KeyPressEventArgs LastKeyPressEventArgs;

    private void SumatraPDFControl_KeyPress(object sender, KeyPressEventArgs e)
    {
        this.KeyPress?.Invoke(this, e);
        // Do not allow SumatraPDF close current window when pressing 'q' key
        if (e.KeyChar == 'q' || e.KeyChar == 'Q') e.Handled = true;
        // Do not allow SumatraPDF reload current document when pressing 'r' key
        if (e.KeyChar == 'r' || e.KeyChar == 'R') e.Handled = true;
        // Do not allow SumatraPDF make an annotation when pressing 'a' key
        if (e.KeyChar == 'a' || e.KeyChar == 'A') e.Handled = true;

        LastKeyPressEventArgs = e;
    }

    private void SumatraPDFControl_Resize(object sender, EventArgs e)
    {
        if (SumatraWindowHandle != IntPtr.Zero)
        {
            MoveWindow(SumatraWindowHandle, 0, 0, base.Width, base.Height, 0);
        }
    }

    private void ContextMenuStrip_LostFocus(object sender, EventArgs e)
    {
        if (((ContextMenuStrip)sender).Visible) ((ContextMenuStrip)sender).Close();
    }

    private void CloseDocument()
    {
        SumatraPDFMsg(WM_DESTROY);
    }

    private int RaiseDefaultSumatraEvent(string msg, nint dwData)
    {
        var e = new SumatraMessageEventArgs { CallBackReturn = 0, Msg = msg, Data = dwData };
        SumatraMessage?.Invoke(this, e);
        return e.CallBackReturn;
    }

    private void RestartSumatra(string sFile, int page = 1)
    {
        if ((SumatraWindowHandle != IntPtr.Zero) & (SumatraProcess != null))
        {
            CloseDocument();
            SumatraProcess?.Kill();
            pSumatraWindowHandleList.Remove(pSumatraWindowHandle);
            pSumatraWindowHandle = IntPtr.Zero;
        }

        string sumatraComplete = ResolveSumatraPDFPath();
        string platform = (RuntimeInformation.OSArchitecture == Architecture.Arm64)
            ? "arm64"
            : (Environment.Is64BitProcess ? "x64" : "x86");
        string commomErrMsg = "Download or reference the latest package version from NuGet.org site in https://www.nuget.org/packages/SumatraPDF.PluginMode." + platform.Substring(1);

        // Verify if SumatraPDF.exe is available
        if (!File.Exists(sumatraComplete))
        {
            throw new Exception(@"SumatraPDF executable not found. " + commomErrMsg);
        }

        var PSInfo = new ProcessStartInfo
        {
            FileName = sumatraComplete,
            CreateNoWindow = true,
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Minimized,
            Arguments =
                "-plugin " + base.Handle.ToString() +
                //" -invert-colors" +
                //"-set-color-range #333333 #ffffff" +
                //" -appdata \"c:\\users\\marco\\Downloads\"" +
                " -page " + page.ToString() +
                " \"" + sFile + "\""
        };
        SumatraProcess = Process.Start(PSInfo);

        ChildProcessTracer.Default.AddChildProcess(SumatraProcess.Handle);
    }

    public string ResolveSumatraPDFPath()
    {
        string sumatraComplete;
        string assemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        string platform = (RuntimeInformation.OSArchitecture == Architecture.Arm64)
            ? "arm64"
            : (Environment.Is64BitProcess ? "x64" : "x86");

        if (SumatraPDFPath == null || SumatraPDFPath == string.Empty)
        {
            SumatraPDFPath = Path.Combine(assemblyPath, platform);

            sumatraComplete = Path.Combine(SumatraPDFPath, SumatraPDFExe);
            if (!File.Exists(sumatraComplete))
            {
                if (platform == "arm64")
                {
                    sumatraComplete = Path.Combine(assemblyPath, "x64", SumatraPDFExe);
                }
                else if (platform == "x64")
                {
                    sumatraComplete = Path.Combine(assemblyPath, "x86", SumatraPDFExe);
                }
            }
        }
        else
        {
            sumatraComplete = Path.Combine(SumatraPDFPath, SumatraPDFExe);
        }

        return sumatraComplete;
    }

    private void OpenFile()
    {
        SumatraPDFCopyDataMsg("OpenFile", sCurrentFile, Handle.ToString());
    }

    /// <summary>
    /// Structure which represents Horizontal and Vertical Scroll position of current SumatraPDF displayed page.
    /// </summary>
    public struct ScrollStateStruct
    {
        /// <summary>
        /// ScrollStateStruct Constructor
        /// </summary>
        /// <param name="page">Current page</param>
        /// <param name="x">Horizontal scroll position</param>
        /// <param name="y">Vertical scroll position</param>
        public ScrollStateStruct(int page, double x, double y)
        {
            Page = page;
            X = x;
            Y = y;
        }

        /// <summary>
        /// Horizontal scroll position
        /// </summary>
        public double X { get; }

        /// <summary>
        /// Vertical scroll position
        /// </summary>
        public double Y { get; }

        /// <summary>
        /// Current displayed page
        /// </summary>
        public int Page { get; }

        /// <summary>
        /// Converts struct to string
        /// </summary>
        /// <returns>Struct string representation</returns>
        public override string ToString() => $"Page: {Page}, X: {X}, Y: {Y}";
    }

    /// <summary>
    /// Document display mode constants
    /// </summary>
    public enum DisplayModeEnum
    {
        /// <summary>
        /// The continuous form of single page, facing or book view depending on the document's desired page layout
        /// </summary>
        Automatic = 0,

        /// <summary>
        /// Single page display (not continuous)
        /// </summary>
        SinglePage,

        /// <summary>
        /// Dual page display (not continuous)
        /// </summary>
        Facing,

        /// <summary>
        /// Dual page display (not continuous)
        /// </summary>
        BookView,

        /// <summary>
        /// Single page display in continuous mode
        /// </summary>
        Continuous,

        /// <summary>
        /// Dual page display in continuous mode
        /// </summary>
        ContinuousFacing,

        /// <summary>
        /// Dual page display in continuous mode
        /// </summary>
        ContinuousBookView
    };

    /// <summary>
    /// Document zoom virtual enum display constants (fit page, fit width, etc) represented by real zoom factor
    /// </summary>
    public enum ZoomVirtualEnum
    {
        /// <summary>
        /// Constant expressing that no other constants represents the real zoom factor
        /// </summary>
        None = 0,

        /// <summary>
        /// Constant to express a real zoom factor representing a complete page display
        /// </summary>
        FitPage = -1,

        /// <summary>
        /// Constant to express a real zoom factor representing a complete horizontal page display
        /// </summary>
        FitWidth = -2,

        /// <summary>
        /// Constant to express a real zoom factor representing a complete content page display
        /// </summary>
        FitContent = -3
    }

    /// <summary>
    /// Document rotation enum constants
    /// </summary>
    /// <seealso cref="RotateBy(RotationEnum)"/>
    /// <seealso cref="Rotation"/>
    public enum RotationEnum
    {
        /// <summary>
        /// No rotation
        /// </summary>
        RotNone = 0,

        /// <summary>
        /// 90 degrees clockwise rotation
        /// </summary>
        Rot90 = 90,

        /// <summary>
        /// 180 degrees rotation
        /// </summary>
        Rot180 = 180,

        /// <summary>
        /// 270 degrees clockwise rotation
        /// </summary>
        Rot270 = 270
    }

    /// <summary>
    /// Argument class received by <see cref="PageChanged"/> event
    /// </summary>
    public class PageChangedEventArgs : EventArgs
    {
        /// <summary>
        /// PageChangedEventArgs constructor
        /// </summary>
        public PageChangedEventArgs(int Page, string NamedDest)
        {
            this.Page = Page;
            this.NamedDest = NamedDest;
        }

        /// <summary>
        /// Document page
        /// </summary>
        public int Page { get; }

        /// <summary>
        /// Document NamedDest
        /// </summary>
        public string NamedDest { get; }
    }

    /// <summary>
    /// Argument class receiced by <see cref="ContextMenuOpening"/> event
    /// </summary>
    public class ContextMenuOpeningEventArgs : EventArgs
    {
        /// <summary>
        /// ContextMenuOpeningEventArgs constructor
        /// </summary>
        public ContextMenuOpeningEventArgs(int X, int Y)
        {
            this.X = X;
            this.Y = Y;
            Handled = false;
        }

        /// <summary>
        /// X horizontal position, relative to SumatraPDF control, where context menu is about to open
        /// </summary>
        public int X { get; }

        /// <summary>
        /// Y vertical position, relative to SumatraPDF control, where context menu is about to open
        /// </summary>
        public int Y { get; }

        /// <summary>
        /// Indicates wether event will be handled or not (default false)
        /// </summary>
        /// <remarks>
        /// if set to true default context menu will not be displayed
        /// </remarks>
        public bool Handled { get; set; }
    }

    /// <summary>
    /// Argument class received by <see cref="SumatraMessage"/> generic event
    /// </summary>
    public class SumatraMessageEventArgs : EventArgs
    {
        /// <summary>
        /// Indicates wether event will be handled or not (default false)
        /// </summary>
        /// <remarks>
        /// if set to true event will not be handled by SumatraPDF
        /// </remarks>
        public int CallBackReturn { get; set; }

        /// <summary>
        /// Message generated by SumatraPDF
        /// </summary>
        public string Msg { get; set; }

        /// <summary>
        /// Pointer to extended data message generated by SumatraPDF
        /// </summary>
        public nint Data { get; set; }
    }

    /// <summary>
    /// Argument class received by <see cref="ZoomChanged"/> event
    /// </summary>
    public class ZoomChangedEventArgs : EventArgs
    {
        /// <summary>
        /// ZoomChangedEventArgs constructor
        /// </summary>
        public ZoomChangedEventArgs(float Zoom, ZoomVirtualEnum ZoomVirtual, bool MouseWheel)
        {
            this.Zoom = Zoom;
            this.ZoomVirtual = ZoomVirtual;
            this.MouseWheel = MouseWheel;
        }

        /// <summary>
        /// Document zoom real factor
        /// </summary>
        public float Zoom { get; }

        /// <summary>
        /// Document zoom virtual display mode
        /// </summary>
        public ZoomVirtualEnum ZoomVirtual { get; }

        /// <summary>
        /// Indicates wether mouse wheel was responsible for zoom changed event
        /// </summary>
        public bool MouseWheel { get; }
    }

    /// <summary>
    /// Argument class received by <see cref="LinkClick"/> event
    /// </summary>
    public class LinkClickedEventArgs : EventArgs
    {
        /// <summary>
        /// LinkClickedEventArgs constructor
        /// </summary>
        public LinkClickedEventArgs(string LinkText)
        {
            this.LinkText = LinkText;
        }

        /// <summary>
        /// Document link text clicked
        /// </summary>
        public string LinkText { get; }
    }

    /// <summary>
    /// Argument class received by <see cref="DisplayModeChanged"/> event
    /// </summary>
    public class DisplayModeChangedEventArgs : EventArgs
    {
        /// <summary>
        /// DisplayModeChangedEventArgs constructor
        /// </summary>
        public DisplayModeChangedEventArgs(DisplayModeEnum DisplayMode)
        {
            this.DisplayMode = DisplayMode;
        }

        /// <summary>
        /// Document display mode
        /// </summary>
        public DisplayModeEnum DisplayMode { get; }
    }

    /// <summary>
    /// Argument class received by <see cref="ScrollStateChanged"/> event
    /// </summary>
    public class ScrollStateEventArgs : EventArgs
    {
        /// <summary>
        /// ScrollStateEventArgs constructor
        /// </summary>
        public ScrollStateEventArgs(ScrollStateStruct ScrollState)
        {
            this.ScrollState = ScrollState;
        }

        /// <summary>
        /// Document scroll state
        /// </summary>
        public ScrollStateStruct ScrollState { get; }
    }

    /// <summary>
    /// A generic SumatraPDF message not managed by other events.
    /// </summary>
    [Description("A generic SumatraPDF message not managed by other events"), Category("SumatraPDF")]
    public event EventHandler<SumatraMessageEventArgs> SumatraMessage;

    /// <summary>
    /// Occurs after SumatraPDF.exe terminates startup process.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This event occurs only after a new SumatraPDF.exe execution. So, simply loading a new document reutilizing SumatraPDF.exe instance
    /// do not fire this event, but only <see cref="FileOpened"/> event.
    /// </para>
    /// <para>
    /// Because SumatraPDF load documents asyncronously, some methods and properties requires end of startup process in order to function:
    /// eg.: <see cref="PageCount"/> or <see cref="Page"/>. So, use these methods or properties only after <see cref="StartupFinished"/> event or either <see cref="FileOpened"/>.
    /// </para>
    /// <para>
    /// It is only recommended to carry out the initialization procedures when handling the <see cref="FileOpened"/> event because the <see cref="StartupFinished"/>
    /// event is only fired when a new instance of SumatraPDF.exe is executed.
    /// </para>
    /// </remarks>
    /// <seealso cref="FileOpened"/>
    [Description("Occurs after SumatraPDF.exe terminates startup process"), Category("SumatraPDF")]
    public event EventHandler<EventArgs> StartupFinished;

    /// <summary>
    /// Occurs after a new document load by SumatraPDFControl
    /// </summary>
    /// <remarks>
    /// It's recomended to initilize application variables on this event because some properties and methods does not respond correctly during SumatraPDF.exe startup process. This
    /// event garantees that startup process was ended. See <see cref="StartupFinished"/> event for more details.
    /// </remarks>
    /// <seealso cref="StartupFinished"/>
    [Description("Occurs after a new document load by SumatraPDFControl"), Category("SumatraPDF")]
    public event EventHandler<EventArgs> FileOpened;

    /// <summary>
    /// Occurs after changing current visible page
    /// </summary>
    [Description("Occurs after changing current visible page"), Category("SumatraPDF")]
    public event EventHandler<PageChangedEventArgs> PageChanged;

    /// <summary>
    /// Occurs before trying to open ContextMenu by right mouse clicking button.
    /// </summary>
    [Description("Occurs before trying to open ContextMenu by right mouse clicking button"), Category("SumatraPDF")]
    public event EventHandler<ContextMenuOpeningEventArgs> ContextMenuOpening;

    /// <summary>
    /// Occurs after changing zoom factor.
    /// </summary>
    [Description("Occurs after changing zoom factor"), Category("SumatraPDF")]
    public event EventHandler<ZoomChangedEventArgs> ZoomChanged;

    /// <summary>
    /// Occurs after clicking a document link.
    /// </summary>
    [Description("Occurs after clicking a document link"), Category("SumatraPDF")]
    public event EventHandler<LinkClickedEventArgs> LinkClick;

    /// <summary>
    /// Occurs after changing display mode.
    /// </summary>
    [Description("Occurs after changing display mode"), Category("SumatraPDF")]
    public event EventHandler<DisplayModeChangedEventArgs> DisplayModeChanged;

    /// <summary>
    /// Occurs after changing document scroll position (vertical and/or horizontal).
    /// </summary>
    [Description("Occurs after changing document scroll position (vertical and/or horizontal)"), Category("SumatraPDF")]
    public event EventHandler<ScrollStateEventArgs> ScrollStateChanged;

#pragma warning disable CS0108 // Member hides inherited member; missing new keyword

    /// <summary>
    /// Occurs when a character, space or backspace key is pressed while the control has focus.
    /// </summary>
    [Description("Occurs when a character, space or backspace key is pressed while the control has focus"), Category("SumatraPDF")]
    public event EventHandler<KeyPressEventArgs> KeyPress;

    /// <summary>
    /// Occurs when a key is pressed while the control has focus.
    /// </summary>
    [Description("Occurs when a key is pressed while the control has focus"), Category("SumatraPDF")]
    public event KeyEventHandler KeyDown;

    /// <summary>
    /// Occurs when a key is released while the control has focus.
    /// </summary>
    [Description("Occurs when a key is released while the control has focus"), Category("SumatraPDF")]
    public event KeyEventHandler KeyUp;

#pragma warning restore CS0108 // Member hides inherited member; missing new keyword

    /// <summary>
    /// Gets or sets the ContextMenuStrip associated with this control.
    /// </summary>
    /// <remarks>
    /// The ContextMenuStrip for this control, or null if there is no ContextMenuStrip. The default is null.
    /// </remarks>
    [Description("Generic SumatraPDF message, not managed by other events ocurred")]
    public new ContextMenuStrip ContextMenuStrip { get; set; }

    /// <summary>
    /// Path where SumatraPDF executable is present. If not informed assumes same SumatraPDFControl.dll directory
    /// </summary>
    [Description("Path where SumatraPDF executable is present. If not informed assumes same SumatraPDFControl.dll directory"),
        Category("SumatraPDF")]
    public string SumatraPDFPath { get; set; }

    private string pSumatraPDFEXE;

    /// <summary>
    /// SumatraPDF executable file name. Usually SumatraPDF.exe (default) or SumatraPDF-dll.exe
    /// </summary>
    [Description("SumatraPDF executable file name. Usually SumatraPDF.exe (default) or SumatraPDF-dll.exe"),
        Category("SumatraPDF")]
    public string SumatraPDFExe
    {
        get => pSumatraPDFEXE;
        set => pSumatraPDFEXE = value;
    }

    private ScrollStateStruct pScrollState;

    /// <summary>
    /// Get or set current PDF scroll state (X and Y units from visible PDF page)
    /// </summary>
    [Browsable(false)]
    public ScrollStateStruct ScrollState
    {
        get
        {
            SumatraPDFCopyDataMsg("GetProperty", "ScrollState");
            return pScrollState;
        }
        set => SumatraPDFCopyDataMsg("SetProperty", "ScrollState", value.ToString());
    }

    private void GetZoom()
    {
        SumatraPDFCopyDataMsg("GetProperty", "Zoom");
    }

    private void SetZoom(float value)
    {
        SumatraPDFCopyDataMsg("SetProperty", "Zoom", value.ToString(new System.Globalization.CultureInfo("en-US")));
    }

    private float fZoom;

    /// <summary>
    /// Get or set current numeric PDF zoom scale.
    /// </summary>
    /// <remarks>
    /// <see cref="ZoomVirtual"/> property to get or set zoom mode like fit width, fit page etc.
    /// </remarks>
    [Browsable(false)]
    public float Zoom
    {
        get
        {
            GetZoom();
            return fZoom;
        }
        set => SetZoom(value);
    }

    private ZoomVirtualEnum fZoomVirtual;

    /// <summary>
    /// Get or set currrent PDF virtual zoom mode (Fit width, Fit Page, Fit Content or None).
    /// </summary>
    /// <remarks>
    /// <see cref="Zoom"/> property to get or set numeric scale
    /// </remarks>
    [Browsable(false)]
    public ZoomVirtualEnum ZoomVirtual
    {
        get
        {
            GetZoom();
            return fZoomVirtual;
        }
        set
        {
            float fZoomVirtual = (int)value;
            if (fZoomVirtual < 0) SetZoom(fZoomVirtual);
        }
    }

    private void GetDisplayMode()
    {
        SumatraPDFCopyDataMsg("GetProperty", "DisplayMode");
    }

    private DisplayModeEnum eDisplayMode;

    /// <summary>
    /// Get or set mode to display PDF pages (SinglePage, Facing, BookView, Continuous, ContinuousFacing, ContinuousBookView, Automatic)
    /// </summary>
    [Browsable(false)]
    public DisplayModeEnum DisplayMode
    {
        get
        {
            GetDisplayMode();
            return eDisplayMode;
        }
        set => SumatraPDFCopyDataMsg("SetProperty", "DisplayMode", ((int)value).ToString());
    }

    private void GetPage()
    {
        SumatraPDFCopyDataMsg("GetProperty", "Page");
    }

    private void SetPage(int Page)
    {
        SumatraPDFCopyDataMsg("SetProperty", "Page", Page.ToString());
    }

    private int nPage;

    /// <summary>
    /// Get or set current PDF page viewing position
    /// </summary>
    [Browsable(false)]
    public int Page
    {
        get
        {
            GetPage();
            return nPage;
        }
        set => SetPage(value);
    }

    private int nPageCount;

    /// <summary>
    /// Total pages count of loaded document
    /// </summary>
    public int PageCount
    {
        get
        {
            SumatraPDFCopyDataMsg("GetProperty", "PageCount");
            return nPageCount;
        }
    }

    private RotationEnum eRotation;

    /// <summary>
    /// Get current PDF rotation (see RotateBy method to change rotation state)
    /// </summary>
    [Browsable(false)]
    public RotationEnum Rotation
    {
        get
        {
            SumatraPDFCopyDataMsg("GetProperty", "Rotation");
            return eRotation;
        }
    }

    private string sNamedDest;

    /// <summary>
    /// Get or set current PDF NamedDest viewing position
    /// </summary>
    [Browsable(false)]
    public string NamedDest
    {
        get
        {
            GetPage();
            return sNamedDest;
        }
        set => SumatraPDFCopyDataMsg("SetProperty", "NamedDest", value);
    }

    private bool bToolbarVisible;

    /// <summary>
    /// Get or set if SumatraPDF default toolbar is visible
    /// </summary>
    [Browsable(false)]
    public bool ToolBarVisible
    {
        get
        {
            SumatraPDFCopyDataMsg("GetProperty", "ToolbarVisible");
            return bToolbarVisible;
        }
        set => SumatraPDFCopyDataMsg("SetProperty", "ToolbarVisible", value ? "1" : "0");
    }

    private bool bTocVisible;

    /// <summary>
    /// Get or set if SumatraPDF Table of contents (Toc) sidebar is visible (if document doesn't have Toc it always will be false)
    /// </summary>
    [Browsable(false)]
    public bool TocVisible
    {
        get
        {
            SumatraPDFCopyDataMsg("GetProperty", "TocVisible");
            return bTocVisible;
        }
        set => SumatraPDFCopyDataMsg("SetProperty", "TocVisible", value ? "1" : "0");
    }

    private bool bKeyAccelerators;

    /// <summary>
    /// Get or set if Keyboard accelerators (like CTRL+A, CTRL+P, etc.) are enable or not;
    /// </summary>
    /// <remarks>
    /// If keyboard accelerators are enable pressing keys will perform the following actions:
    /// <list type="table">
    /// <listheader>
    ///     <term>Key</term>
    ///     <description>Action</description>
    /// </listheader>
    /// <item><term>CTRL+A</term><description>Select All</description></item>
    /// <item><term>CTRL+C</term><description>Copy Selection</description></item>
    /// <item><term>CTRL+F</term><description>Find</description></item>
    /// <item><term>CTRL+G</term><description>Goto Page</description></item>
    /// <item><term>CTRL+P</term><description>Print Document</description></item>
    /// <item><term>CTRL+Y</term><description>Zoom: Custom</description></item>
    /// <item><term>CTRL+0</term><description>Zoom: Fit Page</description></item>
    /// <item><term>CTRL+1</term><description>Zoom: Actual Size</description></item>
    /// <item><term>CTRL+2</term><description>Zoom: Fit Width</description></item>
    /// <item><term>CTRL+3</term><description>Zoom: Fit Content</description></item>
    /// <item><term>CTRL+6</term><description>View: Single Page</description></item>
    /// <item><term>CTRL+7</term><description>View: Facing</description></item>
    /// <item><term>CTRL+8</term><description>View: Book</description></item>
    /// <item><term>CTRL+PlusKey</term><description>Zoom In</description></item>
    /// <item><term>CTRL+SHIFT+PlusKey</term><description>View: Rotate Right</description></item>
    /// <item><term>CTRL+InsertKey</term><description>Copy Selection</description></item>
    /// <item><term>F3</term><description>Find: Next</description></item>
    /// <item><term>SHIFT+F3</term><description>Find: Previous</description></item>
    /// <item><term>CTRL+F3</term><description>Find: Next Selection</description></item>
    /// <item><term>CTRL+SHIFT+F3</term><description>Find: Previous Selection</description></item>
    /// <item><term>CTRL+MinusKey</term><description>Zoom Out</description></item>
    /// <item><term>CTRL+SHIFT+MinusKey</term><description>View: Rotate Left</description></item>
    /// <item><term>ALT+LeftArrow</term><description>Navigate: Back</description></item>
    /// <item><term>ALT+RightArrow</term><description>Navigate: Forward</description></item>
    /// </list>
    /// Disabling these accelerators allow <see cref="KeyDown"/> raise event of these keys.
    /// </remarks>
    [Browsable(false)]
    public bool KeyAccelerators
    {
        get
        {
            SumatraPDFCopyDataMsg("GetProperty", "EnableAccelerators");
            return bKeyAccelerators;
        }
        set => SumatraPDFCopyDataMsg("SetProperty", "EnableAccelerators", value ? "1" : "0");
    }

    /// <summary>
    /// Default constructor
    /// </summary>
    public SumatraPDFControl()
    {
        SumatraPDFExe = "SumatraPDF.exe";
        pSumatraWindowHandle = IntPtr.Zero;
        InitializeComponent();
        SUMATRAMESSAGETYPE = SUMATRAPLUGIN;
        base.KeyDown += new KeyEventHandler(SumatraPDFControl_KeyDown);
        base.KeyUp += new KeyEventHandler(SumatraPDFControl_KeyUp);
        base.KeyPress += new KeyPressEventHandler(SumatraPDFControl_KeyPress);
    }

    /// <summary>
    /// Reload current loaded document in order to reflect external changes made in it
    /// </summary>
    public void ReloadCurrentFile()
    {
        SumatraPDFFrameCmd(SumatraPDFCommandEmum.CmdRefresh);
    }

    /// <summary>
    /// Load sumatra supported file. Current file will be closed.
    /// </summary>
    /// <param name="PDFFile">File name with complete path</param>
    /// <param name="InitialPage">Initial page to show</param>
    /// <param name="NewSumatraInstance">Open a new SumatraPDF instance (other executable process)</param>
    /// <remarks>
    /// In case of multiple instances of SumatraPDFControl <paramref name="NewSumatraInstance"/> indicates if an pre-existing instance
    /// will be used or not. If true new SumatraPDF executable will be started. The default is false.
    /// </remarks>
    public void LoadFile(string PDFFile, int InitialPage = 1, bool NewSumatraInstance = false)
    {
        sCurrentFile = PDFFile;
        this.Page = InitialPage;
        NamedDest = string.Empty;
        if (SumatraWindowHandle != IntPtr.Zero)
        {
            OpenFile();
            pSumatraWindowHandleList.Remove(SumatraWindowHandle);
            pSumatraWindowHandle = IntPtr.Zero;
        }
        else
        {
            if (NewSumatraInstance || pSumatraWindowHandleList.Count == 0) RestartSumatra(PDFFile, InitialPage);
            else OpenFile();
        }
        SetPage(InitialPage);
    }

    /// <summary>
    /// Copy to clipboard window text selection
    /// </summary>
    public void CopySelection()
    {
        SumatraPDFFrameCmd(SumatraPDFCommandEmum.CmdCopySelection);
    }

    /// <summary>
    /// Select all document text
    /// </summary>
    public void SelectAll()
    {
        SumatraPDFFrameCmd(SumatraPDFCommandEmum.CmdSelectAll);
    }

    /// <summary>
    /// Do a text search on document from beginning
    /// </summary>
    /// <seealso cref="TextSearchNext(bool)"/>
    /// <param name="searchText">Text to search</param>
    /// <param name="matchCase">Match case</param>
    public void TextSearch(string searchText, bool matchCase)
    {
        SumatraPDFCopyDataMsg("TextSearch", searchText, (matchCase ? 1 : 0));
    }

    /// <summary>
    /// Do a text search on document after a first search by <see cref="TextSearch(string, bool)"/>
    /// </summary>
    /// <seealso cref="TextSearch(string, bool)"/>
    /// <param name="forward">True (default) if serching forward, false if backwards</param>
    public void TextSearchNext(bool forward = true)
    {
        SumatraPDFCopyDataMsg("TextSearchNext", (forward ? 1 : 0));
    }

    /// <summary>
    /// Show dialog to print current document (in background)
    /// </summary>
    /// <remarks>
    /// After proceeding with print it occurs in background and SumatraPDF shows an interface to cancel the whole process.
    /// The following only applies for printing as image:
    /// Creates a new dummy page for each page with a large zoom factor,
    /// and then uses StretchDIBits to copy this to the printer's dc.
    /// </remarks>
    public void OpenPrintDialog()
    {
        SumatraPDFFrameCmd(SumatraPDFCommandEmum.CmdPrint, true);
    }

    /// <summary>
    /// Rotate current document
    /// </summary>
    /// <param name="Rotation">Degrees to Rotation</param>
    public void RotateBy(RotationEnum Rotation)
    {
        SumatraPDFCopyDataMsg("SetProperty", "RotateBy", ((int)Rotation).ToString());
    }

    /// <summary>
    /// Go to next document page
    /// </summary>
    public void GoToNextPage()
    {
        SumatraPDFFrameCmd(SumatraPDFCommandEmum.CmdGoToNextPage);
    }

    /// <summary>
    /// Go to previous document page
    /// </summary>
    public void GoToPrevPage()
    {
        SumatraPDFFrameCmd(SumatraPDFCommandEmum.CmdGoToPrevPage);
    }

    /// <summary>
    /// Go to first document page
    /// </summary>
    public void GoToFirstPage()
    {
        SumatraPDFFrameCmd(SumatraPDFCommandEmum.CmdGoToFirstPage);
    }

    /// <summary>
    /// Go to last document page
    /// </summary>
    public void GoToLastPage()
    {
        SumatraPDFFrameCmd(SumatraPDFCommandEmum.CmdGoToLastPage);
    }
}
