param(
    [int] $MusicId = 11820,
    [int] $Difficulty = 0,
    [string] $SwitchScript = 'D:\scripts\sdez.bat',
    [string] $GameDirectory = 'D:\sdez170\Package'
)

$ErrorActionPreference = 'Stop'
# The cabinet launcher forwards these arguments to Sinmai. AquaMai's existing
# StartupDirectToGame handles the guest and track setup; Patice opens its UI.
$env:PATICE_GAME_ARGS = "--music $MusicId --difficulty $Difficulty --autoplay None"
$env:PATICE_TEST = '1'
$started = Get-Date
try {
    Start-Process cmd.exe -ArgumentList "/c `"$SwitchScript`""
}
finally {
    Remove-Item Env:PATICE_GAME_ARGS, Env:PATICE_TEST
}

$log = Join-Path $GameDirectory 'MelonLoader\Latest.log'
while ((Get-Date) -lt $started.AddMinutes(3)) {
    if ((Test-Path $log) -and (Get-Item $log).LastWriteTime -gt $started) {
        $tail = Get-Content $log -Tail 30
        if ($tail -match 'Patice selector opened\.') {
            Write-Output ("Practice ready in {0:N1}s" -f ((Get-Date) - $started).TotalSeconds)
            exit 0
        }
        if ($tail -match '\[Patice\].*(Exception|Error)') {
            throw ($tail -join "`n")
        }
    }
    Start-Sleep -Milliseconds 500
}
throw 'The practice selector did not open within three minutes.'
