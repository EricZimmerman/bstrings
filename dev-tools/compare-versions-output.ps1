$ErrorActionPreference = 'Stop'

#
# Tool to compare performance and output of two bstrings versions
#
# How to use:
# 1. Copy (old) version to subfolder .\v1\bstrings.exe
# 2. Copy (new) version to subfolder .\v2\bstrings.exe
# 3. Copy MEMORY.DMP to the folder of the .ps1 script
# 4. Execute this script from powershell command line with 
#    working directory of the script.
#
#    .\compare-versions-output.ps1
#

$outfile1 = 'out1.txt'
$outfile2 = 'out2.txt'

$memoryDump = 'MEMORY.DMP'

$regexPatterns = @(
    "guid"
    "usPhone"
    "unc"
    "mac"
    "ssn"
    "cc"
    "ipv4"
    "ipv6"
    "email"
    "zip"
    "urlUser"
    "url3986"
    "xml"
    "sid"
    "win_path"
    "var_set"
    "reg_path"
    "b64"
    "bitlocker"
    "bitcoin"
    "aeon"
    "bytecoin"
    "dashcoin"
    "dashcoin2"
    "fantomcoin"
    "monero"
    "sumokoin"
)

if (-not (Test-Path -Path '.\v1\bstrings.exe' -PathType Leaf)) {
    throw '.\v1\bstrings.exe does not exist'
}
if (-not (Test-Path -Path '.\v2\bstrings.exe' -PathType Leaf)) {
    throw '.\v2\bstrings.exe does not exist'
}
if (-not (Test-Path -Path $memoryDump -PathType Leaf)) {
    throw "$memoryDump file does not exist"
}

$results = @()

foreach ($pattern in $regexPatterns) {
    Write-Host "Processing pattern $($pattern)"
    if (Test-Path $outfile1) { Remove-Item $outfile1 }
    if (Test-Path $outfile2) { Remove-Item $outfile2 }

    $v1Result = Measure-Command { .\v1\bstrings.exe -f $memoryDump --lr $pattern -q > $outfile1 }
    $v2Result = Measure-Command { .\v2\bstrings.exe -f $memoryDump --lr $pattern -q > $outfile2 }

    $content1 = Get-Content $outfile1 -Raw
    $content2 = Get-Content $outfile2 -Raw

    if ($content1.Length -ne $content2.Length) {
        throw "Failed: mismatch length"
    }
    if ($content1 -ne $content2) {
        throw "Failed: mismatch content"
    }
    
    Write-Host "Output of $("{0:F1}" -f ((Get-Item $outfile1).Length / 1024)) KB matches"

    $results += [PSCustomObject]@{
        Pattern = $pattern
        V1Runtime = $v1Result.TotalSeconds
        V2Runtime = $v2Result.TotalSeconds
        Gain = (($v1Result.TotalSeconds - $v2Result.TotalSeconds) / $v1Result.TotalSeconds) * 100
    }
}

$results | 
Format-Table Pattern,
    @{ Name = 'Version 1'; Expression = { "{0:N1}sec." -f $_.V1Runtime } },
    @{ Name = 'Version 2'; Expression = { "{0:N1}sec." -f $_.V2Runtime } },
    @{ Name = 'Gain'; Expression = { "{0:N1}%" -f $_.Gain } }
