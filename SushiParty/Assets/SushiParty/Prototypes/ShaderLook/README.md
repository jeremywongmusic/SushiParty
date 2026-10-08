# PROTOTYPE — Shader Look

**Throwaway. Nothing in here is meant to ship.** Delete this folder and the game's own
rendering is back: nothing in here edits a file under `Assets/SushiParty/Scripts`, a scene,
or an `.asset`.

## The question

> What should Sushi Party actually *look* like once it has shaders — the board, the ten
> minigames, and the effects between them?

Three radically different answers, mounted on the **real** board and the **real** minigame
scenes, switched at runtime with `[` and `]`. Off is a fourth position on the dial, because
a look is impossible to judge without the thing it replaces one keypress away.

## What you should know before you look at any of it

The shipped game has **post-processing off on all thirteen cameras** and no Volume in any
scene. `Palette.Glow` sets an emission colour at 2.2× intensity — and roughly two dozen call
sites across the ten minigames and the board already ask for it — but with no Bloom in the
stack, none of that renders. A glowing thing is currently a slightly brighter thing.

So the first thing every variant does is make an effect the project already wrote visible.
Some of what looks new below is not new: it is the existing `Solid` / `Glow` split finally
having a difference.

## The four positions

| | Bet | Lighting | Post | Effects | Soft bodies |
| --- | --- | --- | --- | --- | --- |
| **Off** | the game as it stands | URP Lit | none | none | — |
| **A — Neon Izakaya** | lit from inside | wrapped lambert, hard fresnel rim, near-black ambient | ACES, heavy bloom, chromatic fringe, vignette | expanding rings, star on death | slight |
| **B — Inkbrush** | readability by outline | three flat bands, inverted-hull ink line, screen-space paper grain | Neutral, **no bloom**, high contrast, film grain | star on light, ring on death | none |
| **C — Lacquer** | food, not arcade | wet double specular, fake translucency, warm ambient | ACES, restrained bloom, warm white balance | standing beams, motes on death | jelly wobble |

They disagree about the *rendering model*, not the palette. B deliberately throws away the
thing A is built on — if a variant looks like another one with a filter over it, it was not
worth building.

### What each variant does to the same moment

| Moment | Off | A Neon | B Ink | C Lacquer |
| --- | --- | --- | --- | --- |
| A Double Pounder switch lights | tint changes | ring + sparks, bloom blooms | ink star, flat wash | beam stands up |
| A Tobiko pearl pops | object vanishes | star + embers | ring + embers | motes settle |
| GO | banner | arena-wide ring, lens kick | arena-wide ring, harder kick | arena-wide ring, soft kick |
| Board shrine | glow tint | standing beam | standing beam | standing beam |
| Water / lava | flat colour | crossing waves + sparkle | waves in flat tone | waves + wet spec |
| Bumper Sparks' ring | flat colour | current chasing round it | current, unbloomed | current, wet |

## Running it

Press **Play** on any scene — `Boot`, `MainMenu`, `Board`, or a minigame scene directly.
The prototype installs itself before the first scene loads, the same way `SushiPartyRoot`
installs the service host, so there is nothing to add to a scene and nothing to remember.

| Key | |
| --- | --- |
| `[` / `]` | previous / next variant (wraps through Off) |
| `F1` / `F2` | the same, if brackets are awkward |
| `` ` `` | hide the bar for a clean screenshot |

**Not the arrow keys**, which every prototype of this shape would normally use: left and
right are seat two's movement in all ten minigames, and they answer the forks on the board.
Square brackets are about the only pair this game has left.

It opens on **B — Inkbrush** (`DefaultLook` in `PrototypeLookDirector`). After that the
choice is saved to `PlayerPrefs`, so it survives a reload and survives leaving Play mode —
that is this project's version of a shareable `?variant=` in a URL. A build also accepts
`-look=neon` on the command line, which wins over the saved value.

### The bar

Bottom of the screen, deliberately IMGUI and deliberately ugly so it cannot be mistaken for
part of the design being judged. Under the variant name it prints the live state:

```
skinned 412   ·   flourishes 7 live   ·   post on   ·   [ ] or F1/F2 to switch, ` to hide
```

`skinned` is how much of the scene the prototype actually reached. If a minigame looks
half-converted, that number is the first place it shows.

## How it gets in without touching the game

Every shape in Sushi Party goes through `Palette.Solid` or `Palette.Glow`, and both stamp
the material with a name — `SP_Solid_RRGGBBAA` or `SP_Glow_RRGGBBAA`. That name is the seam.
`PrototypeSkinner` sweeps for renderers wearing one and swaps in the variant's material;
anything else — UI, an authored material — it leaves alone.

