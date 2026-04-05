// UIService.cs
// Port of UIManager.gd — panel state management.
// Progressive disclosure: systems appear when triggered.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace WarStrategy.Core
{
    public enum PanelState
    {
        Hidden,
        Minimal,
        Full
    }

    public class UIService : MonoBehaviour
    {
        public event Action<string, PanelState> PanelStateChanged;

        private Dictionary<string, PanelState> _panelStates = new();

        public void SetPanelState(string panelName, PanelState state)
        {
            _panelStates[panelName] = state;
            PanelStateChanged?.Invoke(panelName, state);
        }

        public PanelState GetPanelState(string panelName)
        {
            return _panelStates.GetValueOrDefault(panelName, PanelState.Hidden);
        }

        /// <summary>
        /// Push a notification to the feed. Type: info, warning, error.
        /// </summary>
        public void PushNotification(string message, string type = "info")
        {
            // Will wire to UI Toolkit NotificationFeed in Phase 4
            Debug.Log($"[Notification:{type}] {message}");
        }
    }
}
