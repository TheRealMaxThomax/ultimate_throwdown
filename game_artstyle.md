# Game art style

**Maps:** Low poly, **flat colours** (Coolors palette below). **Night** scene.  
**Workflow:** Base map in **Hammer** → props from **Blender** (separate files, placed in editor).  
**Seal the map:** Walls / skybox box around the edges — open void causes geometry (map, player, ball) to disappear at some camera angles.

**Props / characters:** Blocky models. Gameplay needs readable shape and size, not detail.

---

## How to build maps

| Tool | What goes there |
|------|-----------------|
| **Hammer** | Road, forecourts, bulk walls, collision, **cone lights** pointing down, map seal box |
| **Blender** | Separate `.blend` files → export as props: vending, store shell, canopy roof/bars, windows, sliding door, trees, grass clumps, footpath tiles, pumps, etc. |

**Modular approach (low poly):** Simple **base structures** in Hammer or Blender (one-off hero meshes). **Decoration kit** you place many times: windows, doors, wood panels, path tiles, grass clumps. Reuse the kit on both sides of the map; **materials and placement** sell new vs old — not two completely different pipelines.

**Grid:** Snap in Hammer on **16 / 32 / 64** so props line up.

**Collision:** Hammer / big props = real collision. Small deco (windows, panels) = usually **no collision**.

---

## Turf Wars layout (Map 1)

One road down the middle. **Two petrol stations** — opposite sides.

| | **New side (clean)** | **Old side (worn)** |
|--|----------------------|---------------------|
| **Feel** | Tidy station, even grass | Neglected, dirt, patchy grass |
| **Walls / canopy** | Eggshell, sharp | Same kit, more grey + mahogany trim |
| **Ground** | Dusty olive grass (even clumps) | **Golden earth** dirt patches + sparse olive grass |
| **Glass / signs** | Cool sky **tint**; vending/sign can use **low emissive** | **Broken neon** — some letters emissive, some **off** (burnt-out tubes) |
| **Lights** | More even warm street pools | Fewer / wider spacing — darker pockets between lamps |

**Road:** Dim grey — neutral lane, both teams readable.

---

## Colour palette (Coolors)

| Name | Hex | Main use |
|------|-----|----------|
| **Eggshell** | `#E0E0CE` | Main light surfaces — walls, canopy soffit, footpath |
| **Dim grey** | `#67697C` | Road, kerbs, metal posts, bins, generic props |
| **Rich mahogany** | `#3D0C11` | Small accents only — trim, door frames, **dark plane behind glass** |
| **Golden earth** | `#9C6615` | Warm accent — wood panels; **dirt on old side** (matte, no emissive) |
| **Dusty olive** | `#688B58` | Grass / trees / planters |
| **Cool sky** | `#35A7FF` | **Accent only** — glass tint, new-side glow; not for huge walls |

**Coverage rule:** ~70% eggshell + grey · ~20% golden earth + olive · ~10% mahogany + cool sky.

### Shades (same colour, lighter / darker)

**Yes** — use **2–3 shades per palette colour** (4 only where you need it, e.g. eggshell walls catching light).  
**Don’t** invent new hues — shift **brightness** only (lighter highlight, darker shadow/underside).

| Colour | Shade ideas |
|--------|-------------|
| **Eggshell** | Base wall · slightly darker soffit · lighter top edge (optional) |
| **Dim grey** (4 shades max) | **Road** base `#67697C` → **curb ~10% darker** → **footpath ~20% lighter** → **forecourt pad ~30% lighter** (last grey step) · metal posts: lighter highlight optional |
| **Eggshell** | **Buildings**, canopy soffit, **petrol price sign**, other clean station surfaces — not road/path/pad grey stack |
| **Golden earth** | Dirt (dull) · wood panel (base) · **bulb** (bright + emissive) — already 3 roles |
| **Dusty olive** | Grass base · darker clump underside · lighter tip (optional) |
| **Mahogany** | Trim · darker recess behind glass |
| **Cool sky** | Glass tint · emissive (brighter) — keep to **2** unless one hero sign needs a third |

**Rules:**  
- If two shades look the same in-game at night, drop one.  
- **Old worn side** uses more **dark** shades; **new side** uses more **light** shades (same palette).  
- Shaders still use the **same vmats** where possible — tint in material or vertex colour, not 20 new materials.

**Golden earth — two roles:**  
- **Dirt** = same hue, **dull / no emissive** (old forecourt patches).  
- **Light bulb** = **brighter** shade of golden earth + **modest emissive** (don’t confuse with dirt).

---

## Materials in s&box (~7 flat vmats)

Reuse these on Hammer brushes and Blender props. Tint in one place; don’t make a new material per object.

| Material | Colour | Emissive? |
|----------|--------|-----------|
| `eggshell` | `#E0E0CE` | No |
| `grey` | `#67697C` | No |
| `mahogany` | `#3D0C11` | No |
| `golden` | `#9C6615` | No (wood / accents) |
| `olive` | `#688B58` | No (grass props) |
| `glass` | `#35A7FF` | Tinted transparent; emissive **off** or very low |
| `emissive_cool` | `#35A7FF` | **Low** — new-side sign / vending glass only |
| `emissive_warm` | Brighter `#9C6615` | **Low** — street lamp **bulb mesh** only |

**Inkscape:** Station logos, neon letters → PNG on flat planes.

---

## Night lighting

### Scene vs map (don’t double up)

