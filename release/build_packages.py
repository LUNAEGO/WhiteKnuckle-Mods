# Thunderstore 업로드용 패키지(zip) 만들기
# 사용: python build_packages.py   (두 플러그인을 먼저 dotnet build -c Release 해 둘 것)
# 결과: release/<패키지>/ 스테이징 폴더와 release/dist/<팀>-<패키지>-<버전>.zip
import io, json, os, shutil, zipfile
from PIL import Image

ROOT = os.path.dirname(os.path.abspath(__file__))
CC = os.path.dirname(ROOT)  # D:\Claude_Code
TEAM = "LUNAEGOS"  # Thunderstore 팀 이름 (실제 팀: thunderstore.io/c/white-knuckle/p/LUNAEGOS/)
BEPINEX = "BepInEx-BepInExPack-5.4.2305"

PACKAGES = [
    {
        "name": "EscapedTestSubject",
        "version": "0.9.3",
        "description": "New binding: an escaped test subject. Weaker body, but eat a grub/pill/injector to mutate your hands into black tentacles you can fire like a Barnacle Hook. Sticky surfaces, hostile Mother. No leaderboards.",
        "files": [(os.path.join(CC, "WKEscapedSubject", "plugin", "bin", "Release", "WKEscapedSubject.dll"), "WKEscapedSubject.dll")],
        "dirs": [],  # 그림·소리는 DLL 안에 들어 있음 (모드 매니저가 폴더를 펼쳐 버려서)
        "icon": (os.path.join(CC, "WKEscapedSubject", "plugin", "Assets", "binding_icon.png"), True),
    },
    {
        "name": "BiologicallyExperimented",
        "version": "1.1.0",
        "description": "New trinket: huge mobility boost (stamina, climb, extra jump, low gravity, +2 pouches) at the cost of a constantly draining hunger meter. Starve and you lose your mobility, then your life. Score -35%. No leaderboards.",
        "files": [
            (os.path.join(CC, "WKBioExperiment", "bin", "Release", "WKBioExperiment.dll"), "WKBioExperiment.dll"),
            (os.path.join(CC, "WKBioExperiment", "Icon", "Biologically Experimented.png"), "Biologically Experimented.png"),
        ],
        "dirs": [],
        "icon": (os.path.join(CC, "WKBioExperiment", "Icon", "Biologically Experimented.png"), False),
    },
]

dist = os.path.join(ROOT, "dist")
os.makedirs(dist, exist_ok=True)
for p in PACKAGES:
    assert len(p["description"]) <= 250, (p["name"], len(p["description"]))
    stage = os.path.join(ROOT, p["name"])
    os.makedirs(stage, exist_ok=True)
    # README.md / CREDITS.md 는 스테이징 폴더에 직접 써 둔 것을 쓴다. 나머지는 매번 새로 복사
    for entry in os.listdir(stage):
        if entry in ("README.md", "CREDITS.md", "CHANGELOG.md"):
            continue
        path = os.path.join(stage, entry)
        shutil.rmtree(path) if os.path.isdir(path) else os.remove(path)
    for src, dst in p["files"]:
        shutil.copy2(src, os.path.join(stage, dst))
    for src, dst in p["dirs"]:
        shutil.copytree(src, os.path.join(stage, dst))
    # 256x256 아이콘 (도트 아이콘은 확대할 때 픽셀 유지)
    src, pixel = p["icon"]
    im = Image.open(src).convert("RGBA")
    if pixel:
        im = im.resize((256, 256), Image.NEAREST)
    else:
        im.thumbnail((256, 256), Image.LANCZOS)
        canvas = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
        canvas.alpha_composite(im, ((256 - im.width) // 2, (256 - im.height) // 2))
        im = canvas
    im.save(os.path.join(stage, "icon.png"))
    manifest = {
        "name": p["name"],
        "version_number": p["version"],
        "website_url": p.get("website", ""),
        "description": p["description"],
        "dependencies": [BEPINEX],
    }
    io.open(os.path.join(stage, "manifest.json"), "w", encoding="utf-8").write(json.dumps(manifest, indent=2, ensure_ascii=False))
    out = os.path.join(dist, "%s-%s-%s.zip" % (TEAM, p["name"], p["version"]))
    if os.path.exists(out) and p["name"] != "EscapedTestSubject":
        print("skip (already built):", out); continue
    with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED) as z:
        for base, _, files in os.walk(stage):
            for f in files:
                full = os.path.join(base, f)
                z.write(full, os.path.relpath(full, stage).replace(os.sep, "/"))
    print(out, os.path.getsize(out) // 1024, "KB")
