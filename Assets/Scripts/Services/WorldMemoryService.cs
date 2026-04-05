// WorldMemoryService.cs
// Port of WorldMemoryDB.gd — reputation tracking with decay.
// Pure C# class (no MonoBehaviour needed).

using System;
using System.Collections.Generic;
using System.Linq;
using WarStrategy.Data;

namespace WarStrategy.Core
{
    /// <summary>
    /// Historical memory system. Tracks war declarations, betrayals, nuclear use, etc.
    /// Reputation scoring with time-based decay. AI reads this for diplomatic decisions.
    /// Fix #13: rebuild uses CurrentStrength directly (same scale as record).
    /// Fix #22: memories + reputations saved/restored by SaveSystem.
    /// </summary>
    public class WorldMemoryService
    {
        public event Action<MemoryRecord> MemoryAdded;
        public event Action<string, float> ReputationChanged;

        public List<MemoryRecord> Memories { get; private set; } = new();
        public Dictionary<string, float> Reputations { get; private set; } = new();

        // Permanent event types — never decay
        private static readonly HashSet<string> PermanentEvents = new()
        {
            "used_nuclear_weapon",
            "committed_mass_atrocity",
            "triggered_nuclear_war"
        };

        /// <summary>
        /// Record a new event in world memory.
        /// </summary>
        public void Record(string eventType, string actorIso, string targetIso,
            float weight, float decayRate, DateData date, string[] witnesses = null)
        {
            bool permanent = PermanentEvents.Contains(eventType);

            var record = new MemoryRecord
            {
                EventType = eventType,
                ActorIso = actorIso,
                TargetIso = targetIso,
                Weight = weight,
                DecayRate = permanent ? 0f : decayRate,
                CurrentStrength = weight,
                Date = date,
                Permanent = permanent,
                Witnesses = witnesses ?? Array.Empty<string>()
            };

            Memories.Add(record);
            UpdateReputation(actorIso);
            MemoryAdded?.Invoke(record);
        }

        /// <summary>
        /// Get reputation score for a country. Higher = more trusted.
        /// </summary>
        public float GetReputation(string iso)
        {
            return Reputations.GetValueOrDefault(iso, 0f);
        }

        /// <summary>
        /// Decay all non-permanent memories. Called yearly.
        /// </summary>
        public void DecayMemories()
        {
            foreach (var mem in Memories)
            {
                if (!mem.Permanent && mem.DecayRate > 0f)
                {
                    mem.CurrentStrength *= (1f - mem.DecayRate);
                }
            }

            // Remove fully decayed memories (strength < 0.01)
            Memories.RemoveAll(m => !m.Permanent && m.CurrentStrength < 0.01f);

            // Rebuild all reputations
            RebuildReputations();
        }

        /// <summary>
        /// Rebuild reputation for a specific country from its memories.
        /// Fix #13: uses CurrentStrength directly — same scale as Record().
        /// </summary>
        private void UpdateReputation(string iso)
        {
            float score = 0f;
            foreach (var mem in Memories)
            {
                if (mem.ActorIso == iso)
                    score -= mem.CurrentStrength; // negative actions reduce reputation
            }

            float oldRep = Reputations.GetValueOrDefault(iso, 0f);
            Reputations[iso] = score;

            if (Math.Abs(oldRep - score) > 0.01f)
                ReputationChanged?.Invoke(iso, score);
        }

        private void RebuildReputations()
        {
            var affectedIsos = Memories.Select(m => m.ActorIso).Distinct().ToList();
            foreach (var iso in affectedIsos)
                UpdateReputation(iso);
        }

        // ── Save/Load support (Fix #22) ──

        public void RestoreFromSave(List<MemoryRecord> memories, Dictionary<string, float> reputations)
        {
            Memories = memories ?? new List<MemoryRecord>();
            Reputations = reputations ?? new Dictionary<string, float>();
        }
    }
}
