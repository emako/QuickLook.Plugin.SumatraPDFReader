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
using QuickLook.Common.NativeMethods;
using QuickLook.Common.Plugin;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace QuickLook.Plugin.SumatraPDFReader;

public class Plugin : IViewer
{
    public int Priority => 0;

    public void Init()
    {
    }

    public bool CanHandle(string path)
    {
        if (Directory.Exists(path))
            return false;

        return path.EndsWith(".epub", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".mobi", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".cbz", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".cbr", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".fb2", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".chm", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".xps", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".djvu", StringComparison.OrdinalIgnoreCase);
    }

    public void Prepare(string path, ContextObject context)
    {
        context.SetPreferredSizeFit(new Size(1200, 900), 0.9d);
    }

    public void View(string path, ContextObject context)
    {
        try
        {
            var viewer = new SumatraPanel();
            viewer.PreviewFile(path, context);
            context.ViewerContent = viewer;
            viewer.Loaded += (_, _) =>
            {
                // Fix for TextColor issue #2 #3
                // However, it's uncertain whether this fully resolves all related problems
                if (Window.GetWindow(viewer) is Window win)
                {
                    win.DisableDwmBlur();
                }
            };
        }
        catch (Exception e)
        {
            context.ViewerContent = new Label()
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Content = e.Message,
            };
        }

        context.IsBusy = true;
        context.Title = Path.GetFileName(path);

        // Hide the resize effect of external process windows
        _ = Task.Run(async () =>
        {
            await Task.Delay(800);
            context.IsBusy = false;
        });
    }

    public void Cleanup()
    {
    }
}

file static class WindowExtension
{
    public static void DisableDwmBlur(this Window window)
    {
        window.Background = (Brush)window.FindResource("MainWindowBackgroundNoTransparent");

        if (Environment.OSVersion.Version >= new Version(10, 0, 21996))
        {
            if (Environment.OSVersion.Version >= new Version(10, 0, 22523))
            {
                var hwnd = new WindowInteropHelper(window).Handle;

                if (!OSThemeHelper.AppsUseDarkTheme())
                {
                    int isDarkThemeInt = 1;
                    Dwmapi.DwmSetWindowAttribute(hwnd, (uint)Dwmapi.WindowAttribute.UseImmersiveDarkMode, ref isDarkThemeInt, Marshal.SizeOf(typeof(bool)));
                }

                int backdropType = (int)Dwmapi.SystembackdropType.Auto;
                Dwmapi.DwmSetWindowAttribute(hwnd, (uint)Dwmapi.WindowAttribute.SystembackdropType, ref backdropType, Marshal.SizeOf<int>());
            }
            else
            {
                var hwnd = new WindowInteropHelper(window).Handle;

                if (!OSThemeHelper.AppsUseDarkTheme())
                {
                    int isDarkThemeInt = 1;
                    Dwmapi.DwmSetWindowAttribute(hwnd, (uint)Dwmapi.WindowAttribute.UseImmersiveDarkMode, ref isDarkThemeInt, Marshal.SizeOf(typeof(bool)));
                }

                int backdropType = 0;
                Dwmapi.DwmSetWindowAttribute(hwnd, (uint)Dwmapi.WindowAttribute.MicaEffect, ref backdropType, Marshal.SizeOf<int>());
            }
        }
        else if (Environment.OSVersion.Version >= new Version(10, 0))
        {
            // Potential issues on Windows 10 are not maintained
        }
        else
        {
            // Other Windows Version
        }
    }
}