The same seam gives the effects layer its triggers for free. `Shapes.Tint` works by assigning
a fresh Palette material over the top, so **a tracked renderer whose material is no longer
the one we put there is a renderer the game has just re-coloured.** A switch lighting up in
Double Pounder, a sand tile being highlighted, a cart taking a hit, a fork arrow coming on
and the shrine being claimed all arrive as the same event, and not one of them needed a line
adding to its minigame. A renderer that has gone is a thing that died, which covers popped
pearls, collapsed tiles and spent rice balls.

The two whole-screen beats — GO, and the round resolving — are read off
`MinigameController.Phase`, which was already public.

| File | |
| --- | --- |
| `PrototypeLook.cs` | the four variants, as one table of everything a variant decides |
| `PrototypeSkinner.cs` | the renderer sweep, the material cache, and the event triggers |
| `PrototypeFlourishes.cs` | a fixed pool of 128 additive billboards |
| `PrototypePostFx.cs` | one runtime Volume, built in code; turns post on for the live camera |
| `PrototypeLookDirector.cs` | the persistent host, and which flourish answers which moment |
| `PrototypeSwitcherBar.cs` | the bar and the keys |
| `Shaders/SP_Proto_Common.hlsl` | one property block, the ambient model, surface treatments, shadow/depth passes |
| `Shaders/SP_Proto_{Neon,Ink,Lacquer}.shader` | the three skins |
| `Shaders/SP_Proto_Flourish.shader` | ring / mote / beam / star, one shader |

Surfaces are classified by the name the shape was created with — `Water`, `Lava` and `Sea`
are liquid; `Platter`, `Floor`, `Arena` and friends are plate; `ElectricRing` runs a current.
Everything else is a plain solid, which is the right default for the several hundred cubes
that make up the rest.

**Plate is matte on purpose, in all three variants.** It used to carry a lacquer sheen that
swept across it on a slow loop, and that was the most distracting thing in any of them: a
floor is the one surface always on screen and always underneath whatever you are meant to be
watching, so anything moving on it competes with the game for the whole round. It keeps a
static grain, which gives a thirty-unit counter a sense of scale without catching the light.

## Known limits — read these before filing any of it as a bug

- **Editor and development builds only.** `Install()` is behind
  `#if UNITY_EDITOR || DEVELOPMENT_BUILD`. For a dev build the four shaders also need adding
  to **Project Settings → Graphics → Always Included Shaders**, or `Shader.Find` returns null
  and every variant renders as Off. The bar says `SHADER MISSING` when that happens.
- **The HUD and the menu are untouched.** Both are screen-space UGUI, which URP composites
  after post, so nothing here bloom or grains the windows. The select screen has no
  `MeshRenderer` in it at all, so `skinned` reads 0 there. That is correct, not broken.
- **No custom render passes.** Everything full-screen is stock URP volume components, so
  nothing here can leave the renderer in a state deleting this folder will not undo.
- **The camera kick is small and mostly in post** — chromatic fringe, barrel, vignette —
  because Mecha March drives `Camera.main` itself and a prototype fighting it would be
  measuring the wrong thing. There is a positional shake as well, and it only un-applies
  itself if the camera is still where it was left, so Mecha March keeps its corridor.
- **The skinner re-scans for new renderers every 0.15s** and re-skins tracked ones every
  frame. On something that strobes faster than the scan — a 12 Hz invulnerability blink — the
  first frame of each blink is the game's own material. Judge blink cadence in Off.
- **Every skinned renderer carries a `MaterialPropertyBlock`** (it is how the per-object seed
  and the light-up flash get in), which takes them out of the SRP Batcher. On a game made of
  a few hundred primitives that costs nothing you will notice, but do not read a frame time
  from here — measure it again after the winner is folded in properly.
- **No tests, no error handling beyond what makes it run.** It is a prototype.

## When one of them wins

1. Say which, and why, and what you would take from the other two — the useful answer is
   almost always *"A's rims on C's specular"* rather than a whole variant.
2. Fold the winner into the real code properly. `Palette` is the place: `Solid` and `Glow`
   become the winning shader, a Volume goes into the scenes or onto the persistent root, and
   the effect triggers become explicit calls from the minigames rather than a material-swap
   sniffer. **Do not promote this code** — it was written under prototype rules and the
   sniffer in particular is a trick, not a design.
3. Keep this folder as the primary source. The project is not under git; if that changes,
   this belongs on a throwaway branch rather than in main, with a pointer to it from
   wherever the implementation is tracked. Variants left in main rot fast.
