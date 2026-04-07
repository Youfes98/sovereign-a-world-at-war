# Unity Port Architecture — WarStrategyGame

## Source Codebase Summary
- ~10,600 lines GDScript across 40 files
- 11 autoloads (GameClock, GameState, UIManager, WorldMemoryDB, ProvinceDB, EconomySystem, BuildingSystem, MilitarySystem, AISystem, SaveSystem, GameTheme)
- 23 UI panels, 1 custom shader (250 lines GLSL)
- 66MB data/assets (JSON, PNG, shaders)
- Reviewed and fixed across 5 review passes (50+ fixes)

## Godot Autoload Registration Order (Matters for Init)
1. GameClock — tick driver, must be first
2. GameState — data authority, second
3. UIManager — panel lifecycle
4. WorldMemoryDB — diplomatic memory
5. ProvinceDB — map data loader
6. EconomySystem — monthly economy sim
7. BuildingSystem — construction queues
8. MilitarySystem — units, combat, pathfinding
9. AISystem — AI decision loop
10. SaveSystem — JSON persistence
11. GameTheme — UI styling constants

## Hard Rules (Non-Negotiable)

### 1. Explicit System Update Order — No Event-Based Tick Chains
The Godot version uses signal chains: `GameClock.tick_month` → subscribers fire in subscription order. This is fragile.

**Unity rule:** Use a `SimulationManager` MonoBehaviour that calls systems in explicit order:
```
SimulationManager.TickMonth() {
    EconomySystem.Tick(date);
    BuildingSystem.Tick(date);
    MilitarySystem.TickRecruitment(date);
    AISystem.Tick(date);
    // UI refresh happens via events AFTER all simulation is done
}
```
Systems do NOT subscribe to clock events directly. Only `SimulationManager` subscribes to the clock.

### 2. NativeArray for Territory Ownership from Day One
`territory_owner` is the hottest data structure — read by 6+ systems every tick.

```csharp
// NOT this:
Dictionary<string, string> territoryOwner;

// THIS from day one:
public NativeArray<int> TerritoryOwner; // indexed by province ID (int)
public NativeArray<int> ProvinceToCountry; // default ownership
```
This makes Burst-compiled pathfinding and AI evaluation possible without refactoring.

### 3. UI Data Binding — No Destroy-Rebuild
The Godot version destroys and recreates UI nodes on every data change. This caused:
- GC pressure from hundreds of throwaway nodes
- Visual glitches (briefly doubled labels)
- The single biggest source of UI bugs

**Unity rule:** Use UI Toolkit with data binding. Panels bind to data models. When data changes, labels update text — no node creation/destruction.

### 4. Typed Data Classes — No Dictionary<string, object>
The Godot version uses untyped dictionaries everywhere (`data.get("treasury", 0.0)`). This caused:
- Dict dot-access crashes (MilitaryPanel)
- Silent type coercion bugs (JSON float→int)
- No compile-time safety

**Unity rule:** Define proper C# data classes:
```csharp
public class CountryData {
    public string Iso;
    public string Name;
    public float GdpRawBillions;
    public float Treasury;
    public float Stability;
    public float DebtToGdp;
    public float CreditRating;
    public float Infrastructure;
    public float TaxRate;
    public float TaxMin, TaxMax;
    public float BudgetMilitary, BudgetInfrastructure, BudgetResearch;
    public int Population;
    public string PowerTier;
    public string GovernmentType;
    // ... all fields typed, no .get() calls
}
```

---

## Architecture Layers

### Layer 1 — Data & Services (Port First)

| Godot Autoload | Unity Equivalent | Pattern |
|---|---|---|
| GameState.gd (176 lines) | GameStateService.cs | Singleton + C# events |
| GameClock.gd (111 lines) | GameClockService.cs | MonoBehaviour, drives SimulationManager |
| ProvinceDB.gd (319 lines) | ProvinceDatabase.cs | ScriptableObject, O(1) pixel lookup |
| WorldMemoryDB.gd (122 lines) | WorldMemoryService.cs | Pure C# class |
| SaveSystem.gd (250 lines) | SaveService.cs | JSON → Application.persistentDataPath |
| GameTheme.gd (550 lines) | ThemeConfig.cs | ScriptableObject + USS stylesheet |
| UIManager.gd (86 lines) | UIService.cs | Panel state management |

**Service Locator (not DI):**
```csharp
public static class Services {
    public static GameStateService GameState { get; private set; }
    public static GameClockService Clock { get; private set; }
    public static ProvinceDatabase ProvinceDB { get; private set; }
    // registered at startup by Bootstrap.cs
}
```

