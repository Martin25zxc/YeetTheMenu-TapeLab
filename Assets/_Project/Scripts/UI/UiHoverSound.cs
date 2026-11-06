using UnityEngine;
using UnityEngine.UIElements;

namespace YeetTheMenu.UI
{
    /// <summary>
    /// El único sonido del menú: una nota al entrar en un elemento interactivo.
    ///
    /// Con el puntero suena al entrar en un Button. Ese filtro no es arbitrario: en el mock la
    /// clase .h está en los botones del menú, las pestañas y el "Back", y no en las filas, los
    /// sliders ni los desplegables. En Unity ese conjunto es, exactamente, "ser un Button".
    ///
    /// Con teclado o gamepad no hay hover, así que el equivalente de pasar por encima es recibir
    /// el foco. Ahí sí suena cualquier control, porque con mando un slider se recorre igual que
    /// un botón. Esa asimetría es a propósito.
    ///
    /// No sabe qué es un ajuste ni qué volumen hay: el volumen lo pone el AudioMixer al que sale
    /// el AudioSource, y quien lo escribe es SettingsApplier, en la capa que se ajusta al migrar.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    [DisallowMultipleComponent]
    public class UiHoverSound : MonoBehaviour
    {
        private const float PointerStillSqr = 4f;      // 2 px: el mismo umbral que usa UiInputMode
        private const float RecentMoveSeconds = 0.1f;  // un hover cuenta como del jugador si el mouse se movió hace menos de esto

        [Header("Sonido")]
        [Tooltip("Opcional. Sin AudioSource el menú se navega igual, en silencio.")]
        [SerializeField] private AudioSource source;

        [Tooltip("Las cinco notas. Se elige una al azar en cada hover, como en el mock.")]
        [SerializeField] private AudioClip[] clips;

        [Header("Cuándo suena")]
        [Tooltip("Con teclado o gamepad, suena también al mover el foco. Si molesta, se apaga acá.")]
        [SerializeField] private bool playOnDirectionalFocus = true;

        [Tooltip("Segundos antes de que el MISMO botón vuelva a sonar con el puntero. El mock usa 2,6.")]
        [SerializeField, Min(0f)] private float sameElementLockout = 2.6f;

        [Tooltip("Segundos mínimos entre dos notas, sea cual sea el elemento. Evita el barro al recorrer una lista con el mando.")]
        [SerializeField, Min(0f)] private float minimumInterval = 0.15f;

        [Tooltip("Registra en consola cada evento que llega, cada nota y cada hover descartado. Para verificar, no para dejarlo activo.")]
        [SerializeField] private bool logEvents;

        private UIDocument document;
        private VisualElement eventRoot;      // raíz del panel: ve todos los eventos

        private Vector2 lastPointerPosition;
        private bool hasPointer;              // ¿el jugador movió el mouse alguna vez?
        private float lastRealMoveTime = float.NegativeInfinity;   // cuándo se movió el mouse DE VERDAD
        private VisualElement lastPointerElement;
        private float lastPointerElementTime = float.NegativeInfinity;
        private float lastPlayTime = float.NegativeInfinity;
        private int navigationFrame = -1;     // frame del último NavigationMove del jugador
        private bool warned;

        private void Awake() => document = GetComponent<UIDocument>();

        private void OnDisable() => Unbind();

        private void Update()
        {
            // Solo vigila que el árbol no se haya recreado. No consulta ningún dispositivo.
            if (eventRoot != null && eventRoot.panel != null) return;
            Unbind();
            TryBind();
        }

        // ── Enlace con el árbol ──

        private void TryBind()
        {
            VisualElement root = document.rootVisualElement;
            if (root?.panel == null) return;

            eventRoot = root.panel.visualTree;

            // TrickleDown: los eventos se ven antes de que un control los consuma, y un solo
            // registro en la raíz alcanza para todo el panel. Así no hay que volver a suscribirse
            // cada vez que ScreenStack instancia una pantalla o SettingsScreen rehace las filas.
            eventRoot.RegisterCallback<PointerMoveEvent>(OnPointerMove, TrickleDown.TrickleDown);
            eventRoot.RegisterCallback<PointerEnterEvent>(OnPointerEnter, TrickleDown.TrickleDown);
            eventRoot.RegisterCallback<NavigationMoveEvent>(OnNavigationMove, TrickleDown.TrickleDown);
            eventRoot.RegisterCallback<FocusInEvent>(OnFocusIn, TrickleDown.TrickleDown);

            // Hasta que el jugador no mueva el mouse, ningún hover cuenta como suyo.
            hasPointer = false;
            lastRealMoveTime = float.NegativeInfinity;
            lastPointerElement = null;
        }

