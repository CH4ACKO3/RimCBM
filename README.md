# RimCBM

RimWorld 1.6 · Alpha · [简体中文](README.zh-CN.md) · [MIT](LICENSE)

Plan implants around body space, power and maintenance. Inspired by the CBM concept in **Cataclysm: Dark Days Ahead**, RimCBM adapts it into a grid-based implant system for RimWorld.

- **Body grids:** fit shaped modules into body regions; use bionics and expansion modules to gain space.
- **Surgery and hot swapping:** surgery prepares implants; deployment and rearrangement require pawn actions. Hot swapping requires support from both the module and its occupied cells.
- **Shared energy:** manage W / Wd storage, generation, fuel, dedicated charging stations and automatic replenishment.
- **Abilities and damage:** passive, toggle, one-shot and reactive abilities; durability, contamination, sterilization, EMP and configurable faults.
- **Native controls:** an expandable health panel, one control Gizmo and context-sensitive right-click commands. Research has its own **Implants** tab.

## Install and play

Requires **RimWorld 1.6** and [Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077). Supports English and 简体中文. No DLC is required for the core prototype.

Download a package from [GitHub Releases](https://github.com/CH4ACKO3/RimCBM/releases), when available, or build from source. Put the package's `RimCBM` folder under `RimWorld/Mods`, then enable Harmony before RimCBM. Git checkouts do not contain the compiled DLL.

Open a pawn's Health tab and click **CBM** above the detailed health list. Schedule installation with **+**, then drag the prepared implant onto a valid grid. The pawn completes the placement action. Right-click implants for removal operations; use the Gizmo for abilities and supply policies.

## Development status

The framework is functional, but the implant catalogue, research progression, artwork and balance are prototypes. Some modules are developer fixtures and do not appear in ordinary trader stock. This is not a finished port of CDDA's full implant catalogue. Combat Extended and third-party race/electricity integrations are not certified.

[Gameplay and extension documentation](https://github.com/CH4ACKO3/RimCBM/blob/main/Docs/README.md) · [Changelog](https://github.com/CH4ACKO3/RimCBM/blob/main/CHANGELOG.md) · [Report a bug](https://github.com/CH4ACKO3/RimCBM/issues)

## Build

With .NET 8 SDK, Python 3.11+ and PowerShell 7:

```powershell
./Tools/CI/Build.ps1
```

CI restores locked reference packages, validates generated XML and translations, builds the mod and engine probe, runs the independent rule tests, and produces a checked release ZIP under `artifacts/ci`. No installed game or Steam credentials are needed for this build; engine tests require your own game installation.

See [development and testing](https://github.com/CH4ACKO3/RimCBM/blob/main/Docs/Development.md) for local references and engine checks, and [release preparation](https://github.com/CH4ACKO3/RimCBM/blob/main/Docs/Release.md) for Workshop materials.

## Credits

Independent implementation inspired by CDDA's gameplay. RimWorld, Harmony and CDDA remain the work of their respective authors. Game assemblies and reference-package binaries are not distributed in the mod package. See [third-party notices](THIRD_PARTY_NOTICES.md).