### Layer 2 — Simulation Systems (Port Second)

| System | Lines | DOTS Plan |
|---|---|---|
| EconomySystem (321) | IJobEntity — monthly tick across 195 countries |
| MilitarySystem (1,208) | Burst pathfinding, NativeArray supply cache |
| AISystem (540) | Parallel jobs — 194 independent country evaluations |
| BuildingSystem (459) | MonoBehaviour, queue management is simple |

**Start MonoBehaviour, migrate hot paths to Jobs+Burst after profiling.**

### Layer 3 — Map Rendering (Port Third)

| Component | Unity Approach |
|---|---|
| map.gdshader (250 lines) | Custom HLSL shader on fullscreen quad |
| MapRenderer (590) | Single quad, 8-texture pipeline unchanged |
| MapCamera (62) | Orthographic + Cinemachine or custom smooth |
| BorderLayer (185) | GPU LineRenderer or shader-based borders |
| LabelLayer (182) | World-space TextMeshPro with overlap rejection |
| UnitOverlay (387) | Screen-space overlay, GPU instancing for icons |

### Layer 4 — UI (Port Last)

**UI Toolkit** with USS styling matching GameTheme palette.

23 panels total. Key panels:
- TopBar, SidebarManager, ResourceBar — persistent HUD
- EconomyPanel, MilitaryPanel, BuildPanel — core gameplay
- CountryCard, DiplomacyPanel, ProvinceInfoPanel — context popups
- CountryPicker, PauseMenu — modal overlays

---

## Port Order

| Phase | What | Est. Effort |
|---|---|---|
| 1. Foundation | Project setup, Service Locator, data models, GameState, GameClock, JSON loading, ProvinceDB | 1 week |
| 2. Map | HLSL shader, MapRenderer, camera, province click, wrapping | 1 week |
| 3. Simulation | Economy, Military (pathfinding, combat, recruitment), Buildings, AI | 2 weeks |
| 4. Core UI | TopBar, Sidebar, CountryCard, Picker, province info | 1 week |
| 5. Full UI | All 23 panels, notifications, rankings, diplomacy | 1.5 weeks |
| 6. Save/Load | SaveSystem, WorldMemoryDB persistence | 2-3 days |
| 7. Polish | Animations, camera, borders, labels, unit overlay | 1 week |
| 8. DOTS | Profile → migrate hot paths to Jobs+Burst | 1-2 weeks |

---

## Signal → Event Migration Map

### Godot Signals (document ALL for port reference)

**GameClock signals:**
- tick_hour(date) → C# event Action<DateData>
- tick_day(date) → SimulationManager calls MilitarySystem.TickDay()
- tick_week(date) → currently only WorldMemoryDB (no subscribers in practice)
- tick_month(date) → SimulationManager explicit ordering (see Rule 1)
- tick_year(date) → GameState.RecalculatePowerTiers(), WorldMemoryDB.DecayMemories()
- speed_changed(speed) → C# event, UI only
- pause_changed(paused) → C# event, UI only

**GameState signals:**
- country_selected(iso) → C# event, UI panels subscribe
- country_deselected() → C# event, UI panels
- country_data_changed(iso) → C# event, UI refresh (NOT simulation)
- player_country_set(iso) → C# event, initialization chain
- war_state_changed(a, b, at_war) → C# event
- territory_changed(id, old, new) → C# event, map + UI

**MilitarySystem signals:**
- units_changed() → C# event, UnitOverlay + UI
- territory_selected(iso) → C# event, UI
- selection_changed() → C# event, UI
- battle_resolved(territory, attacker, defender, won) → C# event, UI + map
- recruitment_queued/completed/cancelled → C# events, UI

**BuildingSystem signals:**
- building_completed(province, type) → C# event
- construction_started/cancelled/updated → C# events, UI

---

## Godot Scene Hierarchy → Unity GameObject Structure

