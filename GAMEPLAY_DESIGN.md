# Gameplay design

**For:** Planning and tuning — dodge, tackle, classes, **ultimates**, future weapons.  
**Not for:** Daily “what do I do today?” → use [`SESSION_NOTES.md`](SESSION_NOTES.md).

---

## Built vs not built

| Feature | Status |
|---------|--------|
| Walk → sprint → charge speed | Built |
| Throw with charge | Built |
| Throw trajectory preview (owner, first arc) | Built — `ThrowTrajectoryPreview`; no bounces; all classes |
| Auto-grab ball | Built (serves as **catch** — no separate catch action) |
| Pass (to teammate) | Built via **charged throw** — no separate pass button |
| Throw cooldown | **Not planned** — `PickupDelayAfterThrow` only blocks instant re-grab after release |
| Tackle + ragdoll | Built (launch strength still tuning) |
| Dodge (double-tap strafe) | Built |
| Teams + team spawns (balance on join) | **Built** — see [`MATCH_FLOW_PLAN.md`](MATCH_FLOW_PLAN.md) |
| Match phases (celebration / intermission / timer / OT) | **Built** — `MatchDirector` |
| Goal zones + dwell scoring | **Built** — `GoalZone`; own-goal impossible by `DefendingTeam` |
| Post-goal reset (teleport, ball center, intermission freeze) | **Built** (slice 4); MP via `PlayerTeam` sync |
| OVERTIME setup (tied timer → reset + intermission) | **Built** — `BeginOvertimeSetup()` |
| Match HUD (score, clock, banner, countdown) | **Built** — placeholder draw on `MatchHud` root |
| Match over + rematch | **Built** — 10s celebration, then host **`1`** (`Slot1`) same-map rematch |
| Map vote (30s, all players) | **Not built** — planned; see [`MATCH_FLOW_PLAN.md`](MATCH_FLOW_PLAN.md) → Later |
| Enemy team outline | **Built** — `Highlight` on camera + `HighlightOutline` on players; ragdoll copy via `RagdollEnemyOutline` |
| Ball carrier glow (held ball) | **Built** — `BallCarrierOutline` on `main_ball`: white ↔ green (teammate) / white ↔ red (enemy) pulse + emissive breathe; everyone except carrier; line-of-sight only (no through walls); tune `OutlineWidth` on ball |
| Ball compass HUD | **Built** — `BallCompassHud`: **BALL** hub + ring-edge triangle toward `main_ball` (white / green / red by possession); player `EyeAngles` bearing; triangle hidden when you carry |
| Held ball position | **Built** — `BallGrab` follows **`hold_R`** on Body `SkinnedModelRenderer` (right hand); `HoldAnchor` fallback. Carry **anim pose** not built (human graph) |
| Ball visual (hero) | **WIP** — `ball_v2.vmat` emissive gold + texture scroll; neutral albedo; team read from glow + compass |
| Crouch / duck | **Disabled** — `PlayerDisableCrouch`; `Duck` unbound in `Input.config` |
| Weapons | **Not built** |
| Class passives | **Partial** — Juggernaut tackle ramp built; others not built |
| Per-class prefabs + spawn | **Built** — `Player_Speedster` / `Player_Juggernaut` / `Player_Sniper`; GNM clones from committed class |
| Loadout v1 (picker, save, join sync) | **Built** — intermission + practice swaps; force-commit; class change = host respawn; join RPC shipped (cross-machine verify at publish) |
| `MatchSetup` + walkable intermission | **Not built** — v1 intermission = frozen + Q menu (slice 2b, after Jugg stomp) |
| Ultimates (charge + Speed Blitz) | **Partial** — charge + assist ✅ + **Speed Blitz 2a–2d ✅** + per-ult max ✅; Jugg/Sniper ults planned (slices 5–6) |
| Unarmed melee + parry | **Not built** — combat slices 1–2 (after ults 5–6) |
| Ball OOB (map slice 1) | **Built** — dwell, whistle, sky-drop, 2-window MP OK |

---

## Speed tiers (movement)

Think of three gears:

1. **Walk** — slowest  
2. **Run (sprint)** — middle  
3. **Charge** — fastest; **only at charge speed can you tackle**

**Dodge drops you one gear** (example: charge → run, run → walk). Ball carrier after dodge → walk.

