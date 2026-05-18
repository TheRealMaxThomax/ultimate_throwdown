# Game art style

Blocky models + **four AmbientCG materials** + slight **tint** per object. Night lighting. Inkscape for signs and flat details.

---

## Build with

- **Hammer** — road, buildings, roofs, cone lights  
- **Blender** — pumps, cars, bins, vending (simple shapes; reuse on other maps)

---

## Four materials (make once)

1. **Asphalt** — road  
2. **Concrete** — walls, pavement, bins  
3. **Metal** — pumps, poles, car bodies  
4. **Plastic / glass** — vending front, windows (can glow at night)

[ambientcg.com](https://ambientcg.com) — Asphalt + Concrete, **1K**, **Color**, **PNG**. Metal optional.

**Rule:** Same four materials everywhere. No new texture per prop.

**Tints:** Same material, small colour shifts (lighter shop wall, bluer car metal, brighter vending glass). Keep shifts subtle.

---

## Signs (station name / logo)

1. Draw in **Inkscape** → PNG  
2. **Flat plane** on the building  
3. Material: PNG + a bit **emissive** so it reads at night  
4. Pole/frame = **metal** or **concrete**

---

## Vending machine

**Layers (front → back):**

1. **Glass** — thin blue-grey **transparent** plane in front  
2. **Snacks** — 4–6 thin boxes, flat colours (red, blue, yellow) **or** one Inkscape shelf PNG on a plane  
3. **Back + body** — **metal**

**Glow:** Emissive on the glass material in s&box **and/or** a small **Hammer light** in front. Test at **night** in-game.

Glass will tint/darken what’s behind it — make snack colours a bit brighter if needed.

---

## Windows (shops + cars)

Shops are **not enterable** — keep insides simple.

| | **Shop window** | **Car window** |
|--|-----------------|----------------|
| Behind glass | Dark plane (or small Inkscape poster) | Nothing or black |
| Glass | Blue-grey transparent (like vending, subtler) | Darker tinted glass |
| Body | **Concrete** | **Metal** |

Same idea: **glass plane in front** of whatever is behind.

---

## Simple props

- Pump — cylinder + box, **metal**  
- Car — box + wheels, **metal** (tint per car)  
- Bin — box, **concrete**  
- Lamp post — pole + **cone light** down in Hammer  

Gameplay needs **readable shape and size**, not detail.

---

## Turf Wars order

1. Road + two stations (Hammer)  
2. Four materials + night lights  
3. Two Inkscape signs  
4. Props (Blender)  
5. Cars later  

**Quick test:** Road + lamp + pump + sign + vending corner. Night.

---

Maps / multiplayer: `SESSION_NOTES.md`
