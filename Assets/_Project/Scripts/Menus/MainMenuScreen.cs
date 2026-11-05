using UnityEngine;
using UnityEngine.UIElements;
using YeetTheMenu.UI;

namespace YeetTheMenu.Menus
{
    /// <summary>Pantalla raíz del menú principal: conecta cada botón con su destino.</summary>
    public class MainMenuScreen : MenuScreen
    {
        private Button firstButton;

        protected override void OnBuild(VisualElement root)
        {
            firstButton = BindClick(root, "btn-host", OnHost);
            BindClick(root, "btn-join", OnJoin);
            BindClick(root, "btn-settings", OnSettings);
            BindClick(root, "btn-credits", OnCredits);
            BindClick(root, "btn-exit", OnExit);
        }

        protected override void OnUnbind() => firstButton = null;

        protected override Focusable InitialFocus => firstButton;

        private void OnHost() => Debug.Log("HOST GAME: fuera del alcance de la Fase 4.", this);
        // Los ids tienen que coincidir con el Id de cada MenuScreen de la escena.
        private void OnJoin() => Stack.Push("join");
        private void OnSettings() => Stack.Push("settings");
        private void OnCredits() => Stack.Push("credits");

        private void OnExit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;   // en el Editor, salir de Play
#else
            Application.Quit();                                // en la build, cerrar el juego
#endif
        }
    }
}