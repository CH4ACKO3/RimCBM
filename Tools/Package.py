"""Build an allowlisted mod archive and a hash manifest; never publish anything."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import shutil
import subprocess
import xml.etree.ElementTree as ET
import zipfile

ROOT = Path(__file__).resolve().parents[1]


def sha256(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build(tag):
    version = ET.parse(ROOT / "About/About.xml").getroot().findtext("modVersion")
    if not re.fullmatch(r"\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?", version or ""):
        raise ValueError("Invalid modVersion")
    if tag and tag != f"v{version}":
        raise ValueError(f"Tag must match v{version}")
    if not (ROOT / "Assemblies/RimCBM.dll").is_file():
        raise ValueError("Build RimCBM before packaging")
    out = ROOT / "artifacts/ci"
    stage = out / "RimCBM"
    # Only replace this script's own staging tree after checking the resolved boundary.
    if not stage.resolve().is_relative_to((ROOT / "artifacts").resolve()):
        raise ValueError("Unsafe staging path")
    if stage.exists():
        shutil.rmtree(stage)
    stage.mkdir(parents=True)
    for folder in ("About", "Defs", "Languages", "Patches", "Textures"):
        for source in sorted((ROOT / folder).rglob("*")):
            if source.is_file() and source.suffix.lower() in {".xml", ".png", ".jpg"}:
                dest = stage / source.relative_to(ROOT)
                dest.parent.mkdir(parents=True, exist_ok=True)
                shutil.copyfile(source, dest)
    for name in ("Assemblies/RimCBM.dll", "README.md", "README.zh-CN.md", "LICENSE", "THIRD_PARTY_NOTICES.md"):
        dest = stage / name
        dest.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(ROOT / name, dest)
    archive = out / f"RimCBM-{version}.zip"
    with zipfile.ZipFile(archive, "w", zipfile.ZIP_DEFLATED) as z:
        for file in sorted(stage.rglob("*")):
            if file.is_file():
                z.write(file, "RimCBM/" + file.relative_to(stage).as_posix())
    payload = []
    for language in ("en", "zh-CN"):
        for source, dest in (
            (ROOT / f"Docs/workshop/Description.{language}.bbcode", out / f"Description.{language}.bbcode"),
            (ROOT / f"Docs/releases/{version}.{language}.md", out / f"release-notes.{language}.md"),
        ):
            shutil.copyfile(source, dest)
            payload.append({"path": dest.name, "sha256": sha256(dest)})
    notes = out / "release-notes.md"
    notes.write_text("\n".join((out / f"release-notes.{lang}.md").read_text(encoding="utf-8") for lang in ("en", "zh-CN")), encoding="utf-8")
    payload.append({"path": notes.name, "sha256": sha256(notes)})
    commit = subprocess.run(["git", "rev-parse", "--verify", "HEAD"], cwd=ROOT, capture_output=True, text=True)
    manifest = {
        "version": version, "tag": tag, "commit": commit.stdout.strip() if commit.returncode == 0 else "uncommitted",
        "archive": archive.name, "archiveSha256": sha256(archive), "publishedFileId": None,
        "files": [{"path": f.relative_to(stage).as_posix(), "sha256": sha256(f)} for f in sorted(stage.rglob("*")) if f.is_file()],
        "payload": payload,
    }
    (out / "manifest.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    print(f"Packaged {len(manifest['files'])} files: {archive}")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--tag", default="")
    build(parser.parse_args().tag)
