# The Moat

**A RimWorld mod for players who love subterranean fortresses, single chokepoints, and surviving under siege.**

*Created by **Keldarton***  
*Supported RimWorld Versions: **1.5**, **1.6***  
*Requires: **Harmony***

---

## The Vision: A Cozy Mountain to Defend

**The Moat** is designed specifically for players who want to play a subterranean colony, heavily relying on the supreme defensive capabilities of living under a mountain:

- **Single Choke Point:** Force every raid, manhunter pack, and mechanoid swarm to funnel into a single, fortified entrance. Build your bunker, firing lines, turrets, and killboxes exactly where you want them.
- **No Aerial Drop Pod Raids:** The entire mountain half is sheltered under thick overhead mountain (`RoofRockThick`). No surprise drop pods punching through the roof into your hospital, bedrooms, or storage!
- **Concentrated Defenses:** Forget 360-degree perimeter paranoia. Focus 100% of your firepower, walls, and traps on the entrance facing the untamed wild.
- **The Ultimate Siege Fantasy:** Experience the thrill of being besieged inside a cozy, self-sustaining mountain stronghold while danger roams the open world outside.

---

## Map Generation Layout

When you generate your starting colony map, **The Moat** splits the map into two distinct worlds:

```
+-----------------------------------+-----------------------------------+
|                                   |                                   |
|       WEST (LEFT HALF):           |       EAST (RIGHT HALF):          |
|                                   |                                   |
|   - 100% Solid Mountain Rock      |   - Flat Open Plains              |
|   - Overhead Mountain Roof        |   - No Hills, No Lakes            |
|   - NO Caves or Hollow Spots      |   - Farmlands, Trees & Pastures   |
|   - NO Surprise Backdoors         |   - Geysers, Ruins & Monolith     |
|   - Mineable Ore Veins (Config)   |   - Colonists & Pods Arrive Here  |
|                                   |                                   |
|             FORTRESS              |           WILDERNESS              |
+-----------------------------------+-----------------------------------+
                                      ^
                                  Chokepoint
                                   Entrance
```

### 1. West Half (The Mountain Fortress)
- **100% Solid Rock Barrier:** The left 50% of the map is packed solid with natural rock walls.
- **True Overhead Mountain:** Complete `RoofRockThick` coverage preventing all overhead drop-pod insertions.
- **Zero Caves & Hollow Pockets:** Guaranteed no hidden caverns, no insect nest pockets waiting to be uncovered, and no secret rear exits.
- **Pristine Excavation:** Shrines, ancient dangers, steam geysers, ruins, and monoliths are strictly prohibited from spawning inside the mountain, giving you clean rock to carve your halls.
- **Ore Veins (Enabled by Default):** Rich veins of steel, plasteel, gold, components, and jade can still embed themselves into the stone for your miners to unearth.

### 2. East Half (The Untamed Wilderness)
- **Flat Open Land:** Mountains and rocky hills are cleared away, giving you wide-open expanses for agriculture, windmills, solar arrays, and pastures.
- **No Obstructive Lakes:** Swamps and deep lakes are dried into arable soil matching local biome fertility.
- **Full Vanilla Features:** Soil varieties, rich soil, wild trees, grazing animals, steam geysers, ancient dangers, ruins, roads, and the Anomaly Void Monolith spawn cleanly across the plains.
- **Safe Player Landing:** Colonists, cargo pods, and starting gear always touch down safely on the open plains.

### 3. Starting Colony Only
- **The Moat affects ONLY your initial starting colony map.**
- Future satellite colonies, quest locations, ancient complexes, hunting grounds, and campsite maps generate with completely normal vanilla terrain!

---

## Mod Settings

Customizable via **Options &rarr; Mod Settings &rarr; The Moat**:

- **Mountain Width Slider:** Adjust the width of the mountain barrier (from 30% up to 70% of the map width, default: **50%**).
- **Allow Ore Veins in the Mountain:** Toggle whether mineable resources spawn inside the rock wall (default: **ON**).
- **Remove Lakes on Plains:** Converts impassable water bodies into dry, buildable land (default: **ON**).
- **Flatten Hills on Plains:** Removes minor rock formations and hills from the plains (default: **ON**).

---

## Installation

### Via Steam Workshop
1. Subscribe to **The Moat** on the Steam Workshop.
2. In RimWorld, go to **Mods** and enable **Harmony**, then enable **The Moat** (placed after Harmony).
3. Start a **New Game** on any world tile!

### Manual Installation
1. Clone or download this repository.
2. Place the folder into your RimWorld mods directory:
   - Windows: `<Steam>/steamapps/common/RimWorld/Mods/TheMoat`
3. Launch RimWorld, enable **Harmony** and **The Moat** in the Mods menu, and enjoy.

---

## Requirements & Compatibility

- **Harmony** library required.
- Compatible with RimWorld **1.5** and **1.6**.
- Safe to add to existing modlists. Map generation applies whenever you begin a new colony.

---

*“Dig deep, fortify the gate, and let the rimworld break against the stone.”*