**Re-enter charge after dodging from charge:** about **2 seconds** before you can charge again (and tackle again).

---

## Dodge (how it should feel)

**Input:** Double-tap strafe left (A) or right (D). Not a separate dodge button.

**Movement (shipped 2026-07-04):** Owner slides laterally up to class **`DodgeDistance`** (literal units) over **`DodgeChannelDurationSeconds`** on the prefab. Works on ground **or** in air (ledge-friendly). **Hard horizontal stop** when the channel ends — Speed Blitz-style; no post-dodge glide. Fixes dodge+jump velocity exploit.

**After dodge you get:**
- A sideways shove
- A very short “can’t be tackled” window (~0.14 s) — not full invincibility
- A cooldown (~3.5 s; slightly shorter if you have the ball)

| You were at… | After dodge you go to… |
|--------------|-------------------------|
| Charge | Run |
| Run | Walk |
| Walk | Walk (still walk, but you get the shove + iframe) |
| Ball carrier | Walk |

**Throw while charging:** Only **Sniper** class may dodge during throw charge (others cannot). Exact cancel rules still TBD.

**Design goal:** Dodge should reward good timing, not let carriers jog to the goal by spamming dodge. Throwing/passing should stay the main way to advance.

**Tuning knobs (change one at a time in playtests):** class `DodgeDistance` (literal slide units), `DodgeChannelDurationSeconds` on `PlayerDodge`, cooldown, iframe length, how long until charge returns.

---

## Tackle (current rules)

- Must be at **charge speed**
- Host checks hit; clients can **request** a tackle with position/aim info
- Hit launches a **ragdoll copy** of the victim (separate object), not physics on the player prefab
- Victim drops the ball; ball gets knocked away
- Victim stands up after on ground and settled, or after a max time
- Brief invincibility after getting up
- **Friendly fire:** tackles can hit **teammates** today (no team filter on victim search). **Ult charge** is **not** awarded for friendly-fire tackles — enemy victims only.

**Multiplayer (built):** Host applies pelvis impulse on a local ragdoll, then `NetworkSpawn` (poll `RagdollPhysicsInitDelay` for bodies). Remote attacker RPC includes **owner Juggernaut charge bonus** so launch power matches what the client had. **Client feel predict (2026-06-14):** owner tackler/victim early camera juice via **`CombatFeelPredictDedupe`** — host still owns knockdown. See [`MULTIPLAYER_NETCODE.md`](MULTIPLAYER_NETCODE.md) and [`SESSION_NOTES_ARCHIVE.md`](SESSION_NOTES_ARCHIVE.md) → Ragdoll (technical).

**Launch tuning:** `TackleLaunchSpeed` and `TackleLaunchArc` on `PlayerTackle` — try values in the **400–800** range in playtests.

**Heavier vs lighter class:** Tackle power uses mass ratio (from `.cdata` `Mass` field).

---

## Three classes (stats)

All numbers live in **`.cdata` files** in the editor — not hardcoded in scripts.

| Class | Role (simple) |
|-------|----------------|
| **Speedster** | Light, small, reaches charge speed fastest |
| **Sniper** | Better throw; can dodge while charging throw (when passive/rules allow) |
| **Juggernaut** | Heavy, big, slow ramp; tackle gets stronger the longer you stay at charge |

**Juggernaut passive (built):** Stay at charge speed → tackle bonus stacks up to a cap. Drop below charge → bonus resets.

**Class ultimates (partial):** Shared charge + assist + per-ult max (slice 4 ✅). **Speedster Speed Blitz** 2a–2d + aim preview v3 ✅. **Juggernaut** ground stomp and **Sniper** path zones planned (slices 5–6).

**Pre–ult gap (Jugg/Sniper before slice 5/6) — approach A:** Classes pickable in loadout now; ult catalog empty → charge HUD runs, **X inactive**. When stomp/path zones ship → add to catalog; class switch auto-picks first ult.

---

## Loadout (Overwatch-style, casual v1)

**Status:** **Shipped 2026-07-06** (picker, persistence, prefab split, join sync RPC).

### Always equipped

Every player has a committed loadout (class + passive + ult when catalog has entries).

