// Services.cs
// Lightweight Service Locator — NOT dependency injection.
// Architecture doc: "Service Locator over DI — correct for this scale"

using UnityEngine;

namespace WarStrategy.Core
{
    /// <summary>
    /// Static service locator. All services registered by Bootstrap.cs at startup.
    /// Preferred over singletons because services can be swapped for testing.
    /// </summary>
    public static class Services
    {
        public static GameStateService GameState { get; private set; }
        public static GameClockService Clock { get; private set; }
        public static SimulationManager Simulation { get; private set; }
        public static ProvinceDatabase ProvinceDB { get; private set; }
        public static WorldMemoryService WorldMemory { get; private set; }
        public static SaveService Save { get; private set; }
        public static UIService UI { get; private set; }

        /// <summary>
        /// Called once by Bootstrap.cs. Order matters — matches Godot autoload order.
        /// </summary>
        public static void Register(
            GameClockService clock,
            GameStateService gameState,
            ProvinceDatabase provinceDB,
            WorldMemoryService worldMemory,
            SimulationManager simulation,
            SaveService save,
            UIService ui)
        {
            Clock = clock;
            GameState = gameState;
            ProvinceDB = provinceDB;
            WorldMemory = worldMemory;
            Simulation = simulation;
            Save = save;
            UI = ui;

            Debug.Log("[Services] All services registered.");
        }

        /// <summary>
        /// Verify all services are registered. Call after Register().
        /// </summary>
        public static bool Validate()
        {
            bool valid = true;
            if (Clock == null) { Debug.LogError("[Services] GameClockService missing!"); valid = false; }
            if (GameState == null) { Debug.LogError("[Services] GameStateService missing!"); valid = false; }
            if (ProvinceDB == null) { Debug.LogError("[Services] ProvinceDatabase missing!"); valid = false; }
            if (WorldMemory == null) { Debug.LogError("[Services] WorldMemoryService missing!"); valid = false; }
            if (Simulation == null) { Debug.LogError("[Services] SimulationManager missing!"); valid = false; }
            if (Save == null) { Debug.LogError("[Services] SaveService missing!"); valid = false; }
            if (UI == null) { Debug.LogError("[Services] UIService missing!"); valid = false; }
            return valid;
        }
    }
}
