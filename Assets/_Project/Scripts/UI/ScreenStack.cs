using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace YeetTheMenu.UI
{
    /// <summary>
    /// Pila de pantallas dentro de UN panel. Instancia cada pantalla en #screen-host,
    /// la anima agregando y quitando clases USS y lleva el historial para volver.
    /// Durante una transición, un bloqueador invisible absorbe clics, foco y navegación.
    /// No toca Time.timeScale, ni el cursor, ni el input del juego.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    [DisallowMultipleComponent]
    public class ScreenStack : MonoBehaviour
    {
        // Clases definidas en screens.uss
        private const string ScreenClass  = "nav-screen";
        private const string VisibleClass = "nav-screen--visible";
        private const string LeavingClass = "nav-screen--leaving";
        private const string BlockerClass = "nav-blocker";
        private const string InstantClass = "nav-host--instant";
        private const string ReducedClass = "nav-host--reduced";

        private static readonly StylePropertyName OpacityProperty = new StylePropertyName("opacity");

        [Tooltip("Nombre del contenedor de pantallas en el UXML del host.")]
        [SerializeField] private string hostName = "screen-host";

        [Tooltip("Id de la pantalla raíz: la base de la pila, que no se cierra.")]
        [SerializeField] private string rootScreenId = "main";

        [Tooltip("Plazo máximo de una transición (s). Se fija al EMPEZAR cada transición. Debe superar la más larga del USS (hoy ≈ 0,37 s).")]
        [SerializeField, Min(0.05f)] private float completionTimeout = 0.75f;

        [Tooltip("Transiciones de duración cero (pruebas y accesibilidad).")]
        [SerializeField] private bool instantTransitions;

        [Tooltip("Registra en consola Run/End/Cancel de opacity. Para verificar, no para dejarlo activo.")]
        [SerializeField] private bool logTransitions;

        private UIDocument document;
        private VisualElement host;
        private VisualElement blocker;
        private VisualElement eventRoot;
        private bool bound;
        private int bindAttempts;

        // Configuración: se arma una vez en Awake.
        private readonly Dictionary<string, MenuScreen> screensById = new();

        // ESTADO: sobrevive a la recreación del árbol.
        private readonly List<MenuScreen> stack = new();
        private readonly List<Focusable> savedFocus = new();   // savedFocus[i] = foco a devolver al volver a stack[i]

        // VISTA: se descarta si el árbol se recrea.
        private readonly Dictionary<MenuScreen, VisualElement> instances = new();

        // Transición en curso. Listas reutilizadas: cero asignaciones por navegación.
        private readonly List<MenuScreen> visibleBefore = new();
        private readonly List<MenuScreen> visibleAfter = new();
        private readonly List<MenuScreen> enteringScreens = new();
        private readonly List<MenuScreen> leavingScreens = new();
        private readonly List<VisualElement> pendingReveal = new();   // esperan para recibir --visible
        private readonly List<VisualElement> animating = new();       // esperan su fin de opacity
        private float deadline;

        private bool focusPending;
        private Focusable preferredFocus;
        private bool reducedMotion;

        public bool IsBusy { get; private set; }
        public bool IsBound => bound && host.panel != null;
        public bool IsOpen => stack.Count > 1;
        public MenuScreen Top => stack.Count > 0 ? stack[stack.Count - 1] : null;

        /// <summary>Lo setea un adaptador externo: el stack no conoce RetroFx.</summary>
        public bool ReducedMotion
        {
            get => reducedMotion;
            set { reducedMotion = value; ApplyHostClasses(); }
        }

        public bool InstantTransitions
        {
            get => instantTransitions;
            set { instantTransitions = value; ApplyHostClasses(); }
        }

        // ─────────────────────────── Ciclo de vida ───────────────────────────

        private void Awake()
        {
            document = GetComponent<UIDocument>();

            // Las pantallas disponibles son los hijos de este objeto: cada escena define las suyas.
            foreach (MenuScreen screen in GetComponentsInChildren<MenuScreen>(true))
            {
                if (string.IsNullOrEmpty(screen.Id) || screen.View == null)
                {
                    Debug.LogWarning($"ScreenStack: '{screen.name}' no tiene Id o UXML; se ignora.", screen);
                    continue;
                }
                if (!screensById.TryAdd(screen.Id, screen))
                    Debug.LogWarning($"ScreenStack: Id repetido '{screen.Id}' en '{screen.name}'; se ignora.", screen);
            }

            if (screensById.TryGetValue(rootScreenId, out MenuScreen root)) stack.Add(root);
            else Debug.LogError($"ScreenStack: no hay ninguna pantalla hija con Id '{rootScreenId}'.", this);
        }

        private void OnDisable() => Unbind();

        private void OnValidate()
        {
            if (Application.isPlaying) ApplyHostClasses();   // permite probar Instant desde el Inspector
        }

        private void Update()
        {
            // El árbol puede no existir todavía o haberse recreado.
            if (!IsBound)
            {
                Unbind();
                if (!TryBind()) return;
            }

            RevealEnteringScreens();

            // Capa 3: red de seguridad. Si aparece este aviso en uso normal, algo está mal.
            if (IsBusy && Time.unscaledTime >= deadline)
            {
                Debug.LogWarning("ScreenStack: transición cerrada por plazo; no llegó el fin de opacity.", this);
                CompleteTransition();
            }

            ApplyPendingFocus();
        }

        // ─────────────────────────── API pública ───────────────────────────

        public void Push(string id)
        {
            if (!CanNavigate()) return;
            if (!screensById.TryGetValue(id, out MenuScreen next))
            {
                Debug.LogWarning($"ScreenStack: no hay ninguna pantalla con Id '{id}'. Registradas: {string.Join(", ", screensById.Keys)}.", this);
                return;
            }
            if (stack.Contains(next)) return;

            ComputeVisible(visibleBefore);
            savedFocus.Add(CurrentFocus());
            stack.Add(next);
            ComputeVisible(visibleAfter);

            GetOrCreate(next).BringToFront();   // la que entra se dibuja encima
            BeginTransition(null);
        }

        /// <summary>"Volver" pedido por el usuario (botón o Escape). La pantalla puede interceptarlo.</summary>
        public void Back()
        {
            if (!CanNavigate() || !IsOpen) return;
            if (Top.HandleBack()) return;
            Pop();
        }

        public void Pop()
        {
            if (!CanNavigate() || !IsOpen) return;

            ComputeVisible(visibleBefore);
            stack.RemoveAt(stack.Count - 1);
            Focusable restore = savedFocus[savedFocus.Count - 1];
            savedFocus.RemoveAt(savedFocus.Count - 1);
            ComputeVisible(visibleAfter);

            BeginTransition(restore);
        }

        public void PopToRoot()
        {
            if (!CanNavigate() || !IsOpen) return;

            ComputeVisible(visibleBefore);
            stack.RemoveRange(1, stack.Count - 1);
            Focusable restore = savedFocus[0];
            savedFocus.Clear();
            ComputeVisible(visibleAfter);

            BeginTransition(restore);
        }

        private bool CanNavigate() => IsBound && !IsBusy;

        // ─────────────────────────── Transición ───────────────────────────

        private void BeginTransition(Focusable focusToRestore)
        {
            enteringScreens.Clear();
            leavingScreens.Clear();
            pendingReveal.Clear();
            animating.Clear();

            for (int i = 0; i < visibleBefore.Count; i++)
                if (!visibleAfter.Contains(visibleBefore[i])) leavingScreens.Add(visibleBefore[i]);

            for (int i = 0; i < visibleAfter.Count; i++)
            {
                MenuScreen screen = visibleAfter[i];
                if (visibleBefore.Contains(screen)) continue;
                GetOrCreate(screen);
                enteringScreens.Add(screen);
            }

            RequestFocus(focusToRestore);

            // Capa 1: instantáneo. Nada que esperar, así que nunca queda ocupado ni bloqueado.
            if (instantTransitions)
            {
                CurrentFocus()?.Blur();
                for (int i = 0; i < leavingScreens.Count; i++)
                {
                    HideNow(instances[leavingScreens[i]]);
                    leavingScreens[i].Hide();
                }
                for (int i = 0; i < enteringScreens.Count; i++)
                {
                    enteringScreens[i].Show();
                    ShowNow(instances[enteringScreens[i]]);
                }
                enteringScreens.Clear();
                leavingScreens.Clear();
                return;
            }

            IsBusy = true;
            deadline = Time.unscaledTime + completionTimeout;
            Block();   // desde acá, clics, teclado y foco van al bloqueador

            for (int i = 0; i < leavingScreens.Count; i++)
            {
                VisualElement element = instances[leavingScreens[i]];
                element.RemoveFromClassList(VisibleClass);
                element.AddToClassList(LeavingClass);
                animating.Add(element);
            }

            for (int i = 0; i < enteringScreens.Count; i++)
            {
                enteringScreens[i].Show();   // está por aparecer: todavía invisible
                VisualElement element = instances[enteringScreens[i]];
                element.RemoveFromClassList(LeavingClass);
                element.style.display = DisplayStyle.Flex;   // paso 1: se maqueta con opacity 0
                pendingReveal.Add(element);                   // pasos 2 y 3: en RevealEnteringScreens
                animating.Add(element);
            }

            if (animating.Count == 0) CompleteTransition();
        }

        /// <summary>
        /// Agrega --visible recién cuando el motor completó una pasada de estilos con la
        /// pantalla visible y opacity 0: ese estilo calculado es el punto de partida de la transición.
        /// Verificación: con Log Transitions activo debe aparecer "Run opacity".
        /// </summary>
        private void RevealEnteringScreens()
        {
            for (int i = pendingReveal.Count - 1; i >= 0; i--)
            {
                VisualElement element = pendingReveal[i];
                if (element.resolvedStyle.display != DisplayStyle.Flex) continue;
                element.AddToClassList(VisibleClass);
                pendingReveal.RemoveAt(i);
            }
        }

        // Capa 2. Solo cuenta la opacity de la propia caja, no las transiciones de sus hijos.
        private void OnTransitionRun(TransitionRunEvent evt)
        {
            if (logTransitions && IsOwnOpacity(evt.target, evt.currentTarget, evt.stylePropertyNames))
                Log("Run", evt.currentTarget);
        }

        private void OnTransitionEnd(TransitionEndEvent evt)
        {
            if (!IsOwnOpacity(evt.target, evt.currentTarget, evt.stylePropertyNames)) return;
            if (logTransitions) Log("End", evt.currentTarget);
            FinishOne((VisualElement)evt.currentTarget);
        }

        // Una cancelación no significa "llegó", sino "no va a seguir": el estado final
        // lo imponen las clases en CompleteTransition, no este evento.
        private void OnTransitionCancel(TransitionCancelEvent evt)
        {
            if (!IsOwnOpacity(evt.target, evt.currentTarget, evt.stylePropertyNames)) return;
            if (logTransitions) Log("Cancel", evt.currentTarget);
            FinishOne((VisualElement)evt.currentTarget);
        }

        private static bool IsOwnOpacity(IEventHandler target, IEventHandler currentTarget, StylePropertyNameCollection names) =>
            target == currentTarget && names.Contains(OpacityProperty);

        private void FinishOne(VisualElement element)
        {
            // Solo vale si pertenece a ESTA transición y ya recibió su clase de destino.
            if (!IsBusy || !animating.Contains(element)) return;
            if (!element.ClassListContains(VisibleClass) && !element.ClassListContains(LeavingClass)) return;

            animating.Remove(element);
            if (animating.Count == 0) CompleteTransition();
        }

        private void CompleteTransition()
        {
            for (int i = 0; i < leavingScreens.Count; i++)
            {
                if (instances.TryGetValue(leavingScreens[i], out VisualElement element)) HideNow(element);
                leavingScreens[i].Hide();   // OnHide: ya quedó oculta
            }

            // Solo si el plazo venció antes de poder revelarlas: el estado final lo imponen las clases.
            for (int i = 0; i < pendingReveal.Count; i++) pendingReveal[i].AddToClassList(VisibleClass);

            enteringScreens.Clear();
            leavingScreens.Clear();
            pendingReveal.Clear();
            animating.Clear();
            IsBusy = false;
            Unblock();
        }

        // ─────────────────────────── Bloqueo y foco ───────────────────────────

        private void Block()
        {
            blocker.pickingMode = PickingMode.Position;   // captura los clics
            blocker.focusable = true;
            blocker.BringToFront();
            blocker.Focus();                               // sumidero de foco: el teclado no llega a ningún botón
        }

        private void Unblock()
        {
            if (blocker == null) return;
            blocker.Blur();
            blocker.focusable = false;
            blocker.pickingMode = PickingMode.Ignore;
        }

        // Durante una transición, flechas y Tab no deben mover el foco a una pantalla transparente.
        private void OnBlockedNavigation(NavigationMoveEvent evt)
        {
            evt.StopPropagation();
            blocker.focusController?.IgnoreEvent(evt);   // anula la navegación por defecto
        }

        private void RequestFocus(Focusable preferred)
        {
            preferredFocus = preferred;
            focusPending = true;
        }

        private void ApplyPendingFocus()
        {
            if (!focusPending || IsBusy || Top == null) return;   // el foco llega al TERMINAR
            if (!instances.TryGetValue(Top, out VisualElement topElement)) return;
            if (topElement.resolvedStyle.display != DisplayStyle.Flex) return;   // canGrabFocus exige un estilo resuelto

            focusPending = false;
            Focusable target = preferredFocus;
            preferredFocus = null;

            if (!CanFocus(target)) target = Top.GetInitialFocus();
            if (!CanFocus(target)) target = FindFirstFocusable(topElement);
            target?.Focus();
        }

        private static bool CanFocus(Focusable focusable) =>
            focusable is VisualElement element && element.panel != null && element.canGrabFocus;

        private static Focusable FindFirstFocusable(VisualElement element)
        {
            if (element.focusable && element.canGrabFocus) return element;
            for (int i = 0; i < element.hierarchy.childCount; i++)
            {
                Focusable found = FindFirstFocusable(element.hierarchy[i]);
                if (found != null) return found;
            }
            return null;
        }

        private Focusable CurrentFocus() => host?.focusController?.focusedElement;

        private void OnNavigationCancel(NavigationCancelEvent evt)
        {
            if (!IsOpen) return;   // en la raíz, Cancel no hace nada
            evt.StopPropagation();
            Back();                // si hay una transición en curso, Back la ignora
        }

        // ─────────────────────────── Enlace con el árbol ───────────────────────────

        private bool TryBind()
        {
            VisualElement found = document.rootVisualElement?.Q<VisualElement>(hostName);
            if (found == null || found.panel == null)
            {
                if (++bindAttempts == 120)
                    Debug.LogError($"ScreenStack: no encuentro '#{hostName}' en el UXML de '{name}'.", this);
                return false;
            }

            bindAttempts = 0;
            host = found;
            bound = true;
            host.Clear();   // #screen-host pertenece al stack: lo que traiga el UXML es solo vista previa

            eventRoot = host.panel.visualTree;   // raíz del panel: recibe Cancel aunque nada tenga el foco
            eventRoot.RegisterCallback<NavigationCancelEvent>(OnNavigationCancel);

            blocker = new VisualElement
            {
                name = "nav-blocker",
                pickingMode = PickingMode.Ignore,
                focusable = false,
                tabIndex = -1   // nunca se llega a él con Tab
            };
            blocker.AddToClassList(BlockerClass);
            blocker.RegisterCallback<NavigationMoveEvent>(OnBlockedNavigation);
            host.Add(blocker);

            ApplyHostClasses();

            // Reconstruye la vista del estado actual, sin animación, de abajo hacia arriba.
            ComputeVisible(visibleAfter);
            for (int i = visibleAfter.Count - 1; i >= 0; i--)
            {
                MenuScreen screen = visibleAfter[i];
                VisualElement element = GetOrCreate(screen);
                screen.Show();
                ShowNow(element);
            }
            RequestFocus(null);
            return true;
        }

        private void Unbind()
        {
            if (!bound) return;
            bound = false;

            eventRoot?.UnregisterCallback<NavigationCancelEvent>(OnNavigationCancel);
            eventRoot = null;

            // Cada Show recibe su Hide, y cada Build su Unbind.
            for (int i = 0; i < leavingScreens.Count; i++) leavingScreens[i].Hide();
            ComputeVisible(visibleBefore);
            for (int i = 0; i < visibleBefore.Count; i++) visibleBefore[i].Hide();

            foreach (KeyValuePair<MenuScreen, VisualElement> pair in instances)
            {
                pair.Key.Unbind();
                pair.Value.RemoveFromHierarchy();
            }
            instances.Clear();

            blocker?.RemoveFromHierarchy();
            blocker = null;

            enteringScreens.Clear();
            leavingScreens.Clear();
            pendingReveal.Clear();
            animating.Clear();
            IsBusy = false;
            focusPending = false;
            host = null;
        }

        private VisualElement GetOrCreate(MenuScreen screen)
        {
            if (instances.TryGetValue(screen, out VisualElement element)) return element;

            element = screen.View.Instantiate();   // TemplateContainer: la "caja" de la pantalla
            element.name = "nav-" + screen.Id;
            element.AddToClassList(ScreenClass);   // sin esta clase la caja mide 0×0
            element.style.display = DisplayStyle.None;
            element.RegisterCallback<TransitionRunEvent>(OnTransitionRun);
            element.RegisterCallback<TransitionEndEvent>(OnTransitionEnd);
            element.RegisterCallback<TransitionCancelEvent>(OnTransitionCancel);
            host.Add(element);
            instances.Add(screen, element);        // se conserva: la próxima apertura no instancia

            screen.Build(this, element);
            return element;
        }

        private static void ShowNow(VisualElement element)
        {
            element.RemoveFromClassList(LeavingClass);
            element.style.display = DisplayStyle.Flex;
            element.AddToClassList(VisibleClass);
        }

        private static void HideNow(VisualElement element)
        {
            element.RemoveFromClassList(VisibleClass);
            element.RemoveFromClassList(LeavingClass);
            element.style.display = DisplayStyle.None;
        }

        /// <summary>De arriba hacia abajo, hasta la primera pantalla Replace inclusive.</summary>
        private void ComputeVisible(List<MenuScreen> result)
        {
            result.Clear();
            for (int i = stack.Count - 1; i >= 0; i--)
            {
                result.Add(stack[i]);
                if (stack[i].Backdrop == ScreenBackdrop.Replace) break;
            }
        }

        private void ApplyHostClasses()
        {
            if (host == null) return;
            host.EnableInClassList(InstantClass, instantTransitions);
            host.EnableInClassList(ReducedClass, reducedMotion && !instantTransitions);
        }

        private void Log(string kind, IEventHandler handler) =>
            Debug.Log($"ScreenStack: {kind} opacity en {((VisualElement)handler).name} (t = {Time.unscaledTime:F2})", this);
    }
}