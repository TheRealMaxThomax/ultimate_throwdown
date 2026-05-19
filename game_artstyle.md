# Game art style

Blocky models + **AmbientCG** surfaces where they fit + **one custom glass** material. Night lighting. Inkscape for signs and flat details. **Map 1 can be acceptable** — polish maps later when gameplay feels good.

---

## Build with

- **Hammer** — road, buildings, roofs, cone lights  
- **Blender** — pumps, cars, bins, vending (simple shapes; reuse on other maps)

---

## Materials (current approach)

**AmbientCG** — pick whatever reads right per surface (asphalt, brick, pavement, metal panels, etc.). Not limited to a fixed set of four.

| Source | Typical use |
|--------|-------------|
| **AmbientCG** | Roads, walls, brick, pavement, metal-looking surfaces |
| **Custom in s&box** | **Glass** — translucent blue-grey (not on AmbientCG) |
| **Inkscape PNG** | Signs, logos, optional vending shelf |

**Texture size:** **1K–2K** for tiling ground/walls; **4K** OK for **sky HDRI** only. Skip **8K** (slow compiles, little gain).

**Rules of thumb:**
- **Reuse** a material when two areas are close enough — **tint** instead of a new download.
- Don’t add a new texture for every small prop.
- **Night test** in-game — materials that look fine in the editor can read too dark or shiny outside.

**Starting-point mapping** (not strict):

- Road → asphalt  
- Footpath / bins → concrete or paving  
- Station walls → brick or concrete  
- Pumps, poles, car bodies → metal-style AmbientCG  
- Windows / vending front → **custom glass** + dark interior behind  

---

## Signs (station name / logo)

1. Draw in **Inkscape** → PNG  
2. **Flat plane** on the building  
3. Material: PNG + a bit **emissive** so it reads at night  
4. Pole/frame = metal- or concrete-style AmbientCG

---

## Vending machine

**Layers (front → back):**

1. **Glass** — thin blue-grey **transparent** plane in front  
2. **Snacks** — 4–6 thin boxes, flat colours (red, blue, yellow) **or** one Inkscape shelf PNG on a plane  
3. **Back + body** — metal-style material

**Glow:** Emissive on the glass material in s&box **and/or** a small **Hammer light** in front. Test at **night** in-game.

Glass will tint/darken what’s behind it — make snack colours a bit brighter if needed.

---

## Windows (shops + cars)

Shops are **not enterable** — keep insides simple.

| | **Shop window** | **Car window** |
|--|-----------------|----------------|
| Behind glass | Dark plane (or small Inkscape poster) | Nothing or black |
| Glass | Blue-grey transparent (like vending, subtler) | Darker tinted glass |
| Body | Brick / concrete AmbientCG | Metal-style AmbientCG |

Same idea: **glass plane in front** of whatever is behind.

---

## Simple props

- Pump — cylinder + box, metal-style  
- Car — box + wheels, metal-style (tint per car)  
- Bin — box, concrete / paving  
- Lamp post — pole + **cone light** down in Hammer  

Gameplay needs **readable shape and size**, not detail.

---

## Scale reference (game units)

- Player: **72 × 32** default — smallest **62 × 24**, largest **88 × 42**
- Ball grab **45** · dodge sideways **175** · goal box **250 × 500 × 200**
- Field size: **`length ≈ seconds × 350`** at charge (4 s ≈ 1400 units)
- Paths: **~100+** wide for Juggernaut; snap grid **16 / 32 / 64** in Hammer
- Greybox → compile → Play with a real player before art pass

---

## Turf Wars order

1. Road + two forecourts (flat paved ends — Hammer)  
2. Materials + night sky + station lights  
3. Two Inkscape signs  
4. Props (Blender)  
5. Cars later  

**Quick test:** Road + forecourt + lamp + pump + sign + vending corner. Night.

**Quality bar for v1:** reads as petrol station, window, tunnel — not perfect mesh or realism. Upgrade maps later.

---

Maps / multiplayer: `SESSION_NOTES.md` (CRITICAL + Hammer); full map notes in archive if needed
