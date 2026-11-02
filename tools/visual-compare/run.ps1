# FairyGUI for Godot - visual comparison: renders the same examples with FairyGUI-unity and with this port,
# then writes side-by-side images (Unity left, Godot right) and a difference score per pair.
#
#   pwsh tools/visual-compare/run.ps1                     # all steps
#   pwsh tools/visual-compare/run.ps1 -Steps godot,compose -Only Basics,Bag
#
# Steps: setup (copy FairyGUI-unity from _ref into a scratch Unity project), build (Unity batch build of a
# Windows player), unity (capture), godot (capture), compose. Output: _shots/{unity,godot,compare}.
param(
    [string[]]$Steps = @('setup', 'build', 'unity', 'godot', 'compose'),
    [string[]]$Only = @(),
    [double]$Wait = 3.0,
    # Run the Unity build in a normal editor window (for licenses without the headless/batchmode entitlement).
    [switch]$NoBatchmode,
    [string]$UnityExe = 'G:\unity\Editor\6000.4.9f1\Editor\Unity.exe',
    [string]$GodotExe = 'G:\software\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe'
)
$ErrorActionPreference = 'Stop'
# `pwsh -File run.ps1 -Steps a,b` passes "a,b" as one string: split it here.
$Steps = @($Steps | ForEach-Object { $_ -split ',' } | Where-Object { $_ })
$Only = @($Only | ForEach-Object { $_ -split ',' } | Where-Object { $_ })
$repo = (Resolve-Path "$PSScriptRoot\..\..").Path
$src = "$repo\_ref\FairyGUI-unity"
$proj = "$repo\_ref\unity-capture"
$player = "$proj\Build\Capture.exe"
$shots = "$repo\_shots"
$inv = [Globalization.CultureInfo]::InvariantCulture

# Example name (Godot DemoRegistry) -> Unity scene. Basics also captures each sub-demo.
$Examples = [ordered]@{
    'Basics' = 'Example 01 - Basics'; 'Bag' = 'Example 02 - Bag'; 'Cooldown' = 'Example 03 - CoolDown'
    'Emoji' = 'Example 04 - Emoji'; 'Joystick' = 'Example 05 - Joystick'; 'HeadBar' = 'Example 06 - HeadBar'
    'EmitNumbers' = 'Example 07 - EmitNumbers'; 'Transition' = 'Example 08 - Transition'; 'Extension' = 'Example 09 - Extension'
    'ModalWaiting' = 'Example 10 - ModalWaiting'; 'CutScene' = 'Example 11 - CutScene'; 'VirtualList' = 'Example 15 - VirtualList'
    'LoopList' = 'Example 16 - LoopList'; 'Guide' = 'Example 18 - Guide'; 'HitTest' = 'Example 19 - HitTest'
    'Curve' = 'Example 21 - Curve UI'; 'Filter' = 'Example 23 - Filter'; 'TurnCard' = 'Example 24 - TurnCard'
    'TreeView' = 'Example 25 - TreeView'; 'PullToRefresh' = 'Example 26 - PullToRefresh'; 'ScrollPane' = 'Example 27 - ScrollPane'
    'TypingEffect' = 'Example 28 - TypingEffect'
}
$names = @($Examples.Keys | Where-Object { $Only.Count -eq 0 -or $Only -contains $_ })

function Q([string]$s) { '"' + $s + '"' }

function Run([string]$exe, [string[]]$argv, [int]$timeoutSec) {
    $p = Start-Process -FilePath $exe -ArgumentList ($argv -join ' ') -PassThru -WindowStyle Normal
    $null = $p.Handle  # keeps ExitCode available after exit
    if (-not $p.WaitForExit($timeoutSec * 1000)) {
        Stop-Process -Id $p.Id -Force
        Write-Warning "timeout after ${timeoutSec}s: $exe"
        return -1
    }
    return $p.ExitCode
}

