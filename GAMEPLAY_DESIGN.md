# Gameplay design

**For:** Planning and tuning — dodge, tackle, classes, future weapons.  
**Not for:** Daily “what do I do today?” → use [`SESSION_NOTES.md`](SESSION_NOTES.md).

---

## Built vs not built

| Feature | Status |
|---------|--------|
| Walk → sprint → charge speed | Built |
| Throw with charge | Built |
| Auto-grab ball | Built |
| Tackle + ragdoll | Built (launch strength still tuning) |
| Dodge (double-tap strafe) | Built |
| Tackle whiff (miss penalty) | **Not built** |
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

## Tackle whiff (future — not in game yet)

**Problem:** Attacker charges in, defender dodges out, attacker stays at charge speed and tries again for free.

**Planned fix:**
- **Outer zone** — existing tackle range
- **Inner zone** — smaller “you’re really on them” distance
- If attacker was **inner + threatening** and then **no tackle lands** (dodge out, bad angle, peel off) → **penalty** (e.g. drop to sprint for a moment)

**Skill idea:** Late dodge when attacker is already close = good for defender. Early dodge while attacker is still far = attacker can adjust.

Details for when we implement → host tracks threaten state; works with existing `PlayerDodge` iframe (iframe = no hit; whiff = miss tax).

---

## Tackle (current rules)

- Must be at **charge speed**
- Host checks hit; clients can **request** a tackle with position/aim info
- Hit launches a **ragdoll copy** of the victim (separate object), not physics on the player prefab
- Victim drops the ball; ball gets knocked away
- Victim stands up after on ground and settled, or after a max time
- Brief invincibility after getting up

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

---

## Future passives / ults (one-liners only)

**Speedster:** Reward for dodging a tackle that would have hit (speed boost / stay at run speed).  
**Sniper:** Dodge during throw charge; ult = ball creates ragdoll zones along path.  
**Juggernaut:** Stronger tackle while at charge (partially in); ult = AOE knockdown stomp.

For exact field names on `ClassData`, see [`NAMING_CANON.md`](NAMING_CANON.md).