- **First session (no save):** preset **Speedster** + **`speed_blitz`** + default passive — **not** random.
- **After any save write:** last committed loadout persists across Turf Wars, practice, relaunch.
- **Class switch in picker:** auto-select **first passive** and **first ult** for that class.
- **`UltChargeHud`:** always on for all classes.

### Pending vs committed

- **Pending** — highlighted while loadout screen is open (Q).
- **Committed** — what spawn/combat use.
- **Force-commit:** when round begins (`Intermission` → `Playing`, future **`MatchSetup`** timer → 0, …) — close UI; pending → committed even if player never pressed Confirm.
- **Save writes:** first preset, picker Confirm, force-commit, host apply after class change — **not** every highlight while browsing.

### When loadout can change

| Phase | Turf Wars / match maps | Practice (`PracticeArenaMode`) |
|-------|------------------------|--------------------------------|
| **`MatchSetup`** (future) | Yes — pre-round timer + pick | — |
| **`Intermission`** | Yes (v1: frozen + menu; walkable room later) | — |
| **`Playing`** | Locked | Anytime |
| **`GoalCelebration`** | No | Anytime |
| **`MatchOver`** | No | Anytime |

- **Casual v1:** swap every intermission + pre-match (when `MatchSetup` ships) — not lock-for-full-match.
- **Class change** in allowed window → **host respawn** (destroy → clone class prefab → cosmetics → `NetworkSpawn`).
- **Ult/passive only** → in-place enable + `ResyncFromEquippedUltOnHost()`.

### Slots (v1 UI)

- **Class + ult** picker; passive auto on class switch (no passive picker until a class has 2+ passives).
- Ult rule: when class has ≥1 ult in catalog, one must be selected (auto if only one).

### Persistence tiers

| Tier | What | Status |
|------|------|--------|
| **Local save** | `loadouts/{steamId}.json` on `FileSystem.Data` | **Now** — last class/ult on this PC |
| **Join sync** | Client RPC sends committed loadout on connect; host validates + caches | **Shipped** — cross-machine verify at publish |
| **Cloud / progression** | Server-trusted unlocks + XP; optional prefs sync | **Later** (progression slice) |

- **Default (no file):** Speedster + Speed Blitz + default passive.
- **Mid-round join:** spawn with last committed; change at next intermission.
- **v1 unlocks:** all catalog options (no grind gate). Progression slice filters to unlocked only later.

### MP authority

- On connect: owner sends committed loadout → host `LoadoutAuthority.TryValidateCommittedLoadout` → apply at spawn.
- Swap requests: host validates phase (`Intermission`, future `MatchSetup`, or practice) → apply.
- Equipped ids synced on **`PlayerLoadout`** `[Sync(FromHost)]` — **not** on `PlayerTeam`.

Wiring detail → [`ARCHITECTURE.md`](ARCHITECTURE.md) § Loadout & spawn.

---

## Ultimates (shared charge system)

Every player carries **0% → 100%** ult charge. At **100%** they can use their class ult **once**; on commit, charge drops to **0%**. There is **no** separate post-ult regen lockout — refilling from 0% already takes long enough.

### What players see

- HUD shows **percentage only** (0–100%).
- Internally the host tracks **points** (`currentPoints` / equipped ult `maxPoints`). v1: **100 points = 100%** when no ult is wired; each ult declares its own cap on its component.
- **Balance:** a stronger ult might need e.g. **150 points** to reach 100% while a lighter ult needs **100** — event rewards stay the same in raw points (goal always +40, etc.), but the bar fills slower on heavy ults. Display stays 0–100%. **Ult swap:** raw points carry over; % recalculates against the new ult&apos;s max (no swap penalty).

### Passive regen

- Ticks up slowly **only during `MatchPhase.Playing`** (live round time).
- **Paused** during goal celebration, intermission, and post-match celebration.
- Charge **% persists** across rounds within a match (e.g. end a round at 100% → start next round still at 100%).
- **Rematch** (host `1` after match over) resets everyone to **0%**.

### Charge sources (v1)

| Source | Who gets credit | Notes |
|--------|-----------------|-------|
| **Passive regen** | Holder | Playing only; default **0.2**/s |
| **Goal** | **Scorer only** | Default **40** pts |
| **Tackle** | **Attacker only** | Enemy victims only — **no charge on friendly-fire tackles**. Default **10** pts |
| **Assist** | **Passer** (throw only) | Teammate scores within **`AssistWindowSeconds`** (default **10**) after first solid ball contact or teammate catch. **Void:** enemy grab, enemy tackle on carrier, relay re-throw resets chain. **Off** in practice arena. Default **25** pts — **playtest OK (2026-06-30)** |
| **Throw** | — | **Not planned** — throwing does not grant charge |

