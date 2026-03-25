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
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Resources;

namespace QuickLook.Plugin.SumatraPDFReader;

public partial class SumatraPanel : UserControl
{
    private SumatraPDFControl _sumatraPDFControl;

    public SumatraPanel()
    {
        InitializeComponent();
        presenter.Background = System.Windows.Media.Brushes.White;
        Unloaded += SumatraPanel_Unloaded;
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
