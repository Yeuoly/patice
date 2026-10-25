param(
    [int] $MusicId = 11820,
    [int] $Difficulty = 0,
    [string] $SwitchScript = 'D:\scripts\sdez.bat'
)

$ErrorActionPreference = 'Stop'
# The cabinet launcher forwards these arguments to Sinmai. AquaMai's existing
# StartupDirectToGame handles the guest and track setup; Patice opens its UI.
$env:PATICE_GAME_ARGS = "--music $MusicId --difficulty $Difficulty --autoplay None"
$env:PATICE_TEST = '1'
try {
    Start-Process cmd.exe -ArgumentList "/c `"$SwitchScript`""
}
finally {
    Remove-Item Env:PATICE_GAME_ARGS, Env:PATICE_TEST
}