Event point defaults (**40** / **25** / **10**) signed off at playtest; passive rate still tune as needed. Per-ult `MaxChargePoints` (slice 4 ✅) can stretch ult fill time without changing these awards.

### When ults can be used

| Phase | Passive regen | Ult activation |
|-------|---------------|----------------|
| **Playing** | On | On |
| **Goal celebration** | Off | **Off** |
| **Intermission** | Off | **Off** |
| **Post-match celebration** | Off | **On** |

### Multiplayer / authority

- Host owns charge truth and ult validation (same “host is referee” rule as tackles).
- Clients request; host decides. Sync charge % to all machines.
- Ult logic lives in **`Code/Ultimates/`** — not in `MatchDirector`.
- **`PlayerUltCharge`** + class ult components on **player prefab** — **manual** wiring; **do not** auto-add via `GameNetworkManager`.

### Spend charge (all ults)

- Charge drops to **0%** on **commit** (e.g. release **X** after aim) — **`TrySpendFullChargeOnHost()`** — not when the effect connects.
- If the player is interrupted during a committed wind-up (e.g. tackled during Speed Blitz’s 3 s channel), the ult **fizzles** but charge is **already spent**.

### Input

- **`Ultimate`** action on **X** (`Input.config`).
- MOBA-style pattern for aimed ults: **hold** to preview (owner-only) → **release** to commit.

### Ball + ults (general)

- Default: **cannot use ult while holding the ball** (or ult auto-disabled while carrying).
- Exception later: ball-required ults (e.g. Sniper) only work **with** the ball.

### Feedback (v1 vs later)

- v1: **`UltChargeHud`** — floored **%** (e.g. `99.9` → `99%`); panel left of `MovementRampHud`; at 100% stays **white** for **`ReadyHighlightDelaySeconds`** (~0.4 s) then **blue** while still charged.
- v1: Speed Blitz owner-only **segmented ground** preview (`SpeedBlitzAimPreview` — path + hit width; blue `#24b0ff`; `speed_blitz_preview.vmat`). Tune **`PlaneWidthBaseSize`** / **`PlaneLengthBaseSize`** separately.
- v1: **`ComicBurstPalette.Ult`** — blue knockdown comic on Speed Blitz launch (not connect hang); future ults reuse palette.
- Later: **circular** ult meter — % in center, clockwise ring fill, ult icon unfade.

### Voided / not planned

- **Speedster dodge-reward** / stay-at-run-speed ult — voided.
- **Throw** granting ult charge — not planned.

### Ship order (first pass)

1. Shared charge + HUD (`PlayerUltCharge`, `UltChargeHud`)  
2. Speedster **Speed Blitz** — core dash → hold/release preview → **2c polish ✅** → **2d wind-up ✅** → **aim preview v3 ✅ (2026-06-30)**
3. Assist charge ✅  
4. Per-ult `MaxChargePoints` balance ✅  
5. **Juggernaut** stomp → **Sniper** path zones  
6. **Weapons** (after all three first ults)

---

## Speed Blitz (Speedster ult — first ship)

**Status:** **Slices 2a–2d + aim preview v3 ✅ (2026-06-30)** — hold/release preview (segmented ground planes, `speed_blitz_preview.vmat`), dash feel, connect/launch SFX, ult comic, wind-up VFX/pose/glow. **Class:** Speedster only.

### Slice 2d ✅ (shipped 2026-06-30)

**Fantasy:** Electric charge during wind-up → dash carries energy (body glow) → connect hang holds glow → **discharge burst + glow fade** at ragdoll launch. **~2 s wind-up** — sparks, SFX rise, body glow, and (when ready) pose ramp **together**.

**VFX (editor prefabs + code phases):**

