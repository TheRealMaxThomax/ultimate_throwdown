# Session Notes

Use this file as persistent project memory between chats.
Keep entries short, specific, and current.

## Project Snapshot
- **Current Goal:** Tackle + movement polish — launch tuning, broader MP stress tests. Ragdoll camera: free look + stand-up blend to `PlayerController` camera. **Dodge shipped** (double-tap strafe, host RPC, iframe, tier penalties); tackle whiff (inner threaten) still future.
- **Current Branch:** **`main`** — routine **commit + push** to **`main`**; use short-lived feature branches only when a change needs isolation.
- **Build/Run Status:** Compiles clean. Client + host tackles + dodge shove (`Rigidbody` velocity add); ragdoll visible early (`NetworkSpawn` before impulse delay); clothing on ragdoll via `BoneMergeTarget`; stand-up after grounded+settled time with `RagdollMaxDuration` cap. Hammer **`testing_map`** mounts at Play via **`StartupMapBootstrap`** / **`MapInstance`** (**`ultimate_throwdown.sbproj`** **`Resources`: `*`**).
- **Last Updated:** 2026-05-10 — **`PlayerClass`** applies **ClassData** **`CapsuleHeight`** / **`CapsuleRadius`** → **`PlayerController`** body + **`ModelScale`** on skin roots (short retry for cosmetics); **`PlayerTackle`** ragdoll root matches victim **`ModelScale`**. **`CatchUpSpeedBoost`:** **`MomentumMultiplier`** scales prefab-snapshotted **`AccelerationTime`** / **`DeaccelerationTime`** (+ eased walk/run cap toward tier); throw charge resets accel times to baseline; tackle tier **`IsAtChargeSpeed`** still instant ramp. Forward intent for charge ramp: **`Forward`** action + keyboard vs controller analog path. *(Earlier 2026-05-11 line: tackle strip, practice NPC locks, etc. — still in file below.)*

## Code Folder Structure
- `Code/Ball/` — BallGrab, BallThrow, BallClientFeel, ThrowChargeBar
- `Code/Player/` — `CatchUpSpeedBoost.cs` (also defines **`PlayerDodge`** component at file bottom — s&box compile quirk avoided separate `PlayerDodge.cs`). `PlayerClass.cs` (also defines **`ClassData`** `[GameResource]` — avoid separate `ClassData.cs`). PlayerCosmeticsSync, PlayerTackle, RagdollClientFeel
- `Code/Network/` — GameNetworkManager
- `Code/Map/` — **`StartupMapBootstrap.cs`** (host spawns **`MapInstance`** for **`testing_map`** if scene has none)
- New systems get their own folder (e.g. `Code/Ultimates/`, `Code/UI/`)

## Hammer map (`testing_map`)
- **Edit:** **`Assets/Maps/testing_map.vmap`** → compile → **`testing_map.vpk`** / `.los` (don’t author on `.vpk`).
- **Play mounts map because:** **`StartupMapBootstrap`** (`Code/Map/StartupMapBootstrap.cs`) creates **`MapInstance`** with **`MapName` = `testing_map`**. **`Metadata.MapList` in `.sbproj` alone does not load geometry into Play** for this project.
- **Critical `.sbproj`:** **`"Resources": "*"`** — without it, **`maps/testing_map.vpk`** may not ship → **`Failed to mount vpk`**. **`MapList`** includes **`testing_map`** and **`facepunch.flatgrass`** (ids are logical strings, not filenames).
- **Scene:** Optional **`MapInstance`** on **`throwdown_prototype`** with same **`MapName`** for editor preview; **remove/disable duplicate grass floor** when Hammer floor is authoritative.
- **Arena prototype:** **Roof removed**, **`light_environment`** sun primary — sealed interior + local floods cost hours for marginal readability vs directional silhouettes; revisit roof/interior later.
- **Compile:** **`light_environment` brightness** / bake-driven mood → use **`full`** when **`entities-only`** leaves brightness “stuck wrong.” **Quirk:** after compile, **viewport sometimes black until Stop → Play** refreshes the loaded map.

## Important Decisions
- **Keep `BallGrab` as the source of truth for hold state.**
  - Why: One owner of state avoids desync bugs between multiple scripts.
  - Date: 2026-04-28
- **Split throw into a separate `BallThrow` component.**
  - Why: Better organization, easier debugging, cleaner future animation integration.
  - Date: 2026-04-28
- **Use optional direct ball reference (`MainBall`) with name fallback (`MainBallName`).**
  - Why: Direct reference is safer; name fallback keeps flexibility.
  - Date: 2026-04-28
- **Use host-approved RPC flow for grab/drop/throw in multiplayer.**
  - Why: Prevents client-side state divergence and keeps one source of gameplay truth.
  - Date: 2026-04-29
- **Use startup scene `scenes/throwdown_prototype.scene` with `GameNetworkManager` active.**
  - Why: Local multi-window networking tests require the correct gameplay scene and per-connection player spawning.
  - Date: 2026-04-29
- **Keep cosmetics sync in a dedicated visual component (`PlayerCosmeticsSync`) instead of `GameNetworkManager`.**
  - Why: Isolates visual/avatar timing logic from network spawn authority to avoid multiplayer spawn regressions.
  - Date: 2026-04-30
- **Use `ClothingContainer.CreateFromConnection(ownerConnection, false)` for remote cosmetics apply.**
  - Why: Remote clients may not have ownership verification for other players; `removeUnowned=true` can strip valid cosmetics.
  - Date: 2026-04-30
- **Lock player/clothing LOD after cosmetics apply (`LodOverride = 0`).**
  - Why: Prevents camera-distance/angle skin tone and cosmetic visual glitches caused by LOD transitions.
  - Date: 2026-04-30
