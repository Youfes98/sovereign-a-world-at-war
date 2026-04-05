// SimulationManager.cs
// Hard Rule #1: Explicit system update order — no event-based tick chains.
// This is the ONLY subscriber to GameClock tick events.
// Systems are called in deterministic order, then UI refresh events fire AFTER.

using System;
using UnityEngine;
using WarStrategy.Data;

namespace WarStrategy.Core
{
    /// <summary>
    /// Central simulation coordinator. Subscribes to clock ticks and calls
    /// systems in explicit order. UI refreshes happen via events AFTER
    /// all simulation is complete.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public class SimulationManager : MonoBehaviour
    {
        // ── Post-simulation UI refresh events ──
        public event Action<DateData> OnPostSimulationTick;
        public event Action<DateData> OnMonthEnd;
        public event Action<DateData> OnYearEnd;

        // System references (set after all systems are created)
        // These will be the MonoBehaviour system components
        // For now, stubs — filled in Phase 3 when systems are ported

        private void Start()
        {
            var clock = Services.Clock;
            if (clock == null)
            {
                Debug.LogError("[SimulationManager] GameClockService not registered!");
                return;
            }

            // Subscribe to clock — we are the ONLY subscriber
            clock.TickHour += OnTickHour;
            clock.TickDay += OnTickDay;
            clock.TickMonth += OnTickMonth;
            clock.TickYear += OnTickYear;
        }

        private void OnDestroy()
        {
            var clock = Services.Clock;
            if (clock != null)
            {
                clock.TickHour -= OnTickHour;
                clock.TickDay -= OnTickDay;
                clock.TickMonth -= OnTickMonth;
                clock.TickYear -= OnTickYear;
            }
        }

        // ── Tick handlers — explicit system ordering ──

        private void OnTickHour(DateData date)
        {
            // Hourly: unit movement, supply checks
            // MilitarySystem.TickMovement(date);
        }

        private void OnTickDay(DateData date)
        {
            // Daily: unit supply, attrition
            // MilitarySystem.TickDaily(date);

            OnPostSimulationTick?.Invoke(date);
        }

        private void OnTickMonth(DateData date)
        {
            // Monthly — THE critical ordering. Matches Godot signal chain.
            // Each system reads results from the previous one.

            // 1. Economy first — updates GDP, treasury, debt, credit rating
            // EconomySystem.TickMonth(date);

            // 2. Buildings — construction progress, completion effects
            // BuildingSystem.TickMonth(date);

            // 3. Military — recruitment queue, upkeep costs
            // MilitarySystem.TickRecruitment(date);

            // 4. AI last — reads all updated state, makes decisions
            // AISystem.TickMonth(date);

            // 5. UI refresh AFTER all simulation
            OnMonthEnd?.Invoke(date);
        }

        private void OnTickYear(DateData date)
        {
            // Yearly: power tier recalculation, memory decay
            // GameState.RecalculatePowerTiers();
            // WorldMemory.DecayMemories();

            OnYearEnd?.Invoke(date);
        }
    }
}
