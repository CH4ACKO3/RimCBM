"""Validate repository data or a built release payload without loading the game."""
import argparse
from collections import Counter
import hashlib
import json
from pathlib import Path, PurePosixPath
import re
import xml.etree.ElementTree as ET
import zipfile

ROOT = Path(__file__).resolve().parents[1]


def require(condition, message):
    if not condition:
        raise ValueError(message)


def repository():
    for folder in ("About", "Defs", "Languages", "Patches"):
        for path in (ROOT / folder).rglob("*.xml"):
            ET.parse(path)
    projects, names, item_owners = [], [], []
    for path in (ROOT / "Defs").rglob("*.xml"):
        for node in ET.parse(path).getroot():
            if node.findtext("defName"):
                names.append((node.tag, node.findtext("defName")))
            if node.tag == "ResearchProjectDef":
                projects.append(node)
            for ext in node.findall("modExtensions/li"):
                if ext.get("Class") == "RimCBM.CBMExtension":
                    item_owners.append(ext.findtext("item"))
    for path in (ROOT / "Defs").rglob("*.xml"):
        for node in ET.parse(path).getroot():
            if node.tag == "WorkGiverDef":
                require(node.findtext("giverClass"), f"Missing WorkGiver giverClass: {node.findtext('defName')}")
            texture = node.findtext("graphicData/texPath", "")
            if "/RimCBM/" in texture:
                image = ROOT / "Textures" / (texture + ".png")
                require(image.is_file(), f"Missing texture: {texture}")
                header = image.read_bytes()[:26]
                require(header[:8] == b"\x89PNG\r\n\x1a\n", f"Not PNG: {image}")
                require(int.from_bytes(header[16:20], "big") % 4 == 0 and int.from_bytes(header[20:24], "big") % 4 == 0,
                        f"Texture dimensions must be multiples of four: {image}")
                require(header[25] == 6, f"Equipment texture must preserve RGBA: {image}")
    require(len(names) == len(set(names)), "Duplicate DefNames within a Def type")
    require(all(item_owners) and len(item_owners) == len(set(item_owners)), "Ambiguous implant item identity")
    require(projects and all(p.findtext("tab") == "RimCBM" for p in projects), "All implant research must use the RimCBM tab")
    keys = {}
    for language in ("English", "ChineseSimplified"):
        entries = []
        for path in (ROOT / "Languages" / language / "Keyed").glob("*.xml"):
            entries.extend(n.tag for n in ET.parse(path).getroot())
        duplicates = [k for k, count in Counter(entries).items() if count > 1]
        require(not duplicates, f"Duplicate {language} translation keys: {duplicates}")
        keys[language] = set(entries)
    require(keys["English"] == keys["ChineseSimplified"], "English/Chinese keyed translations differ")
    for lang in ("en", "zh-CN"):
        text = (ROOT / f"Docs/workshop/Description.{lang}.bbcode").read_text(encoding="utf-8")
        require(text.strip() and len(text.encode("utf-8")) < 8000, f"Workshop description empty or oversized: {lang}")
        stack = []
        for slash, tag in re.findall(r"\[(/?)(h1|h2|b|i|list|url)(?:=[^\]]+)?\]", text):
            if slash:
                require(stack and stack.pop() == tag, f"Unbalanced Workshop BBCode: {lang}")
            else:
                stack.append(tag)
        require(not stack, f"Unclosed Workshop BBCode: {lang}")
    metadata = json.loads((ROOT / "Docs/workshop/metadata.json").read_text(encoding="utf-8"))
    require(metadata["appId"] == 294100 and metadata["publishedFileId"] is None, "Unexpected Workshop app/item identity")
    missing = []
    for path in list(ROOT.glob("*.md")) + list((ROOT / "Docs").rglob("*.md")):
        if "archive" in path.parts:
            continue  # Historical evidence includes deliberately external/local artifact links.
        text = re.sub(r"```.*?```", "", path.read_text(encoding="utf-8"), flags=re.S)
        for target in re.findall(r"\]\(([^)]+)\)", text):
            target = target.strip("<>").split("#")[0]
            if not target or re.match(r"[a-zA-Z]+:", target):
                continue
            if not (path.parent / target).exists():
                missing.append(f"{path.relative_to(ROOT)} -> {target}")
    require(not missing, "Broken documentation links:\n" + "\n".join(missing))
    print(f"Validated XML, {len(names)} definitions, translations, research, identity and publishing documents.")


def package(directory):
    directory = directory.resolve()
    manifest = json.loads((directory / "manifest.json").read_text(encoding="utf-8"))

    def member(base, name):
        path = (base / name).resolve()
        require(path.is_relative_to(base), f"Unsafe manifest path: {name}")
        return path

    archive = member(directory, manifest["archive"])
    require(hashlib.sha256(archive.read_bytes()).hexdigest() == manifest["archiveSha256"], "ZIP checksum mismatch")
    expected = {"RimCBM/" + row["path"]: row["sha256"] for row in manifest["files"]}
    require(len(expected) == len(manifest["files"]), "Duplicate manifest file")
    required = {"RimCBM/About/About.xml", "RimCBM/Assemblies/RimCBM.dll", "RimCBM/LICENSE"}
    with zipfile.ZipFile(archive) as z:
        require(len(z.namelist()) == len(set(z.namelist())), "Duplicate ZIP member")
        require(set(z.namelist()) == set(expected) and required <= set(expected), "ZIP entries differ from manifest")
        for name, digest in expected.items():
            path = PurePosixPath(name)
            require(not path.is_absolute() and ".." not in path.parts, "Unsafe archive path")
            relative = path.relative_to("RimCBM")
            allowed = (len(relative.parts) == 1 and relative.name in {"README.md", "README.zh-CN.md", "LICENSE", "THIRD_PARTY_NOTICES.md"}
                       or str(relative) == "Assemblies/RimCBM.dll"
                       or relative.parts[0] in {"About", "Defs", "Patches", "Languages", "Textures"} and relative.suffix in {".xml", ".png", ".jpg"})
            require(allowed, f"Unexpected release content: {name}")
            require(hashlib.sha256(z.read(name)).hexdigest() == digest, f"ZIP entry hash mismatch: {name}")
            staged = member(directory / "RimCBM", str(relative))
            require(hashlib.sha256(staged.read_bytes()).hexdigest() == digest, f"Stage differs from ZIP: {name}")
    for row in manifest["payload"]:
        require(hashlib.sha256(member(directory, row["path"]).read_bytes()).hexdigest() == row["sha256"], f"Payload hash mismatch: {row['path']}")
    print(f"Verified {len(expected)} packaged files, staged content, ZIP and publishing payload hashes.")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--package", type=Path)
    args = parser.parse_args()
    package(args.package) if args.package else repository()
