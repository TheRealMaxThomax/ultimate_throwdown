# Archive — old notes & deep fixes

**You rarely need this file.** Start with [`SESSION_NOTES.md`](SESSION_NOTES.md).

**What’s here:** Why we chose things, ragdoll debugging history, and copy-paste fixes when something breaks again.

---

## Short history (why the game works this way)

- **Ball:** `BallGrab` owns hold state; `BallThrow` is separate. Online = host approves grab/drop/throw.
- **No kicking / no body-push:** Auto-grab when you touch the ball (like Extreme Football Throwdown). Pushing the ball on all clients was too unreliable.
- **Tackle:** Don’t ragdoll the player object — spawn a **host-owned ragdoll** object. Host applies **pelvis `ApplyImpulse`**, then **`NetworkSpawn`**. **`PreLaunchPauseSeconds` > 0:** visible freeze → launch. **`TackleImpactFeel`** + **`CombatFeelPredictDedupe`** = owner-only camera juice with client predict (2026-06-14).
- **Dodge:** Double-tap strafe; shove uses look direction (`EyeAngles`), not body rotation.
- **Cosmetics:** Own component; don’t mix with spawn logic. Use `CreateFromConnection(..., false)` for other players’ clothes.
- **Classes:** Stats in `.cdata` / `ClassData` in `PlayerClass.cs` — don’t split into extra files (caused compile errors before).

---

## Full decision log (with dates)

<details>
<summary>Expand — every dated decision (for AI / archaeology)</summary>

- **2026-04-28** — `BallGrab` = source of truth for hold; `BallThrow` separate; `MainBall` + name fallback.
- **2026-04-29** — Host RPC for ball actions; scene `throwdown_prototype` + `GameNetworkManager`.
- **2026-04-30** — `PlayerCosmeticsSync` separate; clothing API `removeUnowned=false`; LOD lock after apply.
- **2026-05-04** — Auto-grab; no kick; composition not “god object”; drop uses player facing.
- **2026-05-06** — Tackle direction toward victim; pelvis `ApplyImpulse`; disable capsule immediately; separate ragdoll GO; ragdoll clothing via `BoneMergeTarget`; client tackle RPC.
- **2026-05-18** — MP launch parity: poll bodies → impulse → `NetworkSpawn` (not spawn-then-fixed-delay). Owner `ownerTackleChargeBonus` in `RequestTackleApplyOnHost`. **Don’t** use `StartAsleep` or collision-sound mute without explicit wake — zero launch.
- **2026-05-07** — Ragdoll camera + stand-up blend; ball XOR weapon (future); weapon speed penalties (future).
- **2026-05-08** — Dodge shove from `EyeAngles.ToRotation().Right`; `PlayerDodge` in `CatchUpSpeedBoost.cs`.
- **2026-05-10** — `practice_npc` tag; `Network.IsOwner` for camera; momentum multiplier; forward intent for charge.
- **2026-06-06** — Stand up when grounded + settled; `RagdollMaxDuration` cap.

</details>

---

## Ragdoll (technical — if tackles look broken)

**Don’t** put `ModelPhysics` on the **player prefab** for tackles — the client owns that object and fights the physics.

**Do:** Host spawns local `PlayerRagdoll` with `ModelPhysics`, hide player mesh, sync pelvis for camera (`NetRagdollPosition` / `RagdollClientFeel` on victim owner).

**Launch (current):**
1. `CopyBonesFrom` victim renderer.
2. Poll up to `RagdollPhysicsInitDelay` (default **0.08s**) until `Bodies.Count > 0`.
3. `ApplyImpulse` on pelvis (`Bodies[0]`) — `launchVelocity * mp.Mass` (not `PhysicsGroup.Velocity`).
4. **`NetworkSpawn()` after impulse** so clients don’t see a stationary ragdoll then a late launch.

**Client attacker:** `RequestTackleApplyOnHost` sends owner positions + `ownerTackleChargeBonus` (Juggernaut ramp mirror). Host uses `max(hostBonus, ownerBonus)`. Validate hit on **owner** snapshot only — extra host charge/distance gates feel laggy.

