# Game art style

**Maps:** Low poly, **flat colours** (Coolors palette below). **Night** scene.  
**Workflow:** Base geo in **scene Mapping mode** (**M**) → props from **Blender** (`.vmdl`, drag into scene) → **clutter** for grass scatter. Hammer `.vmap` is legacy; gameplay lives in **`.scene`** files.  
**Seal the map:** Walls / skybox box around the edges — open void causes geometry (map, player, ball) to disappear at some camera angles.

**Props / characters:** Blocky models. Gameplay needs readable shape and size, not detail.

---

## How to build maps

| Tool | What goes there |
|------|-----------------|
| **Scene editor (Mapping M)** | Road, forecourts, seal walls, kerbs — brushes/meshes with **mesh collision** |
| **Scene editor (place models)** | Drag `.vmdl` from Asset Browser — path tiles, trees, pumps, station kit |
| **Clutter** | `.clutter` palettes (e.g. `Assets/Clutter/turfwarspoly/grass.clutter`) — paint grass on collidable ground |
| **Blender** | Separate `.blend` → export `.vmdl` — hero + kit pieces listed below |

**Modular approach (low poly):** Simple **base structures** in Mapping mode or Blender (one-off hero meshes). **Decoration kit** you place many times: windows, doors, path tiles, grass clumps. Reuse the kit on both sides; **materials and placement** sell new vs old.

**Grid:** Snap on **16 / 32 / 64** (Mapping mode or prop placement).

**Collision:** Mapping meshes / big props = real collision. Small deco = usually **no collision**.

### Clutter performance (grass)

Clutter is **instanced**, but heavy paint still costs FPS. Prefer **less coverage + lower density** over one huge fill.

| Lever | Starting point (`grass.clutter`) | If FPS drops |
|--------|----------------------------------|--------------|
| **Density** (scatterer) | **10** | Try **4–6**; thin with brush **Opacity** while painting |
| **Tile Radius** | **4** | **2–3** — fewer tiles around camera |
| **Tile Size** | **512** | **1024** — coarser streaming, fewer tiles |
| **Painted area** | Forecourts / verges only | **Erase** under roads, goals, main paths — no grass where players fight |
| **Model** | `grass.vmdl` | Simpler mesh or second entry (`grass_group1` / `grass_group2`) at lower weight |

**Hybrid (often best read):** **Clutter** for soft verges; **hand-placed** `.vmdl` clumps at station corners (10–20 copies) where you want guaranteed detail.

**Mode:** **Volume** + fixed bounds = instances saved in scene (predictable cost). **Infinite** streams with camera — fine for open ground, watch **Tile Radius**.

Judge in **Play** with ball + two players in mind, not editor flycam over the whole map.

**Scene hierarchy (stay sane):** Folder empties e.g. `_MAP_STATIC` (road, seal), `_PROPS` (by station side), `_LIGHTING` (sun, env probe, spots), `_CLUTTER`. Rename blocks when created — avoid dozens of `Block (N)` at root.

**Multiple maps later:** One **`.scene` per playable map** (e.g. `throwdown_turf_wars.scene`) with its own goals/spawns/lighting; shared game code. Set **Startup Scene** in `.sbproj` for which map to Play.

---

## Blender (props)

**Scale:** **1 Blender unit = 1 s&box / Hammer unit** (player height **72**). Type the same numbers you’d use in Hammer.

| Setting | Value |
|--------|--------|
| **Scene → Units → Unit System** | **None** |
| **Unit Scale** | `1` |
| **Snap** | On — **Increment** + **Grid** (snapping panel near magnet) |
| **Overlay → Grid Floor → Scale** | `1` |
| **Overlay → Subdivisions** | `16` (matches Hammer **16 / 32 / 64**; set snap increment to **16**, **32**, or **64** in **Edit → Preferences → Editing** when laying out big pieces) |
| **Scale X / Y / Z** | **Locked** to `1, 1, 1` (Transform locks in **N → Item**) |
| **Edit Mode → Overlays** | **Edge Length** on (size labels on mesh; **Dimensions** in **N → Item** is the real export size) |

**Workflow habits:** Size meshes in **Edit Mode** (**S** → number → Enter). **Ctrl+A → All Transforms** before export. Hide blockers (e.g. player reference) with **H** or Outliner **eye** — export **Selected Objects** only.

**Player reference block (optional):** **32 × 32 × 72** — compare props to human scale; don’t export.

**FBX export preset (static props):** Selected Objects · Forward **-Z** · Up **Y** · Apply Scalings **FBX All** · Binary. Save operator preset (e.g. `S&box Static Prop`).

**ModelDoc:** **Static Prop** archetype · collision **Physics Hull From Render** (or **Physics Shape Box**) · materials on mesh slots. **Place one `.vmdl` many times** in the scene — don’t merge 20 tiles into one mesh unless a strip length is locked.

**Viewport:** If geometry vanishes when zoomed out, **N → View → Clip End** → `10000` or higher.

---

## Turf Wars layout (Map 1)

