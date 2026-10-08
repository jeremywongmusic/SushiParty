# Sushi Party — Chef Tako Minigames

A local-only party game on Unity 6 (6000.0.34f1) with URP and the Input System package.

Six minigames, recreated from the **Bowser Jr. minigames in *Mario Party 9***, with the cast
rebranded: the two players are little octopuses and the rival they face is **Chef Tako**, a
bigger octopus in a chef's hat. The minigame titles are unchanged from the original.

They also hang off a board. **Board mode** is a branching track the pair play across ten
rounds, with one of the six minigames at the end of every round.

## Co-operative, not versus

Two players work together against Chef Tako. Every rules line in the original says "work with
your teammate", and the stakes are shared — the pair wins 5 Coins each, or Chef Tako takes 5
from each of them.

So a CPU in the second seat is a **partner**, not an opponent, and Chef Tako is a third driver
the players never control. This is why the input abstraction matters more here than it would
in a versus game: a partner has to be legible enough to co-ordinate with.

It shapes the board too. Rounds that resolve as one shared Win or Lose cannot add up to a
session the pair play against each other, so the board's prize is scored across both of them
and played as one number against Chef Tako's.

## The cast

Everyone is built by `Shapes.Octopus`: a domed mantle, six tentacles, and forward-facing eyes,
so you can read which way a character is pointing without any imported art.

| | Colour | Marked by |
| --- | --- | --- |
| Seat 1 | Blue | — |
| Seat 2 | Red | — |
| Chef Tako | Purple | A chef's hat, and he is drawn larger in most games |

The mantle is the child named `Body` — what every minigame tints when a character is stunned
and hides when one is blinking through invulnerability. In Pair of Aces, Chef Tako escapes in
a **flying wok** rather than a Clown Car.

## Props and currency

| | Was | Now |
| --- | --- | --- |
| Stake per round | Mini Stars | **Coins** — 5 each way, `MinigameLibrary.CoinStake` |
| Crossfire cannonballs | Cannonballs | **Rice balls** |
| Pair of Aces shots | Cannon shells | **Rice balls** |
| Pair of Aces hazard | Fireballs | **Wasabi bombs**, green |

Shared colours live on `Palette`: `RiceWhite`, `WasabiGreen`, `TakoPurple`, `ChefWhite`. Green
is reserved for wasabi — for things that hurt — so it reads as danger consistently.

## Audio (FMOD)

**FMOD for Unity 2.03.20** is installed and `SUSHIPARTY_FMOD` is set in the Standalone
scripting defines, so `FmodAudioBackend` is live.

**Version matters.** FMOD's bank format is not compatible across minor lines: Studio 2.03
emits bank format 146, which a 2.02 Unity runtime refuses with `ERR_VERSION`. Keep the Unity
integration on the same minor line as the Studio that builds the banks. No amount of path
configuration works around a mismatch.

Without the define everything falls back to `SilentAudioBackend` and the game runs normally in
silence. The define is set for **Standalone only** — add it to any other build target you ship,
or audio there will silently no-op.

### The FMOD Studio project

It lives beside this one at `../SushiPartyAudio` (FMOD Studio 2.03), and builds banks to
`Build/Desktop/`.

Events are grouped by the part of the game they belong to. Every event carries an `Audio` group
track to drop an asset onto; the looping ones carry a marker track for the loop region.

Add a sound to `Sfx`, add the matching event in Studio, then **File → Build**.

**Labelled parameters need a type.** A parameter with `enumerationLabels` but no
`parameterType` builds as a continuous parameter and its labels never reach the bank, so
`setParameterByNameWithLabel` fails with `ERR_EVENT_NOTFOUND` at runtime. `Choice`, `Kind` and
`Difficulty` were all authored this way and had to be repaired.

### How it is put together

| Type | Role |
| --- | --- |
| `Sfx` | The catalogue. Every sound the game can make, as one list of FMOD event paths, with the parameter names each event expects. |
| `GameAudio` | The only thing gameplay touches: `Play`, `PlayAt`, `Loop`, `SetParameter`, `Stop`. |
| `IAudioBackend` | The seam. `FmodAudioBackend` behind `#if SUSHIPARTY_FMOD`, `SilentAudioBackend` otherwise. |
| `AudioHandle` | Opaque handle to a running loop, so no FMOD type ever reaches a minigame. |

Gameplay never types an event path — it names an entry in `Sfx`, so a typo is a compile error
and the full sound list is one file you can hand to whoever builds the FMOD project.

The catalogue holds **81 SFX events and 10 music events**. Four SFX events loop; 42 call sites
are positioned. The largest group is `Board/` at 16 events, because a board turn is mostly
waiting and nearly every beat of one is a sound.

### Global parameters

Fourteen **global** parameters are published on the Studio system as they change. Any event,
snapshot or bus can read them, so a music bed can tighten as the clock runs down with no code
change.