**Dead ends (2026-05-18):** `StartAsleep = true` before impulse → no launch. Muting `EnableCollisionSounds` alone was fine in theory but shipped with sleep; reverted both.

**Stand-up trace:** Exclude colliders tagged `ragdoll` so limbs don’t count as floor.

**Symptom: client tackle much shorter than host** — usually was spawn-then-delay-then-impulse + weak host-side Juggernaut bonus, not `TackleLaunchSpeed` alone.

---

## Fix: client free-ball jitter

**Symptom:** Host ball looks fine; client ball bounces or jitters.

**Fix (already in code — don’t undo without reason):**
1. Host still owns real ball physics.
2. On client, `BallClientFeel` doesn’t run local rigidbody sim on free ball (colliders stay on).
3. Client follows **delayed snapshots** (`InterpolationDelay`), not every frame’s latest position.

**Test:** Two windows, same jump-drop test from same spot.

---

## Fix: component missing in editor

1. Save scripts, wait for compile.
2. Search Add Component again.
3. Recompile or restart s&box if still missing.
4. Fix any compile errors first — errors hide components.

---

## Older handoff log

<details>
<summary>Expand — 2025 ragdoll debug sessions</summary>

- **06/05** — Switched to separate ragdoll object; both screens see ragdoll.
- **06/05** — Launch direction, capsule disable, `TackleLaunchSpeed` too low (150) vs working (~600+).
- **06/05** — Stand-up hover fixed with `ragdoll` tag on limb colliders.

</details>

---

## 2026-06 session chronicle (moved from SESSION_NOTES)

<details>
<summary>Expand — throw anim, ball carrier UX, traffic, charge_run dev log</summary>

- **2026-06-11** — `utd_citizen_human_throw.vmdl` trimmed to `throw_windup` + `charge_run`; graph `1D Blendspace B` wiring; `PlayerBallHoldAnim` animgraph-only charge; `EnsureCustomAnimGraph()` force re-assign after cosmetics; ModelDoc stale cache → editor reboot (ragdoll flop missing).
- **2026-06-10** — `PlayerBallHoldAnim` hold/throw + `ThrowReleaseDelaySeconds`; FBX pipeline in `CITIZEN_ANIMATION_WORKFLOW.md` (ScaleAndMirror 0.3937).
- **2026-06-09** — Ball carrier UX: `hold_R`, `BallCompassHud`, `BallCarrierOutline`, `ball_v2.vmat`; throw polish 2-window MP OK.
- **2026-06-08** — `ThrowTrajectoryPreview`, `ThrowChargeCamera`; ragdoll cam → `PlayerTackle.OnPreRender`.
- **2026-06-05–07** — Road0/Road1 traffic MP; sign flicker removed.
- **2026-06-02** — `feature/human-avatar` branch; citizen_human body.

</details>

---

## MP combat feel predict (2026-06-14 — moved from SESSION_NOTES)

<details>
<summary>Expand — Tier 0–A3 + A2b sprint</summary>

**Shipped:** Client-owner predict for blitz dasher stop/feel (Tier 0), tackle attacker (A1), victim on pre-launch freeze (A2), traffic on direct ragdoll (A2b), shared **`CombatFeelPredictDedupe`** (A3). Host authority unchanged; knockdown/ragdoll/score still host-only.

**Key files:** `SpeedsterSpeedBlitzUlt.cs`, `PlayerTackle.cs`, `CombatFeelPredictDedupe.cs`, `SpeedBlitzAimPreview.cs` (`NetworkMode.Never` for owner preview meshes).

**Verified:** 2–3 window idle-target soak feels good. **Not verified:** moving-target fairness (→ practice scene + Tier C1 lag-comp). **Speed Blitz 2d MP:** wind-up sparks, body glow, discharge — pending 2-window verify (2026-06-18).

**False-positive policy (dash v1):** predicted hit but host miss → stay stopped until host ends dash.

Full roadmap → [`MULTIPLAYER_NETCODE.md`](MULTIPLAYER_NETCODE.md).

</details>

---

## Speed Blitz 2d session detail (2026-06-16 — moved from SESSION_NOTES)

<details>
<summary>Expand — 2c sign-off + early 2d VFX</summary>

