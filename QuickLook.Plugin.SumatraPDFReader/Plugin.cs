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

using QuickLook.Common.Plugin;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

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
