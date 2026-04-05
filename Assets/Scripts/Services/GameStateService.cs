// GameStateService.cs
// Single source of truth for all game data. Port of GameState.gd.
// All systems read/write through this service.
// Country data loaded from StreamingAssets on background thread.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using WarStrategy.Data;

namespace WarStrategy.Core
{
    public class GameStateService : MonoBehaviour
    {
        // ── Events ──
        public event Action<string> CountrySelected;
        public event Action CountryDeselected;
        public event Action<string> CountryDataChanged;
        public event Action<string> PlayerCountrySet;
        public event Action<string, string, bool> WarStateChanged;
        public event Action<string, string, string> TerritoryChanged;
        public event Action OnCountryDataLoaded;

        // ── Data ──
        public Dictionary<string, CountryData> Countries { get; private set; } = new();
        public Dictionary<string, string> TerritoryOwnership { get; private set; } = new();
        public Dictionary<string, bool> WarStates { get; private set; } = new();

        public string PlayerIso { get; private set; } = "";
        public string SelectedIso { get; private set; } = "";
        public bool IsLoaded { get; private set; }

        // ── Country selection ──

        public void SelectCountry(string iso)
        {
            if (string.IsNullOrEmpty(iso) || !Countries.ContainsKey(iso))
            {
                Deselect();
                return;
            }
            SelectedIso = iso;
            CountrySelected?.Invoke(iso);
        }

        public void Deselect()
        {
            if (!string.IsNullOrEmpty(SelectedIso))
            {
                SelectedIso = "";
                CountryDeselected?.Invoke();
            }
        }

        public void SetPlayerCountry(string iso)
        {
            if (!Countries.ContainsKey(iso))
            {
                Debug.LogError($"[GameState] Cannot set player to unknown country: {iso}");
                return;
            }
            PlayerIso = iso;
            PlayerCountrySet?.Invoke(iso);
        }

        // ── Territory ownership ──

        public void SetTerritoryOwner(string provinceId, string newOwner)
        {
            string oldOwner = TerritoryOwnership.GetValueOrDefault(provinceId, "");
            if (oldOwner == newOwner) return;
            TerritoryOwnership[provinceId] = newOwner;
            TerritoryChanged?.Invoke(provinceId, oldOwner, newOwner);
        }

        // ── War state ──

        public bool AreAtWar(string isoA, string isoB)
        {
            return WarStates.GetValueOrDefault(GetWarKey(isoA, isoB), false);
        }

        public void SetWarState(string isoA, string isoB, bool atWar)
        {
            string key = GetWarKey(isoA, isoB);
            WarStates[key] = atWar;
            WarStateChanged?.Invoke(isoA, isoB, atWar);
        }

        private static string GetWarKey(string a, string b)
        {
            return string.Compare(a, b, StringComparison.Ordinal) < 0 ? $"{a}:{b}" : $"{b}:{a}";
        }

        // ── Data access ──

        public CountryData GetCountry(string iso)
        {
            return Countries.GetValueOrDefault(iso);
        }

        public void NotifyCountryDataChanged(string iso)
        {
            CountryDataChanged?.Invoke(iso);
        }

        // ── JSON loading — entirely off main thread ──

        public void LoadCountryData()
        {
            StartCoroutine(LoadCountryDataAsync());
        }

        private IEnumerator LoadCountryDataAsync()
        {
            string filePath = Path.Combine(Application.streamingAssetsPath, "Data", "countries.json");

            List<CountryData> countryList = null;

            var task = Task.Run(() =>
            {
                if (!File.Exists(filePath)) return;
                string json = File.ReadAllText(filePath);
                countryList = JsonParser.ParseCountries(json);
            });

            Debug.Log("[GameState] Loading countries on background thread...");

            while (!task.IsCompleted)
                yield return null;

            if (task.IsFaulted)
            {
                Debug.LogError($"[GameState] Parse failed: {task.Exception?.InnerException?.Message}");
                yield break;
            }

            if (countryList != null)
            {
                Countries.Clear();
                foreach (var c in countryList)
                    Countries[c.Iso] = c;
            }

            IsLoaded = true;
            Debug.Log($"[GameState] Parsed {Countries.Count} countries.");
            OnCountryDataLoaded?.Invoke();
        }
    }
}