- **2026-06-16 (night):** electric hard cut at connect ✅; **`speedblitzwindupvfx`** prefab shipped (later scoped to wind-up only on 2026-06-18).
- **2026-06-16 (2d code):** **`SpeedBlitzWindUpFeel`** — parented clone, **`StopElectric(0)`** at connect; three wind-up sounds via **`PlayFeelSoundAt`**.
- **2026-06-16 (2d design):** sound stack: electric / windup / dash (**speedblitz_dash**, not ragdoll LaunchSound).
- **2026-06-16 (2c):** dash tuning signed off; ult blue comic; MP remote wind-up plant + throw hold clear; ball strip on carrier connect intentional.
- **2026-06-16 (MP remote anim):** blitz wind-up plants on all clients; throw hold RPC fixes.

</details>

---

## Moved from SESSION_NOTES cleanup (2026-07-06)

The sections below were trimmed from [`SESSION_NOTES.md`](SESSION_NOTES.md) to keep the session cheat sheet under ~250 lines. Design specs live in [`GAMEPLAY_DESIGN.md`](GAMEPLAY_DESIGN.md); wiring in [`ARCHITECTURE.md`](ARCHITECTURE.md).

**Full pre-cleanup text (778 lines):** recoverable from git — `git show HEAD:SESSION_NOTES.md` (last committed version before 2026-07-06 cleanup). Nothing was deleted without a home here, in those docs, or in git history.

---

## Editor checklist (full)

**Main Camera (manager):**
- `GameNetworkManager` — `PlayerTemplateRoot`, **`SpeedsterPlayerTemplate`** / **`JuggernautPlayerTemplate`** / **`SniperPlayerTemplate`**, `Team0Spawns` / `Team1Spawns` (6 each)
- `MatchDirector` — `BallSpawn` wired; `Enable Match Debug Logs` optional; `Enable Debug Force Goal` off for ship
- `MapMatchConfig` — team display names
- **`EnemyOutlineCameraSetup`** on Main Camera (adds `Highlight` post-process) — **or** add **`Highlight`** (Post Processing) yourself; keep **Enable Post Processing** on the camera
- **`MatchAudioBootstrap`** — auto via **`GameNetworkManager`**; **`Disable Room Simulation`** on (outdoor default). Uncheck for indoor/tunnel maps.

**`MatchHud` empty (scene UI root):**
- `MatchScoreHud`, `MatchClockHud`, `GoalBannerHud`, `IntermissionHud`, **`MatchOverHud`**
- **`OutOfBoundsBannerHud`** + **`BallOobDropZoneHud`** auto-add on **Main Camera** via **`GameNetworkManager`** — tune in **edit mode** (save scene): **`Ring Model Path`**, **`Ring Material Path`** (`oob_drop_ring.vmat`), **`Ring Outline Extra Diameter`**, **`Stack Panel Size`** / font sizes / **`Stack Row Gap`**
- **`BallOutOfBoundsHost`** on **`main_ball`** — auto via **`GameNetworkManager`**, or add manually

**Map:** Two **`GoalZone`**; **`BallSpawn`** → `MatchDirector`; **`OutOfBoundsZone`** strips + **`playerclip`** walls; **`ball` + `playerclip` → Ignore** in **`Collision.config`**. Traffic: **`TrafficSpawner`** + disabled **`TrafficCarTemplate`** per lane.

**Player prefab (per class):** `BallGrab`, `BallThrow`, `CatchUpSpeedBoost`, `PlayerDodge`, `PlayerTackle`, `PlayerUltCharge`, `PlayerClass`, movement HUDs, **`UltChargeHud`**, **`ThrowChargeBar`**, **`ThrowTrajectoryPreview`**, **`ThrowChargeCamera`**. Speedster-only: **`SpeedsterSpeedBlitzUlt`**, **`SpeedBlitzAimPreview`**, blitz feel/cam/glow. Tune dodge: **`DodgeChannelDurationSeconds`** + class **`DodgeDistance`**. **`PlayerUltCharge`** + ult **`MaxChargePoints`** — manual on prefab, not GNM auto-add. Full tuning knobs (blitz, throw, tackle feel, charge_run anim) → pre-cleanup git version or Turf Wars scene defaults.