| Phase | Behavior |
|-------|----------|
| Wind-up | Blue **`#24b0ff`** sparks + attractor inward; body glow ramps **`GetWindUpLerp()`**; only after release X |
| Dash | Body glow peak; **no** wind-up sparks |
| Connect + hang (~0.65s) | Body glow peak; **no** sparks |
| Ragdoll launch | **`speedblitzdischargevfx`** burst on **dasher chest** + body glow discharge — accent only vs comic + launch boom |
| Miss | Body glow fade at dash end — **no** discharge burst |
| Interrupt | Hard off |

Optional later: soft ring/torus core if silhouette needs help at distance.

**SFX:** Electricity bed + rising pitch over wind-up; dash-start woosh; **hard stop electric at connect crunch**; existing launch boom unchanged.

**Pose (pending):** Olympic blocks — full-body masked layer; fast weight-in (~0.25–0.4s); **`charge_run` off during wind-up**.

**Body glow (shipped):** **`SpeedBlitzBodyGlow`** — subtle blue tint + point light on dasher avatar (destroyed when glow ends); no ult outline (enemy red outline unchanged). Tune on Speedster prefab.

### Shipped in slice 2a + 2b (code)

- **Hold X** at 100% → owner-only corridor preview; **release X** commits (RMB cancel while aiming).
- Wind-up: planted, look locked to committed aim, vulnerable, no cancel.
- Dash: invulnerable; owner-driven through `PlayerController` (wall-slide, step-up); charge_run anim; time-based range.
- First enemy **physically reached** (3D contact + line-of-sight + vertical cap) → knockdown; dash **stops** at actual position; **client-owner predict** for stop + attacker feel + connect crunch (host dedupe).
- Dash end (hit or miss) → forced **walk** ramp (rebuild to charge).
- No charge gain, ball pickup, or dodge during ult.
- **Owner camera (`SpeedBlitzDashCamera`):** wind-up pullback/FOV build → blended dash spike → on enemy hit **`BeginHitRecoveryBlend()`** eases to baseline at contact freeze (not victim launch); miss/timeout uses same end blend. **`ThrowChargeCamera`** release blend uses same transition-frame pattern.
- **Connect feel (2c):** **`BlitzConnectPoseFreeze`** — attacker + victim body pose held during **0.65s** pre-launch hang (`PlaybackRate = 0`). Optional **`ConnectImpactChargeRunCycle`** for consistent impact stride.
- **SFX (2c):** **`ConnectImpactSoundA/B`** — host picks one at random each hit, plays at dash stop (`PlaySpeedBlitzConnectImpactSoundRpc`). **`LaunchSound`** — boom when ragdoll launches after hang. Both **`SoundEvent`** drag-drop on Speedster prefab; **`[Rpc.Broadcast]`** so all clients hear the same pick.
- **Comic (2c):** **`ComicBurstPalette.Ult`** — host picks word/tier; blue fill; spawns at ragdoll **launch** (with **`LaunchSound`**), not at connect hang.
- **MP anim (2c):** Wind-up **`ApplyPlantedHorizontalFreeze`** on all clients; throw release RPC shares owner **`throwPoseEndTime`**; full hold-param clear on remotes.

### Fantasy

Lightning-fast dash over a long distance. Hit an enemy → launch them **much farther** than a normal tackle. Miss or graze a wall → you slid wrong; skill is aim + prediction.

### Flow (full design)

1. Charge must be **100%**. Not holding the ball. `MatchPhase.Playing` or post-match celebration.
2. **Hold X** → owner-only preview: segmented **ground plane** corridor (max range + hit width; `SpeedBlitzAimPreview`). Preview = aim helper; may show through walls — **wall clip won't do**. **Hits** require physical touch + LOS.
3. **Release X** → **commit** (cannot cancel):
   - Charge immediately drops to **0%**.
   - **Camera locks** (full lock — signed off).
   - **3 s wind-up** — player is **vulnerable** (can be tackled; wasted ult if knocked down).
4. **Dash** — player is **invulnerable** during movement:
   - Very fast, long range.
   - **First enemy actually touched** along the dash (contact radius + **`MaxHitVerticalSeparation`** + LOS trace; corridor width/range is coarse filter only) takes a heavy knockdown launch (host ragdoll path, like tackle).
   - **Enemies only** — no friendly fire on dash hit (may revisit).
   - **Walls / traffic:** cannot pass through; on contact **slide along** the surface at the impact angle (tangent motion keeps burning remaining dash distance — not a hard dead stop).
   - No ball pickup during the ult.
