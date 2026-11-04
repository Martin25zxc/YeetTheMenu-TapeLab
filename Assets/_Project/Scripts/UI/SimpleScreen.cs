using UnityEngine;
using UnityEngine.UIElements;

namespace YeetTheMenu.UI
{
    /// <summary>Pantalla sin lógica propia: solo un botón para volver. Sirve para Credits y similares.</summary>
    public class SimpleScreen : MenuScreen
    {
        [Tooltip("Nombre del botón de volver en el UXML. Vacío = sin botón (solo Escape).")]
        [SerializeField] private string backButtonName = "btn-back";

        private Button backButton;

        protected override void OnBuild(VisualElement root)
        {
            backButton = string.IsNullOrEmpty(backButtonName)
                ? null
                : BindClick(root, backButtonName, OnBackClicked);
        }

        // La desuscripción la hace la base; acá solo se suelta la referencia al árbol viejo.
        protected override void OnUnbind() => backButton = null;

        protected override Focusable InitialFocus => backButton;

        private void OnBackClicked() => Stack.Back();
    }
}