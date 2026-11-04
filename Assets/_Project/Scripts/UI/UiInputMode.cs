using UnityEngine;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;

namespace YeetTheMenu.UI
{
    /// <summary>
    /// Publica qué dispositivo usa el jugador como una clase USS, para que haya UN solo
    /// elemento resaltado: con mouse manda :hover, con teclado o gamepad manda :focus.
    /// Emula :focus-visible, que USS no tiene. No mueve el foco: solo decide qué se ve.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    [DisallowMultipleComponent]
    public class UiInputMode : MonoBehaviour
    {
        private const string DirectionalClass = "input-directional";
        private const float MoveThresholdSqr = 4f;   // 2 px: descarta el ruido del mouse

        [Tooltip("Elemento que recibe la clase. Debe ser el que tiene las hojas de estilo, o un descendiente.")]
        [SerializeField] private string targetName = "root";

        [Tooltip("La primera flecha o D-pad revela el elemento enfocado sin moverlo; la segunda ya mueve.")]
        [SerializeField] private bool firstMoveReveals = true;

        [Tooltip("Oculta el cursor del sistema mientras se usa teclado o gamepad.")]
        [SerializeField] private bool hideCursorWhenDirectional;

        [Tooltip("Registra en consola qué evento llega con cada tecla. Para verificar Tab, no para dejarlo activo.")]
        [SerializeField] private bool logEvents;

        private UIDocument document;
        private VisualElement eventRoot;    // raíz del panel: ve todos los eventos
        private VisualElement classTarget;  // #root: donde la clase alcanza a los selectores
        private Vector2 lastPointer;
        private bool hasPointer;
        private bool directional;

        /// <summary>true = el jugador está usando teclado o gamepad.</summary>
        public bool IsDirectional => directional;

        private void Awake() => document = GetComponent<UIDocument>();

        private void OnDisable()
        {
            Unbind();
            if (hideCursorWhenDirectional) Cursor.visible = true;
        }

        private void Update()
        {
            // Solo vigila que el árbol no se haya recreado. No consulta ningún dispositivo.
            if (eventRoot != null && eventRoot.panel != null) return;
            Unbind();
            TryBind();
        }

        private void TryBind()
        {
            VisualElement root = document.rootVisualElement;
            if (root?.panel == null) return;

            eventRoot = root.panel.visualTree;
            classTarget = root.Q<VisualElement>(targetName) ?? root;

            // TrickleDown: los eventos se ven antes de que un control los consuma.
            eventRoot.RegisterCallback<PointerMoveEvent>(OnPointerMove, TrickleDown.TrickleDown);
            eventRoot.RegisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
            eventRoot.RegisterCallback<NavigationMoveEvent>(OnNavigationMove, TrickleDown.TrickleDown);
            eventRoot.RegisterCallback<NavigationSubmitEvent>(OnNavigationSubmit, TrickleDown.TrickleDown);
            eventRoot.RegisterCallback<NavigationCancelEvent>(OnNavigationCancel, TrickleDown.TrickleDown);
            eventRoot.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);

            hasPointer = false;
            Apply();
        }

        private void Unbind()
        {
            if (eventRoot == null) return;

            eventRoot.UnregisterCallback<PointerMoveEvent>(OnPointerMove, TrickleDown.TrickleDown);
            eventRoot.UnregisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
            eventRoot.UnregisterCallback<NavigationMoveEvent>(OnNavigationMove, TrickleDown.TrickleDown);
            eventRoot.UnregisterCallback<NavigationSubmitEvent>(OnNavigationSubmit, TrickleDown.TrickleDown);
            eventRoot.UnregisterCallback<NavigationCancelEvent>(OnNavigationCancel, TrickleDown.TrickleDown);
            eventRoot.UnregisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);

            classTarget?.RemoveFromClassList(DirectionalClass);
            eventRoot = null;
            classTarget = null;
        }

        // ── Mouse y puntero ──

        private void OnPointerMove(PointerMoveEvent evt)
        {
            Vector2 position = evt.position;

            // Si el cursor no se movió, fue la UI la que se movió debajo (por ejemplo, un glitch).
            if (hasPointer && (position - lastPointer).sqrMagnitude < MoveThresholdSqr) return;

            lastPointer = position;
            hasPointer = true;
            SetDirectional(false, "PointerMove");
        }

        private void OnPointerDown(PointerDownEvent evt) => SetDirectional(false, "PointerDown");

        // ── Teclado y gamepad ──

        private void OnNavigationMove(NavigationMoveEvent evt)
        {
            if (logEvents) Debug.Log($"UiInputMode: NavigationMove {evt.direction}", this);

            bool wasPointer = !directional;
            SetDirectional(true, "NavigationMove");

            if (!wasPointer || !firstMoveReveals) return;
            if (eventRoot.focusController?.focusedElement is not VisualElement focused) return;
            if (focused.panel == null) return;   // sin foco válido, dejamos que el foco se mueva

            // Convención de consola: la primera pulsación revela dónde está el foco, no lo mueve.
            evt.StopPropagation();
            eventRoot.focusController.IgnoreEvent(evt);
        }

        // Tab y Shift+Tab. Solo Tab: reaccionar a cualquier tecla confundiría escribir con navegar.
        // No se intercepta: Unity decide cuál es el siguiente control.
        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode != KeyCode.Tab) return;
            if (logEvents) Debug.Log($"UiInputMode: KeyDown Tab (shift = {evt.shiftKey})", this);
            SetDirectional(true, "KeyDown Tab");
        }

        // Enter / aceptar. Un clic de mouse genera ClickEvent, no este.
        private void OnNavigationSubmit(NavigationSubmitEvent evt)
        {
            if (logEvents) Debug.Log("UiInputMode: NavigationSubmit", this);
            SetDirectional(true, "NavigationSubmit");
        }

        // Escape / B. Al volver de una pantalla, el foco restaurado debe verse.
        private void OnNavigationCancel(NavigationCancelEvent evt)
        {
            if (logEvents) Debug.Log("UiInputMode: NavigationCancel", this);
            SetDirectional(true, "NavigationCancel");
        }

        // ── Estado ──

        private void SetDirectional(bool value, string cause)
        {
            if (directional == value) return;
            directional = value;
            if (logEvents) Debug.Log($"UiInputMode: modo {(value ? "direccional" : "puntero")} ({cause})", this);
            Apply();
        }

        private void Apply()
        {
            classTarget?.EnableInClassList(DirectionalClass, directional);
            if (hideCursorWhenDirectional) Cursor.visible = !directional;
        }
    }
}