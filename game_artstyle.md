# Game art style

**Overall:** Low poly, **flat colours** (palette below). **Night** scene. Blocky readable shapes — gameplay silhouette over detail.

**Props / characters:** Blocky models. Shape and size matter more than surface detail. Reuse kit pieces; materials and placement sell variety.

**Maps:** One `.scene` per playable map. Base geo in **scene Mapping mode (M)** → props from **Blender** (`.vmdl`) → **clutter** for grass scatter. Seal maps with walls/skybox box or geometry disappears at camera angles.

**Textures:** Default is **flat palette colours**. Drop in a 1K texture only where one area genuinely needs it. Skip 8K. Reuse vmats; tint instead of new materials.

---

## UI & gameplay colour system

Two separate layers — class identity (world VFX, previews, wind-up) and ult meta (UI payoff). Intentionally distinct.

### Layer 1 — Class identity (world space)

| Class | Colour | Hex | Used on |
|-------|--------|-----|---------|
| **Speedster** | Electric blue | `#24b0ff` | Aim preview corridor, wind-up sparks, body glow |
| **Juggernaut** | Coral orange | `#FA824C` | Quake Slam aim preview rings, wind-up dust/VFX |
| **Sniper** | Purple | `#9E66D3` | Path zone preview, wind-up VFX |

Class colours tell you **which threat is aiming at you** before it fires. Speedster being blue means Speedster *is* a blue class — not that ults are blue.

**Night readability:** if class colours read muddy against the dark scene, make the preview material emissive (`F_SELF_ILLUM` + `g_flSelfIllumBrightness` at runtime). Both current preview materials already support it — it's a tuning knob, not a colour redesign.

### Layer 2 — Ult meta (UI / payoff)

| Moment | Colour | Rationale |
|--------|--------|-----------|
| **Comic burst on ult knockdown** (`ComicBurstPalette.Ult`) | **White** | "Panel blowout" — beyond class colour, beyond tackle tiers. Manga grammar. |
| **Charge HUD at 100% ready** | **White** flash → hold | Same "beyond normal" read; pending HUD update |

White is the **ult tier marker** — belongs to no class. When a burst is white, the read is "super move landed."

### Tackle comic tiers (reference)

Yellow → Orange → Red/Chaos = normal tackle escalation. **White = above all of these.** No overlap.

### OOB markers

OOB drop ring is also white — different shape (ground circle, spatial warning), different timing, rarely simultaneous with ult comic. Not a conflict.

### Sniper colour (TBD)

Avoid blue (Speedster), orange/red (Juggernaut + tackle tiers), yellow (tackle low). **Purple** is the leading candidate — reads as precision/technical, distinct from everything else.

---

## Colour palette approach

Each map gets its own dedicated palette (use [Coolors](https://coolors.co) to build it). The palette is locked per map — don't invent new hues mid-build.

**Rules (apply to every map):**
- Pick **5–6 base colours** per map. Define a dominant (walls/ground), secondary (props/accents), and 1–2 pop colours (signs, glass, emissive).
- **Max 3–4 shades per colour** — brightness shift only, no new hues. If two shades look the same in Play at night, drop one.
- **Coverage rule:** ~70% dominant + neutral · ~20% secondary · ~10% accent/pop. Stick to it or the map reads noisy.
- Reuse the same vmats across the map — tint in one place, not 20 new materials.
- vmat naming: `{palettename}_{amount}{lighter|darker}` — base colour has no suffix (e.g. `eggshell`, `eggshell_40darker`).

Map-specific palettes live with their map notes, not here.

---

## Emissive philosophy

Glow is the **exception**, not the default. Low poly reads best when most surfaces are flat and emissive is reserved for hero moments.

Priority order for any map:
1. Spot/point lights for pools and atmosphere
2. Warm bulb emissive (subtle, repeated)
3. One hero neon sign per area if the map calls for it
4. Everything else → flat colour only

---

## Scale reference (game units)

- **Player:** 72 tall × 32 wide (capsule radius 16). Blender reference block: 32 × 32 × 72. Range: smallest 62 × 24, largest 88 × 42
- **Step up height:** `Move Mode Walk → Step Up Height` — global 24–32 for 16-unit geo
- **Ball grab:** 45 · **dodge sideways:** 175 · **goal box:** 250 × 500 × 200
- **Field size:** `length ≈ seconds × 350` at charge (4 s ≈ 1400 units)
- **Paths:** ~100+ wide for Juggernaut
- Greybox → compile → Play with a real player before art pass

---

## Blender (props)

**Scale:** 1 Blender unit = 1 s&box unit (player height 72).

| Setting | Value |
|---------|-------|
| Scene → Units → Unit System | **None** |
| Unit Scale | `1` |
| Snap | On — Increment + Grid |
| Scale X/Y/Z | Locked to `1, 1, 1` |
| Edit Mode → Overlays | Edge Length on |

**Habits:** Size in Edit Mode (S → number → Enter). Ctrl+A → All Transforms before export. Export Selected Objects only.

**FBX export preset (static props):** Selected Objects · Forward **-Z** · Up **Y** · Apply Scalings **FBX All** · Binary. Save as operator preset (`S&box Static Prop`).

**ModelDoc:** Static Prop archetype · collision Physics Hull From Render · materials on mesh slots. Place one `.vmdl` many times — don't merge kit pieces into one mesh.

---

## Signs & neon (Inkscape + self-illum mask)

**One plane, one material** — colour texture + self-illum mask.

### 1. Colour PNG (what players see)
- Draw full sign in **Inkscape** using flat palette colours.
- Export PNG at the size you'll use on the in-game plane.

### 2. Mask PNG (what glows)
- Duplicate the sign art.
- **White `#FFFFFF`** = emissive (lit letters, logo glow, border tubes).
- **Black `#000000`** = no emissive (dead letters, backing, mounting).
- Export at **same width/height** as colour PNG (alignment matters).

**Broken neon:** paint only the working letters white in the mask; dead letters stay black. Colour PNG still shows the full word.

### 3. In s&box material editor
- Colour / albedo → colour PNG
- Self illum mask → mask PNG
- Emissive tint → cool sky or warm golden earth; keep strength **low**
- Non-emissive frame = mahogany or grey mesh around the plane

**Tips:**
- No grey in the mask — pure white or black only (soft greys = muddy glow).
- If glow bleeds, shrink white areas 1–2 px in Inkscape.
- Alternative: separate planes per letter (more work; use only if mask is awkward).
