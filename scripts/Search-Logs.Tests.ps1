$scriptPath = Join-Path $PSScriptRoot 'Search-Logs.ps1'

Describe 'Search-Logs' {

    BeforeEach {
        $script:tempDir = Join-Path ([System.IO.Path]::GetTempPath()) ([System.Guid]::NewGuid().ToString())
        New-Item -ItemType Directory -Path $script:tempDir | Out-Null
        $script:patternsJson = '[{"name":"error","regex":"ERROR","severity":"error"}]'
    }

    AfterEach {
        Remove-Item -Path $script:tempDir -Recurse -Force -ErrorAction SilentlyContinue
    }

    It 'finds a matching line and reports file, line number, and context' {
        $logFile = Join-Path $script:tempDir 'run.log'
        @('line before', 'ERROR Failed to place order', 'line after') | Set-Content -Path $logFile

        $json = & $scriptPath -LogDir $script:tempDir -DaysBack 1 -PatternsJson $script:patternsJson -ContextLines 1
        $result = $json | ConvertFrom-Json

        $result.issues.Count | Should Be 1
        $result.issues[0].pattern_name | Should Be 'error'
        $result.issues[0].severity | Should Be 'error'
        $result.issues[0].line_number | Should Be 2
        $result.issues[0].line | Should Match 'Failed to place order'
        $result.issues[0].context_before[0] | Should Be 'line before'
        $result.issues[0].context_after[0] | Should Be 'line after'
        $result.scan_errors.Count | Should Be 0
    }

    It 'returns no issues when no lines match' {
        $logFile = Join-Path $script:tempDir 'run.log'
        @('all good here') | Set-Content -Path $logFile

        $json = & $scriptPath -LogDir $script:tempDir -DaysBack 1 -PatternsJson $script:patternsJson
        $result = $json | ConvertFrom-Json

        $result.issues.Count | Should Be 0
        $result.scan_errors.Count | Should Be 0
    }

    It 'skips files older than DaysBack' {
        $logFile = Join-Path $script:tempDir 'old.log'
        @('ERROR should not be found') | Set-Content -Path $logFile
        (Get-Item $logFile).LastWriteTime = (Get-Date).AddDays(-10)

        $json = & $scriptPath -LogDir $script:tempDir -DaysBack 1 -PatternsJson $script:patternsJson
        $result = $json | ConvertFrom-Json

        $result.issues.Count | Should Be 0
    }

    It 'reports a scan error when the log directory does not exist' {
        $missingDir = Join-Path $script:tempDir 'does-not-exist'

        $json = & $scriptPath -LogDir $missingDir -DaysBack 1 -PatternsJson $script:patternsJson
        $result = $json | ConvertFrom-Json

        $result.issues.Count | Should Be 0
        $result.scan_errors.Count | Should Be 1
        $result.scan_errors[0] | Should Match 'Log directory not found'
    }

    It 'records a scan error and continues when PatternsJson is invalid' {
        $logFile = Join-Path $script:tempDir 'run.log'
        @('ERROR boom') | Set-Content -Path $logFile

        $json = & $scriptPath -LogDir $script:tempDir -DaysBack 1 -PatternsJson 'not-json'
        $result = $json | ConvertFrom-Json

        $result.issues.Count | Should Be 0
        $result.scan_errors.Count | Should Be 1
        $result.scan_errors[0] | Should Match 'Failed to parse PatternsJson'
    }

    It 'only scans files matching FileFilter' {
        @('ERROR in log') | Set-Content -Path (Join-Path $script:tempDir 'run.log')
        @('ERROR in txt') | Set-Content -Path (Join-Path $script:tempDir 'run.txt')

        $json = & $scriptPath -LogDir $script:tempDir -DaysBack 1 -FileFilter '*.log' -PatternsJson $script:patternsJson
        $result = $json | ConvertFrom-Json

        $result.issues.Count | Should Be 1
        $result.issues[0].file | Should Match 'run\.log$'
    }

    It 'recurses into subdirectories when -Recurse is set' {
        $subDir = Join-Path $script:tempDir 'sub'
        New-Item -ItemType Directory -Path $subDir | Out-Null
        @('ERROR nested') | Set-Content -Path (Join-Path $subDir 'nested.log')

        $json = & $scriptPath -LogDir $script:tempDir -DaysBack 1 -PatternsJson $script:patternsJson -Recurse
        $result = $json | ConvertFrom-Json

        $result.issues.Count | Should Be 1
    }

    It 'does not recurse into subdirectories by default' {
        $subDir = Join-Path $script:tempDir 'sub'
        New-Item -ItemType Directory -Path $subDir | Out-Null
        @('ERROR nested') | Set-Content -Path (Join-Path $subDir 'nested.log')

        $json = & $scriptPath -LogDir $script:tempDir -DaysBack 1 -PatternsJson $script:patternsJson
        $result = $json | ConvertFrom-Json

        $result.issues.Count | Should Be 0
    }

    It 'matches multiple patterns against the same file' {
        $logFile = Join-Path $script:tempDir 'run.log'
        @('ERROR one thing', 'WARN another thing') | Set-Content -Path $logFile
        $patterns = '[{"name":"error","regex":"ERROR","severity":"error"},{"name":"warn","regex":"WARN","severity":"warning"}]'

        $json = & $scriptPath -LogDir $script:tempDir -DaysBack 1 -PatternsJson $patterns
        $result = $json | ConvertFrom-Json

        $result.issues.Count | Should Be 2
        ($result.issues | ForEach-Object { $_.pattern_name }) -contains 'error' | Should Be $true
        ($result.issues | ForEach-Object { $_.pattern_name }) -contains 'warn' | Should Be $true
    }
}
