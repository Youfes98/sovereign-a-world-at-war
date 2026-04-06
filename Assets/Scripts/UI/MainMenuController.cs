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

            // Load background art
            var bgEl = root.Q("menu-bg");
            if (bgEl != null)
            {
                var bgTex = Resources.Load<Texture2D>("UI/Art/main_menu_bg");
                if (bgTex != null)
                    bgEl.style.backgroundImage = new StyleBackground(bgTex);
            }

            // Load game logo
            var logoEl = root.Q("menu-logo");
            if (logoEl != null)
            {
                var logoTex = Resources.Load<Texture2D>("UI/Art/game_logo");
                if (logoTex != null)
                    logoEl.style.backgroundImage = new StyleBackground(logoTex);
            }

            // Button handlers
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
