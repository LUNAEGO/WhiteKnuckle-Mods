# Escaped Test Subject

A new **binding** for White Knuckle. You escaped the lab, but not in one piece: your body is heavier and weaker, yet under pressure it mutates — your hands turn into black tentacles you can fire like a Barnacle Hook.

## Effects

**+ Score multiplier +35%** (but see *Leaderboards* below)

**+ Sticky Surface**
- Stay on the ground for 5 seconds and the grub effect kicks in (purple screen).
- It waits until you grab a wall, then lasts 7 seconds. Stamina doesn't drain for the first 5 seconds.

![Sticky Surface](https://i.imgur.com/HIxp2yx.gif)

**+ Tentacle Mutation**
- Eat a **grub, pill, injector or Lemon Roach** to charge the mutation (one charge at a time).
- Reach out with a bare hand (hold the hand button without grabbing anything) for **5 seconds**. Both hands tremble as black tentacles crawl up from the wrist and wrap around your fingers and palm.
- In tentacle form you get **5 tentacle shots** and **+35% stamina** (until the shots run out).
- Hold the bare-hand button for **0.75 s** to shoot (30 m range, pulls 10% harder than the Barnacle Hook). A shot is only used when the tentacle latches onto a wall.
- The tentacle stays while you hold the button. Near the anchor, **the hand that fired it** can grab the bare wall; grabbing the anchor retracts the tentacle.
- Remaining shots are shown at the bottom right.

![Tentacle Mutation](https://i.imgur.com/xJYFipj.gif)

**- Weakness**: max health -20%, move speed -20%, inventory capacity -2, gravity +20%, stamina -20%

**! Nest**: the Mother recognizes the escaped subject — you are sent to the Hot Zone.

**! Incompatible** with the trinket *Biologically Experimented* (selecting one deselects the other).

## Leaderboards

Runs with this binding are **never submitted to the online leaderboards**, so modded scores don't compete with vanilla runs. The game also skips the end-of-run win bonus credits in that case, the same as for any run that isn't leaderboard-eligible.

## Installation

**Mod manager (recommended):** install with r2modman or the Thunderstore Mod Manager. BepInEx is installed automatically.

**Manual:** install [BepInExPack](https://thunderstore.io/c/white-knuckle/p/BepInEx/BepInExPack/), then copy `WKEscapedSubject.dll` into `BepInEx/plugins/EscapedTestSubject/`. All sprites and sounds are built into the DLL.

## Configuration

After the first launch, edit `BepInEx/config/LUNAEGO.EscapedTestSubject.cfg` (in r2modman: **Config editor → LUNAEGO.EscapedTestSubject**). Every number above can be changed (restart the game afterwards).

- `Language = English | Korean` — text language (한국어 지원: `Language = Korean`)
- `EnableDebugKeys` — F6 charges the mutation, F7 enters tentacle form instantly (testing)

## Compatibility

- BepInEx 5 (BepInExPack). Made for the current Steam version of White Knuckle.
- If the binding list goes past 9 entries, the picker shrinks the binding grid so everything fits.

## Credits

- Mod by **LUNAEGO**.
- Hand sprites and the binding icon frame are based on **White Knuckle** art by Dark Machine Games (Infested hand cosmetic by Balabanobo). Tentacle overlays and hand-motion frames are generated from those sprites.
- Sounds from [Freesound](https://freesound.org), used under Creative Commons:
  - *Splat_05.wav* by LittleRobotSoundFactory — https://freesound.org/s/316540/ — License: [Attribution 4.0](https://creativecommons.org/licenses/by/4.0/) (tentacle shot, converted to 16-bit, trimmed to 0.75 s)
  - *Slime Movement* by [StephenSaldanha](https://freesound.org/people/StephenSaldanha/) — [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) (mutation charge, trimmed to 5 s)

See `CREDITS.md` for details.

## A note from the author

This is the first mod I've ever made, so there may be rough edges — thank you for your patience. If you enjoyed it, that genuinely makes me happy.

If you'd like to support me, a donation would mean a lot: everything goes toward my uncle, who suffered a stroke and is still in the hospital. No pressure at all — playing the mod is already plenty.

Thank you for playing, and stay tuned — more mods are on the way!

**Donate:** [paypal.me/seonhyubo3](https://paypal.me/seonhyubo3)

> 처음 만든 모드라 부족한 점이 있을 수 있어요. 너그럽게 봐 주시고, 재밌게 즐기셨다면 정말 기쁠 것 같아요.
> 후원해 주시면 뇌졸중으로 쓰러져 아직 병원에 계신 삼촌을 위해 쓰일 거예요.
> 모드를 이용해 주셔서 감사합니다. 앞으로도 새로운 모드를 계속 올릴게요!