**`main_ball`:** `BallCarrierOutline`, **`BallPassAssistState`**, OOB host components.

**Practice arena:** **`PracticeArenaMode`**; team-0 spawns; **`PracticeLaunchMeasure`** / **`PracticeLaunchReadout`**; **`practice_npc`** dummies — **never `NetworkSpawn`**. Patrol runner: **`PlayerController` disabled**, **`PracticeNpcPatrol`** Point A/B.

---

## Gameplay implementation notes

<details>
<summary>Expand — throw preview, blitz, tackle, traffic, cameras</summary>

- **Throw preview:** Owner-only arc + landing marker; `ThrowReleaseMath` shared with `BallThrow`.
- **Ball carrier glow / compass:** `BallCarrierOutline`, `BallCompassHud` — LOS, team colours.
- **Owner camera FOV:** **`PostCameraSetup`** only. Order: throw cam before blitz dash cam.
- **Speed Blitz hits:** Contact + LOS + vertical cap; preview ≠ guaranteed hit.
- **Tackle:** Host ragdoll; pre-launch pause; predict feel + dedupe.
- **Traffic:** Host-only; variants after spawn.
- **Practice NPCs:** Snapshot; RPC knockdown + patrol pose relay.

</details>

---

## Prefab split + loadout — shipped handoff (2026-07-06)

**Shipped:** Code 1–4, editor prefab split, intermission picker, join sync RPC.

**Join sync editor smoke:** Host double `[PlayerLoadout]` on joiner spawn; no errors. Same SteamId ≠ cross-host proof.

**Future (slice 2b):** Walkable intermission + **`MatchSetup`** pre-match timer.

---

## Ult implementation slices — shipped

| Slice | Status |
|-------|--------|
| **1** Shared charge + HUD | ✅ |
| **2a–2d** Speed Blitz | ✅ |
| **3** Assist charge | ✅ |
| **4** Per-ult max points | ✅ |

Design → [`GAMEPLAY_DESIGN.md`](GAMEPLAY_DESIGN.md).

---

## Map slice 1 — ball OOB (shipped 2026-07-04)

Dwell → whistle → banner → drop marker → sky-drop at last-touch feet. Host auto-grab. Off in practice mode.

---

## Tackle comic text — shipped (2026-06)

`TackleComicTextHud` + `TackleComicBurst`. Host-synced randoms. Les Flos + MP verify optional.

---

## Future slices — implementation specs

### Slice 2b — `MatchSetup` + walkable intermission (after ult slice 5)

- **Intermission movement** — walkable spawn room (gate change; team spawns OK until room art)
- **`MatchSetup` phase** + pre-match timer (round 1 **and** rematch)
- **Rematch (when `MatchSetup` exists):** `MatchOver` → **`MatchSetup`** (swap window) → `Playing` — not straight to `Playing` like today
- **Starter room** art/layout (cosmetic polish)
- v1 intermission today = frozen + Q menu — do not block other slices on this

Design → [`GAMEPLAY_DESIGN.md`](GAMEPLAY_DESIGN.md) § Loadout (when swaps allowed).

### Ult slice 5 — Juggernaut ground stomp (next)

- [ ] New component `Code/Ultimates/` — AOE knockdown around self
- [ ] Reuse `ApplyKnockdownFromHost`; MOBA preview pattern as needed
- [ ] Same `PlayerUltCharge` gate + commit rules; register in ult catalog

### Ult slice 6 — Sniper path zones

- [ ] Requires **ball**; exception to “no ult while holding”
- [ ] Zones along throw path — ties into `BallThrow` / trajectory
- [ ] Most complex of the three first ults

### Progression slice (after weapons slice 7)

**Why:** Grind gates must not live in client-editable JSON.

- [ ] **Server-trusted** unlock + XP + skill-point storage (not `FileSystem.Data` alone)
- [ ] **`LoadoutAuthority.IsLoadoutAllowedForPlayer`** — host rejects locked options (stub returns true today)
- [ ] Picker filters to **unlocked only**; earn flow + UI for skill points
- [ ] Optional **cloud** sync for loadout prefs (progression writes still server-side)

---

