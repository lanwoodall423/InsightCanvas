[CmdletBinding()]
param(
    [ValidateSet('api', 'version', 'package', 'freshness', 'all')]
    [string]$Command = 'all',
    [string]$RimWorldDir = $env:RIMWORLD_ROOT,
    [string]$PackageLabel = '',
    [switch]$GenerateApiBaseline
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$sourceRoot = Join-Path $repo 'Source'
$shippedApiPath = Join-Path $repo 'PublicAPI.Shipped.txt'
$unshippedApiPath = Join-Path $repo 'PublicAPI.Unshipped.txt'
$packageRoot = Join-Path $repo 'artifacts'

function Normalize-ApiText([string]$value) {
    if ($null -eq $value) { return '' }
    return (($value -replace '\s+', ' ').Trim())
}

function Get-ApiLines([string]$path) {
    $lines = @(Get-Content -LiteralPath $path)
    $result = New-Object System.Collections.Generic.List[string]
    $braceDepth = 0
    $namespace = ''
    $namespaceDepth = -1
    $typeStack = New-Object System.Collections.Generic.List[object]

    for ($i = 0; $i -lt $lines.Count; $i++) {
        $raw = [string]$lines[$i]
        $line = ($raw -replace '//.*$', '').Trim()
        if ([string]::IsNullOrWhiteSpace($line)) {
            $braceDepth += ([regex]::Matches($raw, '\{')).Count - ([regex]::Matches($raw, '\}')).Count
            continue
        }

        if ($line -match '^namespace\s+([A-Za-z0-9_.]+)') {
            $namespace = $Matches[1]
        }

        $owner = if ($typeStack.Count -gt 0) {
            ($typeStack | ForEach-Object { $_.Name }) -join '.'
        } else { '' }
        $currentTypeKind = if ($typeStack.Count -gt 0) { $typeStack[$typeStack.Count - 1].Kind } else { '' }
        $qualifiedOwner = if ([string]::IsNullOrEmpty($owner)) { $namespace } elseif ([string]::IsNullOrEmpty($namespace)) { $owner } else { "$namespace.$owner" }

        if ($currentTypeKind -eq 'enum' -and $braceDepth -eq $typeStack[$typeStack.Count - 1].BodyDepth) {
            foreach ($member in ($line -split ',')) {
                $memberText = Normalize-ApiText($member)
                if ($memberText -match '^[A-Za-z_][A-Za-z0-9_]*(\s*=\s*.+)?$') {
                    $result.Add("enum-member|$qualifiedOwner::$memberText") | Out-Null
                }
            }
        }

        $declaration = $null
        $declarationStart = $i
        $isExplicitVisibility = $line -match '^\s*(public|protected(?:\s+internal)?)\b'
        $isInterfaceMember = $currentTypeKind -eq 'interface' -and $braceDepth -eq $typeStack[$typeStack.Count - 1].BodyDepth -and
            $line -notmatch '^(\[|\}|\{|;)' -and $line -notmatch '^(public|protected|private|internal)\b'
        if ($isExplicitVisibility -or $isInterfaceMember) {
            $parts = New-Object System.Collections.Generic.List[string]
            $j = $i
            while ($j -lt $lines.Count -and $parts.Count -lt 40) {
                $part = (($lines[$j] -replace '//.*$', '').Trim())
                if (-not [string]::IsNullOrWhiteSpace($part)) { $parts.Add($part) | Out-Null }
                $joined = $parts -join ' '
                if ($joined -match '[;{}]|=>') { break }
                $j++
            }
            $declaration = Normalize-ApiText($parts -join ' ')
            $i = $j
            if ($declaration -match '\b(class|struct|interface|enum|delegate)\s+([A-Za-z_][A-Za-z0-9_]*)') {
                $kind = $Matches[1]
                $name = $Matches[2]
                $typeSignature = ($declaration -split '\{', 2)[0].Trim()
                $result.Add("type|$qualifiedOwner::$typeSignature") | Out-Null
                if ($declaration -match '\{') {
                    $typeStack.Add([pscustomobject]@{ Name = $name; Kind = $kind; BodyDepth = $braceDepth + 1 }) | Out-Null
                }
            } elseif ($declaration -match '\b(class|struct|interface|enum|delegate)\s+') {
                $result.Add("type|$qualifiedOwner::$declaration") | Out-Null
            } else {
                $signature = $declaration
                if ($signature -match '\{\s*(get|set)\b') {
                    $signature = $signature
                } elseif ($signature.Contains('{')) {
                    $signature = ($signature -split '\{', 2)[0].Trim()
                } elseif ($signature.Contains('=>')) {
                    $signature = ($signature -split '=>', 2)[0].Trim() + ';'
                }
                if (-not [string]::IsNullOrWhiteSpace($signature)) {
                    $result.Add("member|$qualifiedOwner::$signature") | Out-Null
                }
            }
        }

        $braceDepth += ([regex]::Matches($raw, '\{')).Count - ([regex]::Matches($raw, '\}')).Count
    }
    return $result
}

function Get-PublicApi {
    $all = New-Object System.Collections.Generic.List[string]
    foreach ($file in (Get-ChildItem -LiteralPath $sourceRoot -Filter '*.cs' -File | Sort-Object FullName)) {
        foreach ($line in (Get-ApiLines $file.FullName)) {
            $all.Add($line) | Out-Null
        }
    }
    return @($all | Sort-Object -Unique)
}

function Read-Baseline([string]$path) {
    if (-not (Test-Path -LiteralPath $path)) { return @() }
    return @(Get-Content -LiteralPath $path | Where-Object { $_ -and -not $_.Trim().StartsWith('#') } | ForEach-Object { $_.Trim() } | Sort-Object -Unique)
}

function Invoke-ApiCheck {
    $current = @(Get-PublicApi)
    if ($GenerateApiBaseline -or -not (Test-Path -LiteralPath $shippedApiPath)) {
        Set-Content -LiteralPath $shippedApiPath -Value (@('# Public API shipped baseline. Update deliberately for an acknowledged major release.') + $current)
        if (-not (Test-Path -LiteralPath $unshippedApiPath)) {
            Set-Content -LiteralPath $unshippedApiPath -Value '# Reviewed compatible additions are listed here before shipment.'
        }
        Write-Host "Public API baseline generated: $($current.Count) entries"
        return
    }
    $shipped = @(Read-Baseline $shippedApiPath)
    $unshipped = @(Read-Baseline $unshippedApiPath)
    $missing = @($shipped | Where-Object { $_ -notin $current })
    $added = @($current | Where-Object { $_ -notin $shipped -and $_ -notin $unshipped })
    $staleUnshipped = @($unshipped | Where-Object { $_ -notin $current })
    if ($missing.Count -gt 0) {
        Write-Error ("Removed or changed shipped API entries:`n" + ($missing -join "`n"))
    }
    if ($added.Count -gt 0) {
        Write-Error ("Unreviewed public API additions; move compatible entries to PublicAPI.Unshipped.txt:`n" + ($added -join "`n"))
    }
    if ($staleUnshipped.Count -gt 0) {
        Write-Error ("PublicAPI.Unshipped.txt contains entries absent from source:`n" + ($staleUnshipped -join "`n"))
    }
    Write-Host "Public API compatibility check passed: $($current.Count) entries"
}

function Get-VersionInfo {
    $about = [xml](Get-Content -Raw -LiteralPath (Join-Path $repo 'About/About.xml'))
    $aboutVersion = [string]$about.ModMetaData.modVersion
    $assemblyText = Get-Content -Raw -LiteralPath (Join-Path $repo 'Source/AssemblyInfo.cs')
    $assemblyVersion = [regex]::Match($assemblyText, 'AssemblyVersion\("([0-9]+\.[0-9]+\.[0-9]+)\.0"\)').Groups[1].Value
    $readmeText = Get-Content -Raw -LiteralPath (Join-Path $repo 'README.md')
    $readmeVersion = [regex]::Match($readmeText, 'current v2 release is `([0-9]+\.[0-9]+\.[0-9]+)`').Groups[1].Value
    $changelogText = Get-Content -Raw -LiteralPath (Join-Path $repo 'CHANGELOG.md')
    $changelogVersion = [regex]::Match($changelogText, 'The next patch release is `([0-9]+\.[0-9]+\.[0-9]+)`').Groups[1].Value
    $checklistText = Get-Content -Raw -LiteralPath (Join-Path $repo 'Documentation/ReleaseChecklist.md')
    if ($checklistText -notmatch ('Assembly version `' + [regex]::Escape($aboutVersion) + '`')) {
        throw "Release checklist does not name version $aboutVersion"
    }
    foreach ($documentationPath in @('Documentation/Integration.md', 'Documentation/Quickstart.md')) {
        $documentationText = Get-Content -Raw -LiteralPath (Join-Path $repo $documentationPath)
        if ($documentationText -notmatch ('Insight Canvas ' + [regex]::Escape($aboutVersion))) {
            throw "$documentationPath does not name version $aboutVersion"
        }
    }
    $releaseNotesText = Get-Content -Raw -LiteralPath (Join-Path $repo ('Documentation/ReleaseNotes-' + $aboutVersion + '.md'))
    if ($releaseNotesText -notmatch ('(?m)^# Insight Canvas ' + [regex]::Escape($aboutVersion) + '\r?$')) {
        throw "Stable release notes do not name version $aboutVersion"
    }
    $publicationText = Get-Content -Raw -LiteralPath (Join-Path $repo 'Documentation/ReleasePublication.md')
    if ($publicationText -notmatch ('InsightCanvas-' + [regex]::Escape($aboutVersion) + '\.zip')) {
        throw "Publication plan does not name version $aboutVersion"
    }
    if ([string]::IsNullOrWhiteSpace($aboutVersion) -or $aboutVersion -ne $assemblyVersion -or $aboutVersion -ne $readmeVersion -or $aboutVersion -ne $changelogVersion) {
        throw "Version mismatch: About=$aboutVersion Assembly=$assemblyVersion README=$readmeVersion CHANGELOG=$changelogVersion"
    }
    return [pscustomobject]@{ Version = $aboutVersion; AssemblyVersion = "$assemblyVersion.0" }
}

function Invoke-VersionCheck {
    $info = Get-VersionInfo
    Write-Host "Version consistency check passed: $($info.Version) / $($info.AssemblyVersion)"
}

function Invoke-FreshnessCheck {
    $info = Get-VersionInfo
    if ([string]::IsNullOrWhiteSpace($RimWorldDir)) { throw 'RimWorldDir is required for freshness validation.' }
    $managed = Join-Path $RimWorldDir 'RimWorldWin64_Data/Managed'
    if (-not (Test-Path -LiteralPath (Join-Path $managed 'Assembly-CSharp.dll'))) { throw "RimWorld managed assemblies not found: $managed" }
    $temp = Join-Path ([System.IO.Path]::GetTempPath()) ('insightcanvas-release-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $temp | Out-Null
    try {
        & dotnet build (Join-Path $repo 'Source/InsightCanvas.csproj') --configuration Release --nologo --output $temp "/p:RimWorldDir=$RimWorldDir"
        if ($LASTEXITCODE -ne 0) { throw "Release build failed with exit code $LASTEXITCODE" }
        $assemblyName = [System.Reflection.AssemblyName]::GetAssemblyName((Join-Path $temp 'InsightCanvas.dll'))
        if ($assemblyName.Version.ToString() -ne $info.AssemblyVersion) {
            throw "Release assembly metadata version mismatch: built=$($assemblyName.Version) expected=$($info.AssemblyVersion)"
        }
        $projectText = Get-Content -Raw -LiteralPath (Join-Path $repo 'Source/InsightCanvas.csproj')
        if ($projectText -match '<Private>true</Private>' -or ($projectText -match '<Reference Include=' -and $projectText -notmatch '<Private>false</Private>')) {
            throw 'Source project must mark all RimWorld and Unity references Private=false.'
        }
        foreach ($name in @('InsightCanvas.dll', 'InsightCanvas.xml')) {
            $built = Join-Path $temp $name
            $checkedIn = Join-Path $repo "1.6/Assemblies/$name"
            if (-not (Test-Path -LiteralPath $built)) { throw "Release output missing $name" }
            if (-not (Test-Path -LiteralPath $checkedIn)) { throw "Checked-in runtime artifact missing $name" }
            $builtHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $built).Hash
            $checkedInHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $checkedIn).Hash
            if ($builtHash -ne $checkedInHash) { throw "Checked-in $name is stale: built=$builtHash checkedIn=$checkedInHash" }
            Write-Host "Freshness match: $name $builtHash"
        }
    } finally {
        Remove-Item -LiteralPath $temp -Recurse -Force -ErrorAction SilentlyContinue
    }
}

