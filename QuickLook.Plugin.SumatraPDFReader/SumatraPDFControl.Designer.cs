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

namespace QuickLook.Plugin.SumatraPDFReader;

partial class SumatraPDFControl
{
    /// <summary>
    /// Variável de designer necessária.
    /// </summary>
    private System.ComponentModel.IContainer components = null;

    /// <summary>
    /// Cleaning resources being used
    /// </summary>
    /// <param name="disposing">true if necessary to dispose managed resources.</param>
    protected override void Dispose(bool disposing)
    {
        if (pSumatraWindowHandle != (IntPtr)0)
        {
            CloseDocument();
            pSumatraWindowHandleList.Remove(pSumatraWindowHandle);
        }
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    #region Código gerado pelo Designer de Componentes

    /// <summary>
    /// Método necessário para suporte ao Designer - não modifique 
    /// o conteúdo deste método com o editor de código.
    /// </summary>
    private void InitializeComponent()
    {
        this.SuspendLayout();
        // 
        // SumatraPDFControl
        // 
        this.BackColor = System.Drawing.SystemColors.ActiveBorder;
        this.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Center;
        this.Cursor = System.Windows.Forms.Cursors.Default;
        this.Size = new System.Drawing.Size(460, 334);
        this.Resize += new System.EventHandler(this.SumatraPDFControl_Resize);
        this.ResumeLayout(false);

    }

    #endregion
}