| Parameter | Type | Driven by |
| --- | --- | --- |
| `Minigame` | Labelled | Which game is running; labels are the `MinigameId` names |
| `RoundPhase` | Labelled | `Idle` / `Briefing` / `Countdown` / `Playing` / `Settling` / `Finished` |
| `Difficulty` | Labelled | `Relaxed` / `Standard` / `Sharp` — Chef Tako's skill dial |
| `RoundProgress` | 0-1 | Elapsed fraction of the round |
| `TimeRemaining` | 0-90s | Seconds left, unnormalised, for anything that ticks |
| `PlayerMotion` | 0-1 | How fast the players are moving |
| `ChefMotion` | 0-1 | How fast Chef Tako is moving |
| `Objective` | 0-1 | Progress toward winning |
| `Paused` | 0/1 | Whether the pause menu is up |
| `Coins` | 0-99 | The pair's combined Coins |
| `CoinSwing` | 0-30 | How much the last turn moved them |
| `BoardRound` | 0-10 | Which board round is running |
| `BoardProgress` | 0-1 | How far through the ten rounds |
| `HumanCount` | 0-2 | How many seats a person is holding |

The motion and objective values come from each minigame's `SampleTelemetry()`. Every game
measures them differently — a wheel's angular velocity, a minecart's rail speed, how boxed in
Chef Tako is — so normalising to 0-1 is the game's job and publishing is the framework's. Some
read naturally as tension: in Zoom Room `Objective` is how close the nearest pursuer is; in
Sand Trap it is how few escape squares Chef Tako has left.

`GameAudio.SetGlobal` drops values that have not moved, so publishing costs almost nothing. The
cache is cleared between scenes so each round republishes from scratch.

### Events that take a parameter

| Event | Parameter | Driven by |
| --- | --- | --- |
| `Round/CountdownTick` | `Step` | 3, 2, 1 — pitch the ladder in Studio |
| `BumperSparks/Impact` | `Force` | Closing speed of the collision |
| `BumperSparks/EngineLoop` | `Speed` | Each vehicle's velocity, one loop per vehicle |
| `Crossfire/CartLoop` | `Speed` | How hard that cart is being pushed |
| `ZoomRoom/ChaseLoop` | `Proximity` | Distance from Chef Tako to his nearest pursuer |
| `Pedal/Stomp` | `Force` | The stomp's leverage on the wheel |
| `Pedal/WheelLoop` | `Speed` | Wheel angular velocity |
| `SandTrap/LineLit` | `Amount` | Which row or column lit up |
| `Board/DieLand` | `Step` | The face that came up, 1-6 |
| `Board/TokenStep` | `Step` | Which step of the move this hop is, so a six can climb a ladder |
| `Board/SpaceStart` | `Kind` | Labelled — which of the six space kinds was landed on |
| `Board/SpaceCoinGain` | `Amount` | Coins gained |
| `Board/SpaceCoinLoss` | `Amount` | Coins lost |
| `UI/TitleConfirm` | `Choice` | Labelled — which title entry was taken |

`Sfx` also declares `UrgencyParameter` and `StateParameter`, which nothing uses and which have
no matching parameter in the Studio project.

### The mixer

`MixerChannel` describes the buses and VCAs the settings screen can move:

| Channel | Kind | Pauses with the game |
| --- | --- | --- |
| `bus:/` — Master | Bus | no |
| `bus:/Music` | Bus | yes |
| `bus:/Game` | Bus | yes |
| `bus:/UI` — Interface | Bus | no |
| `vca:/Music` | VCA | — |
| `vca:/Effects` | VCA | — |

Interface never pauses, so the pause menu still makes noise while everything under it is held.
`MixerSettings` persists the player's levels; `PauseController` holds and releases the pausing
buses.

### Checking the wiring without FMOD

Set `GameAudio.LogEvents = true` and play a round: the silent backend prints the exact event
stream the FMOD project will receive. This is how the call sites were verified with no
middleware installed.

### The cheat sheet — SushiParty > FMOD Cheat Sheet

An editor window that answers "I want to trigger a sound from here, what do I write". Four
tabs: every way into the API with the line to copy; the whole catalogue with what the code
currently says about each entry; the globals; and the FMOD project setup.

The **How to trigger** tab is held to `GameAudio` by reflection in both directions, so a method
added to the facade fails the test suite until the window documents it, and a snippet for a
method that no longer exists fails it too. The **Catalogue** tab is not written down at all:
whether an entry is spatialised, whether it loops and which parameter it carries are read off
the call sites.

It also shows where each entry is played, and says so when nothing plays it — which is how you
find an event the Studio project carries and no line of code will ever fire. That is not the
same as dead: an animation clip can trigger any entry by name through `CharacterAudioRelay`,
and no call site exists for that.

### Notes for the FMOD project

- An event path missing from the loaded banks is reported once and then muted, rather than
  throwing and taking the minigame down with it.
- Positioned events use `set3DAttributes` rather than `AttachInstanceToGameObject`, whose
  overloads have changed between FMOD for Unity versions.
- FMOD does not listen through Unity's `AudioListener`. The backend adds a `StudioListener` to
  the active camera, driven off `SceneManager.sceneLoaded` so it covers every route into a
  scene — Boot, the menu, the board, a retry reload, or pressing Play on a minigame directly.
  It is attached at runtime rather than authored into the scenes on purpose: a serialised
  `StudioListener` would become a missing-script reference in every scene whenever FMOD is
  absent or the define is off, and the project has to keep working in both states.
- All loops are stopped on scene transitions, so nothing survives a minigame ending.

## Colour

Each minigame's accent is tied to what its game is made of: gold for Bumper Sparks' sparks,
rice-orange for Sand Trap, cavern purple for Crossfire Caverns, sky blue for Pair of Aces,
ocean cyan for Pedal to the Paddle, and pink for Zoom Room. They sit at high saturation and
value — the Mario Party look.