        private void Unbind()
        {
            if (eventRoot == null) return;

            eventRoot.UnregisterCallback<PointerMoveEvent>(OnPointerMove, TrickleDown.TrickleDown);
            eventRoot.UnregisterCallback<PointerEnterEvent>(OnPointerEnter, TrickleDown.TrickleDown);
            eventRoot.UnregisterCallback<NavigationMoveEvent>(OnNavigationMove, TrickleDown.TrickleDown);
            eventRoot.UnregisterCallback<FocusInEvent>(OnFocusIn, TrickleDown.TrickleDown);

            eventRoot = null;
            lastPointerElement = null;
        }

        // ── Puntero ──

        private void OnPointerMove(PointerMoveEvent evt)
        {
            Vector2 position = evt.position;

            // Menos de 2 px desde la última posición registrada: o es ruido del mouse, o es la UI
            // que se movió debajo y Unity avisa con un movimiento en el mismo lugar (UiInputMode
            // ya se apoya en esto). En los dos casos el jugador no movió el mouse.
            if (hasPointer && (position - lastPointerPosition).sqrMagnitude < PointerStillSqr) return;

            lastPointerPosition = position;
            lastRealMoveTime = Time.unscaledTime;
            hasPointer = true;
        }

        private void OnPointerEnter(PointerEnterEvent evt)
        {
            if (evt.target is not VisualElement target) return;
            if (logEvents) Debug.Log($"UiHoverSound: PointerEnter → {target.GetType().Name} '{target.name}'", this);

            // El botón mismo, o el texto que lleva adentro: en los dos casos es "un Button".
            Button button = target as Button ?? target.GetFirstAncestorOfType<Button>();
            if (button == null) return;

            // ¿Lo provocó el jugador? Un glitch de cinta (GlitchShift desplaza #root) o un cambio
            // de pantalla meten un botón nuevo debajo de un cursor QUE ESTÁ QUIETO: no hubo ningún
            // movimiento real del mouse en los últimos instantes.
            //
            // Se mira CUÁNDO fue el último movimiento real, no DÓNDE. La posición del hover y la
            // del último movimiento son la misma en los dos casos, así que no distingue nada; el
            // tiempo sí, y no depende de si Unity despacha primero la entrada o primero el
            // movimiento.
            float sinceMove = Time.unscaledTime - lastRealMoveTime;
            if (sinceMove > RecentMoveSeconds)
            {
                if (logEvents) Debug.Log($"UiHoverSound: descartado '{button.name}', el mouse no se movió ({sinceMove:F2} s).", this);
                return;
            }

            // Entrar y salir del mismo botón no lo vuelve a disparar enseguida. Es el bloqueo del
            // mock, pero sin diccionario: alcanza con recordar el último elemento. También frena
            // el segundo evento de la misma entrada (primero el botón, enseguida su texto).
            if (button == lastPointerElement && Time.unscaledTime - lastPointerElementTime < sameElementLockout) return;

            lastPointerElement = button;
            lastPointerElementTime = Time.unscaledTime;

            Play(button.name);
        }

        // ── Teclado y gamepad ──

        private void OnNavigationMove(NavigationMoveEvent evt)
        {
            // Solo se anota el frame, no se reproduce acá. Si el foco no llega a moverse —porque
            // UiInputMode se comió la primera pulsación para revelar el foco, o porque el
            // nav-blocker la detuvo durante una transición— no hay FocusIn y no suena nada.
            navigationFrame = Time.frameCount;
        }

        private void OnFocusIn(FocusInEvent evt)
        {
            if (!playOnDirectionalFocus) return;

            // El foco también lo mueve el código: ScreenStack al abrir una pantalla, SettingsScreen
            // al cambiar de pestaña. Suena solo el foco que movió el jugador, en el mismo frame que
            // su pulsación. Un clic de mouse tampoco suena: no genera NavigationMove.
            if (Time.frameCount != navigationFrame) return;

            if (evt.target is not VisualElement element) return;

            Play(element.name);
        }

        // ── Reproducción ──

        private void Play(string what)
        {
            if (source == null || clips == null || clips.Length == 0)
            {
                if (!warned)
                {
                    Debug.LogWarning("UiHoverSound: falta el AudioSource o los clips; el menú queda en silencio.", this);
                    warned = true;
                }
                return;
            }

            // Las notas duran 2,5 s y se superponen a propósito: la escala pentatónica menor hace
            // que varias juntas suenen como un acorde. Lo único que hay que evitar es la
            // ametralladora al recorrer una lista con el mando.
            float now = Time.unscaledTime;   // sin escalar: la pausa del juego no tiene que afectar al menú
            if (now - lastPlayTime < minimumInterval) return;
            lastPlayTime = now;

            AudioClip clip = clips[Random.Range(0, clips.Length)];
            if (clip == null) return;

            source.PlayOneShot(clip);   // PlayOneShot mezcla varias notas sobre la misma fuente
            if (logEvents) Debug.Log($"UiHoverSound: {clip.name} en '{what}'", this);
        }
    }
}