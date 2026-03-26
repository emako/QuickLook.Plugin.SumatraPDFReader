![QuickLook icon](https://user-images.githubusercontent.com/1687847/29485863-8cd61b7c-84e2-11e7-97d5-eacc2ba10d28.png)

# QuickLook.Plugin.SumatraPDFReader

This plugin allows [QuickLook](https://github.com/QL-Win/QuickLook) to preview multi-format (EPUB, MOBI, CBZ, CBR, FB2, CHM, XPS, DjVu) from [SumatraPDF](https://github.com/sumatrapdfreader/sumatrapdf).

SumatraPDF Reader is not a replacement of PDF Viewer for QuickLook.

Alternative PDF Viewer: [QuickLook.Plugin.PdfViewer-Native](https://github.com/QL-Win/QuickLook.Plugin.PdfViewer-Native)

## Supported extensions

The following file extensions are treated as SumatraPDF Reader supporting files.

- epub
- mobi
- cbz
- cbr
- fb2
- chm
- xps
- djvu
- pdf (optional, disabled by default)

## Configuration

How to enable PDF Support?

By default, this plugin does not handle PDF files to avoid conflicts with the default QuickLook PDF viewer. If you prefer to use SumatraPDF for viewing PDF files, you can enable PDF support by adding the following setting to your QuickLook configuration file:

**Configuration file location:**

- Standard installation: `%APPDATA%\pooi.moe\QuickLook\QuickLook.Plugin.SumatraPDFReader.config`
- Portable installation: `<QuickLook Installation>\UserData\QuickLook.Plugin.SumatraPDFReader.config`

**Add this line to the configuration file:**
```xml
<?xml version="1.0" encoding="utf-8"?>
<Settings>
  <EnablePdf>true</EnablePdf>
</Settings>
```

After enabling this setting, restart QuickLook for the changes to take effect.

## Developer

How to apply the modification of `SumatraPDF.exe`?

```bash
cd SumatraPDF && git apply ../QuickLook.Plugin.SumatraPDFReader.patch
```

How to install VS2022?

```bash
winget install --id "Microsoft.VisualStudio.2022.Community"  --version "17.10.0" --override "–installPath=""D:\Program Files (x86)\Visual Studio 2022"""
```

## Thanks

https://github.com/sumatrapdfreader/sumatrapdf

https://github.com/marcoscmonteiro/SumatraPDFControl

## Licenses

![GPL-v3](https://www.gnu.org/graphics/gplv3-127x51.png)

This project references many other open-source projects. See [here](https://github.com/QL-Win/QuickLook/wiki/On-the-Shoulders-of-Giants) for the full list.

All source codes are licensed under [GPL-3.0](https://opensource.org/licenses/GPL-3.0).

If you want to make any modification on these source codes while keeping new codes not protected by GPL-3.0, please contact me for a sublicense instead.

