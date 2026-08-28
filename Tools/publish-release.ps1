<#
.SYNOPSIS
  Builds UrlRouter.exe and publishes it as a GitHub Release, with the version.json the app's
  update check polls.

.DESCRIPTION
  This is the local equivalent of .github/workflows/release.yml, and the two must stay in step:
  they produce the same three artifacts (UrlRouter.exe, UrlRouter.exe.sha256, version.json) with
  the same publish flags. Neither uses FolderProfile.pubxml, which exists to install to the fixed
  path Windows registration points at, not to stage a release. Prefer the workflow for real releases - pushing a tag builds on a
  clean runner and leaves an audit trail. Use this script when GitHub Actions is unavailable, or
  with -NoPublish as a dry run before tagging.

  What it does, in order:
    1. Resolves the version (a -Version argument, else yyyy.M.d.HHmm) and writes version.txt.
    2. Publishes Release with -p:Version=, which stamps the assembly from that one value.
    3. Verifies the built executable actually reports that version. This is the check that
       matters: UpdateService compares the feed against the *running exe's* FileVersion, so a
       build whose stamp disagrees with the feed installs and then offers itself again forever.
    4. Writes version.json and the .sha256 sidecar into artifacts\.
    5. Creates the release with `gh` and uploads all three.

  version.json is uploaded under a fixed name so the feed URL
  (.../releases/latest/download/version.json) always resolves to the newest release's copy. The
  url it names points at that specific release's tag rather than the latest/download alias, so an
  installed copy is always offered exactly the executable the manifest described and hashed.

.PARAMETER Version
  Four-part version to publish. Defaults to yyyy.M.d.HHmm (local time). No component may have a
  leading zero - see the ValidateVersion target in src\UrlRouter.csproj for why.

.PARAMETER Notes
  Release notes. Shown verbatim in the update prompt and the Updates tab. Defaults to
  "Build <version>".

.PARAMETER NotesFile
  Path to a file whose contents become the release notes.

.PARAMETER MinRequiredVersion
  Sets minRequiredVersion in version.json, which removes the "skip this version" option for
  anyone below it. For a fix that must not be skippable.

.PARAMETER Draft
  Create the release as a draft. Nothing is offered to anyone until it is published, because
  releases/latest/download resolves only published, non-prerelease releases - which is what makes
  this a safe dry run against real GitHub.

.PARAMETER NoPublish
  Do everything except touch GitHub: resolve, build, verify, and write artifacts\. Needs no auth.

.EXAMPLE
  .\Tools\publish-release.ps1 -NoPublish
  Build and inspect the version.json you are about to ship.

.EXAMPLE
  .\Tools\publish-release.ps1 -Notes "Fixes Safe Links unwrapping for Teams v2 links."
#>
[CmdletBinding()]
param(
    [string] $Version,
    [string] $Notes,
    [string] $NotesFile,
    [string] $MinRequiredVersion,
    [switch] $Draft,
    [switch] $NoPublish
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$Repo        = 'JamTheRadar/UrlRouter'
$RepoRoot    = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$Project     = Join-Path $RepoRoot 'src\UrlRouter.csproj'
$VersionFile = Join-Path $RepoRoot 'version.txt'
$StageDir    = Join-Path $RepoRoot 'artifacts'
$PublishDir  = Join-Path $StageDir 'publish'
$BuiltExe    = Join-Path $PublishDir 'UrlRouter.exe'

function Step($message) { Write-Host "`n==> $message" -ForegroundColor Cyan }
function Fail($message) { throw $message }

# Every native command goes through this, and only $LASTEXITCODE decides success.
#
# Windows PowerShell 5.1 turns anything a native command writes to stderr into a *terminating*
# error while $ErrorActionPreference is 'Stop', and `2>$null` on the line does not prevent it.
# `gh release view` on a tag that does not exist yet writes "release not found" to stderr, which
# would kill this script at its own pre-flight check - so the first publish of any version could
# never get past step 1. `gh release create` and `dotnet publish` write to stderr too.
function Invoke-Native {
    param([Parameter(Mandatory)] [scriptblock] $Command)
    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try { & $Command 2>&1 } finally { $ErrorActionPreference = $previous }
}

# ---------------------------------------------------------------------------------------------
Step 'Prerequisites'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { Fail 'dotnet is not on PATH.' }

if (-not $NoPublish) {
    if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
        Fail 'The GitHub CLI (gh) is not on PATH. Install it, or re-run with -NoPublish.'
    }
    Invoke-Native { gh auth status } | Out-Null
    if ($LASTEXITCODE -ne 0) { Fail 'gh is not authenticated. Run: gh auth login' }
}

# ---------------------------------------------------------------------------------------------
Step 'Version'

if (-not $Version) { $Version = Get-Date -Format 'yyyy.M.d.HHmm' }