## Window style

Everything is built from one small vocabulary in `UiKit`:

| Helper | What it is |
| --- | --- |
| `RoundedSprite` | A nine-sliced rounded rectangle, generated at runtime. Sharp corners are the one thing these windows never have. |
| `Window(host, body)` | Drop shadow, thick white outline, rounded body. Three stacked sliced panels, no shader, no imported art. |
| `Banner(host, text, fill)` | A title plate hanging off the top edge of a window. |
| `Pill(...)` | A rounded plate for prompts and control hints. |
| `Chunky(text)` | Thick dark outline plus a soft drop shadow on a label. Keeps white text legible over bright, busy panels without a plate behind every word. |

The title screen, select screen, rules card, results card and spec sheet all use the same
window. Menu tiles are cream windows with an accent title plate and the picture inset below;
the selected one swaps its white outline for its accent colour, lifts above its neighbours and
scales up — a coloured ring reads across a room in a way a background tint does not.

Text colour follows the surface: `Ink`/`InkDim` on dark, `DarkInk`/`DarkInkDim` on cream.
Getting that backwards is the easy mistake when moving a panel from navy to cream.

## Menus

`MainMenu.unity` carries three screens.

**Title.** `TitleMenuController` — Play Board, Play Minigames, Audio Settings. It is what you
land on, and the select grid stays hidden behind it until you ask for it.

**Select.** `MainMenuController` builds a **3-column grid** of the six games. Each card shows a
picture of the game rather than a description, the way Mario Party's own select does; roles and
objective sit in the details panel underneath.

The picture is a **real screenshot** when one exists at
`Assets/SushiParty/Resources/Previews/<SceneName>.png`. Play any round in the Editor and press
**F9** to capture the current frame straight into that folder — the menu picks it up with no
further wiring. Until one exists, `MinigamePreview` draws a small diagram of the actual
mechanic; all six have one. The diagrams are UGUI rects plus a generated circle sprite, so they
cost nothing and need no assets.

**Settings.** `SettingsMenuController` — the mixer levels above, with an audition sound so a
slider can be heard while it moves. It saves in `OnDestroy`, which covers the screen being taken
down by a scene change rather than by the player.

Every decision the select screen makes lives in `MenuSelection`, away from the pixels: where the
cursor is, where a nudge moves it, whether an entry can be opened, who fills each seat, which
skill dial moved, and whether the next thing to open is one minigame or the whole board.
Wrapping is the reason it was worth splitting out — it is per-line and never carries, so left
off the start of a row comes back on the right of *that same row*. The `MatchSetup` it edits is
the live one the flow service carries between scenes, so a team picked once is still picked when
you come back from a round.

The menu canvas matches **width** rather than the 0.5 default; a height-matched scale blows a
fixed-width grid off both edges of a wide, short window.

**Pause.** `PauseMenuController` lives on the persistent service host, so it is available in
every scene except the menu. `Esc` opens it: Resume, Audio settings, Quit to menu. While it is
up, gameplay stops reading input — `BoardController` and `MinigameController` both return early
on `PauseMenuController.IsPaused`, because the menu's confirm key and the game's Primary action
share `Space` and gamepad South.

## Layers

```
Assets/SushiParty/
  Scenes/
    Boot.unity              entry point; hands off to the menu
    MainMenu.unity          title, select and settings screens
    Board.unity             the board mode; generated, not authored
    Minigames/*.unity       one scene per minigame (6)
  Scripts/
    Core/                   roster data, session lifecycle, round clock, scene flow
    Input/                  the human/CPU seam
    Audio/                  Sfx catalogue, GameAudio facade, FMOD backend, mixer
    Board/                  the board: the layout graph, session, die, view, turn loop
    Presentation/           materials, generated UI, HUD, fader, character animation
    Menu/                   title, select, settings and pause screens
    Minigames/              one folder per game, plus Shared/ for reused parts
    Editor/                 scene tooling, animation builder, FMOD cheat sheet
  Prototypes/ShaderLook/    throwaway look study; delete the folder and nothing changes
  Tests/                    377 EditMode tests
```

### Core

| Type | Role |
| --- | --- |
| `MinigameId` / `MinigameDefinition` / `MinigameLibrary` | The roster: names, scenes, objectives, roles, time limits, design notes. Authored in code as the single source of truth. |
| `MatchSetup` | What the menu produces — one minigame or the whole board (`SessionMode`), which game, who fills each seat, and the two skill dials. |
| `MinigameContext` | What a round receives: definition, setup, and the two live `Participant`s. |
| `RoundClock` | The round's phase machine: rules card → 3-2-1-GO → clock → settle → results. Plain C#; time and one button go in, a `RoundTick` of what should happen comes out. |
| `MinigameController` | Abstract base. Turns a `RoundTick` into banners, stingers and colours, and owns the coin stakes. |
| `Vulnerability` | The stun / immunity / blink clock that Bumper Sparks, Crossfire Caverns and Pair of Aces used to each hand-roll. |
| `MinigameFlow` | Persistent service; loads scenes, carries setup across the load, records outcomes, and holds the live `BoardSession` between rounds. |
| `SushiPartyRoot` | Creates the service host before the first scene loads, from any entry scene. |