if ($Steps -contains 'setup') {
    New-Item -ItemType Directory -Force $proj | Out-Null
    foreach ($d in 'Assets', 'Packages', 'ProjectSettings') {
        robocopy "$src\$d" "$proj\$d" /MIR /NFL /NDL /NJH /NJS /NP | Out-Null
        if ($LASTEXITCODE -ge 8) { throw "robocopy $d failed ($LASTEXITCODE)" }
    }
    # Unity 6 ships TextMeshPro inside ugui 2.x; drop the 1.x package and the TMP define (no TMP example is captured).
    $m = Get-Content "$proj\Packages\manifest.json" -Raw | ConvertFrom-Json -AsHashtable
    $m.dependencies.Remove('com.unity.textmeshpro')
    $m.dependencies['com.unity.ugui'] = '2.0.0'
    $m | ConvertTo-Json -Depth 5 | Set-Content "$proj\Packages\manifest.json"
    Remove-Item "$proj\Packages\packages-lock.json" -ErrorAction SilentlyContinue
    $ps = "$proj\ProjectSettings\ProjectSettings.asset"
    (Get-Content $ps -Raw).Replace('Standalone: FAIRYGUI_TMPRO', 'Standalone: FAIRYGUI_CAPTURE') | Set-Content $ps -NoNewline
    Remove-Item -Recurse -Force "$proj\Assets\Examples\TextMeshPro", "$proj\Assets\Scripts\Extensions\TextMeshPro" -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Force "$proj\Assets\Examples\Capture" | Out-Null
    Copy-Item "$PSScriptRoot\unity\UnityCaptureRunner.cs" "$proj\Assets\Examples\Capture\" -Force
    Copy-Item "$PSScriptRoot\unity\CaptureBuild.cs" "$proj\Assets\Editor\" -Force
    Write-Host "setup: $proj"
}

if ($Steps -contains 'build') {
    $log = "$proj\build.log"
    $argv = @('-quit', '-projectPath', (Q $proj), '-buildTarget', 'Win64',
        '-executeMethod', 'CaptureBuild.Build', '-captureBuildOut', (Q $player), '-logFile', (Q $log))
    if (-not $NoBatchmode) { $argv = @('-batchmode') + $argv }
    $code = Run $UnityExe $argv 5400
    if ($code -ne 0 -or -not (Test-Path $player)) {
        Get-Content $log -Tail 40 -ErrorAction SilentlyContinue
        throw "Unity build failed (exit $code), see $log"
    }
    Write-Host "build: $player"
}

if ($Steps -contains 'unity') {
    New-Item -ItemType Directory -Force "$shots\unity" | Out-Null
    foreach ($n in $names) {
        $argv = @('-screen-fullscreen', '0', '-screen-width', '1136', '-screen-height', '640', '-logFile', (Q "$shots\unity\$n.log"),
            '-captureScene', (Q $Examples[$n]), '-captureName', $n, '-captureOut', (Q "$shots\unity"), '-captureWait', $Wait.ToString($inv))
        if ($n -eq 'Basics') { $argv += @('-captureSubdemos', '1') }
        $code = Run $player $argv 180
        Write-Host ("unity  {0,-14} exit {1}" -f $n, $code)
    }
}

if ($Steps -contains 'godot') {
    New-Item -ItemType Directory -Force "$shots\godot" | Out-Null
    foreach ($n in $names) {
        $argv = @('--path', (Q $repo), '--resolution', '1136x640', '--position', '40,40', 'res://tests/CaptureRunner.tscn', '--',
            "--example=$n", ('--out=' + (Q "$shots\godot")), ('--wait=' + $Wait.ToString($inv)))
        if ($n -eq 'Basics') { $argv += '--subdemos' }
        $code = Run $GodotExe $argv 180
        Write-Host ("godot  {0,-14} exit {1}" -f $n, $code)
    }
}

if ($Steps -contains 'compose') {
    & "$PSScriptRoot\compose.ps1" -Shots $shots
}