One road down the middle. **Two petrol stations** — opposite sides.

| | **New side (clean)** | **Old side (worn)** |
|--|----------------------|---------------------|
| **Feel** | Tidy station, even grass | Neglected, dirt, patchy grass |
| **Walls / canopy** | Eggshell, sharp | Same kit, more grey + mahogany trim |
| **Footpaths** | **Lighter dim grey** (grey ladder — ~20% lighter than road) · modular **path tiles** placed in scene | Same tile kit; can read more worn via placement / edge grime later |
| **Ground** | Dusty olive grass (even clumps) | **Golden earth** dirt patches + sparse olive grass |
| **Road centre lines** | `eggshell` — full-width, even | `eggshell_40darker` — faded; `eggshell_50darker` on thin / broken scraps |
| **Glass / signs** | Cool sky **tint**; vending/sign can use **low emissive** | **Broken neon** — some letters emissive, some **off** (burnt-out tubes) |
| **Lights** | More even warm street pools | Fewer / wider spacing — darker pockets between lamps |

**Road surface:** **Dim grey** `#67697C` — neutral lane, both teams readable. Lines are separate geometry (brushes or thin props), not painted on the asphalt vmat alone.

**Road line geometry:** New = full stripes. Old = thinner meshes, gaps, cut-off ends — material darkness does the rest.

---

## Colour palette (Coolors)

| Name | Hex | Main use |
|------|-----|----------|
| **Eggshell** | `#E0E0CE` | Walls, canopy soffit, **new road lines**; darker eggshell **shades** for old lines |
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
| **Eggshell** | Base wall · slightly darker soffit · **new road lines** (base) · **old lines ~40% darker** · **thin old lines ~50% darker** (~`#86867C` / `#707067` ballpark — tune at night in Play) |
| **Dim grey** (4 shades max) | **Road** base `#67697C` → **curb ~10% darker** → **footpath ~20% lighter** → **forecourt pad ~30% lighter** (last grey step) · metal posts: lighter highlight optional |
| **Eggshell (surfaces)** | **Buildings**, canopy soffit, **petrol price sign** — not footpath (use grey ladder) |
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

## Materials in s&box (flat vmats)

Reuse these on mapping meshes and Blender props. Tint in one place; don’t make a new material per object.

### vmat naming

**Pattern:** `{palette}_{amount}{lighter|darker}` — base colour has **no suffix**.

| Example | Meaning |
|---------|---------|
| `eggshell` | Base `#E0E0CE` |
| `eggshell_40darker` | ~40% darker than base |
| `eggshell_50darker` | ~50% darker than base |
| `eggshell_30lighter` | ~30% lighter than base (example — soffit / highlight) |

Same idea for other palette vmats when needed (e.g. `grey_20lighter` for footpaths). **Don’t** use `old_40` / `old_50` in filenames — use **lighter** / **darker** + percent.

### Turf Wars vmats (core)

| Material | Colour | Emissive? |
|----------|--------|-----------|
| `eggshell` | `#E0E0CE` | No (walls, new road lines) |
| `eggshell_40darker` | ~40% darker than eggshell | No (old road lines — faded) |
| `eggshell_50darker` | ~50% darker than eggshell | No (old thin / broken line scraps) |
| `grey` | `#67697C` | No |
| `mahogany` | `#3D0C11` | No |
| `golden` | `#9C6615` | No (wood / accents) |
| `olive` | `#688B58` | No (grass props) |
| `glass` | `#35A7FF` | Tinted transparent; emissive **off** or very low |
| `emissive_cool` | `#35A7FF` | **Low** — new-side sign / vending glass only |
| `emissive_warm` | Brighter `#9C6615` | **Low** — street lamp **bulb mesh** only |

**Inkscape:** Station logos, neon letters → PNG on flat planes.

---

## Night lighting (scene)

Lighting is authored in the **same `.scene`** as the map geo (e.g. `throwdown_turf_wars.scene`). **Play** is the truth — editor viewport often lies.

### Stack (what each piece does)

| Piece | Role |
|-------|------|
| **Ambient Light** | Overall darkness — **use this** for base fill (replaces deprecated **`Directional Light` → Sky Color**) |
| **Envmap Probe** | Material “glue” / indirect read — large bounds over play area; darken **Tint** if the level washes out |
| **Directional Light** | Dim **Light Color** for moon angle + shadows — **not** the main darkness knob |
| **Spot / Point lights** | Street pools (warm, pointing down) — **Fog Mode enabled** on spots |
| **Volumetric fog** (camera) | Light **beams in air** / hazy cones — needs fog in the world, not just emissive |
| **Fog volume** (optional) | Local mist along road / forecourts — beams read strongest inside or near the volume |
| **Bloom** (camera + bulb mesh) | Soft **bulb halo** — complements volumetrics; not a substitute for fog |
| **Sky Box 2D** | Horizon look — dark **Tint**, night material |

**Tune order:** **Ambient** (dark enough?) → **Env probe** (materials look right?) → **spots** along road/forecourts → **directional** last (shadow direction/strength).