`RoundClock` and `Vulnerability` were both lifted out of code that already worked, for the same
reason: neither could be exercised without pressing Play. `RoundClock` came out of
`MinigameController.Update`, which had the whole phase machine tangled up with the HUD and FMOD
calls, so "does a five-second warning fire exactly once" was a question you could only answer by
sitting through a round. The three copies of `Vulnerability` had already drifted apart on blink
cadence; one clock means one cadence, and it kept 12Hz.

### The `MinigameController` contract

A new minigame implements four members and inherits everything else:

```csharp
public override MinigameId Id => MinigameId.ZoomRoom;

protected override void OnPrepare();                          // build the arena
protected override void OnPlay(float deltaTime);              // one live frame
protected override IParticipantInput CreateCpuBrain(Participant p);  // the co-op partner
// then call Win(detail) or Lose(detail) when the objective resolves
```

Optional hooks: `OnBegin`, `OnTimeUp`, `OnConclude`, `OnSettle`, `UsesRoundStructure`.

The base class supplies the HUD, the timer, the 3-2-1-GO, retry and the coin stakes.
`TimeRemaining`, `P1`, `P2`, `Hud` and `Context` are available to subclasses. It no longer
*decides* any of the phase machine — `RoundClock` does, and what is left here is the translation
from a decision into a banner, a sting and a colour, in one place and one order.

A round the board owns ends differently, and the controller needs no help to know it: the
results card hands the player back to the board instead of offering a retry. Retry is
meaningless there, because the stake was paid into the board session the moment the round
resolved.

### The human/CPU seam

`IParticipantInput` is the only thing gameplay reads:

```csharp
Vector2 Move { get; }
Vector2 Aim  { get; }                      // absolute pointer; see below
bool IsHeld(MinigameAction action);        // Primary / Secondary
bool WasPressed(MinigameAction action);
void Tick(float deltaTime);
```

`Aim` is currently unused. It was added for Pair of Aces, which turned out to need a *relative*
crosshair driven by `Move` instead: two hot-seat players cannot share one mouse, and the
original's Wii Remote pointer has no two-player keyboard equivalent. The channel is left in
place for a future game with a genuine absolute-pointer input.

`HumanParticipantInput` polls a keyboard half or a gamepad. `CpuBrain` synthesises the same
values — it *presses buttons*, it does not call gameplay methods. **No minigame branches on
whether a seat is human**, which is what makes the per-seat Human/CPU toggle free for every game
you add.

Difficulty is reaction delay and aim error (`ReactionDelay`, `AimJitter`, `Competence`), not
movement speed, so a relaxed partner is late rather than sluggish.

### What a brain can see

A brain used to hold a reference to its minigame and call back into it, which meant every game
had to make public whatever its brains happened to want — positions, timers, grids, whose turn
it was. None of it could be exercised without a loaded scene.

Every game now publishes a single `readonly struct` read-model instead, and its brains are handed
a delegate that produces one:

```csharp
public BumperSparksSnapshot Snapshot();     // the whole brain-facing surface of the game

public sealed class ChefSparksBrain : CpuBrain<BumperSparksSnapshot>
{
    protected override void Think(in BumperSparksSnapshot world, float deltaTime) { ... }
}
```

`CpuBrain<TSnapshot>` takes a `Func<TSnapshot>` rather than the minigame, samples it once a
frame and passes it down by `in`, so reading the world every frame allocates nothing.

Two things fall out. The brain-facing surface of the whole project is **one `Snapshot()` per
game**. And a brain is testable on its own: build a snapshot by hand, tick it, assert on `Move`
and `WasPressed` — no scene, no `GameObject`, no audio middleware.

### Chef Tako

He is **not** generally a `CpuBrain`. He goes through the input seam in exactly two places:

| | How he is driven |
| --- | --- |
| Bumper Sparks | `ChefSparksBrain`, a `CpuBrain<BumperSparksSnapshot>` |
| Board mode | `BoardBrain`, a `CpuBrain<BoardSnapshot>` |
| The other five | Scripted by the minigame itself — `TickChef`, `MoveChef` |

Those are the two places where he does the same thing the players do: drives a vehicle in the
same arena on the same physics, or rolls a die and walks the same track. Everywhere else he
moves in his own terms — tile to tile across a collapsing grid, node to node through a maze,
along a rail in a flying wok — and there is no `Move`/`Primary` a human could press that would
produce that motion, so synthesising one would be dressing up a scripted route as an input.

Where he *does* go through the seam, whatever consumes his input takes an `IParticipantInput`
and nothing more, so handing that seat a `HumanParticipantInput` gives a third person control of
him with nothing else changing.

## Controls

| | Move | Primary (jump / fire / pound-start) | Secondary |
| --- | --- | --- | --- |
| Seat 1 | `WASD` | `Space` or `F` | `Left Shift` or `G` |
| Seat 2 | Arrow keys | `Right Ctrl`, `Numpad 0`, `.` | `Right Shift`, `Numpad 1`, `/` |
| Gamepad | Left stick / D-pad | `A` | `X` / `B` |

**Title screen:** arrows or WASD to choose, `Enter`/`Space` to take an entry.

**Select screen:** arrows or WASD to choose, `Enter`/`Space` to play the highlighted card,
**`B` (or gamepad north) to play the board instead**, `1`–`4` to change seats and skill; mouse
hover and click also work.

**In a game or on the board:** `Esc` opens the pause menu. On the board, press to roll when it
is your seat's turn; at a fork, left/right move the highlight between the ways on and Primary
takes the one it is sitting on.

