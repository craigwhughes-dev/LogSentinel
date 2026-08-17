<#
Scans one log directory for pattern matches within the last N days and emits a single
JSON object (issues + scan_errors) to stdout. PowerShell 5.1 compatible — no ?:, no ??.
Never touches Claude or writes report files; that's the C# orchestrator's job.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$LogDir,

    [int]$DaysBack = 1,

    [string]$FileFilter = '*.log',

    [switch]$Recurse,

    [Parameter(Mandatory = $true)]
    [string]$PatternsJson,

    [int]$ContextLines = 2
)

$ErrorActionPreference = 'Stop'

$issues = New-Object System.Collections.ArrayList
$scanErrors = New-Object System.Collections.ArrayList

try {
    $patterns = $PatternsJson | ConvertFrom-Json
}
catch {
    $scanErrors.Add("Failed to parse PatternsJson: $($_.Exception.Message)") | Out-Null
    $patterns = @()
}

if (-not (Test-Path -LiteralPath $LogDir -PathType Container)) {
    $scanErrors.Add("Log directory not found: $LogDir") | Out-Null
}
else {
    $cutoff = (Get-Date).AddDays(-1 * $DaysBack)

    $gciParams = @{
        Path        = $LogDir
        Filter      = $FileFilter
        File        = $true
        ErrorAction = 'Stop'
    }
    if ($Recurse) { $gciParams['Recurse'] = $true }

    try {
        $files = Get-ChildItem @gciParams | Where-Object { $_.LastWriteTime -ge $cutoff }
    }
    catch {
        $scanErrors.Add("Failed to enumerate files in ${LogDir}: $($_.Exception.Message)") | Out-Null
        $files = @()
    }

    foreach ($file in $files) {
        foreach ($pattern in $patterns) {
            try {
                $found = Select-String -LiteralPath $file.FullName -Pattern $pattern.regex -Context $ContextLines, $ContextLines -ErrorAction Stop
            }
            catch {
                $scanErrors.Add("Search failed on $($file.FullName) for pattern '$($pattern.name)': $($_.Exception.Message)") | Out-Null
                continue
            }

            foreach ($m in $found) {
                $before = @()
                if ($m.Context -and $m.Context.PreContext) { $before = @($m.Context.PreContext) }
                $after = @()
                if ($m.Context -and $m.Context.PostContext) { $after = @($m.Context.PostContext) }

                $issue = [ordered]@{
                    file            = $file.FullName
                    line_number     = $m.LineNumber
                    pattern_name    = $pattern.name
                    severity        = $pattern.severity
                    line            = $m.Line
                    context_before  = $before
                    context_after   = $after
                }
                $issues.Add($issue) | Out-Null
            }
        }
    }
}

$result = [ordered]@{
    issues      = @($issues)
    scan_errors = @($scanErrors)
}

$result | ConvertTo-Json -Depth 6