## Combat slice spec (full)

### Combat slice 1 — unarmed melee (`PlayerMelee`)

**Why:** Scrappy knockdown when there’s no room to **charge** tackle (future sumo shrink endgame, tight spaces). Weaker than tackle; **2 hits** to confirm. **Weapons slice 7** reuses this pipeline for armed LMB swings.

**Movement tiers (3 only):** Walk → Sprint → Charge (`CatchUpSpeedBoost.IsAtChargeSpeed`). **Tackle** = charge tier only. **Melee** = walk + sprint only — **blocked at charge tier**.

**Input:** **LMB** tap without ball → melee (future: weapon swing when armed). Hold LMB with ball = throw charge (unchanged).

**Hierarchy:** Tackle (charge, 1 hit, unparriable, class mass) → unarmed 2-hit → parry punish (slice 2).

#### Combat slice 1a — core (solo / host)

- [ ] **`Code/Player/PlayerMelee.cs`** — host validates hits; owner predict feel (like tackle — **`CombatFeelPredictDedupe`**)
- [ ] **LMB** swing — short range, aim at target. Active hit frames ~**0.2–0.3s**. **Whiff** uses swing recovery (anti-spam)
- [ ] **Per-victim combo:** **2 hits** within **`ComboWindowSeconds`** (default **5**) from first hit → knockdown. Stacks persist across target switches
- [ ] **Hit 1:** hitmarker + tick SFX; micro-hitstop; tiny knockback. **No** ball drop. **No** victim speed-tier drop
- [ ] **Hit 1 on ult wind-up:** chip only — no knockback; wind-up not interrupted until knockdown
- [ ] **Hit 2 / knockdown:** **`ApplyKnockdownFromHost`** — weak universal impulse (not class-scaled). Ball drops. Enemy-only ult charge **+10**
- [ ] **Knockdown** interrupts committed ult wind-up (needs 2 connects). Hit 1 does **not** reset wind-up timer
- [ ] **Can melee hit ball carriers**; **carriers cannot** melee or parry
- [ ] **Cannot melee while:** holding ball, ragdolled, active ult, dodging, ~**1s after dodge**, charging throw, **`IsAtChargeSpeed`**
- [ ] **Dodge iframes** respected; no hits on ragdolled victims or post-stand-up invincibility
- [ ] **Friendly fire:** can hit teammates; **no** ult charge on FF knockdown
- [ ] **No** tackle comic on hit 1 — hitmarker only

#### Combat slice 1b — MP + tune

- [ ] 2-window verify; tune range, swing recovery, combo window, knockback, ragdoll impulse
- [ ] [`MULTIPLAYER_NETCODE.md`](MULTIPLAYER_NETCODE.md) checklist — owner predict + host dedupe on confirm

### Combat slice 2 — parry (later)

- [ ] **Melee swings only** — **cannot** parry tackles
- [ ] Successful parry → next melee confirm on that attacker = **1-hit** knockdown (within window)
- [ ] Not while holding ball

Summary also in [`GAMEPLAY_DESIGN.md`](GAMEPLAY_DESIGN.md) § Combat slice 1.

---

## Multiplayer testing (full)

See also [`MULTIPLAYER_NETCODE.md`](MULTIPLAYER_NETCODE.md) → **Testing** after predict/lag-comp work.

