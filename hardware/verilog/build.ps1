# Usage: ./build.ps1 [build|run|waves|clean|all]

$compile = 'iverilog -g2012 -Wall -Wimplicit -Wportbind -Wtimescale -Wselect-range -Irtl/include -o out/scpu rtl/rom.v rtl/ram.v rtl/sevenseg_scan.v rtl/mmio_device.v rtl/scpu_core.v sim/scpu_tb.v'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$out  = Join-Path $root 'out'
if (-not (Test-Path $out)) { New-Item -ItemType Directory -Path $out | Out-Null }

function Build {
  Write-Host "[build] $compile"
  Invoke-Expression $compile
}

function Run {
  Build
  Write-Host "[run] vvp out\scpu"
  Push-Location $out
  & vvp .\scpu
  Pop-Location
}

function Waves {
  $vcd = Join-Path $out 'scpu.vcd'
  if (Test-Path $vcd) {
    Write-Host "[waves] gtkwave out\scpu.vcd"
    & gtkwave $vcd
  } else {
    Write-Host "[waves] out\scpu.vcd not found" -ForegroundColor Yellow
  }
}

function Clean {
  Remove-Item -Recurse -Force -ErrorAction SilentlyContinue $out
  Write-Host "[clean] removed out/"
}

$action = if ($args.Count -gt 0) { $args[0].ToLower() } else { 'all' }
switch ($action) {
  'build' { Build }
  'run'   { Run }
  'waves' { Waves }
  'clean' { Clean }
  default { Build; Run; Waves }
}