$parts = $Version.Split('.')
if ($parts.Count -ne 4) { Fail "Version '$Version' must have four components." }
foreach ($part in $parts) {
    # Reject a leading zero before the build does, so the failure names the release rather than
    # arriving as an MSBuild error three minutes later.
    if ($part -notmatch '^(0|[1-9][0-9]*)$') {
        Fail "Version component '$part' is not in normal form (no leading zeros). Use e.g. 834, not 0834."
    }
    if ([int]$part -gt 65535) { Fail "Version component '$part' exceeds 65535, System.Version's limit." }
}

$tag = "v$Version"
Write-Host "  Publishing $Version as tag $tag"

if (-not $NoPublish) {
    Invoke-Native { gh release view $tag --repo $Repo } | Out-Null
    if ($LASTEXITCODE -eq 0) { Fail "Release $tag already exists. Pick another version." }
}

# UTF-8 with no BOM, no trailing newline: the csproj reads this file and trims it, but a BOM
# would survive the trim and land in the version string.
[System.IO.File]::WriteAllText($VersionFile, $Version, (New-Object System.Text.UTF8Encoding $false))

# ---------------------------------------------------------------------------------------------
Step 'Build'

if (Test-Path $PublishDir) { Remove-Item $PublishDir -Recurse -Force }
New-Item -ItemType Directory -Force -Path $PublishDir | Out-Null

Invoke-Native {
    dotnet publish $Project -c Release -p:Version=$Version -p:PublishDir=$PublishDir `
        -r win-x64 --self-contained false -p:PublishSingleFile=true -p:PublishReadyToRun=true
} | Write-Host
if ($LASTEXITCODE -ne 0) { Fail 'dotnet publish failed.' }

if (-not (Test-Path $BuiltExe)) { Fail "Publish produced no executable at $BuiltExe." }

# ---------------------------------------------------------------------------------------------
Step 'Verify'

# The point of the whole arrangement. UpdateService reads FileVersion off the running exe, so if
# the built file disagrees with what version.json is about to advertise, the update installs
# cleanly and then nags forever.
$builtVersion = (Get-Item $BuiltExe).VersionInfo.FileVersion
if ($builtVersion -ne $Version) {
    Fail "The built executable reports $builtVersion but this release is $Version. Refusing to publish."
}
Write-Host "  UrlRouter.exe reports $builtVersion"

# .NET rather than Get-FileHash: that cmdlet lives in a module which is not always available
# in a constrained or -NoProfile host, and a release script should not fail on the shell it is
# run from.
$sha = [System.Security.Cryptography.SHA256]::Create()
try {
    $stream = [System.IO.File]::OpenRead($BuiltExe)
    try { $hash = [System.BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '').ToLowerInvariant() }
    finally { $stream.Dispose() }
} finally { $sha.Dispose() }
Write-Host "  sha256 $hash"

# ---------------------------------------------------------------------------------------------
Step 'Manifest'

if ($NotesFile) {
    if (-not (Test-Path $NotesFile)) { Fail "Notes file not found: $NotesFile" }
    $Notes = Get-Content $NotesFile -Raw
}
if (-not $Notes) { $Notes = "Build $Version" }

$manifest = [ordered]@{
    version     = $Version
    url         = "https://github.com/$Repo/releases/download/$tag/UrlRouter.exe"
    sha256      = $hash
    notes       = $Notes
    releasePage = "https://github.com/$Repo/releases/tag/$tag"
}
if ($MinRequiredVersion) { $manifest.minRequiredVersion = $MinRequiredVersion }

$manifestPath = Join-Path $StageDir 'version.json'
$hashPath     = Join-Path $StageDir 'UrlRouter.exe.sha256'

# No BOM. System.Text.Json reads a leading U+FEFF as an unexpected character and throws, and the
# only symptom is the update check quietly never finding anything again. Set-Content -Encoding
# utf8 writes one on PowerShell 5.1, hence WriteAllText with an explicit encoding.
$utf8NoBom = New-Object System.Text.UTF8Encoding $false
[System.IO.File]::WriteAllText($manifestPath, ($manifest | ConvertTo-Json -Depth 4), $utf8NoBom)
[System.IO.File]::WriteAllText($hashPath, "$hash  UrlRouter.exe`n", $utf8NoBom)

Write-Host "  $manifestPath"
Get-Content $manifestPath | Write-Host

if ($NoPublish) {
    Step 'Done (-NoPublish): nothing was sent to GitHub.'
    return
}

# ---------------------------------------------------------------------------------------------
Step 'Publish'

$ghArgs = @(
    'release', 'create', $tag,
    $BuiltExe, $manifestPath, $hashPath,
    '--repo', $Repo,
    '--title', "URL Router $Version",
    '--notes', $Notes
)
if ($Draft) { $ghArgs += "--draft" }

Invoke-Native { gh @ghArgs } | Write-Host
if ($LASTEXITCODE -ne 0) { Fail 'gh release create failed.' }

Step "Published $tag"
if ($Draft) {
    Write-Host '  Draft: nothing is offered to users until you publish it, because' -ForegroundColor Yellow
    Write-Host '  releases/latest/download resolves only published, non-prerelease releases.' -ForegroundColor Yellow
}
