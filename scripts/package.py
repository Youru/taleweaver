"""Builds what is given away: one executable per system, with the front and the content inside.

    python3 -I taleweaver/scripts/package.py [win-x64] [linux-x64] [--scenario scenarios/homebrew/x.json] [--content DIR]
    python3 -I taleweaver/scripts/package.py --content-pack middara      # the content alone: dist/middara.taleweaver.zip

--content: the folder holding packs/, scenarios/, assets/ (and reference/rules/rulebook/). By default
TALEWEAVER_CONTENT_ROOT, or the repository around taleweaver/. The CI gives taleweaver/demo: nothing of the game.

Out: taleweaver/dist/ (git ignores it). Needs the .NET SDK, Node and Pillow.
The content is a whitelist: nothing of sources/, tools/, the saves or the workshop's scenarios leaves unless named.
The pictures are Succubus Publishing's: give the result to people, never to a public place (docs/design/distribution-audit.md).
"""

import argparse
import datetime
import io
import json
import os
import re
import shutil
import subprocess
import tarfile
import tempfile
import zipfile
from collections import Counter
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path

from PIL import Image

APP = Path(__file__).resolve().parents[1]
ROOT = APP.parent  # the content: replaced by --content
WEB = APP / "web"
API = APP / "src" / "Taleweaver.Api"
OUT = APP / "dist"
VERSION = os.environ.get("TALEWEAVER_VERSION", "0.1.0").removeprefix("v")  # the CI gives the tag
QUALITY = 82
SYSTEMS = ("win-x64", "linux-x64")

PICTURE = re.compile(r"assets/[^\"'`\s)<>]+")
ENCOUNTER = "assets/templates/encounter/"
# Named by the code piece by piece, so taken whole
WHOLE = ("assets/templates/discipline-card/icons/", "assets/rulebook/icons/")

DEMO_NOTICE = """TALEWEAVER {version}

Compagnon pour jeux de plateau d'aventure, avec un pack et un scénario de démonstration.
Lancer : double-cliquez sur Taleweaver.exe (Windows) ; tar -xzf puis ./Taleweaver (Linux). Le navigateur s'ouvre.
Windows peut afficher « Windows a protégé votre ordinateur » (fichier non signé) : Informations complémentaires,
puis Exécuter quand même. Gardez la fenêtre noire ouverte pendant la partie ; fermez-la pour quitter.
"""

NOTICE = """TALEWEAVER {version}

Compagnon non officiel de Middara: Unintentional Malum. Les textes et les images du jeu appartiennent à
Succubus Publishing et sont utilisés avec l'accord du créateur, sans aucun usage commercial.
Ne revendez pas ce fichier et ne le mettez pas en ligne.
ATTENTION, SPOILERS : le fichier contient des cartes cachées et des aventures. Ne fouillez pas son contenu.

LANCER
  Windows : double-cliquez sur Taleweaver.exe. Si Windows affiche « Windows a protégé votre ordinateur »,
            c'est que le fichier n'est pas signé : Informations complémentaires, puis Exécuter quand même.
  Linux   : tar -xzf Taleweaver-linux-x64.tar.gz, puis ./Taleweaver dans un terminal.
  Le premier lancement prend un peu de temps. Votre navigateur s'ouvre tout seul.
  Gardez la fenêtre noire ouverte pendant la partie ; fermez-la pour quitter.

TÉLÉPHONES ET TABLETTE
  Même Wi-Fi que le PC. Acceptez la demande du pare-feu pour les réseaux privés.
  Dans la partie, bouton Room : les joueurs scannent le QR code. Bouton Écrans : un QR code pour mener
  depuis une tablette (Android, iPad, Mac...), un autre pour montrer la table aux joueurs.
  Ce PC doit rester allumé. Wi-Fi de confiance seulement : les échanges ne sont pas chiffrés.
  Sur le PC du MJ, gardez l'adresse localhost : c'est elle qui fait de vous le MJ.

METTRE À JOUR : remplacez le fichier. Parties et scénarios sont gardés.
DONNÉES : Windows %APPDATA%\\Middara ; Linux ~/.config/Middara (dossiers saves et scenarios).
DÉSINSTALLER : supprimez le fichier et le dossier app de %LOCALAPPDATA%\\Middara (~/.local/share/Middara),
               puis les données ci-dessus si vous ne tenez pas à vos parties.
PDF : il faut Edge, Chrome ou Chromium.

----

Unofficial companion for Middara: Unintentional Malum. The game's texts and pictures belong to Succubus
Publishing and are used with the creator's agreement, for no commercial use. Do not sell or upload this file.
SPOILERS: it holds hidden cards and adventures, do not browse its content.

START: run Taleweaver.exe (Windows: More info, then Run anyway) or ./Taleweaver (Linux, from a terminal).
       Keep the console window open while you play; close it to quit.
PHONES AND TABLET: same Wi-Fi, accept the firewall prompt, then the Room button of the game.
       Screens button: one QR code to run the game from a tablet (Android, iPad, Mac...), one to show the table.
       Keep this computer on, use a trusted Wi-Fi (nothing is encrypted).
       On the GM's computer, stay on localhost.
UPDATE: replace the file. Games and scenarios are kept.
PDF: needs Edge, Chrome or Chromium.
"""


