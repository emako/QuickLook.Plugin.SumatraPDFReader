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

using QuickLook.Common.Helpers;
using QuickLook.Common.Plugin;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Resources;

namespace QuickLook.Plugin.SumatraPDFReader;

public partial class SumatraPanel : UserControl
{
    private SumatraPDFControl _sumatraPDFControl;

    public SumatraPanel()
    {
        InitializeComponent();
        presenter.Background = System.Windows.Media.Brushes.White;
        Loaded += SumatraPanel_Loaded;
        Unloaded += SumatraPanel_Unloaded;
    }

    private void SumatraPanel_Loaded(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is Window win)
        {
            WindowHelper.DisableBlur(win);
        }
    }

    private void SumatraPanel_Unloaded(object sender, RoutedEventArgs e)
    {
        _sumatraPDFControl?.Dispose();
        _sumatraPDFControl = null;
    }

    public void PreviewFile(string path, ContextObject context)
    {
        _sumatraPDFControl?.Dispose();
        _sumatraPDFControl = new SumatraPDFControl();

        // Resolve settings file
        string sumatraPDFPath = Path.GetDirectoryName(_sumatraPDFControl.ResolveSumatraPDFPath());
        string settingsPath = Path.Combine(sumatraPDFPath, "SumatraPDF-settings.txt");

        string theme = OSThemeHelper.AppsUseDarkTheme() ? "Darker" : "Light";
        StreamResourceInfo info = Application.GetResourceStream(new Uri($"pack://application:,,,/QuickLook.Plugin.SumatraPDFReader;component/Resources/SumatraPDF-settings-{theme}.txt"));
        using Stream stream = info?.Stream;
        using StreamReader streamReader = new(stream, Encoding.UTF8);
        string s = streamReader.ReadToEnd();

        File.WriteAllText(settingsPath, s);

        // Load file from SumatraPDF.exe
        _sumatraPDFControl.LoadFile(path);
        presenter.Child = _sumatraPDFControl;
    }
}

file static class WindowHelper
{
    public static void EnableBlur(Window window)
    {
        var accent = new AccentPolicy();
        var accentStructSize = Marshal.SizeOf(accent);
        accent.AccentState = AccentState.AccentEnableBlurbehind;
        accent.AccentFlags = 2;
        accent.GradientColor = 0x99FFFFFF;

        var accentPtr = Marshal.AllocHGlobal(accentStructSize);
        Marshal.StructureToPtr(accent, accentPtr, false);

        var data = new WindowCompositionAttributeData
        {
            Attribute = WindowCompositionAttribute.WcaAccentPolicy,
            SizeOfData = accentStructSize,
            Data = accentPtr
        };

        User32.SetWindowCompositionAttribute(new WindowInteropHelper(window).Handle, ref data);

        Marshal.FreeHGlobal(accentPtr);
    }

    public static void DisableBlur(Window window)
    {
        var accent = new AccentPolicy();
        var accentStructSize = Marshal.SizeOf(accent);
        accent.AccentState = AccentState.AccentDisabled;

        var accentPtr = Marshal.AllocHGlobal(accentStructSize);
        Marshal.StructureToPtr(accent, accentPtr, false);

        var data = new WindowCompositionAttributeData
        {
            Attribute = WindowCompositionAttribute.WcaAccentPolicy,
            SizeOfData = accentStructSize,
            Data = accentPtr
        };

        var hwnd = new WindowInteropHelper(window).EnsureHandle();
        User32.SetWindowCompositionAttribute(hwnd, ref data);

        Marshal.FreeHGlobal(accentPtr);

        var margins = new Dwmapi.Margins(0, 0, 0, 0);
        Dwmapi.DwmExtendFrameIntoClientArea(hwnd, ref margins);

        if (Environment.OSVersion.Version >= new Version(10, 0, 22523)) // Windows 11 build 22523 or later
        {
            var backdropType = (int)Dwmapi.SystembackdropType.None;
            Dwmapi.DwmSetWindowAttribute(hwnd, (uint)Dwmapi.WindowAttribute.SystembackdropType, ref backdropType, Marshal.SizeOf(typeof(int)));
        }
        else if (Environment.OSVersion.Version >= new Version(10, 0, 21996)) // Windows 11 build 21996 or later
        {
            var micaEnabled = 0;
            Dwmapi.DwmSetWindowAttribute(hwnd, (uint)Dwmapi.WindowAttribute.MicaEffect, ref micaEnabled, Marshal.SizeOf(typeof(int)));
        }

        if (Environment.OSVersion.Version >= new Version(10, 0, 21996)) // Windows 11 build 21996 or later
        {
            var cornerPreference = (int)Dwmapi.WindowCornerPreference.Round;
            Dwmapi.DwmSetWindowAttribute(hwnd, (uint)Dwmapi.WindowAttribute.WindowCornerPreference, ref cornerPreference, Marshal.SizeOf(typeof(int)));
        }
    }

    public enum WindowCompositionAttribute
    {
        WcaAccentPolicy = 19,
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct WindowCompositionAttributeData
    {
        public WindowCompositionAttribute Attribute;
        public nint Data;
        public int SizeOfData;
    }

    private enum AccentState
    {
        AccentDisabled = 0,
        AccentEnableGradient = 1,
        AccentEnableTransparentgradient = 2,
        AccentEnableBlurbehind = 3,
        AccentInvalidState = 4,
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AccentPolicy
    {
        public AccentState AccentState;
        public int AccentFlags;
        public uint GradientColor;
        public readonly int AnimationId;
    }
}

file static class User32
{
    [DllImport("user32.dll")]
    public static extern int SetWindowCompositionAttribute(nint hwnd,
        ref WindowHelper.WindowCompositionAttributeData data);
}

file static class Dwmapi
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Margins(int cxLeftWidth, int cxRightWidth, int cyTopHeight, int cyBottomHeight)
    {
        public int cxLeftWidth = cxLeftWidth;
        public int cxRightWidth = cxRightWidth;
        public int cyTopHeight = cyTopHeight;
        public int cyBottomHeight = cyBottomHeight;
    }

    public enum WindowAttribute
    {
        UseImmersiveDarkMode = 20,
        WindowCornerPreference = 33,
        SystembackdropType = 38,
        MicaEffect = 1029,
    }

    public enum WindowCornerPreference
    {
        Default = 0,
        DoNotRound = 1,
        Round = 2,
        RoundSmall = 3,
    }

    public enum SystembackdropType
    {
        Auto = 0,
        None = 1,
        MainWindow = 2,
        TransientWindow = 3,
        TabbedWindow = 4,
    }

    [DllImport("DwmApi.dll")]
    public static extern int DwmExtendFrameIntoClientArea(nint hwnd, ref Margins pMarInset);

    [DllImport("dwmapi.dll")]
    public static extern int DwmSetWindowAttribute(nint hwnd, uint dwAttribute, ref int pvAttribute, int cbAttribute);
}
