[CmdletBinding(PositionalBinding=$false)]
param ()

Set-StrictMode -version 2.0
$ErrorActionPreference = "Stop"

function MarkShipped([string]$dir) {
    $shippedFilePath = Join-Path $dir "PublicAPI.Shipped.txt"
    $shipped = Get-Content $shippedFilePath
    if ($null -eq $shipped) {
        $shipped = @()
    }

    $unshippedFilePath = Join-Path $dir "PublicAPI.Unshipped.txt"
    $unshipped = Get-Content $unshippedFilePath
    $removed = @()
    $removedPrefix = "*REMOVED*";
    $nullableHeader = "#nullable enable"
    Write-Host "Processing $dir"

    foreach ($item in $unshipped) {
        if ($item.Length -gt 0 -and $item -ne $nullableHeader) {
            if ($item.StartsWith($removedPrefix)) {
                $item = $item.Substring($removedPrefix.Length)
                $removed += $item
            }
            else {
                $shipped += $item
            }
        }
    }

    $shipped = [string[]]@($shipped | ?{ -not $removed.Contains($_) })
    [Array]::Sort($shipped, [StringComparer]::Ordinal)
    [IO.File]::WriteAllText($shippedFilePath, ($shipped -join "`n") + "`n")
    [IO.File]::WriteAllText($unshippedFilePath, $nullableHeader + "`n")
}

try {
    $repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
    Push-Location $repoRoot

    foreach ($file in Get-ChildItem -re -in "PublicApi.Shipped.txt") {
        $dir = Split-Path -parent $file
        MarkShipped $dir
    }
}
catch {
    Write-Host $_
    Write-Host $_.Exception
    exit 1
}
finally {
    Pop-Location
}