The fork uses Move and Primary — the two channels every minigame already reads — rather than a
control of its own, so a CPU seat can answer a junction without any code knowing which kind of
seat it is.

The board gets a key of its own on the select screen rather than a seventh card, because it is
not a minigame: choosing it does not open a game, it opens a board that will pick its own
minigame ten times.

The original is a Wii Remote game, so motion inputs are remapped — waving a remote becomes a
button press, tilting becomes a stick direction. Bindings live in `KeyboardScheme`.

## The six minigames

| Minigame | Co-op mechanic | Win condition | Length |
| --- | --- | --- | --- |
| Bumper Sparks | Symmetric arena brawl; hit him from the inside so the impulse carries him out | Ring him 3× | 30s |
| Sand Trap | Asymmetric: seat 1 owns rows, seat 2 owns columns; highlights must overlap in time | Drop him through the floor | 30s |
| Crossfire Caverns | Facing rails; every missed shot carries on into your teammate | 3 direct hits | 30s |
| Pair of Aces | Aim with travel time, dodge his wasabi bombs or your cannon jams | 3 hits on the flying wok | 30s |
| Zoom Room | He is strictly faster; only a pincer shrinks his escape distance | Catch him in the maze | 30s |
| Pedal to the Paddle | Stomp the forward side of a shared wheel while it carries you toward the water | Beat him to the goal | 60s |

They deliberately span the design space rather than repeating one template: continuous and
discrete, physics and grid, symmetric and asymmetric roles, timing-sync and spatial-split,
aiming and rhythm. Between them they are the real test that the framework does not assume one
shape of minigame.

`Scripts/Minigames/Shared` holds `PoundCharacter` (the walk/jump/ground-pound body Sand Trap
uses) and `PoundActuator` (which turns a brain's "pound now" intent into the required two
presses). `MazeGrid` and `SandGrid` stay with their own games.

`PlaceholderMinigame` renders any definition's objective, roles and design notes as a spec
sheet. Nothing uses it now that every entry is implemented, but it is how a newly added game
gets a real scene before it gets gameplay.

Every minigame scene's camera is fixed and fully authored; nothing drives `Camera.main` at
runtime.

## The board game

`Board.unity`, `Scripts/Board/`.

| | |
| --- | --- |
| Track | A winding, hand-authored path of **27 spaces with two forks in it**. Three tokens — the two octopuses and Chef Tako — take one turn each per round, on a six-sided die. |
| Turn | Roll, then walk one space at a time, answering any fork you reach on the way. The space you stop on does something. |
| Round | All three move, then one of the six minigames. Its Win or Lose pays the `CoinStake`: 5 Coins to each octopus, or 5 from each of them to Chef Tako. |
| Prize | **Golden Coins.** Bought at the shrine for 10 Coins, and only bought — there is no other way to get one. |
| Length | 10 rounds. Then the pair's *combined* Golden Coins play Chef Tako's; Coins break a tie, and a draw goes to Chef Tako. |

Chef Tako is a mover here and still never a `Participant`. The pair is two seats, so he is
ticked alongside them rather than inside them.

Six space kinds — few enough that every one is something you can shout across a room. "You
landed on wasabi" reads instantly in a way "you landed on a blue space" never does:

| Space | What it does | How many |
| --- | --- | --- |
| Start | Where all three begin. Inert, and stays inert — the shrine is not allowed to land on it. | 1 |
| Coins | +3 Coins. | 15 |
| Chef Tako's cut | −3 Coins, floored at zero. A broke token really does lose nothing, and the banner says so rather than printing "−0 COINS". | 6 |
| Shrine | Sells one Golden Coin for 10 Coins, then moves elsewhere. Turns you away if you cannot pay, with a sound of its own. | 1 |
| Wasabi | Lose your next turn. | 2 |
| The current | Swap places with whichever *other* token has rolled furthest. | 2 |

The shrine opens ten spaces out from Start, halfway along the east lobe — the long way round at
the big fork. Ten is past any first roll, so nobody claims a Golden Coin on turn one, and the
lobe is the only route it is on, so nobody claims it without having chosen to walk further for
it. It relocates every time it sells, the same thing Mario Party does with its star and for the
same reason: otherwise the winning play is to sit on it. Where it goes next is rolled on the
board's own die, so a seeded session replays identically. It always moves along the default
route, so it never tucks itself away down a shortcut where the fork would stop meaning anything.

### The two forks

A ring was the first shape, and a ring gave the player nothing to decide: the die does
everything and every token takes the only road there is. So the track branches, twice.

| Fork | The two ways on | What you are choosing |
| --- | --- | --- |
| **Space 5**, the big one | The **east lobe** — nine spaces, with the shrine on it — against a **chord straight across the middle**, four spaces, opening with wasabi. | Five steps against Golden Coins. The shrine cannot be reached any other way, so the long way is the only way anyone scores, and the short way is the only way to be round again in time to keep scoring. |
| **Space 17**, the small one | **Round the outside**, three spaces, two of them plates of coins — against the **corner cut**, two spaces, the first of them Chef Tako's cut. | One step against a plate of coins. The same bet in miniature, small enough to take without thinking. |

Both forks rejoin — the big one after the lobe, the small one at Start — so a choice is never a
way of getting stranded, only a way of arriving somewhere sooner or richer.