- Each Hammer map has its own **`light_environment`** + **`env_sky`**.
- **Disable or delete** the scene **Directional Light** in `throwdown_prototype.scene` when using map instances — two suns keep the level bright and fight your night tune.
- Darkness lives on the **map**, not the scene.

### `light_environment` (Hammer)

| Setting | Night starting point |
|---------|----------------------|
| **Brightness** | `0.02`–`0.1` (directional moon/sun — shadows change most here) |
| **Sky intensity** | `0.2`–`0.3` |
| **Sky ambient bounce colour** | **Very dark** — `0 0 0` or `10 12 18` (this is **RGB**, not a separate slider; ~`147 147 147` keeps the map daytime-bright) |
| **Sky color** | Dim cool grey-blue, not `255 255 255` |
| **Ambient color** | `0 0 0` or very dark blue-grey |
| **Lower hemisphere is black** | On |
| **Sun light min brightness** | `0` if present (high values act like a brightness floor) |

**Order:** cones → dark bounce **colour** → sky intensity → brightness. **Compile → Play** after changes (editor preview lies).

If still too bright: disable **light probe / combined probe volume** once to test, then rebake; check scene isn’t adding a **Directional Light** again.

### Street cones + emissive

**Main read = Hammer cone lights** pointing down (warm tint, golden-earth family). Use many on paths and forecourts.

**Street lamp prop:**  
- Pole / housing → grey or eggshell (no emissive)  
- Small **bulb mesh** → brighter golden earth + **low emissive**  
- **Cone entity** above does the real ground lighting — bulb emissive is for silhouette, not lighting the whole map  

**Emissive budget (low poly):** Glow is the **exception**. Priority order:

1. Cone lights (scene)  
2. Warm bulb emissive (repeated, subtle)  
3. **One** broken neon sign on old side (some letters on, some off)  
4. New-side vending / sign glass (cool sky, low)  
5. Everything else → flat colour only  

If everything glows, nothing reads.

**Test at night in-game** after every lighting pass — editor preview lies.

---

## Glass & vending

**Not enterable shops** — keep behind glass simple.

| Layer (front → back) | New side | Old side |
|----------------------|----------|----------|
| Glass plane | Cool sky tint (`glass`) | Same or less saturation |
| Behind glass | Mahogany or grey plane | Darker, maybe faded poster |
| Glow | Low `emissive_cool` + optional small Hammer light | Usually **no** vending glow |

**Vending:** Glass plane → snack boxes (flat colours) or Inkscape shelf → body (grey / golden / mahogany).

---

## Signs & neon (Inkscape + self-illum mask)

**One plane, one material** — colour texture + **self-illum mask** (standard approach).

### 1. Colour PNG (what players see)

- Draw the full sign in **Inkscape** (flat colours from palette).  
- Export PNG — same pixel size you’ll use on the in-game plane.

### 2. Mask PNG (what glows)

- **Duplicate** the sign art.  
- **White `#FFFFFF`** = emissive (lit letters, logo glow, border tubes).  
- **Black `#000000`** = no emissive (dead letters, backing, mounting).  
- Export second PNG — **same width/height** as the colour PNG (alignment matters).

**Broken neon (old side):** paint only the “working” letters white in the mask; dead letters stay black. The colour PNG still shows the full word — mask controls glow only.

### 3. In s&box material editor

- **Colour / albedo** → colour PNG  
- **Self illum mask** → mask PNG  
- **Emissive tint** → cool sky or warm golden earth; keep strength **low** — mask does the shape  
- Plane in Hammer / on prop — non-emissive frame = mahogany or grey mesh around it

### Tips

- No grey in the mask — only **pure white or black** (soft greys = muddy glow).  
- If glow bleeds, shrink white areas 1–2 px in Inkscape.  
- **Alternative:** separate planes per letter (more work; use only if mask is awkward).

New side: same workflow; more letters white in the mask. Old side: fewer white islands = broken sign read.

One hero neon per old side is enough.

---

## Blender prop list (Turf Wars)

Export each as its own prop file; place in Hammer:

- Vending machine  
- Petrol station **store shell** (shape only)  
- Canopy roof + canopy bars  
- Windows, sliding door  
- Trees, grass clumps  
- Footpath square tiles  
- Pumps, bins, cars (later)  

---

## Scale reference (game units)

- Player: **72 × 32** default — smallest **62 × 24**, largest **88 × 42**
- Ball grab **45** · dodge sideways **175** · goal box **250 × 500 × 200**
- Field size: **`length ≈ seconds × 350`** at charge (4 s ≈ 1400 units)
- Paths: **~100+** wide for Juggernaut
- Greybox → compile → Play with a real player before art pass

---

## Turf Wars build order

1. Hammer — road, two forecourts, seal walls, greybox lights  
2. Flat vmats from palette  
3. Blender hero pieces — canopy, store shell  
4. Blender kit — windows, door, path tiles, grass, trees  
5. Place kit on both sides; dress **new vs old** (materials + grass/dirt)  
6. Street lamps + cone lights; tune warm pools  
7. Signs — new clean + old broken neon  
8. Vending, pumps, cars when core read works  

**Quick test:** Road + both forecourts + one lamp + one station corner + night Play.  
**Quality bar:** Reads as two petrol stations across a road at night — not perfect mesh.

---

## Old notes (optional textures)

You can still drop in **1K AmbientCG** on a surface if one area needs it, but **default is flat palette colours**. Skip 8K. Reuse materials; tint instead of downloading more.

---

Multiplayer / map load: `SESSION_NOTES.md`
