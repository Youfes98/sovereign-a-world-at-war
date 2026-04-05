// GameClockService.cs
// Multi-rate tick system. Drives entire simulation via SimulationManager.
// Port of GameClock.gd — same speed table, same tick hierarchy.
// Hard Rule #1: Systems do NOT subscribe to clock events directly.
// Only SimulationManager subscribes. Systems are called in explicit order.

using System;
using UnityEngine;
using WarStrategy.Data;

namespace WarStrategy.Core
{
    public class GameClockService : MonoBehaviour
    {
        // ── Events (UI-only subscribers — simulation uses SimulationManager) ──
        public event Action<int> SpeedChanged;
        public event Action<bool> PauseChanged;

        // ── Tick events (ONLY SimulationManager subscribes to these) ──
        public event Action<DateData> TickHour;
        public event Action<DateData> TickDay;
        public event Action<DateData> TickWeek;
        public event Action<DateData> TickMonth;
        public event Action<DateData> TickYear;

        // ── State ──
        public DateData Date { get; private set; } = new(2026, 1, 1, 0);
        public int Speed { get; private set; } = 1;
        public bool Paused { get; private set; } = true;
        public int TotalDays { get; set; } // saved/restored — Fix #12

        // Speed table: hours per real second at each speed level
        // Matches Godot: [0 (pause), 1×, 3×, 12×, 48×, 168×]
        private static readonly float[] SpeedMultipliers = { 0f, 1f, 3f, 12f, 48f, 168f };

        private const int DaysPerMonth = 30;
        private float _hourAccumulator;

        private void Update()
        {
            if (Paused || Speed == 0)
                return;

            float hoursPerSecond = SpeedMultipliers[Mathf.Clamp(Speed, 0, SpeedMultipliers.Length - 1)];
            _hourAccumulator += hoursPerSecond * Time.deltaTime;

            // Process accumulated hours
            while (_hourAccumulator >= 1f)
            {
                _hourAccumulator -= 1f;
                AdvanceHour();
            }
        }

        private void AdvanceHour()
        {
            var date = Date;
            date.Hour++;

            if (date.Hour >= 24)
            {
                date.Hour = 0;
                date.Day++;
                TotalDays++;

                // Weekly tick — Fix #9: use global counter, not rolling per-month
                if (TotalDays % 7 == 0)
                {
                    Date = date;
                    TickWeek?.Invoke(Date);
                }

                // Monthly tick
                if (date.Day > DaysPerMonth)
                {
                    date.Day = 1;
                    date.Month++;

                    // Yearly tick
                    if (date.Month > 12)
                    {
                        date.Month = 1;
                        date.Year++;
                        Date = date;
                        TickYear?.Invoke(Date);
                    }

                    Date = date;
                    TickMonth?.Invoke(Date);
                }

                Date = date;
                TickDay?.Invoke(Date);
            }

            Date = date;
            TickHour?.Invoke(Date);
        }

        // ── Public API ──

        public void SetSpeed(int speed)
        {
            Speed = Mathf.Clamp(speed, 1, SpeedMultipliers.Length - 1);
            SpeedChanged?.Invoke(Speed);
        }

        public void SetPaused(bool paused)
        {
            Paused = paused;
            PauseChanged?.Invoke(paused);
        }

        public void TogglePause()
        {
            SetPaused(!Paused);
        }

        public void RestoreDate(DateData date, int totalDays)
        {
            Date = date;
            TotalDays = totalDays;
        }

        public string GetDateString()
        {
            string[] months = {
                "", "Jan", "Feb", "Mar", "Apr", "May", "Jun",
                "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"
            };
            int m = Mathf.Clamp(Date.Month, 1, 12);
            return $"{Date.Day} {months[m]} {Date.Year}";
        }
    }
}
