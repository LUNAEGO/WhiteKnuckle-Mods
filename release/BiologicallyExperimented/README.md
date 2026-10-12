# Biologically Experimented

A new **trinket** for White Knuckle. Drugs flowing through your veins push your body past its limits — as long as you can keep it fed.

## Effects

**+ Boost**: stamina +30%, climb speed +15%, gravity -20%, extra jump +1, +2 pouches (inventory slots)

**- Weakness**: score -35%

**! Side effect — Hunger**
- A hunger meter drains from full to empty in **4 min 30 s** (uses the game's own hunger meter).
- Food restores **50%**; pills and injectors restore **100%**.
- Below **50%** the screen slowly turns gray.
- At **15%** or below, all mobility boosts (stamina, climb, extra jump, gravity) shut off until you eat. Pouches stay.
- At **0%** your health drains until you die.

**! Incompatible** with the binding *Escaped Test Subject* (selecting one deselects the other).

Unlocked from the start, cost 1.

## Leaderboards

Runs with this trinket are **never submitted to the online leaderboards**. The game also skips the end-of-run win bonus credits in that case, the same as for any run that isn't leaderboard-eligible.

## Installation

**Mod manager (recommended):** install with r2modman or the Thunderstore Mod Manager. BepInEx is installed automatically.

**Manual:** install [BepInExPack](https://thunderstore.io/c/white-knuckle/p/BepInEx/BepInExPack/), then copy `WKBioExperiment.dll` **and** `Biologically Experimented.png` into `BepInEx/plugins/BiologicallyExperimented/`.

## Configuration

After the first launch, edit `BepInEx/config/LUNAEGO.BiologicallyExperimented.cfg` (in r2modman: **Config editor → LUNAEGO.BiologicallyExperimented**). All values, hunger speed and the gray-screen mode (Auto / PostProcess / Overlay / Off) can be changed (restart the game afterwards).

- `Language = English | Korean` — text language (한국어 지원: `Language = Korean`)
- `EnableDebugKeys` — F8 sets hunger to 0%, F9 fills it (testing)

## Notes

- Hunger resets to full when a saved floor is loaded, because the game's hunger module doesn't save its value.
- BepInEx 5 (BepInExPack). Made for the current Steam version of White Knuckle.

## Credits

- Mod by **LUNAEGO**. Trinket icon is original art by LUNAEGO.
- Uses White Knuckle's built-in hunger meter (Dark Machine Games).

## A note from the author

This is the first mod I've ever made, so there may be rough edges — thank you for your patience. If you enjoyed it, that genuinely makes me happy.

If you'd like to support me, a donation would mean a lot: everything goes toward my uncle, who suffered a stroke and is still in the hospital. No pressure at all — playing the mod is already plenty.

Thank you for playing, and stay tuned — more mods are on the way!

**Donate:** [paypal.me/seonhyubo3](https://paypal.me/seonhyubo3)

> 처음 만든 모드라 부족한 점이 있을 수 있어요. 너그럽게 봐 주시고, 재밌게 즐기셨다면 정말 기쁠 것 같아요.
> 후원해 주시면 뇌졸중으로 쓰러져 아직 병원에 계신 삼촌을 위해 쓰일 거예요.
> 모드를 이용해 주셔서 감사합니다. 앞으로도 새로운 모드를 계속 올릴게요!
