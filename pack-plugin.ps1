$revision = git describe --always --tags
$xml = [xml](Get-Content ..\QuickLook.Plugin.Metadata.Base.config)
$xml.Metadata.Version="$revision"
$xml.Save(".\Release\QuickLook.Plugin.Metadata.config")

Remove-Item ..\QuickLook.Plugin.SumatraPDFReader.qlplugin -ErrorAction SilentlyContinue

$files = Get-ChildItem -Path .\Release\ -Exclude *.pdb,*.xml
Compress-Archive $files .\QuickLook.Plugin.SumatraPDFReader.zip
Move-Item .\QuickLook.Plugin.SumatraPDFReader.zip .\QuickLook.Plugin.SumatraPDFReader.qlplugin
