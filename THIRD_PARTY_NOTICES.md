# Third-party notices

RimCBM's own code and documentation are licensed under MIT (see LICENSE).

- **RimWorld / Unity**: proprietary runtime software. The source build references game APIs; no game runtime DLLs or decompiled game sources belong in this repository or release archive. XML may refer to assets supplied by the player's installed game.
- **Harmony**: required separately by the player. The build uses Lib.Harmony reference metadata; 0Harmony.dll is not included in our release ZIP.
- **Krafs.Rimworld.Ref**: pinned NuGet reference package used only to compile on CI. Its assemblies are not playable game binaries and are not shipped in the mod.
- **Cataclysm: Dark Days Ahead**: gameplay inspiration. RimCBM is an independent adaptation, not an official CDDA project or a redistribution of its full code/assets.
- **Search and Rescue**: this repository's CI/release conventions were adapted from the author's MIT-licensed sibling project.

Third-party names, trademarks and runtime assets retain their respective ownership and licensing.