A junction is drawn wider than an ordinary tile, with its two exits leaving it as two separate
arrows, because a fork you cannot see coming is a surprise rather than a decision. The spaces
are drawn where the layout says they sit and joined by a visible path.

There is no finish line: what ends a board game here is the round count, so the track loops and
keeps all three tokens live for all ten rounds instead of stranding whoever falls behind. That
matters more than it would elsewhere, because two of the three are supposed to be co-operating
and a partner who is out of it has nothing left to co-operate about.

### Answering a fork

Left and right shift the highlight between the ways on; Primary confirms the one it is sitting
on. The lit route runs all the way to the tile at its far end, because the destination is the
part of the answer that matters.

Both are channels every minigame already reads, so a CPU seat answers a junction by pressing
exactly what a person presses. There is deliberately no `ChooseFor(seat)` on the controller: a
call like that would be a second path through the junction, and the moment there are two paths
only one of them is the tested one.

Underneath, the session refuses to move a token through a junction nobody has answered — it
throws rather than quietly taking the first exit — so a fork cannot be skipped by a caller that
forgot it existed.

### Why scoring is shared and movement is not

Minigames that resolve as one shared Win or Lose leave a board built on them only one honest
reading: the session has to be won or lost by the pair together. Golden Coins are added up
across the two octopuses and played as one number against Chef Tako's, and the HUD boxes the two
of them together with that total underneath.

Movement is not shared, because it cannot be and still be a board game. Each token takes its own
roll, picks its own way at a fork, keeps its own Coins, and buys its own Golden Coin when it can
afford one. That is what leaves a turn worth watching when the score is a joint one.

### The parts

| Type | Role |
| --- | --- |
| `BoardLayout` | The track, as a directed graph: what each space is, where it was authored to sit, and which spaces it leads to. Plain data. |
| `BoardSession` | The rules. Turn order, what a space does, the minigame settlement, the result. Plain C# with the die injected. |
| `IDie` / `RandomDie` / `ScriptedDie` | Where a roll comes from. Seeded in play, scripted in tests. |
| `BoardSnapshot` / `BoardBrain` | The same read-model seam the minigames use. |
| `BoardView` | The board and the three tokens as geometry, generated: a tile wherever the layout says, joined to each exit by a mat with a chevron on it. Knows where space 7 *is*, never what it does. |
| `BoardHud` | A card per token, the round counter, the die panel, the banner, the results window. |
| `BoardController` | The turn loop, and `MinigameController`'s sibling. Owns every pause, every hop and every stinger. |

`BoardLayout` and `BoardSession` name no `UnityEngine` type at all, and the die is a seam rather
than ceremony — the rules are almost entirely "what happens after a number comes up", and none
of that is checkable if the number is decided by `UnityEngine.Random` deep inside the turn loop.
An entire ten-round session plays out in a test with no scene loaded.

The controller and the session split on one line. `BeginTurn` rolls and hands back a *plan* —
seat, roll and where the token is standing, and **no destination**, because with junctions on the
board where a five ends up is not knowable until the mover has answered every fork on the way.
The token is walked with `StepOnce`, one space per call, stopping dead at any junction until
`ChooseExit` says which way; `CompleteTurn` applies whatever the space does and hands back a
`TurnReport` that does carry the destination. Everything the banner says comes off that
`TurnReport` rather than being read back out of the session, so a call-out can never disagree
with what was applied.

`TakeTurn` rolls, walks and resolves in one call, answering each fork with a delegate you hand it
or with the first way on if you hand it nothing. That is for tests and for a turn nobody is
watching. The scene never uses it: on screen the walk *is* the turn.

Pacing is the rest of the job, and it is all constants at the top of `BoardController`. A board
game is mostly pauses, and the pauses are the design.

### Out to a minigame and back

`MinigameFlow` holds the live `BoardSession`, because the Board scene is torn down and rebuilt
around every round. The scene is rebuilt from nothing and the controller `Adopt`s the session
back off the service before `Start` runs.

A round is settled the instant the minigame resolves, in `RecordOutcome` — not when the player
presses on through the results card. That is the one place a result is ever announced, so
settling it there is what makes "once per round" true however the player leaves the minigame
afterwards. The latch is the session's own `AwaitingMinigame` flag rather than a second flag kept
beside it, so a duplicate result is turned away by the same fact that let the first one through.
An abandoned round is not a result at all — quitting to the menu mid-round has never carried
stakes.

Press Play on `Board.unity` directly and there is no flow service to load anything, so each round
is settled on the board's own die and the session carries on.

### The brain

There is exactly one real decision on this board, and `BoardBrain` exists to make it. The whole
heuristic is three rules:

| When | What it takes |
| --- | --- |
| It can pay the shrine's 10 Coins, or is within a plate of coins or so of paying | The way on that reaches the shrine in fewest steps — the long way, because that is the only way the shrine is on. |
| It cannot | The way on *furthest* from the shrine, which is the shortcut. |
| It is relaxed | Sometimes the wrong one anyway. |

Being wrong is the difficulty dial, not being slow. A relaxed seat misreads a fork about a third
of the time, a standard one about one fork in eight, and a sharp one never, off `Competence` and
`AimJitter`. Reaction delay still sets how long it sits before rolling, and still never buys it
anything.

