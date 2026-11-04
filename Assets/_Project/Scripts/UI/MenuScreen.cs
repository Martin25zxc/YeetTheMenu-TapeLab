using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace YeetTheMenu.UI
{
    /// <summary>Qué pasa con las pantallas de abajo cuando esta termina de entrar.</summary>
    public enum ScreenBackdrop
    {
        Replace,   // las oculta: cambio de vista (Main Menu → Credits)
        Overlay    // las deja visibles: ventana encima (Gameplay → Pause)
    }

    /// <summary>
    /// Contrato de una pantalla para ScreenStack. Va en un GameObject HIJO del ScreenStack.
    /// La pantalla nunca se muestra ni se oculta sola: eso es trabajo exclusivo del stack.
    ///
    /// Ciclo de vida que garantiza el stack:
    ///   OnBuild(root) → se creó una VISTA para esta pantalla (se repite si el árbol se recrea)
    ///   OnShow()      → ENTRA en la navegación: está por aparecer (todavía invisible)
    ///   OnHide()      → SALIÓ de la navegación: ya quedó oculta
    ///   OnUnbind()    → la vista se descarta; siempre empareja un OnBuild
    /// Recrear la vista NO dispara OnShow ni OnHide: la pantalla nunca dejó de estar abierta.
    /// Lo que deba sobrevivir a una recreación (texto escrito, errores) vive en campos,
    /// y OnBuild lo vuelve a pintar.
    /// </summary>
    public abstract class MenuScreen : MonoBehaviour
    {
        [Tooltip("Identificador único dentro de su ScreenStack (por ejemplo: main, credits).")]
        [SerializeField] private string id;

        [Tooltip("UXML de esta pantalla.")]
        [SerializeField] private VisualTreeAsset view;

        [Tooltip("Replace: oculta lo de abajo. Overlay: lo deja visible (la pantalla trae su propio velo).")]
        [SerializeField] private ScreenBackdrop backdrop = ScreenBackdrop.Replace;

        public string Id => id;
        public VisualTreeAsset View => view;
        public ScreenBackdrop Backdrop => backdrop;

        /// <summary>El stack dueño de esta pantalla. Disponible desde OnBuild.</summary>
        protected ScreenStack Stack { get; private set; }

        // Suscripciones hechas con BindClick: se deshacen solas en Unbind.
        private readonly List<Button> clickButtons = new();
        private readonly List<Action> clickActions = new();

        // ── Lo que invoca el stack (internal: solo código del mismo ensamblado) ──

        internal void Build(ScreenStack stack, VisualElement root)
        {
            Stack = stack;
            OnBuild(root);
        }

        internal void Unbind()
        {
            OnUnbind();   // primero la pantalla, que todavía puede usar sus referencias

            for (int i = 0; i < clickButtons.Count; i++)
                clickButtons[i].clicked -= clickActions[i];   // el MISMO delegado que se suscribió

            clickButtons.Clear();
            clickActions.Clear();
        }

        /// <summary>true entre su OnShow y su OnHide. Recrear la vista no lo cambia.</summary>
        public bool IsShown { get; private set; }

        // Idempotentes: reconstruir el árbol visual no es entrar ni salir de la pantalla.
        internal void Show()
        {
            if (IsShown) return;
            IsShown = true;
            OnShow();
        }

        internal void Hide()
        {
            if (!IsShown) return;
            IsShown = false;
            OnHide();
        }
        internal Focusable GetInitialFocus() => InitialFocus;
        internal bool HandleBack() => OnBackRequested();

        // ── Ayuda para las pantallas ──

        /// <summary>Busca un botón por nombre y le conecta una acción. La desconexión es automática.</summary>
        protected Button BindClick(VisualElement root, string buttonName, Action action)
        {
            Button button = root.Q<Button>(buttonName);
            if (button == null)
            {
                Debug.LogWarning($"{GetType().Name} '{id}': no existe el botón '{buttonName}'.", this);
                return null;
            }

            button.clicked += action;
            clickButtons.Add(button);
            clickActions.Add(action);
            return button;
        }

        // ── Lo que implementa cada pantalla ──

        /// <summary>Cablear controles. Se llama una vez por cada árbol instanciado.</summary>
        protected abstract void OnBuild(VisualElement root);

        /// <summary>Soltar lo que OnBuild tomó y la base no conoce (eventos externos, referencias).</summary>
        protected virtual void OnUnbind() { }

        /// <summary>Está por aparecer, todavía invisible: refrescar contenido.</summary>
        protected virtual void OnShow() { }

        /// <summary>Ya quedó oculta: limpiar o restablecer sin que se vea.</summary>
        protected virtual void OnHide() { }

        /// <summary>Qué recibe el foco al terminar de entrar. null = el primer elemento enfocable.</summary>
        protected virtual Focusable InitialFocus => null;

        /// <summary>"Volver" pedido por el usuario. Devolver true si la pantalla lo resolvió sola.</summary>
        protected virtual bool OnBackRequested() => false;
    }
}