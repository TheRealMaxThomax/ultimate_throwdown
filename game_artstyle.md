# Game art style

Simple plan for maps like **Turf Wars** (and future maps).

---

## The look

- **Blocky** models (simple shapes — no fancy sculpting).
- **Four shared materials** on almost everything (see below).
- **Night** lighting with lamp cones shining down.
- **Retro filter** on the main game camera.
- **Signs** you draw in Inkscape — that’s where personality comes from.

Not flat cartoon colours. Not realistic high-poly models.

---

## Where you build what

- **Hammer** — road, buildings, roofs, lights.
- **Blender** — pumps, cars, bins, vending machines (basic shapes). Make once, reuse on other maps.

---

## Four materials (use these everywhere)

Make each **once** in the editor. Put every prop in one of these buckets:

1. **Asphalt** — road  
2. **Concrete** — walls, pavement, bins  
3. **Metal** — pumps, poles, cars (or plain dark grey with no texture)  
4. **Plastic / glass** — vending machine front (can glow at night)

**Signs** — separate. Inkscape PNG on a flat board. Not a fifth texture pack.

**Rule:** Don’t download a new texture for every object. Same four looks, many objects.

---

## Where to get textures

**Start here:** [ambientcg.com](https://ambientcg.com) (free, CC0)

- Download **Asphalt** and **Concrete** — size **1K**, file **Color**, format **PNG**.
- Metal: same from AmbientCG **or** skip and use flat grey in the editor.

**Only if metal looks too realistic later:** swap metal for one small texture from itch.io or OpenGameArt (still one metal for the whole game).

**Optional:** open the PNG in GIMP, make it smaller (512) or slightly darker. Not required for v1.

---

## Simple props (good enough)

- Pump = cylinder + box  
- Car = box + wheels (or one free low-poly car, painted with **metal**)  
- Vending machine = box, glowing front  
- Lamp post = pole + **cone light** in Hammer pointing down  

---

## Turf Wars — build in this order

1. Road + two station areas (Hammer)  
2. Four materials + night lights  
3. Two Inkscape signs  
4. Pump, bin, vending (Blender)  
5. Cars / traffic (when the layout is fun)

---

## Quick test (one evening)

Road corner + one lamp + one pump + one sign + retro camera. Play at night.

If it feels good, keep going. If too shiny/real, use flatter metal and fewer bumpy texture maps.

---

## Remember

**Many simple objects. Four materials. Your signs. Night lights. Retro camera.**

More detail (maps, multiplayer): `SESSION_NOTES.md`