5. v1: **one target** per use (first contact only). Variations / upgrades later.

### Tuning knobs (inspector / playtest)

- Dash range, speed, hit width, wind-up duration (2 s default), launch force, slide friction along walls.
- **Camera:** `WindUpToDashBlendDurationSeconds`, `DashEndBlendDurationSeconds` on **`SpeedBlitzDashCamera`** (hit recovery + miss end).
- **SFX:** `ConnectImpactSoundA`, `ConnectImpactSoundB`, `LaunchSound`, `ConnectImpactSoundVolume`, `LaunchSoundVolume` on **`SpeedsterSpeedBlitzUlt`**. Defaults: `Assets/Sounds/Crunch/speed_blitz_connect_crunch_a/b.sound`, `Assets/Sounds/Explosions/speed_blitz_launch.sound`. **2d adds:** wind-up electric bed + rise (`SoundEvent` on ult); cut electric at connect crunch.

---

## Other class ults (planned — not designed in detail)

| Class | Ult (working name) | One-line |
|-------|-------------------|----------|
| **Juggernaut** | Ground stomp | AOE knockdown around self |
| **Sniper** | Path zones (name TBD) | Ball throw creates ragdoll zones along path; requires ball |

---

## Weapons (future — not built)

- Hold **ball OR weapon**, never both
- Touch ball while armed → drop weapon, then grab ball (host order)
- Holding weapon slows non-Juggernaut classes to Juggernaut’s armed speed
- Swinging weapon drops speed one tier (same idea as dodge)

---

## Combat slice 1 — unarmed melee (planned)

**Why:** Scrappy knockdown when there’s no room to charge-tackle (sumo endgame, tight spaces). Weaker than tackle; **2 hits** to knockdown. **Weapons slice 7** reuses this pipeline.

**Input:** **LMB** tap without ball → melee. Hold LMB with ball = throw charge (unchanged).

**Tiers:** Melee allowed at **walk + sprint only** — blocked at charge tier (`IsAtChargeSpeed`). Tackle stays charge-only.

**Core rules:**
- Host validates; owner predict feel (`CombatFeelPredictDedupe`)
- **2 hits** on same victim within combo window → knockdown via `ApplyKnockdownFromHost` (weak universal impulse)
- Hit 1: hitmarker + micro-hitstop; no ball drop; no victim tier drop
- Hit 1 on target in ult wind-up: chip only — no interrupt until knockdown
- Knockdown: ball drops; enemy-only ult charge +10; interrupts committed ult wind-up
- Can hit ball carriers; **carriers cannot** melee or parry
- Blocked while: holding ball, ragdolled, active ult, dodging, ~1s post-dodge, charging throw, charge tier
- FF: can hit teammates; no ult charge on FF knockdown
- No tackle comic on hit 1

**Combat slice 2 (later):** parry melee swings only → next confirm = 1-hit knockdown.

Full spec + checklist → [`SESSION_NOTES_ARCHIVE.md`](SESSION_NOTES_ARCHIVE.md) § Combat slice spec.

---

## Balance reminders (when playtesting)

- Good dodge should sometimes force a missed tackle — not every time
- Good tackles should still land often enough to feel fair
- Carriers shouldn’t reliably dodge past **every** defender to score; passing matters

For exact field names on `ClassData`, see [`NAMING_CANON.md`](NAMING_CANON.md). Ult component names → [`NAMING_CANON.md`](NAMING_CANON.md) (`Code/Ultimates/`) when built.

---

## Match flow (summary)

Full spec: [`MATCH_FLOW_PLAN.md`](MATCH_FLOW_PLAN.md).

- **Round win:** carrier holds ball in opponent `GoalZone` for ~0.35s.
- **Match win:** first to **5** round wins, or OT golden goal if timer ends tied on round wins.
- **After goal:** 5s celebration (move freely) → reset → 20s intermission (freeze except camera) → resume. Timer **paused** during celebration + intermission.
- **OVERTIME:** tied at 0:00 → reset + 20s intermission (no celebration) → golden-goal `Playing`; clock shows **OVERTIME**.
- **HUD:** score + `M.SS` clock + goal banner + intermission countdown on scene `MatchHud` root (reads `PlayerTeam` sync).
- **Teams:** ids `0`/`1`; display names per map; balance on join.