- **Auto-grab on contact: walking into the ball picks it up automatically.**
  - Why: Client-side prediction required to make body-push feel consistent across host/client is too complex for current stage. Auto-grab is the same compromise made by Extreme Football Throwdown (Garry's Mod). Intentional ball interactions are grab, drop, and throw only (no separate kick).
  - Date: 2026-05-04
- **Removed `BallPlayerPushLock` entirely.**
  - Why: Auto-grab intercepts players before they reach push range; the component was redundant.
  - Date: 2026-05-04
- **Composition over inheritance; flat components over Core/Hub architecture.**
  - Why: s&box is built for component composition. A central "Core data" file creates God Object risks. Each component owns one job.
  - Date: 2026-05-04
- **No separate kick mechanic.**
  - Why: Auto-grab on contact already handles moving the free ball into play; adding kick would duplicate that role. Focus stays on grab/drop/throw feel and tuning.
  - Date: 2026-05-04
- **Drop placement uses player-facing yaw (not hold anchor forward).**
  - Why: Keeps drop side consistent relative to current movement/facing instead of depending on anchor orientation.
  - Date: 2026-05-04
- **Dropped ball inherits scaled player velocity via inspector slider.**
  - Why: Makes drop feel responsive while keeping tuning control (`DropVelocityScale`) and preserving no-push guardrails.
  - Date: 2026-05-04
- **Tackle launch direction uses flat horizontal vector toward victim, not attacker velocity.**
  - Why: Attacker velocity includes vertical Z (jump/slope), causing random launch angles. Victim-direction is always consistent regardless of attacker movement state.
  - Date: 2026-05-06
- **Ragdoll launch impulse on spawned `ModelPhysics`:** after local physics init delay, **`PhysicsBody.ApplyImpulse` on the pelvis only** (`Bodies[0]`): `launchDir × effectiveLaunchSpeed × totalRagdollMass`. Matches whole-body momentum; limbs flex via joints (not rigid same-velocity all bones).
  - Why: `ModelPhysics.PhysicsGroup` is null on spawned ragdolls. Pelvis-only `Rigidbody`/per-body velocity was unreliable. One COM-scale impulse behaves well with the ragdoll constraint graph.
  - Date: 2026-05-06
- **Ragdoll `NetworkSpawn` vs impulse order (current):** `NetworkSpawn` runs **right after** mesh + `CopyBonesFrom` + clothing so the ragdoll replicates before the hidden-player gap; host then waits **`RagdollPhysicsInitDelay`** and applies pelvis **`ApplyImpulse`**. Earlier pipeline tried impulse-then-spawn to keep bodies in local physics; current tradeoff prioritizes visibility; if launches weaken, increase `RagdollPhysicsInitDelay` slightly.
  - Date: 2026-06-06
- **`ModelPhysics.MotionEnabled = true` and `IgnoreRoot = false` must be set explicitly on the spawned ragdoll.**
  - Why: `MotionEnabled` ensures physics drives the renderer (not the other way around). `IgnoreRoot = false` ensures the root physics body drives the GameObject's `WorldPosition` so `NetRagdollPosition` (which reads `ragdollObject.WorldPosition`) actually tracks the ragdoll as it flies.
  - Date: 2026-05-06
- **Victim's `PlayerController` and `Collider` components disabled immediately in `ExecuteTackle` on the host, not just in `ApplyRagdollLocally`.**
  - Why: `ApplyRagdollLocally` runs one frame after the tackle fires. The ragdoll spawns in that same frame, so bones would spawn inside an active capsule and get ejected in random directions before the launch velocity fires. Immediate disable closes that gap.
  - Date: 2026-05-06
- **Tackle power uses `ClassData.Mass` ratio and Juggernaut charge passive (when `TackleChargeRampRate` / `MaxTackleChargeBonus` are non-zero).**
  - Why: Matches design doc — heavy vs light matchups and Juggernaut ramp at charge speed; `tacklePower = clamp(attackerMass/victimMass, 0.5, 2.5) × (1 + tackleChargeBonus)` scales ragdoll impulse and ball knock-off force. Bonus resets when not at charge speed or when ragdolled.
  - Date: 2026-05-06
- **Tackled owning client:** `RagdollClientFeel` on player root snapshots `NetRagdollPosition` via `PlayerTackle.SyncedRagdollPelvisPosition`, delayed playback (`InterpolationDelay`, `MaxSnapshots`) — same idea as `BallClientFeel`. Host victim still snaps in `PlayerTackle` only.
  - Date: 2026-05-06
- **`CatchUpSpeedBoost` at-charge for remote pawns:** owner computes `IsAtChargeSpeed`; `[Sync] NetAtChargeSpeed` replicates so the host can evaluate charge-speed for client-owned attackers (`IsProxy` skip no longer blocks host).
  - Date: 2026-05-06
- **Client attacker tackles:** `TryOwnerRequestTackleOnHost` + `[Rpc.Host] RequestTackleApplyOnHost` with victim `GameObject.Id` (Guid), `horizontalMoveDirectionFromOwner`, attacker/victim position snapshots; slop + radius fudge; `NetTackleBlockedUntil` (FromHost, backing field) mirrors tackle cooldown for owners.
  - Why: Host `PlayerController.Velocity` for remote pawns is unreliable; SteamId can collide in local multi-instance.
  - Date: 2026-05-06
- **Ragdoll outfit on spawned ragdoll:** extra victim `SkinnedModelRenderer`s (cosmetics) cloned under ragdoll root with `CopyFrom` + `BoneMergeTarget` = physics body (not naked base-only).
  - Date: 2026-05-06
- **Ragdoll visibility gap:** `NetworkSpawn` immediately after mesh + `CopyBonesFrom` + clothing; assign `victim.ragdollObject` then wait `RagdollPhysicsInitDelay` and `ApplyImpulse` (order changed from “impulse then spawn” to avoid frame hole while player renderers hidden).
  - Date: 2026-05-06
- **Stand-up timing:** `RagdollDuration` = consecutive seconds **grounded and settled** (floor trace + pelvis speed); bouncing/jostling resets accum. `RagdollMaxDuration` = hard cap from tackle. Intentional: other players bumping the ragdoll can delay get-up until cap.
  - Date: 2026-06-06
- **Ragdoll camera (owner):** While `NetIsRagdolled`, `Input.AnalogLook` applied to `PlayerController.EyeAngles` (controller disabled); main camera third-person orbit from `EyeAngles` + `RagdollCameraDistance` / `RagdollCameraHeight`. Preserves look for stand-up.
  - Date: 2026-05-07
- **Stand-up camera blend:** After ragdoll, `OnPreRender` lerps main camera from last ragdoll frame toward **that frame's `PlayerController` result** (read `activeCamera` before overwrite) so third-person math matches; smoothstep; `StandUpCameraBlendDuration` default **0.6s** on `PlayerTackle`.
  - Date: 2026-05-07
- **`PlayerTackle` single source file:** Ragdoll host helpers live in `PlayerTackle.cs` (merged from former partial) for reliable game compile.
  - Date: 2026-05-07
- **Third-person distance (editor):** `PlayerController` `CameraOffset` **X = 185** current tuning (was ~256; closer feels better for tackles/ball read).
  - Date: 2026-05-07
- **Ball and weapon are mutually exclusive — one or the other.** Ball carriers cannot hold a weapon; simplifies tackle/chase edge cases and avoids weapon+ball clipping.
  - Date: 2026-05-07
- **Weapon hold penalty (non-exempt classes):** While holding a weapon, **cap both** movement knobs to **Juggernaut’s** `TimeToCatchUpSpeed` **and** `CatchUpMoveSpeed` (same ramp **and** same charge top speed as Jugg). **Juggernaut** uses `IgnoreWeaponSpeedPenalty` / baseline — no double tax; **relative** advantage is that armed Speedster/Sniper no longer outpace armed Jugg at charge.
  - Date: 2026-05-07
- **Weapon swing penalty:** **One speed tier down**, same mapping as **dodge** (charge→run, run→walk, walk→walk + swing effects/cooldowns). **Not** “hard snap to walk for N seconds” as the primary rule (ClassData `WeaponSwingSpeedPenaltyDuration` may be repurposed or layered — see Weapons section).
  - Date: 2026-05-07
- **Armed player touches ball (auto-grab):** **Host-authoritative** sequence: **drop / strip weapon first**, then normal **grab** path — avoids weapon+ball **clipping** / jank. If grab fails (pickup blocked), define whether weapon is restored or stays dropped (prefer **atomic swap only on success** to avoid stuck empty-handed).
  - Date: 2026-05-07
- **Tackle whiff:** use **inner threaten radius** (fraction of `TriggerSphereRadius`) **+ short memory** so **dodge out / fail after real pressure** still applies attacker penalty (e.g. sprint strip); outer-sphere-only + bad angle is insufficient — see **Tackle whiff penalty** section.
  - Date: 2026-05-08
- **Dodge vs tackle whiff (implementation):** Build **`PlayerDodge`** so it **composes** with **inner threaten + short memory** (not a separate silo). Host owns threaten + whiff outcome; `PlayerDodge` exposes **iframe / `IsDodging`** (and synced fields) so tackles can **nullify on iframe** while whiff tax still keys off **“threatening then failed without a hit”** (dodge-out is one failure mode). **Speedster passive** reuses the **same inner + threaten** signal for reward tiering — see **Tackle whiff penalty** and **Passives**.
  - Date: 2026-05-08
- **Dodge input:** **Double-tap A** → dodge **left**; **double-tap D** → dodge **right** (no separate dodge button). Double-tap max interval is a **tunable** inspector value (start ~**200–350 ms**; tune vs accidental triggers).
  - Date: 2026-05-08
- **Throw charge + dodge:** **Sniper only** may dodge while **charging throw**; **other classes** cannot dodge at all until throw charge ends. (How Sniper’s throw charge interacts with dodge — cancel vs carry — **TBD** when implementing.)
  - Date: 2026-05-08
- **Codegen / compile layout:** **`ClassData`** and **`PlayerDodge`** are defined **`PlayerClass.cs`** and **`CatchUpSpeedBoost.cs`** (file tails) — restoring standalone **`ClassData.cs` / `PlayerDodge.cs` caused **`CS0246`** in-editor for this repo. Prefer keeping them merged until toolchain is verified.
  - Date: 2026-05-08
- **Dodge shove direction (owner `ApplyShoveVelocity`):** Use **`pc.EyeAngles.ToRotation().Right.WithZ(0)`** (normalized) for lateral impulse, same basis as ragdoll camera (`ToRotation()`), not **`GameObject.WorldRotation.Forward`** nor **`Rotation.FromYaw(EyeAngles.yaw).Forward`**. Body rotation can lag eye on join; `FromYaw` alone did not match engine angle convention and broke or zeroed shove. **`PlayerController`** via **`Components.Get<PlayerController>( FindMode.EverythingInSelfAndDescendants )`**. If flattened `Right` collapses (extreme pitch), fall back to **`Cross(Up, WorldRotation.Forward)`** as before.
  - Date: 2026-05-08
- **Dodge / whiff / tackle balance (north star):** Expect **heavy playtest tuning** (dodge cadence, inner threaten, memory, whiff strength, carrier walk-after-dodge, charge-only tackles). **Goals:** (1) A well-timed dodge **can** force a real whiff / miss tax — **not** spammable “I win” against every charge. (2) Good attackers should still **connect** often enough that tackles feel **fair and readable**, not hopeless. (3) **Ball carriers** must **not** be able to **chain-dodge** their way to a goal as the default plan — juking the whole team should be **rare, high-skill**, not reliable. (4) For **most** players and situations, **progression should favor throwing** (pass / advance) over **infinite run-and-dodge** carry. Use inner vs outer dodge, cooldowns, post-dodge walk tier, whiff tax, and field geometry together — **one lever at a time** in sessions.
  - Date: 2026-05-08
- **Ragdoll MainCamera + stand-up blend only for `this.Network.IsOwner`:** scene dummies (often `!IsProxy` on host) shared the same MainCamera as the real player; **`practice_npc`** or any non-owned ragdoll must not drive orbit/blend. **`this.Network.IsOwner`** gates free-look camera, `OnPreRender` blend start, and Rpc uses **`this.Network.Owner`** for clarity.
  - Date: 2026-05-10
- **`practice_npc` tagged roots (victim):** after tackle ragdoll recovery, stand at **snapshot** pre-tackle **`WorldPosition`** + **`PlayerController.EyeAngles`** (human pawns still use **floor trace** `NetStandUpPosition`). For ClassData / whiff lane testing; do **not** tag real players.
  - Date: 2026-05-10
- **`ClassData.MomentumMultiplier` via `CatchUpSpeedBoost`:** Snapshots prefab **`PlayerController.AccelerationTime`** / **`DeaccelerationTime`** once; owner applies **`baseline × multiplier`** each frame (**`>1`** slower to speed up/down, **`<1`** snappier). Smoothes walk/run **cap** toward tier target separately; **`IsAtChargeSpeed`** / tackle eligibility stay on **instant** ramp (`GetTargetSpeed`). **`BallThrow`** charge clears movement by resetting accel times to baseline on that path.
  - Date: 2026-05-10

## Movement / dodge / camera v1 (design)
Locked **design + starting tuning** for charge-time camera, dodge, and how they interact with tackle. **Not all implemented** — numbers are playtest anchors.

### Charge / camera
- **At charge speed:** mild **look / yaw damp** (enough to kill **snap 180°** re-tackles; corners and map flow still playable). Final scale is engine-specific — try a **~0.65–0.8×** effective-turn band in playtest, then tune.
- **Lateral readjust while charging:** **dodge** drops to **running** (tier down + cooldown) as the intentional “I need strafe / correction” escape — not relying on harsh full-map turn punishment alone.

### Re-enter charge (after dodge from charge)
- **~2.0 s** before player can **re-enter charge** (tackles stay **charge-only**, so no legal tackle until then anyway). Tune band **~1.2–2.5 s** if pacing shifts.

### Dodge behaviour
| From | After dodge |
|------|----------------|
| Charge | Run |
| Run | Walk |
| Walk | Walk (no extra tier — **shove + short tackle-only iframe + cooldown** only) |
| Ball carrier (capped at run) | Walk |

- **Lateral shove:** medium–strong; **starting band ~200–260** in editor — **rescale** once world units vs `TriggerSphereRadius` are confirmed.
- **Tackle-only iframe:** **~140 ms** start (**120–160 ms** band); **short**, paired with **bigger shove** (not long generic invuln).
- **Input:** **Double-tap A** = dodge **left**; **double-tap D** = dodge **right**. Use a **max interval** between taps (tunable; see Important Decisions).
- **Throw charge:** **Non-Sniper:** cannot dodge while charging throw. **Sniper:** may dodge while charging; whether that **cancels** windup vs **preserves** charge is **TBD**. **On any successful dodge,** clear throw charge/windup if still active unless Sniper-preserves-charge rules say otherwise once designed.

### Cooldowns
- **Baseline dodge cooldown:** **~3.5 s**.
- **Ball carrier:** **slightly quicker** — **~3.0 s** (−0.5 s) or **~85–90%** of baseline. Compensates for **walk** after dodge without encouraging infinite stall; narrow or remove the gap if stalling dominates tests.

### 1v1 / asymmetry (tuning target)
- **Carrier dodges first** → **walk**; attacker may stay **faster** — intentional **resource** trade. **Attacker** still avoids “free second tackle” if **tackle = charge-only** + **re-charge-after-dodge** holds.
- **Master feel check:** **~2–4 s** time-to-contact on **open ground** after carrier dodge (adjust shove, walk-with-ball, charge top speed, cooldowns **one lever at a time**).

### Balance intent — dodge vs tackle vs scoring (ongoing tune)
This block is **design intent**, not starting numbers — revisit after dodge + whiff ship.

- **Dodge causing whiffs** should reward **timing and reads** (inner-pressure, baiting chargers). It should **not** make “dodge on cooldown repeat” the easy answer to every closing defender.
- **Tackles** should stay **viable**: attackers who close well should **land** often enough that defense is about **angles and teamwork**, not only praying the carrier misclicks.
- **Ball carrier dribble fantasy:** Outrunning **multiple** defenders by **only** dodge juke → **possible for exceptional plays**, **not** the normal way to march the field. Throwing/passing stays the **pressure valve** when multiple defenders converge.
- **Levers** (combine, don’t max one knob alone): dodge CD (including carrier tweak), iframe length, lateral shove, inner threaten fraction, memory window, whiff attacker penalty strength, walk-with-ball after dodge, re-charge gate, tackle cone/radius.

### One-liner
**Charge = mild commitment (turn damp) + dodge (double-tap A/D) = tier down + shove + short tackle buffer + cooldown (carrier slightly shorter CD) + re-charge gate after charge-tier dodge; **Sniper** may dodge during throw charge, **others** cannot; tackle eligibility stays charge-only; whiff tax (inner threaten + memory) stays a separate host path from iframe.**

## Tackle whiff penalty (design — not implemented)
Attacker **miss tax** so “stay on charge and try again” after a real exchange isn’t free — especially if the ball carrier **burned dodge**. Charge-time look tuning alone was not enough.

### Why not “definition A” only
**A** = valid tackle target somewhere inside full **`TriggerSphereRadius`**, but approach cone fails. If the victim **dodges out** of the sphere, there is often **no one in range** anymore → **no** whiff under strict A → attacker stays on **charge** and can try again immediately. Rewards **dodge** too little.

### Chosen approach: inner threaten + short memory
- **Outer sphere** (existing): `TriggerSphereRadius` — when a tackle **can** connect (`TryFindTackleVictim` logic).
- **Inner threaten zone** (new knob): smaller horizontal distance (e.g. **fraction of** `TriggerSphereRadius`, tune ~**0.5–0.75×**). Means “**actually on**” the runner, not grazing the outer rim.
- While **at charge** and a **valid victim** is inside **inner** zone → set **armed / threatening** state (host-authoritative).
- **Short memory** (~**0.1–0.2 s**): if threatening then **fails** without a successful tackle — victim **dodges** out, attacker peels off, **bad angle** never converts — apply **whiff penalty** (e.g. **drop to sprint** / strip catch-up tier via `CatchUpSpeedBoost` timers; exact hook TBD).
- **Cross-use — Speedster passive:** a **short speed boost** and/or **run-speed floor** after a dodge **nullifies** a tackle can use the **same inner + threaten state** on the host (no second ad-hoc radius). Optional tier: **inner-pressure** dodge = full passive; **outer-only** = smaller buff or none — lines up with “late dodge” skill expression above.

Throttle so grazing the outer ring doesn’t spam penalties every frame.

### Skill expression
- **Carrier:** **Late**, well-timed dodge when attacker is already **inner** — more likely to force attacker miss tax; **early** dodge while attacker only in **outer** ring — attacker can **adjust**, defender may **waste** dodge.
- **Attacker:** don’t **telegraph** inner-zone entry; shape approach so dodge is wasted or tackle still lands.

### Implementation notes (when built)
- Host-owned truth for threaten + whiff outcome; align with existing tackle RPC / cooldown patterns.
- **`PlayerDodge` integration:** Whiff logic is **not** “iframe only.” Iframe handles **hit nullification**; **threaten + memory** handles **attacker miss tax** when pressure was real and no tackle landed (including victim **dodge out**). Keep both paths host-visible and synced where needed.
- Optional later: pair with **Juggernaut** ramp reset on whiff.

### Playtest reminder (once player dodge exists in-game)
**Reminder — stop and retune:** after dodge is in, **you (or the next session)** should revisit tackle pressure and **deliberately adjust at least one** of **`TackleDirectionThreshold`** (approach cone), **inner threaten zone** (fraction of `TriggerSphereRadius` or separate radius), or the **hybrid** path (outer ring + short memory). Real dodge timing shifts how often inner vs outer exchanges happen; don’t leave cone + inner + memory at pre-dodge guesses.

**Scoring / macro feel:** If carriers can **consistently** dodge-stall to goal in scrims, bias tuning toward **stronger whiff tax**, **longer dodge CD with ball**, **tighter inner threaten for passive rewards**, or **weaker chain-dodge** (post-dodge walk / fewer escape vectors) — see **Important Decisions** and **Balance intent — dodge vs tackle vs scoring**.

## Weapons (design — future implementation)
Host-authoritative when gameplay outcomes matter; **not built yet**. Aligns with movement/dodge tier rules above.

### Carry rules
- **Ball XOR weapon:** cannot carry **both**; ball carrier has **no** weapon.
- **Touch free ball while armed:** **strip weapon → grab ball** (same auto-grab concept as today, extended). Order on host avoids physics clipping between weapon mesh/colliders and ball.

### Hold penalty (weapon equipped, off ball)
- **Non-Jugg classes:** use **Juggernaut’s** `TimeToCatchUpSpeed` **and** `CatchUpMoveSpeed` while holding — **ramp and top charge speed** both match Jugg (“everyone fights at Jugg’s armed weight class”).
- **Juggernaut:** `IgnoreWeaponSpeedPenalty = true` (or equivalent) — **no extra** hold penalty beyond their normal slow curve; advantage is **comparative** vs other classes who were scaled **down** to Jugg.

### Swing / attack
- **Speed tier:** **drop one tier** — **same table as dodge** (charge→run, run→walk, walk remains walk).
- **`WeaponSwingSpeedPenaltyDuration` in `ClassData`:** name predates this rule; **repurpose or remove** when implementing — e.g. optional lockout before re-entering charge, overlap with weapon CD, or delete if tier-only is enough. Do **not** assume “force walk for N seconds” unless playtests add it back as a layer.
- **Throw charge after weapon swing:** **TBD** in code; consider clearing throw windup on swing for parity with **dodge clears throw** (v1).

### One-use / durability (existing intent)
- **Ranged / explosive:** consumed on use.
- **Melee:** only breaks on **successful hit** (per earlier design).

### Future: **Weapons specialist** class (melee-first roster)
- **Not Sniper:** Sniper stays **throw / ball** fantasy; specialist is **melee weapon** identity — low conceptual overlap if most weapons are melee.
- **Passive options (pick one per match, like other classes):** e.g. **lighter weapon tax** — faster ramp with weapon, higher top with weapon, or **both** (justify with tradeoffs).
- **Tradeoff:** **weaker tackles** vs other bruisers — **peak power with weapon equipped**, not equal tackle threat when disarmed. Tune so tackles are **weaker**, not **useless**.

### Sniper note (throw, not weapons roster)
- Throw-charge / dodge exceptions stay **Sniper-only**; do not fold throw passives into weapons specialist.

## Constraints and Rules
Only include constraints that are easy to forget and expensive to violate.

- Engine/version constraints: s&box component workflow; compile errors can hide components from inspector list.
- Performance constraints: Keep gameplay logic simple for now; no heavy systems needed yet.
- Code style/conventions: Beginner-readable names, short methods, one source of truth for state.
- Tooling/workflow constraints: After script edits, allow recompile/restart editor if component does not appear.
- Networking constraints: Ball gameplay state is host-authoritative; avoid client-only gameplay mutation paths.
- Cosmetics/visual constraints: Keep avatar/cosmetics apply logic visual-only and isolated from spawn authority components.
- Refactor safety constraint: Mark cleanup chats as `refactor-only, no behavior change intended`, keep scope to 1-2 files, and run quick regression checks after.
- Scene/inspector constraint: Agent does not edit `.scene` or prefab files. All in-engine setup is done by Max.

## Network-Safe Rules (Always Apply)
Treat these as mandatory implementation rules for all new gameplay features.

1. **Authority first (before coding):**
   - Decide who owns truth for gameplay outcomes (default: host/server).
   - Do not let clients finalize gameplay outcomes.
2. **Input is a request, not a result:**
   - Client sends action request to host (for example tackle, grab, throw).
   - Host validates and applies final result.
3. **Sync final gameplay state:**
   - Sync authoritative end states (`IsHolding`, `IsRagdolled`, cooldown state, etc.).
   - Do not rely on local-only gameplay booleans for shared truth.
4. **Separate gameplay and visuals:**
   - Gameplay logic must be authoritative and replicated.
   - Client-only effects/animations are fine, but must not decide outcomes.
5. **2-window validation is required before done:**
   - Host action visible correctly on client.
   - Client action visible correctly on host.
   - Rapid repeat/spam test.
   - At least one edge case (jump/mid-air/moving fast/timing overlap).

### Feature Build Pattern (Use Every Time)
1. Define authority and synced state fields first.
2. Implement host RPC request flow for actions.
3. Apply outcomes on host only, then replicate/sync.
4. Add minimal visual handling after gameplay sync is stable.
5. Run multiplayer checklist before marking feature complete.

## Collaboration Preferences
- Experience level: Beginner (new to development).
- Communication style: Explain steps in simple language, avoid jargon where possible, and include quick "what/why" context for commands and changes.
- **Git:** Default integration branch is **`main`** — commit and push session / gameplay work to **`main`** unless intentionally isolating on a short-lived branch.
- Reminder: If naming confusion/repeated renames starts happening, propose expanding Naming Canon (especially before team size or system count grows).

## Prerequisites for Planned Systems
These are small gaps in existing code that must be filled before the planned systems can be built. Do not build these proactively — add them at the time they're needed.

- [x] `CatchUpSpeedBoost` needs a `public bool IsAtChargeSpeed { get; private set; }` getter so the tackle system can check if the attacker qualifies.
- [x] `ClassData` **`MomentumMultiplier`** applied in **`CatchUpSpeedBoost`** (scales **`PlayerController.AccelerationTime`** / **`DeaccelerationTime`** + smoothed cap).
- [ ] `CatchUpSpeedBoost` speed/timing properties need to be driven from `ClassData` instead of inspector values.
- [ ] `BallThrow` force and charge timing need to accept per-class multipliers from `ClassData`.
- [ ] Player capsule size (height/radius) needs to be set from `ClassData.ModelScale` when class system is built.

## Undecided (revisit later)
**Convention:** For anything deferred (design, exploits, tuning forks): add **one short bullet** with enough context to decide later; **delete it** once resolved—keeps “what’s undecided?” scans in one place (`SESSION_NOTES.md` § Undecided).

- **Forward + backward together:** cancelling movement still lets charge ramp build; might be cool fake-out or too strong hidden tech without UI/tell—later choose fix ramp vs ship with readability.
- **Closed-roof arena:** revisit sealed stadium lighting vs **open roof + sun** prototype once gameplay baseline is stable (see Known Issues **`testing_map`** silhouette note).
- **Tackle hit stop:** optional local-only juice vs relying on post-tackle sprint tier; defer until tackle feel is otherwise locked.

## Known Issues / Risks
- [x] Anti-dribble / passive push resolved via auto-grab on contact.
  - Decided: body-push is too complex to make consistent across host/client. Auto-grab is the EF Throwdown compromise and fits the game design.
- [x] MUST SOLVED: client first-contact non-solid bug (could walk/jump into ball until first grab+drop).
  - Fix landed: `BallClientFeel` free-ball owner proxy now disables rigidbodies only and keeps colliders enabled.
- [ ] Throw tuning still placeholder (force/arc may need balancing).
  - Impact: Throw feel may be too strong/weak depending on map and movement speed.
  - Owner: Max
  - Next action: Tune `ThrowForce`, `ThrowUpForce`, and `ThrowStartOffset` in inspector playtests.
- [ ] While charging throw, walk/run animations can still play in place even though movement is locked.
  - Impact: Mechanics are correct, but visual polish is rough during charge.
  - Owner: Max
  - Next action: Handle with explicit charge/throw animation states in animation pass.
- [ ] Future animation integration not done yet.
  - Impact: Visual quality lower for now, but mechanics are playable.
  - Owner: Max
  - Next action: Add throw/run-holding animations after mechanics lock-in.
- [ ] Multiplayer still needs broader stress testing (rapid pickup/drop/throw spam, jump-drop edge cases, longer sessions).
  - Impact: Core behavior is much better, but edge desync risk remains until stress tests pass repeatedly.
  - Owner: Max
  - Next action: Run 2-window multiplayer consistency pass for 15-20 minutes and log any repro steps.
- [ ] Need one fresh regression pass after this session's changes.
  - Impact: Auto-grab + cleanup touches several files; one clean 2-window pass recommended.
  - Owner: Max
  - Next action: Fresh host + client session, test grab/drop/throw/auto-grab, confirm no regressions.
- [ ] **`testing_map`:** Sealed interior made **projected caster silhouettes** on Hammer floor/walls hard to read (player + ball); **open roof + `light_environment`** workaround for now — revisit interior-only lighting / **`r.shadows.*`** / minimal Facepunch repro if this persists on closed arenas.

## Current Tackle Status (Resume Here Next Chat)

### What works
- Tackle detection: **host** uses local velocity; **client owners** request via RPC with snapshots (charge speed synced from owner).
- **Dodge:** Double-tap strafe (actions `left`/`right` by default); host RPC; **`PlayerDodge`** on player; shove via **`Rigidbody.Velocity`**; tackle iframe **`IsImmuneToTackle`**; **`BallThrow.NetIsChargingThrow`** + Sniper `ClassName` gate; ramp / recharge block in **`CatchUpSpeedBoost`**.
- **Ragdoll visual** on both screens: separate `PlayerRagdoll` with base body + cosmetics (`BoneMergeTarget`); early `NetworkSpawn` to reduce invisible gap.
- Player model hidden during ragdoll: `hiddenRenderers` cached and re-enforced every frame.
- Camera follows ragdoll (`NetRagdollPosition` / `RagdollClientFeel` on owning client).
- **Ragdoll owner camera:** free look (`EyeAngles` + third-person orbit via `RagdollCameraDistance` / `RagdollCameraHeight`) **only when `this.Network.IsOwner`** (single shared MainCamera).
- **Stand-up camera:** `OnPreRender` blends from last ragdoll camera pose to `PlayerController`'s camera for the frame (`StandUpCameraBlendDuration`, default **0.6s**).
- Stand-up: host waits for **grounded + settled** time (`RagdollDuration` consecutive) or **`RagdollMaxDuration`** cap; floor trace for `NetStandUpPosition` **unless** victim root has tag **`practice_npc`** (then **`NetStandUpPosition`** + **`NetPracticeNpcStandEyeAngles`** restore pre-tackle snapshot); invincibility after.
- Post-tackle invincibility working.
- `Bodies[0]` = pelvis; launch via **`ApplyImpulse`** (not `PhysicsGroup.Velocity`).

### What is still missing / known issues
- **Tackle whiff penalty:** inner threaten + short memory (design in SESSION_NOTES); **not** in code yet.
- **Launch force tuning:** `TackleLaunchSpeed` / `TackleLaunchArc` still dial-in (rough band ~400–800 per older notes).
- **Stand-up animation:** optional future — get-up clip instead of instant pose; retime or replace camera blend when built.
- **Stand-up hover** (limb trace): mitigated with `.WithoutTags("ragdoll")` on body parts.

### Scene setup that must be correct every session (recompile may drop some)
- `PlayerTackle` on root player object (gets dropped on recompile — check every session)
- **`PlayerDodge` on root player** (double-tap dodge; `ShoveVelocityMultiplier` + strafe action names)
- `RagdollClientFeel` on root player — smooths owning client's ragdoll camera/puppet (`InterpolationDelay`, `FollowSharpness`; optional tune)
- `PlayerClass` on root player with Speedster/Sniper/Juggernaut `.cdata` asset assigned
- `CatchUpSpeedBoost` on root player
- **`PlayerController` third person:** `CameraOffset` **X = 185** (current feel tuning; editor-only)
- **Do NOT add `ModelPhysics` to the player prefab** — it is no longer used on the player object. The ragdoll is a separately spawned object.
- **`practice_npc` (tag on root):** on dummy **roots** used for tackle / ClassData tests only — restores **pre-tackle** position + **EyeAngles** after ragdoll; **never** on real spawned players.
- **`MapInstance`** (optional): **`MapName`** **`testing_map`** — aligns scene preview with compiled arena; **`StartupMapBootstrap`** skips spawning if one already exists.
- When Hammer floor is in use: **disable/remove** old **scene grass plane** (avoid double floor / z-fight).
- All three `.cdata` assets must have values set manually:
  - Movement: StartMoveSpeed=140, SprintMoveSpeed=220, CatchUpMoveSpeed=320, TimeToSprintSpeed=2, TimeToCatchUpSpeed=4
  - Tackle: TriggerSphereRadius=40, **RagdollDuration** (= seconds grounded+settled before stand), **RagdollMaxDuration**, **RagdollGroundSpeedMax**, **RagdollGroundTraceDown**, **RagdollGroundTraceUp**, PostTackleInvincibilityDuration=1, BallLaunchForceOnTackle=500, BallPickupLockoutAfterTackle=1.5 (open each `.cdata` in editor for new fields / defaults)
  - Dodge: **DodgeCooldown**, **DodgeDistance** (default in type **260**; old assets may still read **200**), **DodgeInvincibilityWindow** (type default **0.14** s; assets may still hold **0.3**)
  - Mass: Mass=80 (same placeholder for all three for now)

## Current Plan (Top 3)
1. **MP regression** — dodge + grab/drop/throw + tackles on **`testing_map`** (2-window), note any desync on dodge apply.
2. **Tune ragdoll launch** — `TackleLaunchSpeed` / `TackleLaunchArc`; try class-specific caps later.
3. **Tackle whiff** (when ready) — inner threaten + memory; then **stand-up animation** / camera blend coordination.

---

## End-of-Session Handoff
- **2026-05-10:** **`PlayerClass`** applies **ClassData** capsule + **`ModelScale`** (**`PlayerController.BodyHeight`/`BodyRadius`** + skin roots; retry window for cosmetics); **`PlayerTackle`** ragdoll **`LocalScale`** uses victim **`ModelScale`**. **`CatchUpSpeedBoost`:** **`MomentumMultiplier`** × **`AccelerationTime`**/**`DeaccelerationTime`** (snapshot) + smoothed cap to tier; throw charge resets accel to baseline; **`Forward`** action casing + strafe/charge ramp (**`UsingController`** analog path). SESSION_NOTES checklist + commit + push.
- **2026-05-10 (Hammer / arena):** Added **`Assets/Maps/testing_map.vmap`** pipeline: **`Code/Map/StartupMapBootstrap.cs`** mounts **`MapInstance`** with **`MapName` `testing_map`**; **`ultimate_throwdown.sbproj`** **`"Resources": "*"`** so **`maps/testing_map.vpk`** ships; **`MapList`** updated. Concrete/`AmbientCG` workflow noted; interior lighting time sink → **roof removed**, **`light_environment`** sun; **`full`** compile when brightness bake stuck vs **`entities-only`**; **viewport black-until-Stop→Play** after compile. Optional scene **`MapInstance`** + remove duplicate grass floor when aligned.
- **2026-05-10:** **`practice_npc`** root tag + **`NetPracticeNpcStandEyeAngles`** for dummy stand pose after tackle; ragdoll **MainCamera / AnalogLook / stand-up blend** gated on **`this.Network.IsOwner`** (fixes host camera following NPC ragdolls). **`ClassData`** / `.cdata` dropped unused turn-speed fields; charge yaw unchanged (**`CatchUpSpeedBoost.ChargeYawMaxDegreesPerSecond`**). **`BallGrab`** pickup block replicated via **`NetPickupBlockedRemain`**; attacker carrier-tackle **`AttackerPickupLockoutAfterCarrierTackle`** → **`BlockPickupForSeconds`**. Disambiguated **`this.Network.Owner` / `IsOwner`** where needed. SESSION_NOTES + commit + push.
- **2026-05-08 (later):** Dodge shove direction fix in **`PlayerDodge.ApplyShoveVelocity`**: lateral from **`EyeAngles.ToRotation().Right`** + **`FindMode.EverythingInSelfAndDescendants`** for `PlayerController`; spawn / yaw alignment bugs resolved. SESSION_NOTES + commit + push.
- **2026-05-08:** **`PlayerDodge`** implemented (merged into **`CatchUpSpeedBoost.cs`**). **`ClassData`** merged into **`PlayerClass.cs`** (fix editor **`CS0246`**). **`BallThrow`** owner-only updates + **`NetIsChargingThrow`**. Dodge shove **`Rigidbody.Velocity`** add; **`DodgeDistance` / iframe / multiplier** tuning. SESSION_NOTES + commit.
- **2026-06-06:** Ground-based ragdoll recovery (`RagdollDuration` = consecutive grounded+settled time; `RagdollMaxDuration` + trace/speed tunables in `ClassData`). SESSION_NOTES updated for MP tackle RPC, cosmetics ragdoll, early `NetworkSpawn`, merging decisions.
- Earlier 06/05 history: separate host ragdoll GO, pelvis `ApplyImpulse`, `RagdollClientFeel`, stand-up floor trace `.WithoutTags("ragdoll")`, etc. (see bullets above).

*(Older session logs below kept for archaeology.)*

## Ragdoll Visual Research

- What changed (06/05/26 session 2): Discovered the correct approach for multiplayer ragdoll — spawn a separate host-owned ragdoll GameObject instead of trying to ragdoll the player object. Implemented Option D: host spawns PlayerRagdoll with ModelPhysics, NetworkSpawn makes it visible everywhere, player renderers hidden during ragdoll, camera follows via NetRagdollPosition, stand-up snaps to NetStandUpPosition. Both screens now see the ragdoll. Committed and pushed to feature/class-system.

### Why ragdolling the player object itself fails
All approaches that try to ragdoll the player's own `GameObject` hit the same wall: **the client owns the player's transform and overrides physics every frame.**

- `IgnoreRoot=false` + `ModelPhysics` on player: host physics moves the body, client keeps syncing `WorldPosition` back. Character freezes from client's view.
- `IgnoreRoot=true` + `SimulateRagdoll` (carry bones by delta): bones travel with root on owner, but physics doesn't simulate on proxy machines so host sees no collapse. Also: additional `SkinnedModelRenderer` components (clothing) on the same object don't get driven by `ModelPhysics` and appear as T-pose ghost.
- **Rule: Do not attempt to use `ModelPhysics` on a client-owned networked player object. It will not work.**

### Option D — Spawn a separate host-owned ragdoll object (CURRENT APPROACH, WORKING)
The key insight from GMod's EFT and s&box's sandbox ragdolls: **don't ragdoll the player — spawn a separate ragdoll object that the host owns.**

How it works:
1. Host spawns a new `GameObject` ("PlayerRagdoll") at victim's position with victim's base model + `ModelPhysics`.
2. `NetworkSpawn()` makes it visible on all clients automatically. Physics runs on host, syncs to all.
3. Host sets `PhysicsGroup.Velocity` after 0.05s init delay (wait for physics bodies to initialise). All bones fly as a unit; joints settle naturally.
4. Host syncs ragdoll's `WorldPosition` â†’ `NetRagdollPosition` each frame. Owner sets own `WorldPosition = NetRagdollPosition` to follow for camera.
5. On stand-up: `NetStandUpPosition` = ragdoll's final position. Owner snaps there. Ragdoll destroyed.
6. Player renderers hidden via `hiddenRenderers` list (cached at tackle time, re-enforced every `OnUpdate` frame).

### Key API facts confirmed
- `modelPhysics.Bodies` returns 16 bodies for citizen model — valid for inspecting/identifying bones.
- `Bodies[0].Component.GameObject.Name = "pelvis"` — the root/pelvis body is index 0.
- **`modelPhysics.PhysicsGroup.ApplyImpulse(vel, true)`** — correct API for launching the whole ragdoll. `withMass=true` multiplies impulse by each body's mass so every bone gets the same velocity change. Also wakes sleeping bodies.
- Must wait until `PhysicsWereCreated == true && PhysicsGroup != null` before calling — `PhysicsGroup.BodyCount` can return 0 even when bodies exist (different list from `ModelPhysics.Bodies`). `PhysicsWereCreated` is the definitive ready flag.
- `PhysicsGroup.Velocity = x` does not reliably work — group may have 0 bodies at the time it's set, or may not wake sleeping bodies. Use `ApplyImpulse` instead.
- `body.Component.Velocity = x` per-body does NOT reliably work for ragdoll launch — the constraint solver ignores or overrides it. Do not use for launch.
- Additional `SkinnedModelRenderer` on the same ragdoll `GameObject` as `ModelPhysics` are NOT driven by physics — they appear as T-pose ghost. Only link one renderer to `ModelPhysics`.
- `GetAll<SkinnedModelRenderer>` for hiding must be called at tackle time, not at `OnStart` — cosmetics are added dynamically after spawn.

### Remaining ragdoll polish (not blocking)
- **Naked ragdoll:** Only base model on ragdoll (no clothing). Clothing on a separate physics-driven ragdoll requires investigation of multi-renderer physics approaches.

---

## Planned Systems (Design Locked, Not Yet Built)

### Class System

Three player classes. **All player stats read from `ClassData` — never hardcoded on components.**

| Class | Mass | Size | Speed Ramp | Throw |
|---|---|---|---|---|
| Speedster | Lightest | Smallest | Reaches charge speed fastest | Default |
| Sniper | Middle | Middle | Middle ramp | Faster charge, higher force |
| Juggernaut | Heaviest | Biggest | Slowest ramp | Default |

**`ClassData` fields (s&box `[GameResource]`):** *(C# type lives in `Code/Player/PlayerClass.cs` — not `ClassData.cs`.)*

| Field | Type | Notes |
|---|---|---|
| `ClassName` | string | Display name |
| `Mass` | float | Gameplay-only — used in tackle formula, not a physics property |
| `CapsuleHeight` | float | Player capsule height |
| `CapsuleRadius` | float | Player capsule radius |
| `ModelScale` | float | Visual scale |
| `TriggerSphereRadius` | float | Tackle detection sphere radius; scales with class size |
| `StartMoveSpeed` | float | Replaces hardcoded value in `CatchUpSpeedBoost` |
| `SprintMoveSpeed` | float | Replaces hardcoded value in `CatchUpSpeedBoost` |
| `CatchUpMoveSpeed` | float | Replaces hardcoded value in `CatchUpSpeedBoost` |
| `TimeToSprintSpeed` | float | Speedster lowest, Juggernaut highest |
| `TimeToCatchUpSpeed` | float | Speedster lowest, Juggernaut highest |
| `MomentumMultiplier` | float | **`CatchUpSpeedBoost`:** × snapshotted **`PlayerController.AccelerationTime`** / **`DeaccelerationTime`** + eased walk/run cap (**1** baseline, **>1** heavier, **<1** snappier); tackles still use instant ramp tier |
| `ThrowPower` | float | Sniper > 1.0, others 1.0 |
| `DodgeCooldown` | float | Cooldown between dodges |
| `DodgeDistance` | float | How far a dodge travels |
| `DodgeInvincibilityWindow` | float | Invincibility frames during dodge |
| `RagdollDuration` | float | Seconds down after being tackled (default 2s) |
| `TackleChargeRampRate` | float | Juggernaut passive: bonus multiplier per second at charge speed |
| `MaxTackleChargeBonus` | float | Juggernaut passive: cap on the ramp bonus |
| `IgnoreWeaponSpeedPenalty` | bool | If true, class ignores weapon-hold speed slowdown (intended **Juggernaut** while weapon system exists) |
| `WeaponSwingSpeedPenaltyDuration` | float | Legacy field name — **weapon swing = tier drop** like dodge (see **Weapons (design)**); repurpose or remove on implementation |

**Global (not in ClassData):**
- `TackleLaunchSpeed` — single force tuning knob on `PlayerTackle`, not per-class. Class multipliers applied on top when class system is built.
- **Universal charge yaw** — **`CatchUpSpeedBoost.ChargeYawMaxDegreesPerSecond`** on each player (same knob for every class unless you revisit per-class tuning later).

**Explicitly removed:**
- `ThrowChargeSpeedMultiplier` — dropped; Sniper's identity is `ThrowPower` + dodge-while-charging passive
- `MaxSpeed`, `Acceleration` — dropped; keeping three-tier speed system (`StartMoveSpeed`/`SprintMoveSpeed`/`CatchUpMoveSpeed`)
- `WeaponMissSpeedPenalty` — renamed to `WeaponSwingSpeedPenaltyDuration` (penalty applies on all swings, not miss-only)
- `WalkTurnSpeed`, `RunTurnSpeed`, `ChargeTurnSpeed` — dropped 2026-05-10: rotation while walk/run stays default **mouse / look sensitivity** via `PlayerController`; **charging** yaw cap is universal **`CatchUpSpeedBoost.ChargeYawMaxDegreesPerSecond`** (same for every class).

**Notes:**
- `Mass` is gameplay-only. `PlayerController` is a character controller with no physics mass. The tackle formula uses this float directly.
- Class size affects model scale, capsule dimensions, and trigger sphere radius — all live in `ClassData`.
- `CatchUpSpeedBoost` will need to read from `ClassData` instead of its own inspector properties when this is built. Ask before modifying it.
- `BallThrow` force will need to read from `ClassData` (`ThrowPower`). Ask before modifying it.
- Weapon system is future work — see **Weapons (design — future implementation)** for locked notes (`IgnoreWeaponSpeedPenalty`, tier-drop swing, ball XOR weapon, armed auto-grab sequence, specialist class sketch).
- **Summary:** Hold weapon ⇒ **both** Juggernaut `TimeToCatchUpSpeed` **and** `CatchUpMoveSpeed` for non-exempt classes. Swing ⇒ **one tier down** (same as dodge), not primary “walk for N seconds.” Ball **or** weapon only; armed contact with free ball ⇒ **strip weapon then grab** on host. One-use: ranged/explosive consumed on use; melee breaks on successful hit.
- Passives and ults are designed (see below) but not being built yet. They will be selectable at match/round start.

---

### Dodge Mechanic (**implemented** — tune in `.cdata` + `PlayerDodge` inspector)

- **Input:** **Double-tap** strafe **left/right** (`PlayerDodge` defaults `LeftStrafeAction` / `RightStrafeAction` → project actions e.g. `left`/`right`; A/D follows user keybinds for those actions). **`DoubleTapMaxInterval`** on component.
- **Host:** `[Rpc.Host]` validates caller, cooldown, throw-charge vs **Sniper** `ClassName`, syncs iframe / penalties / dodge apply id. **Owning client:** consumable shove = **`Rigidbody.Velocity` += lateral × (`ClassData.DodgeDistance` × **`ShoveVelocityMultiplier`**)**, clamp caps in code (~6000). **Lateral** = flattened **`EyeAngles.ToRotation().Right`** (view-relative strafe), not pawn `WorldRotation` (see Important Decisions).
- **`BallThrow`:** **`NetIsChargingThrow`** for host validation; **`ClearThrowChargeLocal()`** when dodge clears windup (Sniper dodge-while-charge rules per design).
- **`CatchUpSpeedBoost`:** Applies ramp pulse + **`SyncedBlockCatchUpUntil`** recharge gate after charge-tier dodge.
- **`PlayerTackle`:** Skips victim while **`PlayerDodge.IsImmuneToTackle`**.
- **Not built:** **Tackle whiff** inner threaten + memory; Speedster dodge passive payouts (hooks: `IsDodging`, iframe timing).

---

### Passives and Ults (Designed, Not Being Built Yet)

Selectable per class at match/round start.

**Speedster**
- *Passive:* **Successful dodge** vs a tackle — host attempt would connect, victim **`IsDodging`** (iframe) → nullify hit → grant **short speed boost** and/or **keep them at running speed** (floor so the read isn’t punished with walk-tier after burn). See **Tackle whiff penalty → inner threaten**: reuse that **inner** distance + threaten flag to tier reward (**inner-pressure** dodge = full passive; **outer-only** = lighter or no proc — one tuned knob for both whiff tax and Speedster payoff).
- *Ult:* Lightning dash. Long-distance charge in a fixed direction. Charge-up time required. **Facing direction is snapshotted at the moment the charge begins — player cannot adjust aim during charge-up.** On contact with a player, delivers a powerful tackle.

**Sniper**
- *Passive:* Can dodge while charging throw. Other classes cannot dodge during charge. (`PlayerDodge` must check if player is Sniper before allowing dodge during charge state.)
- *Ult:* Sonic boom throw. Ball travels its normal arc, but creates an AOE ragdoll zone along the flight path each frame. Requires a flag (`IsUltThrow` or a projectile mode) on the ball so it knows to run sweep logic. Most complex system of the four — build last.

**Juggernaut**
- *Passive:* The longer they stay at charge speed, the more powerful their tackle. Bonus accumulates at `TackleChargeRampRate` per second, capped at `MaxTackleChargeBonus`. Resets immediately when they drop below charge speed (counterplay: slow them down to reset their stack).
- *Ult:* AOE circular stomp. Centered on the Juggernaut. All players within radius are sent flying as ragdolls.

---

### Tackle System

**Core rules:**
- Trigger sphere on each player (radius from `ClassData.TriggerSphereRadius`), slightly larger than capsule. **Never `OnPhysicsCollision`** — unreliable in multiplayer.
- Tackles only trigger when attacker is at charge speed (`CatchUpSpeedBoost.IsAtChargeSpeed`; owner-replicated).
- **Host** detection uses local velocity when valid; **client owners** send tackle RPC with aim/position snapshots (host validates).
- Velocity + direction check before applying: dot product of attacker forward / move dir vs direction to victim must exceed `TackleDirectionThreshold`.
- Host-authoritative outcomes; ragdoll + stand-up timing on host.

**Launch force (implemented in `PlayerTackle.ExecuteTackle`):**
- Base: `TackleLaunchSpeed` × `TackleLaunchArc` defines direction; effective speed = `TackleLaunchSpeed × tacklePower`.
- `tacklePower = massRatio × (1 + tackleChargeBonus)`, where `massRatio = clamp(attacker.Mass / victim.Mass, 0.5, 2.5)` from each player's `ClassData` (default mass 80 if missing).
- Ball knock-off: same direction; speed = `BallLaunchForceOnTackle × tacklePower` from victim's `ClassData`.
- **Juggernaut-style ramp (any class with non-zero ramp fields):** Host accumulates `tackleChargeBonus` at `TackleChargeRampRate` per second while `IsAtChargeSpeed`, capped by `MaxTackleChargeBonus`; resets to 0 when below charge speed or when this player is ragdolled. Tune Juggernaut's `.cdata` — Speedster/Sniper can leave ramp at 0.
- Ragdoll impulse still uses pelvis `ApplyImpulse(launchDir × effectiveSpeed × ragdollPhysics.Mass)` after local init delay.

**On tackle hit:**
1. Victim enters full ragdoll — launched by pelvis `ApplyImpulse` after `RagdollPhysicsInitDelay`.
2. Victim drops ball; ball launched per `BallLaunchForceOnTackle × tacklePower`; pickup lockout per `BallPickupLockoutAfterTackle`.
3. After **grounded + settled** for `RagdollDuration` consecutive seconds (or **`RagdollMaxDuration`** cap from tackle), stand-up trace and `NetStandUpPosition`.
4. Post-tackle invincibility: `PostTackleInvincibilityDuration`.

**Ragdoll physics init:**
- Inspector **`RagdollPhysicsInitDelay`** on `PlayerTackle` (default 0.05s): host wait after `NetworkSpawn` before `ApplyImpulse`. Too low can weaken launch.

**Auto-grab stays as distance check (not trigger sphere):**
- `OnPhysicsCollision` unreliability is the reason tackles need trigger spheres. Auto-grab doesn't have that problem.
- Distance check runs every frame and only targets one known object. Switching to a trigger sphere adds editor setup and collider filtering for no gain.

## Proven Fix Recipes (Reuse)
Use these when the same symptom appears again.

### Client Free-Ball Jitter (Host looks good, client looks floaty/jittery)
**Symptom**
- Host sees natural bounces, client sees rapid up/down jitter or floaty correction (especially jump-apex drop tests).

**What actually fixed it**
1. Keep host as full gameplay/physics authority (`BallGrab` remains source of truth).
2. In `BallClientFeel`, disable local free-ball rigidbody simulation on owning client during free-ball visual follow, but keep colliders enabled (prevents local physics fighting host corrections while preserving first-contact solidity).
3. Buffer recent host snapshots on client (`position`, `rotation`, timestamp) and render from delayed buffered target (`InterpolationDelay`) instead of chasing latest transform each frame.
4. Avoid double smoothing: when buffered target is available, render directly from buffered interpolation result.
5. Add velocity-aware interpolation path (snapshot linear velocity + Hermite position interpolation) to better preserve bounce/throw arc shape.

**What did NOT help enough**
- Tuning only `FreeBallVisualFollowSharpness` / `ContactBoostSharpness` / `ContactBoostDuration` without changing interpolation approach.
- Hard snap thresholds for normal motion (reduced one jitter pattern but made roll/push look framey/teleporty).

**Validation path**
- 2+ windows.
- Repeat exact apex jump-drop test and normal run/push test from same start point.
- Confirm host/client ball path similarity improved without introducing framey motion.

### Body-Push Consistency (Host can push ball, client cannot)
**What was tried and failed:**
- Gain-detection (per-frame horizontal speed increase near player) — per-frame impulses too small to catch reliably.
- Grace-period after throw — ball still pushable during grace window.
- `BallPush` RPC component — correct pattern but adds complexity and round-trip delay makes it feel different to host.

**Decision:** Don't solve body-push. Use auto-grab instead. Same compromise as Extreme Football Throwdown (Garry's Mod).

## Component Missing Recovery (s&box)
If a component (for example `BallThrow`) does not appear in Add Component:

1. Save all scripts.
2. Wait for script compile to finish.
3. Try Add Component search again with exact name.
4. Recompile scripts once (or trigger project refresh).
5. If still missing, restart s&box/editor.
6. After restart, confirm no compile errors before adding component.

## Naming Canon
Use these exact names unless explicitly changed in chat.

- Core state owner component: `BallGrab`
- Client feel helper component: `BallClientFeel`
- Throw helper component: `BallThrow`
- Catch-up movement component: `CatchUpSpeedBoost`
- Throw charge display component: `ThrowChargeBar`
- Hold-state public getter: `IsHolding`
- Held-ball public getter: `HeldBall`
- Ball selection properties: `MainBall`, `MainBallName`
- Interaction properties: `InteractDistance`, `InteractAction`, `HoldAnchor`
- BallGrab pickup/drop timing properties: `PickupDelayAfterDrop`, `DropperNoPushWindow`
- BallGrab drop no-push tuning properties: `DropperNoPushRadius`, `DropperMaxHorizontalSpeed`
- Drop tuning properties in `BallGrab`: `DropSideOffset`, `DropVelocityScale`
- BallGrab prompt property: `PromptText`
- Auto-grab rate limiter in `BallGrab`: `nextAutoGrabAttemptAt`
- Internal ball references in `BallGrab`: `ballObject`, `ballOriginalParent`, `ballCollidersToRestore`, `ballBodiesToRestore`
- Multiplayer manager component: `GameNetworkManager`
- Network manager properties: `PlayerTemplateName`, `DisableTemplateOnStart`
- Sync property in `BallGrab`: `NetIsHolding`; host countdown sync: **`NetPickupBlockedRemain`** (`FromHost`, extends with `MathF.Max` in `BlockPickupForSeconds`)
- Host-synced ball transform properties in `BallGrab`: `NetHeldBallWorldPosition`, `NetHeldBallWorldRotation`
- Host-synced ball velocity properties in `BallGrab`: `NetHeldBallLinearVelocity`, `NetHeldBallAngularVelocity`
- Client-read synced ball transform getters in `BallGrab`: `SyncedBallWorldPosition`, `SyncedBallWorldRotation`
- Client-read synced ball velocity getters in `BallGrab`: `SyncedBallLinearVelocity`, `SyncedBallAngularVelocity`
- Multiplayer debug property: `EnableNetDebugLogs`
- Free-ball feel properties in `BallClientFeel`: `FreeBallVisualFollowSharpness`, `ContactBoostSharpness`, `ContactBoostDuration`
- Snapshot interpolation properties in `BallClientFeel`: `InterpolationDelay`, `MaxSnapshots`
- Throw properties in `BallThrow`: `ThrowAction`, `ThrowForce`, `ThrowUpForce`, `ThrowStartOffset`, `PickupDelayAfterThrow`, `ThrowDirectionSource`
- Charge tuning properties in `BallThrow`: `MinThrowChargeTime`, `MaxThrowChargeTime`, `MinThrowForceMultiplier`, `MinThrowUpForceMultiplier`
- Charge-state public getter in `BallThrow`: `IsChargingThrow`
- Charge-state sync field (inspector-exposed internals): **`NetIsChargingThrow`**; **`ClearThrowChargeLocal()`** on owner clears windup
- Catch-up movement properties in `CatchUpSpeedBoost`: `ForwardAction`, `StartMoveSpeed`, `SprintMoveSpeed`, `CatchUpMoveSpeed`, `TimeToSprintSpeed`, `TimeToCatchUpSpeed`, `MinForwardInput`; charge-speed sync: `NetAtChargeSpeed` (owner → others), public `IsAtChargeSpeed`
- Throw charge bar property in `ThrowChargeBar`: `ChargeBarOffset`
- Core release method in `BallGrab`: `ReleaseHeldBall()`
- Ball ownership handoff method in `BallGrab`: `TransferBallOwnershipToHost()`
- Pickup lockout method in `BallGrab`: `BlockPickupForSeconds(float seconds)`
- Cosmetics sync component: `PlayerCosmeticsSync`
- Cosmetics sync properties: `FirstApplyDelay`, `RetryInterval`, `MaxApplyAttempts`, `LockHighestLodAfterApply`, `EnableDebugLogs`
- Class data resource: `ClassData` (type declared in **`PlayerClass.cs`**)
- Class data fields: `ClassName`, `Mass`, `CapsuleHeight`, `CapsuleRadius`, `ModelScale`, `TriggerSphereRadius`, `StartMoveSpeed`, `SprintMoveSpeed`, `CatchUpMoveSpeed`, `TimeToSprintSpeed`, `TimeToCatchUpSpeed`, `MomentumMultiplier`, `ThrowPower`, `DodgeCooldown`, `DodgeDistance`, `DodgeInvincibilityWindow`, `RagdollDuration` (grounded+settled consecutive seconds before stand), `RagdollMaxDuration`, `RagdollGroundSpeedMax`, `RagdollGroundTraceDown`, `RagdollGroundTraceUp`, `PostTackleInvincibilityDuration`, `BallLaunchForceOnTackle`, `BallPickupLockoutAfterTackle`, `TackleChargeRampRate`, `MaxTackleChargeBonus`, `IgnoreWeaponSpeedPenalty`, `WeaponSwingSpeedPenaltyDuration` *(no walk/run/charge turn fields — see Explicitly Removed)*
- Tackle system component: `PlayerTackle`
- Tackle inspector properties: `TackleDirectionThreshold`, `TackleCooldown`, `TackleLaunchSpeed`, `TackleLaunchArc`, `RagdollCameraDistance`, `RagdollCameraHeight`, `EnableTackleDebugLogs`, `TackleRpcPositionSlop`, `TackleRpcRadiusFudge`, `RagdollPhysicsInitDelay`, `StandUpCameraBlendDuration`, **`AttackerPickupLockoutAfterCarrierTackle`**
- Stand-up camera blend internals: `lastRagdollCameraPos`, `lastRagdollCameraRot`, `standUpCameraBlendFromPos`, `standUpCameraBlendFromRot`, `standUpCameraBlendStartTime`
- Editor (not code): `PlayerController` third-person `CameraOffset` (current X **185**)
- Tackle synced properties: `NetIsRagdolled`, `NetRagdollPosition`, `NetStandUpPosition`, **`NetPracticeNpcStandEyeAngles`** (`FromHost`; **`practice_npc`** stand facing only), `NetIsTackleImmune`, `NetTackleBlockedUntil` (host-authored cooldown end time for tackle RPC alignment)
- Tag constant in `PlayerTackle`: **`PracticeNpcTag`** = **`practice_npc`**
- Tackle practice-dummy internals (non-synced unless noted): `practiceNpcPreTackleCaptured`, `practiceNpcPreTackleWorldPosition`, `practiceNpcPreTackleEyeAngles`; helper **`CapturePracticeNpcPreTacklePoseIfTagged`**
- Tackle host-only fields: `ragdollObject`; tackle RPC throttle `nextRemoteTackleRequestAt`; cooldown `tackleBlockedUntil` + synced `NetTackleBlockedUntil` / `netTackleBlockedUntil`
- Tackle renderer cache: `hiddenRenderers` (list of SkinnedModelRenderers hidden during ragdoll)
- Tackle collider cache: `disabledColliders` (list of Colliders disabled during ragdoll, re-enabled on stand-up)
- Tackle public getters: `IsRagdolled`, `IsTackleImmune`, `SyncedRagdollPelvisPosition`
- Tackled-player client feel component: `RagdollClientFeel` (same GO as `PlayerTackle`; owning client)
- Ragdoll snapshot feel properties in `RagdollClientFeel`: `InterpolationDelay`, `MaxSnapshots`, `FollowSharpness`
- Dodge component **`PlayerDodge`** (`Code/Player/CatchUpSpeedBoost.cs` tail): `LeftStrafeAction`, `RightStrafeAction`, `DoubleTapMaxInterval`, `CarrierDodgeCooldownFactor`, `RechargeBlockedAfterChargeDodge`, `ShoveVelocityMultiplier`, `EnableDodgeDebugLogs`
- Dodge getters: `IsImmuneToTackle`, `IsDodging`, `SyncedBlockCatchUpUntil`, `LatestPenaltyKind`, `DodgeApplySequence`
- Juggernaut passive ClassData fields: `TackleChargeRampRate`, `MaxTackleChargeBonus`
- Hammer bootstrap: **`StartupMapBootstrap`** (`Code/Map/StartupMapBootstrap.cs`) — implements **`ISceneStartup`**, spawns **`MapInstance`** if missing
- Map runtime component: **`MapInstance`** — **`MapName`** for this project: **`testing_map`** (basename; not `local.…` triple id on **`MapName`** when loading package-local map)
- Hammer source asset: **`Assets/Maps/testing_map.vmap`**; compiled outputs **`testing_map.vpk`**, **`testing_map.los`**
- Game project package maps (`.sbproj`): **`Resources`** should include compiled maps (**`*`** current choice); **`Metadata.MapList`** lists playable map ids

## Next Chat Kickoff
Paste this at the start of a new session:

`Read SESSION_NOTES.md first, continue from Current Plan, and propose any needed updates before coding.`

## End-of-Session Handoff
- What changed (06/05/26 session 2): Discovered the correct approach for multiplayer ragdoll — spawn a separate host-owned ragdoll GameObject instead of trying to ragdoll the player object. Implemented Option D: host spawns PlayerRagdoll with ModelPhysics, NetworkSpawn makes it visible everywhere, player renderers hidden during ragdoll, camera follows via NetRagdollPosition, stand-up snaps to NetStandUpPosition. Both screens now see the ragdoll. Committed and pushed to feature/class-system.
- What changed (06/05/26 session 3): Fixed three tackle issues: (1) launch direction, (2) capsule collision on ragdoll spawn, (3) attempted PhysicsGroup.Velocity fix (this later turned out to not be the real API — see session 4).
- What changed (06/05/26 session 4): Long debug session. Root cause of "ragdoll not flying" was simply TackleLaunchSpeed set too low (150 = ~35 units of travel, invisible). Confirmed working at 600+. Along the way: discovered PhysicsGroup is always null on ModelPhysics ragdolls (both via ModelPhysics and PhysicsBody); switched to PhysicsBody.Velocity per-body as the launch API; moved NetworkSpawn to AFTER velocity is set (keeps bodies in local physics world during velocity assignment); added explicit ModelPhysics.MotionEnabled=true and IgnoreRoot=false.
- What is still in progress: TackleLaunchSpeed needs in-game tuning (400–800 is the sweet spot range). Ragdoll is naked (clothing excluded). Camera snap on stand-up (lerp pending).
- What changed (06/05/26 session 4 continued): Fixed stand-up hover — floor trace was hitting ragdoll's own limb colliders (only root GO was excluded, not children). Fixed by tagging all body part GOs as "ragdoll" and using .WithoutTags("ragdoll") in the trace.
- Exactly what to do next: Open new chat, read SESSION_NOTES, tune TackleLaunchSpeed and upward arc (Vector3.Up * 0.35f in SpawnRagdollObject) until the fly feels right.
