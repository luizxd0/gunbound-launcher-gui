[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ClientRoot,

    [Parameter(Mandatory = $true)]
    [string]$PublishRoot,

    [string]$ManifestName = "manifest.txt",

    [string]$FilesSubDir = "gamefiles",

    [ValidateSet("SHA256", "SHA1", "MD5")]
    [string]$HashAlgorithm = "SHA256",

    [string[]]$Exclude = @(
        "Launcher.exe",
        "Launcher.pdb",
        "Launcher.ini",
        "launcher-debug.log",
        ".launcher-hash-cache.txt"
    ),

    [switch]$PruneDeleted
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Normalize-RelativePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$BasePath,

        [Parameter(Mandatory = $true)]
        [string]$FullPath
    )

    $relative = $FullPath.Substring($BasePath.Length).TrimStart('\', '/')
    return ($relative -replace '\\', '/')
}

function Should-ExcludeFile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RelativePath,

        [Parameter(Mandatory = $true)]
        [string[]]$Patterns
    )

    foreach ($pattern in $Patterns) {
        if ([string]::IsNullOrWhiteSpace($pattern)) {
            continue
        }

        if ($RelativePath -like $pattern) {
            return $true
        }

        if ([System.IO.Path]::GetFileName($RelativePath) -like $pattern) {
            return $true
        }
    }

    return $false
}

function Read-ExistingManifestHashMap {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ManifestPath
    )

    $map = New-Object 'System.Collections.Generic.Dictionary[string,string]' ([System.StringComparer]::OrdinalIgnoreCase)
    if (-not (Test-Path -LiteralPath $ManifestPath -PathType Leaf)) {
        return $map
    }

    foreach ($line in Get-Content -LiteralPath $ManifestPath) {
        $trimmed = if ($null -eq $line) { "" } else { $line.Trim() }
        if ([string]::IsNullOrWhiteSpace($trimmed)) {
            continue
        }
        if ($trimmed.StartsWith("#") -or $trimmed.StartsWith(";") -or $trimmed.StartsWith("//")) {
            continue
        }

        $parts = $trimmed.Split('|')
        if ($parts.Length -lt 2) {
            continue
        }

        $pathToken = if ($null -eq $parts[0]) { "" } else { $parts[0] }
        $hashToken = if ($null -eq $parts[1]) { "" } else { $parts[1] }
        $path = $pathToken.Trim().Trim('"')
        $hash = $hashToken.Trim().Trim('"').ToUpperInvariant()
        if ([string]::IsNullOrWhiteSpace($path) -or [string]::IsNullOrWhiteSpace($hash)) {
            continue
        }

        $map[$path] = $hash
    }

    return $map
}

if (-not (Test-Path -LiteralPath $ClientRoot -PathType Container)) {
    throw "ClientRoot does not exist: $ClientRoot"
}

$clientRootResolved = [System.IO.Path]::GetFullPath((Resolve-Path -LiteralPath $ClientRoot).Path)
$publishRootResolved = [System.IO.Path]::GetFullPath($PublishRoot)

if (-not $clientRootResolved.EndsWith([System.IO.Path]::DirectorySeparatorChar.ToString())) {
    $clientRootResolved += [System.IO.Path]::DirectorySeparatorChar
}
if (-not $publishRootResolved.EndsWith([System.IO.Path]::DirectorySeparatorChar.ToString())) {
    $publishRootResolved += [System.IO.Path]::DirectorySeparatorChar
}

$filesRoot = [System.IO.Path]::Combine($publishRootResolved, $FilesSubDir)
$manifestPath = [System.IO.Path]::Combine($publishRootResolved, $ManifestName)

[System.IO.Directory]::CreateDirectory($publishRootResolved) | Out-Null
[System.IO.Directory]::CreateDirectory($filesRoot) | Out-Null

$existingManifestHashes = Read-ExistingManifestHashMap -ManifestPath $manifestPath

$manifestEntries = New-Object 'System.Collections.Generic.List[object]'
$copiedCount = 0
$skippedCount = 0

$sourceFiles = Get-ChildItem -LiteralPath $clientRootResolved -Recurse -File | Sort-Object FullName
foreach ($file in $sourceFiles) {
    $relativePath = Normalize-RelativePath -BasePath $clientRootResolved -FullPath $file.FullName
    if (Should-ExcludeFile -RelativePath $relativePath -Patterns $Exclude) {
        continue
    }

    $hashHex = (Get-FileHash -LiteralPath $file.FullName -Algorithm $HashAlgorithm).Hash.ToUpperInvariant()
    $manifestEntries.Add([PSCustomObject]@{
            RelativePath = $relativePath
            Hash = $hashHex
            Size = [int64]$file.Length
        }) | Out-Null

    $targetPath = Join-Path $filesRoot ($relativePath -replace '/', '\')
    $targetDir = Split-Path -Parent $targetPath
    if (-not [string]::IsNullOrWhiteSpace($targetDir)) {
        [System.IO.Directory]::CreateDirectory($targetDir) | Out-Null
    }

    $copyNeeded = $true
    $oldHash = $null
    if ($existingManifestHashes.TryGetValue($relativePath, [ref]$oldHash)) {
        if (($oldHash -eq $hashHex) -and (Test-Path -LiteralPath $targetPath -PathType Leaf)) {
            $copyNeeded = $false
        }
    }

    if ($copyNeeded) {
        Copy-Item -LiteralPath $file.FullName -Destination $targetPath -Force
        $copiedCount++
    }
    else {
        $skippedCount++
    }
}

$removedCount = 0
if ($PruneDeleted) {
    $currentPaths = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($entry in $manifestEntries) {
        $null = $currentPaths.Add($entry.RelativePath)
    }

    if (-not $filesRoot.EndsWith([System.IO.Path]::DirectorySeparatorChar.ToString())) {
        $filesRoot += [System.IO.Path]::DirectorySeparatorChar
    }

    foreach ($existingFile in Get-ChildItem -LiteralPath $filesRoot -Recurse -File) {
        $existingRelativePath = Normalize-RelativePath -BasePath $filesRoot -FullPath $existingFile.FullName
        if (-not $currentPaths.Contains($existingRelativePath)) {
            [System.IO.File]::Delete($existingFile.FullName)
            $removedCount++
        }
    }
}

$manifestLines = New-Object 'System.Collections.Generic.List[string]'
$manifestLines.Add("# path|hash|size") | Out-Null
$manifestLines.Add("# hash-algorithm=$HashAlgorithm") | Out-Null

$sortedEntries = $manifestEntries | Sort-Object RelativePath
foreach ($entry in $sortedEntries) {
    $manifestLines.Add(("{0}|{1}|{2}" -f $entry.RelativePath, $entry.Hash, $entry.Size)) | Out-Null
}

Set-Content -LiteralPath $manifestPath -Value $manifestLines -Encoding UTF8

Write-Host "Patch build complete."
Write-Host "ClientRoot : $clientRootResolved"
Write-Host "PublishRoot: $publishRootResolved"
Write-Host "Manifest   : $manifestPath"
Write-Host "Files copied: $copiedCount"
Write-Host "Files reused: $skippedCount"
if ($PruneDeleted) {
    Write-Host "Files removed: $removedCount"
}
Write-Host "Manifest entries: $($sortedEntries.Count)"
