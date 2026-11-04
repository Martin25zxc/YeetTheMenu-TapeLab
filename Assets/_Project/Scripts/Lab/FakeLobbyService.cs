using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using YeetTheMenu.Online;

namespace YeetTheMenu.Lab
{
    /// <summary>
    /// Servicio de lobby de mentira para el laboratorio: tarda, y a veces falla A PROPÓSITO.
    /// Si nunca fallara, la UI de error no existiría. No migra: el juego real trae su implementación.
    /// </summary>
    public class FakeLobbyService : MonoBehaviour, ILobbyService
    {
        private static readonly LobbyJoinError[] RandomFailures =
            { LobbyJoinError.NotFound, LobbyJoinError.Full, LobbyJoinError.Network };

        [Tooltip("Demora simulada de la red, en segundos. El mock usaba 3.")]
        [SerializeField, Min(0f)] private float delaySeconds = 3f;

        [Tooltip("Probabilidad de fallar cuando no se fuerza un resultado.")]
        [SerializeField, Range(0f, 1f)] private float failureChance = 0.3f;

        [Tooltip("None = decide al azar con Failure Chance. Cualquier otro valor fuerza ese resultado (para pruebas).")]
        [SerializeField] private LobbyJoinError forcedError = LobbyJoinError.None;

        [Tooltip("Registra cada intento en la consola, con su número.")]
        [SerializeField] private bool logRequests = true;

        private int requestCount;

        public async Task<LobbyJoinResult> JoinAsync(string code, CancellationToken cancellationToken)
        {
            int request = ++requestCount;

            // Regla 3: el servicio valida aunque la UI ya lo haya hecho.
            if (!LobbyCode.IsWellFormed(code))
                return Report(request, code, LobbyJoinResult.Failure(LobbyJoinError.InvalidCode));

             try
            {
                // Tiempo REAL: una red no se detiene porque el juego tenga timeScale en 0
                // (Awaitable.WaitForSecondsAsync usa tiempo escalado). Sin ConfigureAwait(false):
                // la continuación tiene que volver al hilo principal.
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                if (logRequests) Debug.Log($"FakeLobbyService: #{request} {code} → cancelado", this);
                throw;   // regla 2: la cancelación se informa con la excepción
            }

            return Report(request, code, Decide(code));
        }

        private LobbyJoinResult Decide(string code)
        {
            LobbyJoinError error = forcedError;
            if (error == LobbyJoinError.None && UnityEngine.Random.value < failureChance)
                error = RandomFailures[UnityEngine.Random.Range(0, RandomFailures.Length)];

            return error == LobbyJoinError.None
                ? LobbyJoinResult.Success("HAUNT-" + code)
                : LobbyJoinResult.Failure(error);
        }

        private LobbyJoinResult Report(int request, string code, LobbyJoinResult result)
        {
            if (logRequests) Debug.Log($"FakeLobbyService: #{request} {code} → {result}", this);
            return result;
        }

        // Prueba del servicio solo, antes de que exista la UI.
        [ContextMenu("Probar: unirse con ABC123")]
        private async void TestJoin()
        {
            if (!Application.isPlaying)
            {
                Debug.Log("FakeLobbyService: la prueba funciona en Play.", this);
                return;
            }

            try
            {
                await JoinAsync("ABC123", destroyCancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Se destruyó el objeto durante la prueba: no hay nada que hacer.
            }
        }
    }
}