```
Main (Node)                          → GameManager (GameObject)
├── Map (Node2D)                     → MapRoot (GameObject)
│   ├── MapRenderer (Sprite2D x3)   → MapQuad (MeshRenderer + HLSL shader)
│   ├── BorderLayer (CanvasItem)     → BorderRenderer (LineRenderer or shader)
│   ├── LabelLayer (CanvasItem)      → LabelRoot (TextMeshPro instances)
│   └── MapCamera (Camera2D)         → Main Camera (Orthographic)
└── HUD (CanvasLayer)                → UI Document (UI Toolkit)
    ├── UnitOverlay (Control)        → UnitOverlayUI (screen-space, GPU instanced)
    ├── TopBar                       → TopBar.uxml
    ├── SidebarManager               → Sidebar.uxml (dynamic panel host)
    ├── CountryCard                  → CountryCard.uxml
    ├── NotificationFeed             → NotificationFeed.uxml
    ├── ProvinceInfoPanel            → ProvinceInfo.uxml
    ├── RankingsPanel                → Rankings.uxml
    ├── BuildPanel                   → BuildPanel.uxml
    ├── PauseMenu                    → PauseMenu.uxml
    └── CountryPicker                → CountryPicker.uxml
```

## C# Data Models (Replace Untyped Dictionaries)

```csharp
// Core data classes — every field that was a dict key becomes a typed property
public class CountryData {
    public string Iso, Iso2, Name, Capital, Region, Subregion;
    public string GovernmentType, PowerTier;
    public float GdpRawBillions, GdpNormalized;
    public float Treasury, Stability, DebtToGdp, CreditRating, Infrastructure;
    public float TaxRate, TaxMin, TaxMax;
    public float BudgetMilitary, BudgetInfrastructure, BudgetResearch;
    public int Population, PopulationNormalized, MilitaryNormalized;
    public float LiteracyRate;
    public Color MapColor;
    public Vector2 Centroid, CapitalCentroid;
    public bool Landlocked;
}

public class ProvinceData {
    public string Id, Name, ParentIso, Terrain;
    public Vector2 Centroid;
    public Color DetectColor;
    public int Population;
    public float AreaKm2, GdpContribution;
    public Vector2[] Polygon;
}

public class UnitData {
    public string Id, Type, Owner, Location, ArmyId;
    public string BaseProvince, DeployedTo;  // air units
    public int Strength, DaysRemaining;
    public float Morale;
    public string[] Path;
}

public class RelationData {
    public string IsoA, IsoB;
    public int DiplomaticScore, EscalationLevel;
    public float TradeVolume, LoansOwed;
    public bool AtWar, Alliance, TradeDeal, MilitaryAccess;
}

public class BuildingData {
    public string Type;
    public int Level;
}

public class ConstructionItem {
    public string Province, Type;
    public float Progress, Cost;
}

public class RecruitmentItem {
    public string UnitType, Province;
    public float Progress, Cost;
    public int TrainMonths;
}

public class MemoryRecord {
    public string EventType, ActorIso, TargetIso;
    public string[] Witnesses;
    public float Weight, DecayRate, CurrentStrength;
    public DateData Date;
}

public struct DateData {
    public int Year, Month, Day, Hour;
}
```

## Data Files (Transfer As-Is)

- data/countries.json — 195 countries with real GDP/pop/govt data
- data/provinces.json — 4584 provinces with coordinates, parent ISO
- data/adjacencies.json — province and country adjacency graphs
- data/sea_adjacencies.json — naval movement graph
- assets/map/provinces.png — 8192x4096 pixel-color province bitmap
- assets/map/terrain.png — terrain base texture
- assets/map/heightmap.png — elevation data
- assets/map/detail.png — noise/detail texture
- assets/map/biome_atlas.png — 6-biome texture atlas
- assets/shaders/map.gdshader — port to HLSL
- assets/flags/ — country flag PNGs
- assets/units/ — unit sprite PNGs

---

## Known Issues Fixed in Godot (Don't Re-Introduce)

1. Budget sliders must call _apply_budget_effects (not be decorative)
2. recruit_unit must go through training queue (no instant spawn)
3. Only ONE input handler for speed/pause keys
4. Credit rating must update monthly based on debt
5. Trade bonus must be proportional to GDP, apply to all countries
6. Building GDP bonus needs diminishing returns (not linear compound)
7. Debt restructuring triggers at 150% (not infinite spiral)
8. Power tiers recalculate annually
9. Territory capture must cancel defender queues + handle air bases
10. AI must: target player, make peace, build all building types, use tier-aware weights

See REVIEW_REPORT.txt for the complete 50+ fix list.

---

## Unbuilt Systems (Build Fresh in Unity)

See docs/AI_ROADMAP.md for full specs:
- Alliance system (propose/accept/honor)
- Escalation ladder (0-6 levels)
- Peace negotiations with war score
- Loans & debt traps
- Sanctions with economic effect
- WorldMemoryDB integration (record events, AI reads reputation)
- Military access grants
- Coalition warfare
