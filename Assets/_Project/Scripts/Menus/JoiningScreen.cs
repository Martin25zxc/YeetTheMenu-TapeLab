using System;
using System.Threading;
using UnityEngine;
using UnityEngine.UIElements;
using YeetTheMenu.Online;
using YeetTheMenu.UI;

namespace YeetTheMenu.Menus
{
    /// <summary>
    /// "JOINING HAUNT...": ejecuta el intento de unión mientras la pantalla está abierta.
    /// El intento pertenece a la NAVEGACIÓN (OnShow/OnHide), no a la vista:
    /// reconstruir el árbol no lo reinicia ni lo cancela.
    /// </summary>
    public class JoiningScreen : MenuScreen
    {
        [Tooltip("Componente que implementa ILobbyService. En el laboratorio: FakeLobbyService.")]
        [SerializeField] private MonoBehaviour lobbyService;

        [Tooltip("Tiempo mínimo visible (s), para que la pantalla no parpadee si la respuesta llega enseguida.")]
        [SerializeField, Min(0f)] private float minimumVisibleSeconds = 0.6f;

        private Button cancelButton;
        private string pendingCode;
        private Action<LobbyJoinError> onFailed;
        private CancellationTokenSource attempt;   // UN token por intento: cancelar uno no afecta al siguiente

        private ILobbyService Lobby => lobbyService as ILobbyService;

        /// <summary>Lo llama quien abre esta pantalla, justo antes del Push.</summary>
        public void Begin(string code, Action<LobbyJoinError> failed)
        {
            pendingCode = code;
            onFailed = failed;
        }

        private void OnValidate()
        {
            // Unity no serializa interfaces: se guarda un MonoBehaviour y se comprueba que la implemente.
            if (lobbyService != null && lobbyService is not ILobbyService)
            {
                Debug.LogError($"JoiningScreen: '{lobbyService.GetType().Name}' no implementa ILobbyService.", this);
                lobbyService = null;
            }
        }

        // Ciclo de la VISTA: solo reconectar el botón. No toca el intento en curso.
        protected override void OnBuild(VisualElement root) =>
            cancelButton = BindClick(root, "btn-cancel", OnCancelClicked);

        protected override void OnUnbind() => cancelButton = null;

        protected override Focusable InitialFocus => cancelButton;

        // Ciclo de NAVEGACIÓN: entrar arranca el intento; salir lo cancela.
        protected override void OnShow() => RunJoin();

        // Acción urgente: se cancela YA. Devolver false deja que el stack haga su Pop habitual.
        protected override bool OnBackRequested()
        {
            CancelAttempt();
            return false;
        }

        // Red de seguridad: ninguna operación sobrevive a la salida de su pantalla (PopToRoot, etc.).
        protected override void OnHide() => CancelAttempt();

        private void OnCancelClicked() => Stack.Back();

        private void CancelAttempt()
        {
            attempt?.Cancel();   // el intento cancelado libera su propio token al terminar (finally)
            attempt = null;
        }

        /// <summary>
        /// async void solo como punto de entrada: todo lo que puede fallar está dentro de un try.
        /// Reglas: la cancelación que PEDIMOS nunca navega; cualquier otro final vuelve a una pantalla útil.
        /// </summary>
        private async void RunJoin()
        {
            CancelAttempt();

            // Se cancela si alguien lo pide o si este objeto se destruye (por ejemplo, al salir de Play).
            CancellationTokenSource source = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
            attempt = source;
            CancellationToken token = source.Token;
            float shownAt = Time.unscaledTime;

            try
            {
                LobbyJoinResult result;
                ILobbyService lobby = Lobby;
                string code = pendingCode;

                if (lobby == null || string.IsNullOrEmpty(code))
                {
                    // Error de configuración: se avisa al desarrollador y la UI se recupera como ante cualquier fallo.
                    Debug.LogError("JoiningScreen: falta el servicio de lobby o el código (¿se llamó a Begin?).", this);
                    result = LobbyJoinResult.Failure(LobbyJoinError.Unknown);
                }
                else
                {
                    try
                    {
                        result = await lobby.JoinAsync(code, token);
                    }
                    catch (OperationCanceledException) when (!token.IsCancellationRequested)
                    {
                        // Una cancelación que NO pedimos (típicamente, un timeout interno del SDK) es un fallo de red.
                        result = LobbyJoinResult.Failure(LobbyJoinError.Network);
                    }
                    catch (Exception exception) when (exception is not OperationCanceledException)
                    {
                        // Un error inesperado del servicio no puede dejar la UI colgada en "Joining".
                        Debug.LogException(exception, this);
                        result = LobbyJoinResult.Failure(LobbyJoinError.Unknown);
                    }
                }

                // Al menos un frame de distancia, y solo cuando el stack puede navegar:
                // enlazado a su árbol (la UI podría estar desactivada), sin transición en curso
                // y después del tiempo mínimo visible.
                do
                {
                    await Awaitable.NextFrameAsync(token);
                }
                while (!Stack.IsBound || Stack.IsBusy || Time.unscaledTime - shownAt < minimumVisibleSeconds);

                // Si llegamos acá, nadie canceló: Joining sigue arriba de la pila.
                if (result.Succeeded)
                {
                    Debug.Log($"JoiningScreen: unido a {result.LobbyId}. En el juego real, acá se carga la partida.", this);
                    Stack.PopToRoot();
                }
                else
                {
                    onFailed?.Invoke(result.Error);   // Join prepara el mensaje mientras está oculta
                    Stack.Pop();
                }
            }
            catch (OperationCanceledException)
            {
                // Solo llega acá la cancelación que pedimos nosotros: el resultado ya no le importa a nadie.
            }
            finally
            {
                if (attempt == source) attempt = null;
                source.Dispose();
            }
        }
    }
}