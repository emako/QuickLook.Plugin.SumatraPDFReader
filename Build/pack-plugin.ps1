$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$releaseDir = Join-Path $repoRoot "Build\Release"

$revision = git -C $repoRoot describe --always --tags
$xml = [xml](Get-Content (Join-Path $repoRoot "QuickLook.Plugin.Metadata.Base.config"))
$xml.Metadata.Version = "$revision"
$xml.Save((Join-Path $releaseDir "QuickLook.Plugin.Metadata.config"))

Remove-Item "QuickLook.Plugin.SumatraPDFReader.qlplugin" -ErrorAction SilentlyContinue
Remove-Item "QuickLook.Plugin.SumatraPDFReader.zip" -ErrorAction SilentlyContinue

if (-not (Test-Path $releaseDir)) {
	throw "Release directory not found: $releaseDir"
}

$files = Get-ChildItem -Path $releaseDir |
	Where-Object { $_.Name -notmatch '\.(pdb|xml)$' } |
	Select-Object -ExpandProperty FullName
if (-not $files) {
	throw "No files found to package in: $releaseDir"
}

Compress-Archive -Path $files -DestinationPath "QuickLook.Plugin.SumatraPDFReader.zip"
Move-Item "QuickLook.Plugin.SumatraPDFReader.zip" "QuickLook.Plugin.SumatraPDFReader.qlplugin"