def run(*command, cwd=APP):
    executable = shutil.which(command[0]) or command[0]
    subprocess.run([executable, *map(str, command[1:])], cwd=cwd, check=True)


def text_files(folder, *suffixes):
    return sorted(
        p
        for p in folder.rglob("*")
        if p.suffix in suffixes and p.is_file() and ".test." not in p.name and not {"bin", "obj"} & set(p.parts)
    )


def content_files(scenarios):
    # Every scenario folder but the workshop's (homebrew): those leave only when named
    official = [p for p in sorted((ROOT / "scenarios").iterdir()) if p.is_dir() and p.name != "homebrew"]
    book = ROOT / "reference" / "rules" / "rulebook"
    files = [
        *text_files(ROOT / "packs", ".json"),
        *(f for folder in official for f in text_files(folder, ".json")),
        *(text_files(book / "chapters", ".md") + [book / "glossary.md"] if (book / "glossary.md").exists() else []),
        # Its translations, line for line (reference/<lang>/rules/rulebook/chapters)
        *(f for lang in ("fr",) for f in text_files(ROOT / "reference" / lang / "rules" / "rulebook" / "chapters", ".md")),
    ]
    for given in scenarios:
        path = (ROOT / given).resolve()
        if ROOT / "scenarios" not in path.parents or not path.exists():
            raise SystemExit(f"Not a scenario of scenarios/: {given}")
        files += text_files(path, ".json") if path.is_dir() else [path]
    return files


def pictures(content):
    """What the content and the code name. A picture named but missing stops the build."""
    code = [*text_files(WEB / "src", ".ts", ".vue", ".css"), *text_files(API, ".cs")]
    everything = {p.relative_to(ROOT).as_posix() for p in (ROOT / "assets").rglob("*") if p.is_file()}
    text = "\n".join(p.read_text(encoding="utf-8") for p in [*content, *code])
    named = set(PICTURE.findall(text))

    concrete = {n for n in named if re.search(r"\.(png|jpe?g|webp|svg)$", n) and not set("*${}…") & set(n)}
    examples = set(PICTURE.findall("\n".join(
        line for p in code for line in p.read_text(encoding="utf-8").splitlines() if line.lstrip().startswith(("//", "/*", "*"))
    )))
    if missing := sorted(concrete - everything - examples):
        # The code names pictures of the game (icons, templates): another content does without them
        if is_game():
            raise SystemExit("Named but missing:\n  " + "\n  ".join(missing))
        print(f"{len(missing)} pictures named by the code, not in this content: left out")

    kept = named & everything
    for name in everything:
        if name.startswith(WHOLE):
            kept.add(name)
        elif name.startswith(ENCOUNTER):
            local = name[len(ENCOUNTER):]
            if local in text or local.startswith("print-background-"):
                kept.add(name)
    return sorted(kept)


def is_game():
    return (ROOT / "packs" / "um1" / "pack.json").exists()


def compressed(name, alone):
    """(name in the bundle, bytes): WebP when that is smaller and no other picture has the same name."""
    original = (ROOT / name).read_bytes()
    if not alone or not name.endswith((".png", ".jpg")):
        return name, original
    with Image.open(io.BytesIO(original)) as source:
        transparent = "A" in source.getbands() or "transparency" in source.info
        out = io.BytesIO()
        source.convert("RGBA" if transparent else "RGB").save(out, "WEBP", quality=QUALITY, method=5)
    if out.tell() >= len(original):
        return name, original
    return os.path.splitext(name)[0] + ".webp", out.getvalue()


