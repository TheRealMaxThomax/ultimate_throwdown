# Gameplay design

**For:** Planning and tuning — dodge, tackle, classes, future weapons.  
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
| Ball carrier glow (held ball) | **Built** — `BallCarrierOutline` on `main_ball`: gold colour-pulse outline + emissive breathe; everyone except carrier; line-of-sight only (no through walls) |
| Teammate carrier off-screen arrow | **WIP** — `BallCarrierOffscreenHud` on player; team-only; needs polish |
| Crouch / duck | **Disabled** — `PlayerDisableCrouch`; `Duck` unbound in `Input.config` |
| Weapons | **Not built** |
| Class passives / ults | **Not built** (stats in `.cdata` mostly are) |

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

**Multiplayer (built):** Host applies pelvis impulse on a local ragdoll, then `NetworkSpawn` (poll `RagdollPhysicsInitDelay` for bodies). Remote attacker RPC includes **owner Juggernaut charge bonus** so launch power matches what the client had. See [`SESSION_NOTES_ARCHIVE.md`](SESSION_NOTES_ARCHIVE.md) → Ragdoll (technical).

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

**Future passives / ults (not built):** Speedster reward for dodging a tackle; Sniper sonic boom throw; Juggernaut ground stomp. See bottom of this file for one-line ideas.

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

---

## Future passives / ults (one-liners only)

**Speedster:** Reward for dodging a tackle that would have hit (speed boost / stay at run speed).  
**Sniper:** Dodge during throw charge; ult = ball creates ragdoll zones along path.  
**Juggernaut:** Stronger tackle while at charge (partially in); ult = AOE knockdown stomp.

For exact field names on `ClassData`, see [`NAMING_CANON.md`](NAMING_CANON.md).

---

## Match flow (summary)

Full spec: [`MATCH_FLOW_PLAN.md`](MATCH_FLOW_PLAN.md).

- **Round win:** carrier holds ball in opponent `GoalZone` for ~0.35s.
- **Match win:** first to **5** round wins, or OT golden goal if timer ends tied on round wins.
- **After goal:** 5s celebration (move freely) → reset → 20s intermission (freeze except camera) → resume. Timer **paused** during celebration + intermission.
- **OVERTIME:** tied at 0:00 → reset + 20s intermission (no celebration) → golden-goal `Playing`; clock shows **OVERTIME**.
- **HUD:** score + `M.SS` clock + goal banner + intermission countdown on scene `MatchHud` root (reads `PlayerTeam` sync).
- **Teams:** ids `0`/`1`; display names per map; balance on join.
