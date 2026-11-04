using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace YeetTheMenu.Online
{
    /// <summary>Por qué falló un intento de unión. None = no falló.</summary>
    public enum LobbyJoinError
    {
        None,
        InvalidCode,   // formato incorrecto
        NotFound,      // no existe un lobby con ese código
        Full,          // el lobby está lleno
        Network,       // no se pudo hablar con el servidor (incluye timeouts)
        Unknown        // cualquier otra cosa
    }

    /// <summary>Resultado de un intento de unión. Los fallos esperables son datos, no excepciones.</summary>
    public readonly struct LobbyJoinResult
    {
        public LobbyJoinError Error { get; }
        public string LobbyId { get; }
        public bool Succeeded => Error == LobbyJoinError.None;

        private LobbyJoinResult(LobbyJoinError error, string lobbyId)
        {
            Error = error;
            LobbyId = lobbyId;
        }

        public static LobbyJoinResult Success(string lobbyId) => new(LobbyJoinError.None, lobbyId);

        // Un "fallo sin error" no tiene sentido: se convierte en Unknown en lugar de pasar por éxito.
        public static LobbyJoinResult Failure(LobbyJoinError error) =>
            new(error == LobbyJoinError.None ? LobbyJoinError.Unknown : error, null);

        public override string ToString() => Succeeded ? $"OK ({LobbyId})" : Error.ToString();
    }

    /// <summary>
    /// Contrato entre la UI y el sistema de lobbies. La UI solo conoce esta interfaz.
    /// Toda implementación, falsa o real, cumple estas reglas:
    ///  1. Los fallos esperables (código inexistente, lobby lleno, red) se DEVUELVEN; no se lanzan.
    ///  2. Si se pide cancelación: interrumpe la operación cuando sea posible, lanza
    ///     OperationCanceledException y compensa lo que haya alcanzado a ocurrir
    ///     (por ejemplo, abandonar un lobby al que ya se había unido). No siempre es instantáneo.
    ///  3. No lanza OperationCanceledException por motivos propios: un timeout interno es un
    ///     fallo y se devuelve como Network.
    ///  4. Valida el formato aunque la UI ya lo haya hecho: la UI valida para ayudar; el servicio, porque manda.
    /// Si un SDK trabaja con callbacks en lugar de Task, el adaptador usa TaskCompletionSource.
    /// </summary>
    public interface ILobbyService
    {
        Task<LobbyJoinResult> JoinAsync(string code, CancellationToken cancellationToken);
    }

    /// <summary>Formato de los códigos de lobby. Lo usan la UI y el servicio: una sola fuente de verdad.</summary>
    public static class LobbyCode
    {
        public const int Length = 6;

        /// <summary>
        /// Deja solo lo que puede formar parte de un código: letras A–Z y dígitos 0–9, en mayúsculas.
        /// Quita espacios, guiones y cualquier otro símbolo. Nunca recorta: un código largo se ve completo.
        /// </summary>
        public static string Normalize(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return string.Empty;

            var builder = new StringBuilder(raw.Length);
            foreach (char c in raw)
            {
                char upper = char.ToUpperInvariant(c);   // sin depender del idioma del sistema
                if ((upper >= 'A' && upper <= 'Z') || (upper >= '0' && upper <= '9'))
                    builder.Append(upper);
            }
            return builder.ToString();
        }

        /// <summary>Exactamente 6 letras A–Z o dígitos 0–9: la misma regla que el mock.</summary>
        public static bool IsWellFormed(string code)
        {
            if (code == null || code.Length != Length) return false;

            for (int i = 0; i < code.Length; i++)
            {
                char c = code[i];
                bool letter = (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');
                bool digit = c >= '0' && c <= '9';
                if (!letter && !digit) return false;   // char.IsLetterOrDigit aceptaría 'Ñ' o 'á'
            }
            return true;
        }
    }
}