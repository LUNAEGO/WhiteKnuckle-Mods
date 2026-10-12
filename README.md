# White Knuckle Mods by LUNAEGO

BepInEx 5 mods for [White Knuckle](https://store.steampowered.com/app/3195790/White_Knuckle/).

| Mod | What it adds | Download |
|---|---|---|
| **Escaped Test Subject** | A binding: weaker body, sticky surfaces, and hands that mutate into black tentacles you can fire like a Barnacle Hook | [Thunderstore](https://thunderstore.io/c/white-knuckle/p/LUNAEGOS/EscapedTestSubject/) |
| **Biologically Experimented** | A trinket: big mobility boost paid for with a constantly draining hunger meter | Thunderstore (coming soon) |

Players: install from Thunderstore (r2modman / Thunderstore Mod Manager). This repository is the source code.

Bug reports and suggestions are welcome in [Issues](../../issues).

## Repository layout

```
WKEscapedSubject/
  plugin/            Escaped Test Subject plugin (C#). Assets/ is embedded into the DLL
  tools/             Generators for the tentacle overlays, hand-motion frames and icons
  DESIGN.md          Design notes (Korean)
WKBioExperiment/     Biologically Experimented plugin (C#)
release/             Thunderstore package READMEs and build_packages.py
```

## Building

Requirements: .NET SDK (any recent version), White Knuckle with BepInEx 5 installed.

```
cd WKEscapedSubject/plugin
dotnet build -c Release -p:GameDir="C:\path\to\White Knuckle"
```

`GameDir` defaults to the author's install path; point it at your own White Knuckle folder. The game's managed DLLs and BepInEx are referenced from there and are not included in this repository. A successful build copies the DLL into `BepInEx/plugins/`. Add `-p:Deploy=false` to build without copying.

`release/build_packages.py` builds the Thunderstore zips (needs Python with Pillow).

## License

- **Code**: MIT — see [LICENSE](LICENSE).
- **Art and sounds are not covered by the MIT license.**
  - Hand sprites and tentacle frames are derived from White Knuckle (Dark Machine Games; Infested hand cosmetic by Balabanobo) and remain the property of their owners.
  - Sounds are from Freesound under CC BY 4.0 — see [release/EscapedTestSubject/CREDITS.md](release/EscapedTestSubject/CREDITS.md).
  - Icons are original art by LUNAEGO.

## Support

If you enjoy these mods, donations go toward my uncle, who is recovering from a stroke: [paypal.me/seonhyubo3](https://paypal.me/seonhyubo3)
