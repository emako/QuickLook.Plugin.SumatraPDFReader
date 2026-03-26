$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")

Set-Location (Join-Path $repoRoot "SumatraPDF")
git checkout -f 3.6rel
git apply ../QuickLook.Plugin.SumatraPDFReader.patch

$solutionPath = Join-Path $repoRoot "SumatraPDF\vs2022\SumatraPDF.sln"
$platforms = @("ARM64", "x64", "x86")

$vsWhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
$msbuildPath = $null

if (Test-Path $vsWhere) {
    $msbuildPath = & $vsWhere -latest -products * -requires Microsoft.Component.MSBuild -find "MSBuild\\**\\Bin\\MSBuild.exe" | Select-Object -First 1
}

if (-not $msbuildPath) {
    $msbuildCommand = Get-Command msbuild -ErrorAction SilentlyContinue
    if ($msbuildCommand) {
        $msbuildPath = $msbuildCommand.Source
    }
}

if (-not $msbuildPath) {
    throw "MSBuild.exe not found. Install Visual Studio 2022 Build Tools (MSBuild workload) or add MSBuild to PATH."
}

# Force UTF-8 source decoding for all cl.exe invocations in this process.
if ([string]::IsNullOrWhiteSpace($env:CL)) {
	$env:CL = "/utf-8"
}
elseif ($env:CL -notmatch "(^|\s)/utf-8(\s|$)") {
	$env:CL = "$($env:CL) /utf-8"
}

foreach ($platform in $platforms) {
	$msbuildPlatform = if ($platform -eq "x86") { "Win32" } else { $platform }
	Write-Host "Building SumatraPDF.sln (Release|$platform, MSBuild Platform=$msbuildPlatform)..."
	& $msbuildPath $solutionPath /t:Build /m /p:Configuration=Release /p:Platform=$msbuildPlatform
	if ($LASTEXITCODE -ne 0) {
		if (-not $failedPlatforms) {
			$failedPlatforms = @()
		}
		$failedPlatforms += $platform
		Write-Warning "Build failed for Release|$platform, continue with next platform..."
	}
}

if ($failedPlatforms -and $failedPlatforms.Count -gt 0) {
	$failedText = $failedPlatforms -join ", "
	throw "Build failed for platform(s): $failedText"
}
