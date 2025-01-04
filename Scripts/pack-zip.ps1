Remove-Item ..\QuickLook.Plugin.SumatraPDFReader.qlplugin -ErrorAction SilentlyContinue

$files = Get-ChildItem -Path ..\Build\Release\ -Exclude *.pdb,*.xml
Compress-Archive $files ..\QuickLook.Plugin.SumatraPDFReader.zip
Move-Item ..\QuickLook.Plugin.SumatraPDFReader.zip ..\QuickLook.Plugin.SumatraPDFReader.qlplugin