function Invoke-Package {
    $info = Get-VersionInfo
    $label = if ([string]::IsNullOrWhiteSpace($PackageLabel)) { $info.Version } else { $PackageLabel }
    if ($label -notmatch '^[0-9]+\.[0-9]+\.[0-9]+(?:-rc\.[0-9]+)?$') {
        throw "Invalid package label '$label'; use a version or version-rc.N."
    }
    $stage = Join-Path ([System.IO.Path]::GetTempPath()) ('insightcanvas-package-' + [guid]::NewGuid().ToString('N'))
    $zipName = "InsightCanvas-$label.zip"
    $zipPath = Join-Path $packageRoot $zipName
    New-Item -ItemType Directory -Path $stage -Force | Out-Null
    New-Item -ItemType Directory -Path $packageRoot -Force | Out-Null
    try {
        Copy-Item -LiteralPath (Join-Path $repo 'About') -Destination (Join-Path $stage 'About') -Recurse
        Copy-Item -LiteralPath (Join-Path $repo '1.6') -Destination (Join-Path $stage '1.6') -Recurse
        $loadFolders = Join-Path $repo 'LoadFolders.xml'
        if (Test-Path -LiteralPath $loadFolders) { Copy-Item -LiteralPath $loadFolders -Destination $stage }
        $files = @(Get-ChildItem -LiteralPath $stage -Recurse -File | Sort-Object FullName)
        $dlls = @($files | Where-Object Extension -ieq '.dll')
        if ($dlls.Count -ne 1 -or $dlls[0].Name -ne 'InsightCanvas.dll') { throw 'Package must contain exactly one framework DLL named InsightCanvas.dll.' }
        foreach ($file in $files) {
            $relative = $file.FullName.Substring($stage.Length + 1).Replace('\', '/')
            if ($relative -notmatch '^(About/|1\.6/|LoadFolders\.xml$)') { throw "Unexpected runtime file: $relative" }
            if ($file.Name -match '(?i)(Assembly-CSharp|UnityEngine|RimWorld).*\.dll$' -or ($file.Extension -ieq '.dll' -and $file.Name -ne 'InsightCanvas.dll')) { throw "Proprietary or unexpected DLL in package: $relative" }
        }
        if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force }
        Add-Type -AssemblyName System.IO.Compression
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        $archive = [System.IO.Compression.ZipFile]::Open($zipPath, [System.IO.Compression.ZipArchiveMode]::Create)
        try {
            foreach ($file in $files) {
                $relative = $file.FullName.Substring($stage.Length + 1).Replace('\', '/')
                $entry = $archive.CreateEntry($relative, [System.IO.Compression.CompressionLevel]::Optimal)
                $entry.LastWriteTime = [DateTimeOffset]::new(1980, 1, 1, 0, 0, 0, [TimeSpan]::Zero)
                $input = [System.IO.File]::OpenRead($file.FullName)
                try { $output = $entry.Open(); try { $input.CopyTo($output) } finally { $output.Dispose() } } finally { $input.Dispose() }
            }
        } finally { $archive.Dispose() }
        $hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $zipPath).Hash.ToLowerInvariant()
        $shaPath = "$zipPath.sha256"
        Set-Content -LiteralPath $shaPath -Value "$hash  $zipName"
        $inspection = [System.IO.Compression.ZipFile]::OpenRead($zipPath)
        try {
            $names = @($inspection.Entries | ForEach-Object FullName)
            if (@($names | Where-Object { $_ -match '(?i)(Assembly-CSharp|UnityEngine|RimWorld).*\.dll$' }).Count -gt 0) { throw 'Package inspection found a proprietary game DLL.' }
            if (@($names | Where-Object { $_ -match '(?i)InsightCanvas\.dll$' }).Count -ne 1) { throw 'Package inspection found an invalid framework DLL count.' }
        } finally { $inspection.Dispose() }
        Write-Host "Package created: $zipPath"
        Write-Host "SHA-256: $hash"
        Write-Host "Package inspection passed: $($files.Count) runtime files"
    } finally {
        Remove-Item -LiteralPath $stage -Recurse -Force -ErrorAction SilentlyContinue
    }
}

switch ($Command) {
    'api' { Invoke-ApiCheck }
    'version' { Invoke-VersionCheck }
    'package' { Invoke-Package }
    'freshness' { Invoke-FreshnessCheck }
    'all' {
        Invoke-ApiCheck
        Invoke-VersionCheck
        Invoke-Package
    }
}