def licences(systems, restored):
    parts = [f"Taleweaver {VERSION}: third-party software\n"]

    def add(title, folder):
        found = [p for p in sorted(folder.glob("*")) if p.is_file() and re.match(r"(licen[cs]e|notice|third-party)", p.name, re.I)]
        body = "\n\n".join(p.read_text(encoding="utf-8", errors="replace") for p in found)
        parts.append(f"{'=' * 78}\n{title}\n{'=' * 78}\n{body}\n")

    lock = json.loads((WEB / "package-lock.json").read_text(encoding="utf-8"))["packages"]
    for path, package in sorted(lock.items()):
        if path and not package.get("dev") and (WEB / path).is_dir():
            add(f"{path.split('node_modules/')[-1]} {package.get('version')} ({package.get('license', '?')})", WEB / path)

    assets = json.loads(restored.read_text(encoding="utf-8"))
    store = Path(next(iter(assets["packageFolders"])))
    for key, library in sorted(assets["libraries"].items()):
        if library["type"] != "package":
            continue
        specs = list((store / library["path"]).glob("*.nuspec"))
        spec = specs[0].read_text(encoding="utf-8-sig") if specs else ""
        licence = re.search(r"<license [^>]*>([^<]+)", spec)
        owner = re.search(r"<copyright>([^<]+)", spec)
        add(f"{key} ({licence[1] if licence else '?'}) {owner[1] if owner else ''}", store / library["path"])
    for system in systems:
        for runtime in ("netcore", "aspnetcore"):
            versions = store / f"microsoft.{runtime}.app.runtime.{system}"
            if versions.is_dir() and (builds := sorted(versions.iterdir(), key=lambda p: p.stat().st_mtime)):
                add(f"{versions.name} {builds[-1].name}", builds[-1])
    return "\n".join(parts)


def version():
    def git(*args, cwd=APP):
        return subprocess.run(["git", *args], cwd=cwd, capture_output=True, text=True, check=True).stdout.strip()

    changed = git("status", "--porcelain", "--", ".")
    if is_game() and not changed:
        changed = git("status", "--porcelain", "--", "packs", "scenarios", "assets", cwd=ROOT)
    changed = "-modified" if changed else ""
    return f"{VERSION}+{datetime.date.today():%Y%m%d}.{git('rev-parse', '--short', 'HEAD')}{changed}"


def content_with_pictures(scenarios):
    """(files, [(name in the bundle, bytes)]): the content and the pictures it names, WebP when smaller."""
    content = content_files(scenarios)
    names = pictures(content)
    stems = Counter(os.path.splitext(n)[0] for n in names)
    with ThreadPoolExecutor() as pool:
        images = list(pool.map(lambda n: compressed(n, stems[os.path.splitext(n)[0]] == 1), names))
    return content, images


def write_content(archive, content, images):
    for path in content:
        # The app does not read scenarios/homebrew as official: what is given goes in scenarios/shared
        archive.write(path, path.relative_to(ROOT).as_posix().replace("scenarios/homebrew/", "scenarios/shared/", 1))
    for name, data in images:
        archive.writestr(name, data, compress_type=zipfile.ZIP_STORED)


def build_pack(pack_id, scenarios):
    """The content alone, for an app published without it (Features/ContentPacks): <id>.taleweaver.zip."""
    OUT.mkdir(exist_ok=True)
    content, images = content_with_pictures(scenarios)
    # Named after the packs it brings, in each language they give
    packs = [json.loads(p.read_text(encoding="utf-8"))["name"] for p in content if p.name == "pack.json"]
    name = {lang: " + ".join(n.get(lang) or n["en"] for n in packs) for lang in sorted({"en", *(k for n in packs for k in n)})}
    manifest = {"id": pack_id, "name": name if packs else {"en": pack_id}, "version": version()}
    target = OUT / f"{pack_id}.taleweaver.zip"
    with zipfile.ZipFile(target, "w", zipfile.ZIP_DEFLATED) as archive:
        archive.writestr("content-pack.json", json.dumps(manifest, ensure_ascii=False, indent=2))
        write_content(archive, content, images)
    print(f"{target}: {len(content)} files, {len(images)} pictures, {target.stat().st_size / 1e6:.0f} MB ({manifest['version']})")