If too bright: lower **Ambient Light** color first; temporarily disable **Envmap Probe** to test, then re-enable with darker tint.

### Street lamps + emissive

**Main read = spot lights** above paths (warm golden-earth family), not emissive on every surface.

**Street lamp prop:**  
- Pole / housing → grey or eggshell (no emissive)  
- Small **bulb mesh** → subtle emissive + **Bloom Layer** on renderer if camera bloom is on  
- **Spot light** above → ground pool; **Fog Mode on** so it scatters in volumetric fog  
- **Broken pole** → `streetlight_broken.vmdl` (no spot, non-emissive bulb). **Unstable lamp** → parent empty + **`StreetLightFlicker`** (child model + child spot); off bulb uses `goldenearth_streetlight_off.vmat` on the **`light.vmat`** slot only  

**Beams through air (“foggy lamp” look):** **Main Camera → Volumetric Fog** (costs ~1–2 ms GPU). Spots alone + emissive = pool + bright bulb, not shafts. Tune in **Play**; optional **fog volumes** along the road for pockets of mist.

**Emissive budget (low poly):** Glow is the **exception**. Priority order:

1. Spot lights along road / forecourts  
2. Warm bulb emissive (subtle, repeated)  
3. **One** broken neon sign on old side  
4. New-side vending / sign glass (cool, low)  
5. Everything else → flat colour only  

**Test at night in Play** after every pass.

### Legacy Hammer note

Old Turf Wars Hammer passes used **`light_environment`** + cone entities. Same *ideas* (dark ambient, warm cones, low emissive) — different components in scene. Do not also load a Hammer map via **`MapInstance`** unless you want double lighting.

---

## Glass & vending

**Not enterable shops** — keep behind glass simple.

| Layer (front → back) | New side | Old side |
|----------------------|----------|----------|
| Glass plane | Cool sky tint (`glass`) | Same or less saturation |
| Behind glass | Mahogany or grey plane | Darker, maybe faded poster |
| Glow | Low `emissive_cool` + optional small spot light | Usually **no** vending glow |

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
- Plane on prop or mapping mesh — non-emissive frame = mahogany or grey mesh around it

### Tips

- No grey in the mask — only **pure white or black** (soft greys = muddy glow).  
- If glow bleeds, shrink white areas 1–2 px in Inkscape.  
- **Alternative:** separate planes per letter (more work; use only if mask is awkward).

New side: same workflow; more letters white in the mask. Old side: fewer white islands = broken sign read.

One hero neon per old side is enough.

---

## Blender prop list (Turf Wars)

Export each as its own prop file; **drag into scene** (or clutter for grass):

- Vending machine  
- Petrol station **store shell** (shape only)  
- Canopy roof + canopy bars  
- Windows, sliding door  
- Trees, grass clumps (or **clutter** from `grass.clutter`)  
- Footpath square tiles (e.g. **120 × 120 × 2** — one `.vmdl`, duplicate; snap **16 / 32 / 64**)  
- Pumps, bins, cars (later)  

---

## Materials on mapping meshes

In **Mapping mode** (**M**): **Texture tool** (**4**) applies `.vmat` to faces (road, forecourt pads, seal walls). **Placed `.vmdl` props** get materials from **ModelDoc** / Blender export, not per-face in the mapper.

---

## Scale reference (game units)

- Player: **72 tall × 32 wide** (capsule radius **16**) — reference block **32 × 32 × 72** in Blender; smallest **62 × 24**, largest **88 × 42**
- **Curbs / path lips:** **`Move Mode Walk` → Step Up Height** on **Player** template — global, **24–32** for 16-unit geo (was **10**). All joins clone the template.
- Ball grab **45** · dodge sideways **175** · goal box **250 × 500 × 200**
- Field size: **`length ≈ seconds × 350`** at charge (4 s ≈ 1400 units)
- Paths: **~100+** wide for Juggernaut
- Greybox → compile → Play with a real player before art pass

---

## Turf Wars build order

1. Scene — road, two forecourts, seal walls (Mapping **M**)  
2. Flat vmats on meshes (incl. `eggshell_40darker` / `eggshell_50darker`)  
3. Night lighting — Ambient + Env probe + spot pools (see above)  
4. Blender hero pieces — canopy, store shell  
5. Blender kit — windows, door, path tiles; **clutter** / place grass, trees  
6. Dress **new vs old** on both sides — **in progress:** footpaths, road lines, worn old lines  
7. Street lamp meshes + spot tune  
8. Signs — new clean + old broken neon  
9. Vending, pumps, cars when core read works  

**Quick test:** Road + both forecourts + one lamp + one station corner + night Play.  
**Quality bar:** Reads as two petrol stations across a road at night — not perfect mesh.

---

## Old notes (optional textures)

You can still drop in **1K AmbientCG** on a surface if one area needs it, but **default is flat palette colours**. Skip 8K. Reuse materials; tint instead of downloading more.

---

Multiplayer / map load: `SESSION_NOTES.md`
