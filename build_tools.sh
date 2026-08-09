#!/bin/bash
# Compiles the standalone Water Sort tools with Roslyn / Mono csc.
# Usage: ./build_tools.sh [sampler|all]
export MSYS_NO_PATHCONV=1

if [ -f "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/Roslyn/csc.exe" ]; then
  CSC="C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/Roslyn/csc.exe"
elif [ -f "C:/Program Files/Unity/Hub/Editor/2022.3.20f1/Editor/Data/MonoBleedingEdge/lib/mono/4.5/csc.exe" ]; then
  CSC="C:/Program Files/Unity/Hub/Editor/2022.3.20f1/Editor/Data/MonoBleedingEdge/lib/mono/4.5/csc.exe"
else
  CSC="csc"
fi

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd -W 2>/dev/null || pwd)"
ROOT="$SCRIPT_DIR"
NEWTONSOFT="$ROOT/Newtonsoft.Json.dll"
NETSTANDARD="C:/Windows/Microsoft.NET/Framework64/v4.0.30319/netstandard.dll"

SRC_GEN="Assets/WaterSort/Scripts/Data/LevelData.cs Assets/WaterSort/Scripts/Generator/WaterSortDifficulty.cs Assets/WaterSort/Scripts/Generator/WaterSortGeneratorEngine.cs Assets/WaterSort/Scripts/Generator/WaterSortSolver.cs"
SRC_TOOL="Assets/WaterSort/Scripts/Generator/LevelGeneratorApp.cs"
SRC_SELFTEST="Assets/WaterSort/Scripts/Testing/SelfTestRunner.cs"
SRC_FULLSUITE="Assets/WaterSort/Scripts/Testing/FullSuiteTestRunner.cs"

compile() {
  local name="$1"; shift
  local out="$ROOT/$name.exe"
  local refs=("-r:$NEWTONSOFT")
  if [ "$name" = "FullSuiteTest" ] && [ -f "$NETSTANDARD" ]; then
    refs+=("-r:$NETSTANDARD")
  fi
  echo "=== Compiling $name ==="
  "$CSC" -nologo -optimize+ -out:"$out" "${refs[@]}" "$@" 2>&1 | grep -v "^Microsoft" | head -40
  ls -la "$out" 2>/dev/null && echo "OK: $name"
}

case "$1" in
  sampler)
    compile DebugSampler $SRC_GEN DebugSampler.cs
    ;;
  all|*)
    compile GeneratorTool $SRC_GEN $SRC_TOOL
    compile SelfTest $SRC_GEN $SRC_SELFTEST
    compile FullSuiteTest $SRC_GEN $SRC_FULLSUITE
    compile DebugSampler $SRC_GEN DebugSampler.cs
    ;;
esac