def build(systems, scenarios):
    OUT.mkdir(exist_ok=True)
    stamp = version()
    with tempfile.TemporaryDirectory(prefix="taleweaver-package-", ignore_cleanup_errors=True) as temporary:
        work = Path(temporary)
        run("npm", "run", "typecheck", cwd=WEB)
        run("npx", "vite", "build", "--outDir", work / "web", "--emptyOutDir", cwd=WEB)

        project = API / "Taleweaver.Api.csproj"
        for system in systems:
            run("dotnet", "restore", project, "-r", system, "--artifacts-path", work / system,
                "-p:SelfContained=true", "-p:PublishSingleFile=true")
        restored = next((work / systems[-1]).rglob("project.assets.json"))

        content, images = content_with_pictures(scenarios)

        bundle = work / "content.zip"
        with zipfile.ZipFile(bundle, "w", zipfile.ZIP_DEFLATED) as archive:
            write_content(archive, content, images)
            for path in sorted((work / "web").rglob("*")):
                if path.is_file():
                    archive.write(path, "wwwroot/" + path.relative_to(work / "web").as_posix())
            archive.writestr("wwwroot/licenses.txt", licences(systems, restored))
        print(f"Content: {len(content)} files, {len(images)} pictures, {bundle.stat().st_size / 1e6:.0f} MB")

        for system in systems:
            windows = system.startswith("win")
            published = work / f"out-{system}"
            options = ["-p:InvariantGlobalization=true"] if not windows else []
            run("dotnet", "publish", project, "-c", "Release", "-r", system, "--self-contained",
                "--artifacts-path", work / system, "-o", published,
                "-p:PublishSingleFile=true", "-p:IncludeNativeLibrariesForSelfExtract=true",
                "-p:EnableCompressionInSingleFile=true", "-p:DebugType=none",
                f"-p:ContentBundle={bundle}", f"-p:InformationalVersion={stamp}",
                "-p:IncludeSourceRevisionInInformationalVersion=false", *options)
            notice = (NOTICE if is_game() else DEMO_NOTICE).format(version=stamp)
            if windows:
                target = OUT / "Taleweaver.exe"
                shutil.copyfile(published / "Taleweaver.Api.exe", target)
                (OUT / "LISEZMOI.txt").write_text(notice, encoding="utf-8-sig", newline="\r\n")
            else:
                target = OUT / f"Taleweaver-{system}.tar.gz"
                with tarfile.open(target, "w:gz", compresslevel=1) as archive:
                    for name, data, mode in (
                        ("Taleweaver", (published / "Taleweaver.Api").read_bytes(), 0o755),
                        ("LISEZMOI.txt", notice.encode("utf-8"), 0o644),
                    ):
                        entry = tarfile.TarInfo(name)
                        entry.size, entry.mode = len(data), mode
                        entry.mtime = datetime.datetime.now().timestamp()
                        archive.addfile(entry, io.BytesIO(data))
            print(f"{target}: {target.stat().st_size / 1e6:.0f} MB ({stamp})")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("systems", nargs="*", help=f"{', '.join(SYSTEMS)} (both by default)")
    parser.add_argument("--scenario", action="append", default=[], help="a homebrew scenario to give with the app")
    parser.add_argument("--content", help="the folder holding packs/, scenarios/, assets/ (taleweaver/demo for the CI)")
    parser.add_argument("--content-pack", metavar="ID", help="make only the content pack <ID>.taleweaver.zip, for the app published without content")
    args = parser.parse_args()
    ROOT = Path(args.content or os.environ.get("TALEWEAVER_CONTENT_ROOT") or APP.parent).resolve()
    if not (ROOT / "packs").is_dir():
        parser.error(f"no packs/ in {ROOT}: give --content (taleweaver/demo for the demonstration)")
    if unknown := set(args.systems) - set(SYSTEMS):
        parser.error(f"unknown system: {', '.join(sorted(unknown))}")
    if args.content_pack:
        build_pack(args.content_pack, args.scenario)
    else:
        build(args.systems or list(SYSTEMS), args.scenario)
