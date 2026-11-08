# Copies every photo from the old Wix site into wwwroot/images so the new site
# no longer depends on Wix. Run once from the RachelTower.Web folder:
#   pwsh scripts/download-images.ps1
# The list of photos comes from Infrastructure/SiteImages.cs.

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$source = Get-Content (Join-Path $root "Infrastructure/SiteImages.cs") -Raw
$target = Join-Path $root "wwwroot/images"
New-Item -ItemType Directory -Force -Path $target | Out-Null

$pattern = 'new\("(?<key>[a-z0-9-]+)",\s*"(?<id>[0-9a-f]{32})"(?:,\s*"(?<ext>[a-z]+)")?'
foreach ($m in [regex]::Matches($source, $pattern)) {
    $ext = if ($m.Groups["ext"].Success) { $m.Groups["ext"].Value } else { "jpg" }
    $file = Join-Path $target "$($m.Groups["key"].Value).$ext"
    if (Test-Path $file) { continue }
    # Wix serves a resized copy; 2000 px wide is plenty for full-screen photos.
    $url = "https://static.wixstatic.com/media/f2e2c0_$($m.Groups["id"].Value)~mv2.$ext/v1/fit/w_2000,h_4000,q_85/image.$ext"
    Write-Host "Downloading $($m.Groups["key"].Value).$ext"
    Invoke-WebRequest -Uri $url -OutFile $file
}
Write-Host "Done. Photos are in wwwroot/images."
