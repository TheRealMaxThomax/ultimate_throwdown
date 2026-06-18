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
| Tackle whiff (miss penalty) | **Deferred** — not building unless playtests need it |
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
| Ultimates (charge + Speed Blitz) | **Partial** — charge + **Speed Blitz 2a/2b/2c ✅**; **2d partial ✅ solo (2026-06-18)** — wind-up sparks, body glow, launch discharge; Olympic pose + 2-window MP left |

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

**Tuning knobs (change one at a time in playtests):** shove strength, cooldown, iframe length, how long until charge returns.

---

## Tackle whiff (deferred)

**Decision (2026-05-18):** **Not building now.** Dodge + **charge yaw** (`ChargeYawMaxDegreesPerSecond` on `CatchUpSpeedBoost`) already buy the carrier time to throw, run, or dodge again after cooldown. Revisit only if playtests show chargers getting unfair second tackles too often.

**Counterplay today (no whiff code):**
- Carrier dodge → **walk** + dodge cooldown
- Attacker at charge keeps speed but **slow turn** to re-aim after a lateral dodge
- Attacker pays full cost only when a tackle **lands** (charge strip, tackle cooldown)

**If we add it later (preferred shape — not the old attacker-sprint idea):**
- **Outer / inner zone** — host tracks “committed” threaten state when attacker is inner range
- On committed miss (dodge out, iframe, peel off, no tackle landed): **ball carrier stays at sprint** instead of dodge’s usual drop to walk
- **Do not** drop the attacker to sprint — they keep charge; re-chase cost stays mostly **yaw + time**
- Still pairs with `PlayerDodge` iframe (no hit during iframe; whiff = tier outcome on carrier, not a second attacker penalty)

**Skill idea (unchanged):** Late dodge when attacker is already close = good for carrier. Early dodge while attacker is still far = attacker can adjust.

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

**Class ultimates (partial):** Shared charge system shipped. **Speedster Speed Blitz** 2a + **2b** + **2c ✅** — MP authority + client dasher predict OK; **2d partial ✅ solo (2026-06-18):** wind-up sparks only, body glow, launch discharge burst; Olympic pose + 2-window MP remain. Juggernaut stomp, Sniper path zones planned.

---

## Ultimates (shared charge system)

Every player carries **0% → 100%** ult charge. At **100%** they can use their class ult **once**; on commit, charge drops to **0%**. There is **no** separate post-ult regen lockout — refilling from 0% already takes long enough.

### What players see

- HUD shows **percentage only** (0–100%).
- Internally the host tracks **points** (`currentPoints` / `maxPoints`). v1: **100 points = 100%** for every class.
- **Future balance:** a stronger ult might need e.g. **150 points** to reach 100% while a weaker ult needs **100** — event rewards stay the same in raw points (goal always +40, etc.), but the bar fills slower on heavy ults. Display stays 0–100%.

### Passive regen

- Ticks up slowly **only during `MatchPhase.Playing`** (live round time).
- **Paused** during goal celebration, intermission, and post-match celebration.
- Charge **% persists** across rounds within a match (e.g. end a round at 100% → start next round still at 100%).
- **Rematch** (host `1` after match over) resets everyone to **0%**.

### Charge sources (v1)

| Source | Who gets credit | Notes |
|--------|-----------------|-------|
| **Passive regen** | Holder | Playing only; rate TBD (points per second) |
| **Goal** | **Scorer only** | Large bump; exact points TBD |
| **Tackle** | **Attacker only** | Enemy victims only — **no charge on friendly-fire tackles** |
| **Assist** | TBD | Not v1; pass → teammate scores within ~**10 s**. **Void** if an enemy touches the ball in that window, or if a teammate receives a relay pass and scores (credit goes to the immediate passer, not the original passer) |
| **Throw** | — | **Not planned** — throwing does not grant charge |

Point values for goal / tackle / passive are **not chosen yet** — tune in playtests.

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
- v1: Speed Blitz owner-only ground preview (path + hit width; blue `#24b0ff` tint matches ult comic).
- v1: **`ComicBurstPalette.Ult`** — blue knockdown comic on Speed Blitz launch (not connect hang); future ults reuse palette.
- Later: **circular** ult meter — % in center, clockwise ring fill, ult icon unfade.

### Voided / not planned

- **Speedster dodge-reward** / stay-at-run-speed ult — voided (no tackle-whiff system).
- **Throw** granting ult charge — not planned.

### Ship order (first pass)

1. Shared charge + HUD (`PlayerUltCharge`, `UltChargeHud`)  
2. Speedster **Speed Blitz** — core dash → hold/release preview → **2c polish ✅** → **2d wind-up** (partial ✅ solo 2026-06-18)
3. Assist charge  
4. Per-class `maxPoints` balance (optional)  
5. **Juggernaut** stomp → **Sniper** path zones  
6. **Weapons** (after all three first ults)

---

## Speed Blitz (Speedster ult — first ship)

**Status:** **Slices 2a + 2b + 2c ✅ (2026-06-16)** — hold/release preview, dash feel, connect/launch SFX, ult comic. **2d partial ✅ solo (2026-06-18):** wind-up **`speedblitzwindupvfx`** only, **`SpeedBlitzBodyGlow`**, **`speedblitzdischargevfx`** at launch. **Next:** Olympic **`blitz_windup`** pose, **2-window MP**. **Optional later:** preview art v3. **Class:** Speedster only.

### Slice 2d (in progress — updated 2026-06-18)

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

**Body glow (shipped):** **`SpeedBlitzBodyGlow`** — subtle blue tint + point light on dasher avatar; no ult outline (enemy red outline unchanged). Tune on Speedster prefab.

### Shipped in slice 2a + 2b (code)

- **Hold X** at 100% → owner-only corridor preview; **release X** commits (RMB cancel while aiming).
- Wind-up: planted, look locked to committed aim, vulnerable, no cancel.
- Dash: invulnerable; owner-driven through `PlayerController` (wall-slide, step-up); charge_run anim; time-based range.
- First enemy in corridor → knockdown; dash **stops** on hit; **client-owner predict** for stop + attacker feel (host dedupe).
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
2. **Hold X** → owner-only preview: **dash line** (max range), **hit width** (capsule corridor), faint **end marker**. Preview geometry = host hit geometry (“between the lines = guaranteed hit” at dash time).
3. **Release X** → **commit** (cannot cancel):
   - Charge immediately drops to **0%**.
   - **Camera locks** (full lock — signed off; lane corridor hit = guaranteed knockdown, no yaw-only or wider cone planned).
   - **3 s wind-up** — player is **vulnerable** (can be tackled; wasted ult if knocked down).
4. **Dash** — player is **invulnerable** during movement:
   - Very fast, long range.
   - **First enemy** along the committed corridor (closest along ray) takes a heavy knockdown launch (host ragdoll path, like tackle).
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

Speedster dodge-reward / stay-at-run-speed ult idea is **voided** — no tackle-whiff system planned.

---

## Weapons (future — not built)

- Hold **ball OR weapon**, never both
- Touch ball while armed → drop weapon, then grab ball (host order)
- Holding weapon slows non-Juggernaut classes to Juggernaut’s armed speed
- Swinging weapon drops speed one tier (same idea as dodge)

---

## Balance reminders (when playtesting)

- Good dodge should sometimes force a missed tackle — not every time
- Good tackles should still land often enough to feel fair
- Carriers shouldn’t reliably dodge past **every** defender to score; passing matters
- Whiff is optional tuning later; don’t ship it until charge yaw + dodge numbers are settled in real 2-player tests

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