It answers the fork the way a person does — nudging the highlight and pressing to confirm,
through `IParticipantInput` like everything else. It flicks the stick and lets go rather than
leaning on it, because a held stick either flies past the option it wanted or moves nothing after
the first frame. It argues the case once per fork and then remembers the answer, since a brain
that re-reasoned every frame would leave the highlight twitching for as long as the question was
up.

## Scene conventions

One scene per minigame, each holding a camera, lighting, and a single root object carrying the
controller. Repetitive and dynamic geometry (the 25-block grid, the hazard ring, projectiles) is
generated at runtime — partly because nobody hand-places a 5×5 grid, and partly because
`GameObject.CreatePrimitive` assigns a built-in-pipeline material that renders magenta under URP.
Everything generated routes through `Palette`.

Fixed reference points *are* authored, so scenes stay meaningful to edit: Bumper Sparks has three
draggable spawn markers wired into the controller.

`Board.unity` is the exception: the board, the tokens and the HUD are all generated at runtime,
so the scene is a camera, a light and one root object. The one thing authored by hand — where
each of the 27 spaces sits — lives in `BoardLayout.CreateDefault` as coordinates, where a fork
can be moved by editing a number instead of by dragging tiles in a scene nobody can diff.

UI is generated by `UiKit` rather than authored, because the menu is a data-driven grid and the
HUD changes shape per game. It uses legacy `UnityEngine.UI.Text` with the built-in font, not
TextMeshPro — TMP silently renders nothing until "TMP Essential Resources" is imported, which is
a nasty failure mode for generated UI. Swap `UiKit.Label` once you have imported them.

The menus are driven by direct device polling rather than an `EventSystem`. A couch party game is
driven by whoever grabs a controller, and it avoids the setup that generated UI needs for the
Input System's UI module.

Character animation is generated too: `CharacterAnimationBuilder` writes the octopus clips and
controller into `Resources/Animation`, and `CharacterAnimation` drives them. Clips fire audio
through `CharacterAudioRelay`, which looks entries up in `Sfx` by name.

## Playing and editing

Press Play on **Boot**. You can also press Play on any minigame scene directly, or on **Board**
— the controller notices nothing initialised it and configures a Player-1-plus-CPU session, so
you can iterate on one game, or on the board, without going through the menu.

Build settings carry 9 entries: Boot, MainMenu, Board and the six minigames.

`SushiPartyEditorTools` keeps three headless entry points for CI, none of them on a menu:

- `SyncScenesToBuildSettings` — rebuilds the list from `MinigameLibrary`, Boot first and Board
  third. Entries it does not own are preserved but disabled, never dropped.
- `ValidateFromCommandLine` — opens all nine scenes, checks each has a camera and a controller
  whose `Id` matches the roster, and reports missing scripts or unassigned references.
- `CreateBoardSceneFromCommandLine` — writes `Board.unity` from nothing: a camera framing the
  whole board, the light the minigames are lit with, and one root carrying `BoardController`.
  Safe to re-run; it refreshes an existing scene in place.

`CharacterAnimationBuilder.GenerateFromCommandLine` regenerates the octopus clips and controller
the same way.

The one editor menu item is **SushiParty > FMOD Cheat Sheet**.

## Tests

**377 EditMode tests** across 26 files, and the whole suite runs in well under a second, because
not one of them loads a scene, instantiates a `GameObject` or needs FMOD.

That is possible because of the assembly split and the plain-C# modules above — `RoundClock`,
`Vulnerability`, `MenuSelection`, `BoardSession`, `MixerSettings` and the read-model seam — so
there is something to test that does not need a scene running.

### Assemblies

| Assembly | Where | What it is |
| --- | --- | --- |
| `SushiParty.Runtime` | `Scripts/` | All gameplay. References Input System, UGUI, FMODUnity, Cinemachine. |
| `SushiParty.Editor` | `Scripts/Editor/` | Editor platform only, references Runtime. It deliberately does *not* reference URP — the board-scene builder adds URP's per-object components by type name instead, because taking on a render-pipeline dependency to add two components is a bad trade for a tool that has to keep compiling whatever the project renders with. |
| `SushiParty.Tests` | `Tests/` | Editor platform only, `nunit.framework.dll`, and constrained to `UNITY_INCLUDE_TESTS` so it ships in nothing. |
| `SushiParty.Prototype.ShaderLook` | `Prototypes/ShaderLook/` | The throwaway look study. Delete the folder and nothing else notices. |

Every script in the project sits in one of these, so Unity generates no `Assembly-CSharp`.

### What is covered