1. Start Play (host).
2. Network menu → **Join via new instance** (second window = client).
3. Check both windows: grab, throw, tackle (**host→client and client→host**, similar launch distance), dodge, **enemy red outlines** (standing + ragdoll, both directions), **goals, reset, intermission, match over, rematch, HUD**, **traffic** (Road0 + Road1: 3 model variants per lane, knockdown, **ball bounce on host**, engine idle/drive).
4. **Throw polish:** trajectory arc + landing marker, charge camera/bar; **planted charge + RMB cancel**; tackle while charging (ragdoll cam OK).
5. **Ball carrier glow:** teammate = white ↔ green; enemy = white ↔ red; **you carry** — no glow; behind wall — no glow.
6. **Ball compass:** triangle orbits ring toward ball; green / red / white by possession; you carry → **BALL** hub + ring, no triangle.
7. **Held ball:** sits on carrier’s **right hand** (`hold_R`), not hip; both windows agree.
8. **Hold/throw anim:** **holditem** while carrying; throw motion on release; **ball leaves hand** after **`ThrowReleaseDelaySeconds`**; remote sees anim.
9. **Charge run overlay:** no ball, max ramp — `charge_run` on **both** windows (remote uses **`NetAtChargeSpeed`**).
10. **Tackle juice:** hitstop/shake/punch on connect; victim **visible freeze** then launch when **`PreLaunchPauseSeconds` > 0**; host→client and client→host.
11. Spam actions once to probe desync.
12. **Ult charge:** % creeps in **Playing** only; frozen in celebration/intermission; goal/tackle bumps; assist +25; FF tackle no bump; persists rounds; rematch clears.
13. **Combat feel predict:** client tackler / dasher / victim / car-hit — juice on contact frame, no double feel.
14. **Speed Blitz 2d (MP):** pose + glow + SFX on remotes; dash hits wall-blocked; join-client spark sprites = blue squares (editor — publish only).
15. **Ball OOB:** whistle + banner + sky-drop; rematch cancels marker.
16. **Practice arena NPCs (MP):** knockdown RPCs + patrol pose relay; no self-launch on static dummies.
17. **Dodge channel:** no long glide; horizontal stop at channel end.

**Per-slice verify table (future):**

| After slice | Verify |
|-------------|--------|
| **Combat 1** | 2-hit unarmed LMB, walk/sprint only, carrier can’t attack, enemy KD +10, ult wind-up chip rules |
| **Combat 2** | Parry → 1-hit punish (melee only) |

**Ball jittery on client?** → “Client free-ball jitter” (this file). **Client tackle short/late?** → “Ragdoll (technical)”.

---

## UI typography (deferred pass)

**Decided (2026-07-04):** two-font split — **not wired in HUD yet** (still Poppins in code defaults).

| Role | Font | Where |
|------|------|--------|
| **Display / comic** | **Les Flos** Sage (± Sans/Chaos on tackle tiers) | Comic bursts, OOB world stack, future menu **titles** |
| **HUD / UI body** | **Barlow Condensed** (SIL OFL) | Score, clock, ult %, menus, loadout body |

**When we do the pass (order):** (1) `PracticeLaunchReadout.ScoreFontFamily` smoke test → (2) match HUD → (3) owner HUDs → (4) menus/loadout. **Do not** put Barlow on OOB stack or comic bursts.

---

## MP gotchas (consolidated)

- Clients use **`PlayerTeam`** sync, not local `MatchDirector`
- Don't add laggy host charge/distance gates on tackle RPC
- Traffic variants after `NetworkSpawn`; spawner only when **`Game.IsPlaying`**
- Never `NetworkSpawn` practice NPCs; patrol pose from host relay
- New combat features: follow [`MULTIPLAYER_NETCODE.md`](MULTIPLAYER_NETCODE.md) checklist

---

- Clients use **`PlayerTeam`** sync, not local `MatchDirector`
- Don't add laggy host gates on tackle RPC
- Traffic variants after `NetworkSpawn`
- Never `NetworkSpawn` practice NPCs

---

## Settled decisions (removed from Open decisions 2026-07-06)

W+S mutex fixed; blitz preview v3 shipped; blitz 2d VFX scope shipped; outdoor reverb off; dodge channel shipped; ball strip on blitz connect intentional.

---

## Recent session notes (pre–2026-07-06)

<details>
<summary>Expand — June–early July 2026</summary>

- **2026-07-05** — Scene default sync; dry audio; tackle SFX; hold pose clear on stand-up
- **2026-07-04** — Dodge channel; throw plant/cancel; OOB MP OK
- **2026-07-03** — Map slice 1a
- **2026-07-02** — Slice 4 playtest OK
- **2026-06-30** — Aim preview v3; assist OK
- **2026-06-23** — Practice patrol MP
- **2026-06-14** — Combat feel predict

</details>

---

## Ragdoll arms z-fight (full note)

Body skin fixed via `CopyFrom`. Arms z-fight likely overlapping bone-merged arm mesh on ragdoll. Left as-is for v1.

