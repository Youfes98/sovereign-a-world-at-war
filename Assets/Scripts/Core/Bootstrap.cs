// Bootstrap.cs
// Entry point — creates and registers all services in correct order.
// Equivalent to Godot's autoload registration in project.godot.

using UnityEngine;
using WarStrategy.Data;
using WarStrategy.UI;

namespace WarStrategy.Core
{
    /// <summary>
    /// Attach to a root GameObject in the scene. Runs before everything else.
    /// Creates services, loads data, and starts the game loop.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class Bootstrap : MonoBehaviour
    {
        private void Awake()
        {
            // Prevent duplicate bootstraps on scene reload
            if (FindObjectsByType<Bootstrap>(FindObjectsSortMode.None).Length > 1)
            {
                Destroy(gameObject);
                return;
            }

            DontDestroyOnLoad(gameObject);

            // Create service instances — order matches Godot autoload chain
            var clock = gameObject.AddComponent<GameClockService>();
            var gameState = gameObject.AddComponent<GameStateService>();
            var provinceDB = gameObject.AddComponent<ProvinceDatabase>();
            var worldMemory = new WorldMemoryService();
            var simulation = gameObject.AddComponent<SimulationManager>();
            var save = gameObject.AddComponent<SaveService>();
            var ui = gameObject.AddComponent<UIService>();

            // Register all services
            Services.Register(clock, gameState, provinceDB, worldMemory, simulation, save, ui);

            if (!Services.Validate())
            {
                Debug.LogError("[Bootstrap] Service registration failed!");
                return;
            }

            // Load country data async (3.5MB — coroutine + background thread parse)
            gameState.LoadCountryData();

            // Province data loaded async (58MB — background thread + coroutine)
            provinceDB.LoadProvinceData();

            // Start paused ��� waiting for country selection (same as Godot Main._ready())
            clock.SetPaused(true);

            // Create scene objects (map, camera, borders, labels)
            if (FindAnyObjectByType<SceneSetup>() == null)
                gameObject.AddComponent<SceneSetup>();

            // Create UI flow controller (main menu + country selection)
            if (FindAnyObjectByType<GameFlowController>() == null)
            {
                var flowGO = new GameObject("GameFlowController");
                flowGO.AddComponent<GameFlowController>();
                DontDestroyOnLoad(flowGO);
            }

            Debug.Log("[Bootstrap] Initialization complete. Waiting for country selection.");
        }
    }
}