| Suite | Tests | What it pins |
| --- | --- | --- |
| `BoardSessionTests` | 50 | Turn order, every space kind, the shrine's price and relocation, Coins flooring at zero, the stake both ways, the ten-round end, the draw going to Chef Tako, the walk stopping dead at an unanswered fork, and every space being reachable from Start. |
| `BoardBrainTests` | 40 | That it rolls on its own turn and nobody else's; and at a fork, that it shoves the highlight toward the shrine when it can pay and toward the shortcut when it cannot, never confirms a way on the highlight is not sitting on, flicks the stick rather than leaning on it, and that a sharp seat reads the fork more reliably than a relaxed one. |
| `MixerSettingsTests` | 27 | Levels, mute, reset, persistence, and surviving a bank reload. |
| `PauseControllerTests` | 24 | Which buses hold and which keep playing, and that a pause left behind by a scene change is released. |
| `MenuSelectionTests` | 22 | Per-row and per-column wrap, hover, the seat toggles, both skill dials, and that there is only one copy of the setup. |
| `FmodCheatSheetTests` | 22 | That the window and `GameAudio` agree in both directions. |
| `BoardFlowTests` | 21 | What the select screen records for a board game, and that one minigame settles exactly one round — including a duplicate result, an abandoned round, and a finished session. |
| `RoundClockTests` | 16 | Briefing → countdown → clock → settle → results, and the five-second warning firing exactly once. |
| `CharacterAudioRelayTests` | 15 | That an animation event naming a missing entry costs a message, never a round. |
| `BoardAudioReporterTests` | 14 | Which globals a board turn publishes, and what it leaves alone. |
| `VulnerabilityTests` | 12 | The hit gate, both windows extending but never shortening, and the one blink cadence. |
| `ContinueGateTests` | 10 | The minimum hold, and an unattended board letting itself out. |
| `AllCpuAdvanceTests` | 10 | That a board playing itself never stalls on a press nobody is there to make. |
| Six minigame brain suites | 48 | One per game, 8 each, all driven off hand-built snapshots. |
| `MusicCatalogueTests` | 9 | That every minigame has a track and none is silent by omission. |
| `CpuBrainSnapshotTests` | 9 | The seam itself, on a fake snapshot and a fake brain. |
| `TitleSelectionTests` | 8 | The title screen's three entries and their wrap. |
| `CharacterAnimationTests` | 7 | The generated controller's parameters. |
| `AudioParameterTests` | 6 | Labelled and numeric parameters, and the invalid case. |
| `GeneratedClipAudioTests` | 5 | That the generated clips carry the audio events they claim to. |
| `AssemblyWiringTests` | 2 | That the test assembly can see runtime types at all. If this fails everything else here is unreachable. |

`MinigameFlow` is a `MonoBehaviour` that loads scenes, so the trips it makes cannot be run here —
but every decision it takes *before* it touches a scene can be, which is why those decisions live
outside the coroutines.

### Running them from the command line

Close the Editor first: Unity holds a project lock and a second instance will refuse. Run from
the Unity project root, the folder holding `Assets/`.

Compile check — `-quit` on its own imports and compiles, and nothing else. Grep the log for
`error CS`:

```bash
"C:/Program Files/Unity/Hub/Editor/6000.0.34f1/Editor/Unity.exe" \
  -batchmode -quit -nographics -silent-crashes -disable-assembly-updater \
  -projectPath . \
  -logFile Logs/compile.log
```

The tests:

```bash
"C:/Program Files/Unity/Hub/Editor/6000.0.34f1/Editor/Unity.exe" \
  -batchmode -nographics -silent-crashes -disable-assembly-updater \
  -runTests -testPlatform EditMode \
  -projectPath . \
  -testResults Logs/editmode.xml \
  -logFile Logs/tests.log
```

**No `-quit` on the test run.** It quits the editor before the runner has finished and you get no
results — which looks a lot like a clean run if you are only watching the console.

`-runTests` reports through the exit code, not the log:

| Exit code | Means |
| --- | --- |
| 0 | The run finished and every test passed. |
| 2 | The run finished and something failed. The failures are in `-testResults`. |
| 3 | The run could not start. Almost always a compile error — read `-logFile`. |

The results file is NUnit XML; its root element carries `total`, `passed` and `failed`.

## Adding a seventh minigame

1. Add a `MinigameId` and a `MinigameDefinition` entry in `MinigameLibrary`, with
   `implemented: false` and its scene name.
2. Copy an existing minigame scene, rename it, give its `.meta` a fresh GUID, and put
   `PlaceholderMinigame` on the root object — it identifies itself from the scene name, so there
   is nothing else to wire. You now have a working card that shows the spec.
3. Add the scene to Build Settings.
4. When you build it for real, swap the controller, implement `Id`, `OnPrepare`, `OnPlay` and
   `CreateCpuBrain`, and flip `implemented: true`.
5. Publish a `<Game>Snapshot` struct and one public `Snapshot()` that fills it, and derive the
   brains from `CpuBrain<TSnapshot>`. That is the whole brain-facing surface, and it is what lets
   the brains have tests before the scene has geometry.
6. Add its events to `Sfx` and to the Studio project, and a track to the music catalogue.
7. Check the scene has a camera and a controller whose `Id` matches the roster.

It becomes eligible for a board round the moment `implemented` is true, so nothing else needs
wiring for it to turn up between rounds.

If the new game involves walking and ground-pounding, start from `PoundCharacter` and
`PoundActuator` rather than writing movement again. If it involves getting hit, start from
`Vulnerability` rather than a fresh pair of timers.

## Rules sources

Objective text and mechanics were taken from the Super Mario Wiki minigame pages rather than from
memory:
[Mario Party 9 minigame list](https://www.mariowiki.com/List_of_Mario_Party_9_minigames),
[Bumper Sparks](https://www.mariowiki.com/Bumper_Sparks),
[Sand Trap](https://www.mariowiki.com/Sand_Trap),
[Crossfire Caverns](https://www.mariowiki.com/Crossfire_Caverns),
[Pair of Aces](https://www.mariowiki.com/Pair_of_Aces),
[Zoom Room](https://www.mariowiki.com/Zoom_Room),
[Pedal to the Paddle](https://www.mariowiki.com/Pedal_to_the_Paddle).
