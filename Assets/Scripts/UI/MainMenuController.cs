using UnityEngine;
using UnityEngine.UIElements;

namespace WarStrategy.UI
{
    public class MainMenuController
    {
        private VisualElement _root;
        private System.Action _onNewGame;
        private System.Action _onLoadGame;
        private System.Action _onExit;

        public void Initialize(VisualElement root, System.Action onNewGame, System.Action onLoadGame, System.Action onExit)
        {
            _root = root;
            _onNewGame = onNewGame;
            _onLoadGame = onLoadGame;
            _onExit = onExit;

            var newGameBtn = root.Q<Button>("new-game-btn");
            if (newGameBtn != null) newGameBtn.clicked += () => _onNewGame?.Invoke();

            var loadGameBtn = root.Q<Button>("load-game-btn");
            if (loadGameBtn != null) loadGameBtn.clicked += () => _onLoadGame?.Invoke();

            var settingsBtn = root.Q<Button>("settings-btn");
            if (settingsBtn != null) settingsBtn.clicked += () => Debug.Log("Settings not implemented");

            var exitBtn = root.Q<Button>("exit-btn");
            if (exitBtn != null) exitBtn.clicked += () => _onExit?.Invoke();
        }

        public void Show() => _root.style.display = DisplayStyle.Flex;
        public void Hide() => _root.style.display = DisplayStyle.None;
    }
}
