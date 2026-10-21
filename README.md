# 🌻 ProsperCraft Sunflower Stability

**A little sunshine. A steadier home.**

Plant a sunflower in a large planter to stabilize your home, workshop, or favorite
gathering place. Each planter reaches **10 blocks in every direction**, including
above and below. Temporal storms remain normal.

Built and tested for **Vintage Story 1.22.7**. Install on **both server and client**.

## Install

1. Download `prospersunflower_1.0.1.zip` from the [latest release](https://github.com/ProsperCraft/SunflowerStability/releases/latest).
2. Place the ZIP in your Vintage Story `Mods` directory without unpacking it.
3. For multiplayer, install the same version in the server's `Mods` directory and
   on every client. Replace older versions; keep only one copy.
4. Restart the game and server after installation or updating.

A GitHub release alone does not make the mod available through Vintage Story's
Mod DB auto-downloader. Install manually if your server cannot download it from
Mod DB.

## Getting started

1. Place a **fired large planter**.
2. Hold a **sunflower seed packet** (`game:seeds-sunflower`) and right-click it.
3. Stay nearby and let your temporal stability recover naturally.

The planter displays a decorative sunflower. Small flowerpots, empty
sunflower-colored ceramics, and seeds stored in chests do not activate the effect.
Vanilla large planters and compatible modded large planters using the native
plant-container class are supported; Bricklayers planters have been tested.
Existing planted sunflowers continue working after updating.

## Coverage and recovery

A planter at `(x, y, z)` covers whole blocks from `x−10` through `x+10`,
`y−10` through `y+10`, and `z−10` through `z+10`, inclusive. That is a
**21 × 21 × 21 block cube**, including its corners and the planter's own block.

Within the cube, environmental temporal stability is raised to at least `1.5`,
the vanilla maximum. Your personal gear recovers naturally over time; it is not
instantly refilled. Higher stability supplied by another mod is preserved.

Additional planters extend coverage. Overlapping areas do not increase recovery
speed. Emptying or breaking a planter removes its effect. Coverage applies in the
normal world while the planter's chunk is loaded.

## Temporal storms

**Protection pauses during active temporal storms.** Native storm stability,
timing, visual effects, and mob spawning remain unchanged. Stabilization resumes
afterward. Existing mobs are not removed.

## World compatibility

The mod uses existing native planter blocks and sunflower seed items. It adds no
block or item IDs and stores no additional world data. Removing the mod leaves
ordinary native planters and seeds; the decorative sunflower rendering requires
this mod's planting attributes.

Only loaded planter block entities are indexed. Stability queries check nearby
spatial buckets and live contents, without scanning world blocks each tick or
force-loading chunks. Break, removal, and chunk-unload callbacks unregister the
planter.

## Build from source

Requirements: .NET 10 SDK and your own Vintage Story 1.22.7 installation, including
`VintagestoryAPI.dll`, `VintagestoryLib.dll`, `Lib/0Harmony.dll`,
`Lib/Newtonsoft.Json.dll`, and `Mods/VSSurvivalMod.dll`.

Set `VINTAGE_STORY` to the installation directory:

```bash
export VINTAGE_STORY="/path/to/vintagestory"
dotnet build ProsperSunflower.csproj -c Release
dotnet run --project tests/Tests.csproj -c Release
```

PowerShell:

```powershell
$env:VINTAGE_STORY = "C:\Path\To\Vintagestory"
dotnet build ProsperSunflower.csproj -c Release
dotnet run --project tests/Tests.csproj -c Release
```

Builds also accept `-p:VSDir="/path/to/vintagestory"`; the test executable still
needs `VINTAGE_STORY` to find game assemblies at runtime. Game assemblies are
referenced locally and are not included in the repository or mod download.

To package a build, place just these two files at the ZIP root:

- `bin/Release/net10.0/ProsperSunflower.dll`
- `modinfo.json`

## Validation

Version 1.0.1 passed **19,193 offline checks** against installed Vintage Story
1.22.7 assemblies: all 9,261 covered cells, cube faces and corners, positive and
negative spatial buckets, independent full-scan comparison, changing contents,
non-stacking overlaps, native planter/rendering properties, real behavior
break/removal/unload callbacks, and Harmony queries during normal and storm
conditions.

Live multiplayer testing confirmed sunflower rendering and immediate recovery
in an unstable keep with 1.0.0. Version 1.0.1 expands that original coverage and
has loaded successfully on the server; its expanded range has offline coverage
checks. This is not a guarantee of compatibility with every modpack.

Report problems using [GitHub Issues](https://github.com/ProsperCraft/SunflowerStability/issues).
Include game/mod versions, whether the problem occurs in singleplayer or
multiplayer, the planter type, and relevant log excerpts with private details
removed.
