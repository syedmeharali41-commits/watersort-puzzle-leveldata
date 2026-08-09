# Water Sort Puzzle — 10,000 Level Hard Redesign

A high-performance Unity 2022.3 Water Sort Puzzle implementation featuring a deterministic, solver-verified 10,000 level generation and validation engine.

## Overview & Architecture

This repository contains the complete Unity game source code, level solver/generator tools, PRD specifications, and automated test runners.

- **Unity Project**: `Assets/WaterSort/` (Game logic, Visuals, UI, GameManager)
- **Generator Engine**: `Assets/WaterSort/Scripts/Generator/` (A* Solver, IDA*, BFS Floor Proofs, Difficulty Profile)
- **Standalone .NET Tool**: `WaterSortGeneratorTool.csproj`
- **Automated Workflow**: `.github/workflows/generate-and-validate-levels.yml`

## Difficulty Scaling (10k Redesign)

- **Color Count ($K$)**: 3 flat for L1-50, scales up to 22.
- **Tube Count ($N$)**: $K+3$ (L1-500), $K+2$ (L501+).
- **Capacity ($C$)**: 4 (1-2000), 5 (2001-4500), 6 (4501-7000), 7 (7001-9000), 8 (9001-10000).
- **Validation**: Every level is solver-verified against its minimum move count and a self-enforcing 15%-per-checkpoint relative difficulty growth rule.
- **Locked Tubes**: Expert (L5001-7500) and World-Class (L7501-10000) bands feature solver-safe locked tube mechanics.

## Running the Level Generator Locally

```bash
# Build standalone CLI tool
dotnet build WaterSortGeneratorTool.csproj -c Release

# Run generator for levels 1 to 250
dotnet run --project WaterSortGeneratorTool.csproj -c Release -- 250 --parallel 4

# Run matrix chunk slice
dotnet run --project WaterSortGeneratorTool.csproj -c Release -- --start 1 --end 500 --out chunk_1.json

# Merge chunks and execute 15% checkpoint verification
dotnet run --project WaterSortGeneratorTool.csproj -c Release -- 10000 --merge chunks_dir/ --out levels.json
```

## GitHub Actions Automated Generation

Level generation can be triggered manually via `workflow_dispatch` in GitHub Actions:

1. Go to **Actions** tab in GitHub.
2. Select **Generate and Validate Water Sort Levels**.
3. Set `total_levels` (e.g. `250` for small test, `10000` for full run).
4. Click **Run workflow**.
5. Download the final validated `levels-json-bundle` artifact upon completion.
