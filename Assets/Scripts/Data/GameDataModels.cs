// GameDataModels.cs
// Typed data classes replacing all untyped Dictionary<string,object> from Godot version.
// Hard Rule #4: Every dict key is now a typed property. No .get() calls, no silent coercion.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace WarStrategy.Data
{
    [Serializable]
    public class CountryData
    {
        // Identity
        public string Iso;
        public string Iso2;
        public string Name;
        public string Capital;
        public string Region;
        public string Subregion;
        public string GovernmentType;
        public string PowerTier; // S, A, B, C, D

        // Economy
        public float GdpRawBillions;
        public float GdpNormalized; // 0-1000 log scale
        public float Treasury;
        public float DebtToGdp;
        public float CreditRating; // 0-100
        public float Infrastructure; // 0-100
        public float TaxRate;
        public float TaxMin;
        public float TaxMax;

        // Budget allocation (0-1 each, must sum to 1)
        public float BudgetMilitary;
        public float BudgetInfrastructure;
        public float BudgetResearch;

        // Population & Military
        public long Population;
        public int PopulationNormalized; // 0-1000 scale
        public int MilitaryNormalized;   // 0-1000 scale
        public float Stability;          // 0-100
        public float LiteracyRate;       // 0-100

        // Map
        public Color MapColor;
        public Vector2 Centroid;
        public Vector2 CapitalCentroid;
        public bool Landlocked;

        // Runtime state (not from JSON)
        public float MonthlyBalance;
        public float Inflation;
    }

    [Serializable]
    public class ProvinceData
    {
        public string Id;
        public string Name;
        public string ParentIso; // owning country ISO-A3
        public string Terrain;   // plains, forest, mountain, desert, tundra, jungle
        public Vector2 Centroid;
        public Color DetectColor; // pixel color in provinces.png for O(1) lookup
        public int Population;
        public float AreaKm2;
        public float GdpContribution;
        public Vector2[] Polygon; // boundary vertices
    }

    [Serializable]
    public class UnitData
    {
        public string Id;
        public string Type;     // Infantry, Armor, Artillery, Fighter, Bomber, etc.
        public string Owner;    // ISO-A3
        public string Location; // province ID
        public string ArmyId;
        public string Domain;   // land, sea, air

        // Air unit specifics
        public string BaseProvince;
        public string DeployedTo;

        // State
        public int Strength;    // 0-100
        public int DaysRemaining;
        public float Morale;    // 0-100
        public string[] Path;   // BFS movement queue
    }

    [Serializable]
    public class RelationData
    {
        public string IsoA;
        public string IsoB;
        public int DiplomaticScore; // -100 to 100
        public int EscalationLevel; // 0-6 (peace to nuclear threshold)
        public float TradeVolume;
        public float LoansOwed;
        public bool AtWar;
        public bool Alliance;
        public bool TradeDeal;
        public bool MilitaryAccess;
    }

    [Serializable]
    public class BuildingData
    {
        public string Type;
        public int Level;
    }

    [Serializable]
    public class ConstructionItem
    {
        public string Province;
        public string Type;
        public float Progress; // 0-1
        public float Cost;
    }

    [Serializable]
    public class RecruitmentItem
    {
        public string UnitType;
        public string Province;
        public float Progress; // 0-1
        public float Cost;
        public int TrainMonths;
    }

    [Serializable]
    public class MemoryRecord
    {
        public string EventType;
        public string ActorIso;
        public string TargetIso;
        public string[] Witnesses;
        public float Weight;
        public float DecayRate;
        public float CurrentStrength;
        public DateData Date;
        public bool Permanent; // nuclear use, mass atrocity — never decays
    }

    [Serializable]
    public struct DateData
    {
        public int Year;
        public int Month;
        public int Day;
        public int Hour;

        public DateData(int year, int month, int day, int hour = 0)
        {
            Year = year;
            Month = month;
            Day = day;
            Hour = hour;
        }

        public override string ToString() => $"{Day:D2}/{Month:D2}/{Year}";
    }

    /// <summary>
    /// Save file container — everything needed to reconstruct game state.
    /// </summary>
    [Serializable]
    public class SaveData
    {
        public DateData Date;
        public string PlayerIso;
        public int TotalDays; // for weekly tick alignment
        public Dictionary<string, CountryData> Countries;
        public Dictionary<string, UnitData> Units;
        public List<ConstructionItem> ConstructionQueue;
        public List<RecruitmentItem> RecruitmentQueue;
        public Dictionary<string, string> TerritoryOwnership; // province → iso
        public List<MemoryRecord> Memories;
        public Dictionary<string, float> Reputations;
    }
}
