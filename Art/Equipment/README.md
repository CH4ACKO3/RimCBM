# Equipment artwork

The runtime textures are the transparent PNGs in `Textures/Things/Building/RimCBM` and `Textures/Things/Item/RimCBM`. Three building masters in `Source/` were created for this project with OpenAI image generation. No RimWorld or Undead People pixels are included. The small sterilization pouch and early equipment studies are original SVG drawings.

The final building directions use overhead silhouettes, quiet grey/sage surfaces, dark outer contours, and restrained internal detail. Equipment exports use 128 pixels per map tile (256×256 or 384×256), the pouch 128×128. The mod uses these exported images, not the full-resolution masters. Preview.png shows the runtime export scale against a neutral ground.

References consulted:

- [RimWorld texture guide](https://rimworldwiki.com/wiki/Modding_Tutorials/Textures): transparency, outline weights and tile resolution. This community page is marked under review.
- [Officially unofficial artstyle guide](https://github.com/spdskatr/RWModdingResources/blob/master/artstyle.md): simple silhouettes, minimal visual noise and restrained interior lines.
- [Undead People autoclave sprite](https://github.com/Goodgulf-PL/UndeadPeopleTileset_Unpacked_New_Version/blob/main/dda/gfx/MSX%2B%2BUnDeadPeopleEdition_v2/pngs_normal_furniture_32x32/medical/f_autoclave/f_autoclave.png): recognition cue of a metal pressure vessel and locking hatch. The RimCBM version uses a top-loading hatch suited to the game camera.

To export with Node.js and the `sharp` package available, run `node Tools/RenderEquipment.cjs`. Set SHARP_MODULE to an absolute installed sharp module path if necessary. The exporter retains alpha and only resizes assets; source masters are untouched. Art tools are optional developer utilities; CI and players use committed PNGs and do not require an image-generation account.

`Tools/DrawEquipment.py` records the early SVG studies. Those studies are retained as editable concept sources and are not the final building textures; run RenderEquipment.cjs after it to restore the final preview/export.
