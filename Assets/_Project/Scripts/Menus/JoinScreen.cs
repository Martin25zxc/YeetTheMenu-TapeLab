using UnityEngine;
using UnityEngine.UIElements;
using YeetTheMenu.Online;
using YeetTheMenu.UI;

namespace YeetTheMenu.Menus
{
    /// <summary>
    /// Pide el código, lo valida y abre Joining. Muestra la ayuda, los errores de formato y los de conexión.
    /// El ESTADO (código y mensaje) vive en campos: la vista es desechable y OnBuild la repinta.
    /// </summary>
    public class JoinScreen : MenuScreen
    {
        private static readonly string HintText = $"{LobbyCode.Length} letters or numbers";
        private const string HintClass = "code-error--hint";
        private const int MaxInput = 20;   // tope de la UI: más que esto se rechaza, nunca se recorta

        [Tooltip("Pantalla que ejecuta el intento de unión.")]
        [SerializeField] private JoiningScreen joining;

        // ESTADO: sobrevive a la recreación de la vista.
        private string enteredCode = string.Empty;
        private string errorMessage;

        // VISTA: se reemplaza si el árbol se recrea.
        private TextField codeField;
        private Label errorLabel;
        
        protected override void OnBuild(VisualElement root)
        {
            codeField = root.Q<TextField>("code-field");
            errorLabel = root.Q<Label>("code-error");
            if (codeField == null || errorLabel == null)
            {
                Debug.LogError("JoinScreen: faltan 'code-field' o 'code-error' en el UXML.", this);
                return;
            }

            // Sin maxLength a propósito: recortaría en silencio un código pegado demasiado largo.
            codeField.SetValueWithoutNotify(enteredCode);   // repinta el estado en la vista nueva
            codeField.RegisterValueChangedCallback(OnCodeChanged);
            // TrickleDown: el TextField no deja subir el Enter (NavigationSubmitEvent); hay que verlo en la bajada.
            codeField.RegisterCallback<NavigationSubmitEvent>(OnCodeSubmit, TrickleDown.TrickleDown);

            BindClick(root, "btn-paste", OnPaste);
            BindClick(root, "btn-confirm", Submit);
            BindClick(root, "btn-back", OnBackClicked);

            ShowError(errorMessage);   // repinta el mensaje, o la ayuda si no hay error
        }

        protected override void OnUnbind()
        {
            if (codeField != null)
            {
                codeField.UnregisterValueChangedCallback(OnCodeChanged);
                codeField.UnregisterCallback<NavigationSubmitEvent>(OnCodeSubmit, TrickleDown.TrickleDown);
            }
            codeField = null;
            errorLabel = null;
        }

        protected override Focusable InitialFocus => codeField;

        // Salió de la navegación: el error del intento anterior deja de tener sentido.
        // No se limpia en OnShow: al volver de un fallo, el mensaje ya está puesto y debe verse.
        protected override void OnHide() => ShowError(null);

        private void OnCodeChanged(ChangeEvent<string> evt)
        {
            string normalized = LobbyCode.Normalize(evt.newValue);
            if (normalized.Length > MaxInput)
            {
                // Se rechaza entero y se explica. El campo vuelve a lo que había.
                codeField.SetValueWithoutNotify(enteredCode);
                ShowError($"That's too long to be a lobby code. Codes have {LobbyCode.Length} letters or numbers.");
                return;
            }

            // Una TECLA que pasa del límite se rechaza, como en los juegos con códigos de sala.
            // Un PEGADO nunca se recorta: si sobra, se ve completo y se explica.
            if (normalized.Length > LobbyCode.Length && IsOneCharInserted(enteredCode, normalized))
                normalized = enteredCode;

            SetCode(normalized);
        }

        private void OnCodeSubmit(NavigationSubmitEvent evt) => Submit();   // Enter dentro del campo

        private void OnPaste()
        {
            string pasted = LobbyCode.Normalize(GUIUtility.systemCopyBuffer);
            if (pasted.Length == 0)
            {
                ShowError("The clipboard doesn't contain a code.");
                return;
            }
            SetCode(pasted);   // el botón sabe que es un pegado: nunca se rechaza ni se recorta
        }

        /// <summary>Único lugar donde cambia el código: actualiza estado, vista y mensaje juntos.</summary>
        private void SetCode(string code)
        {
            if (codeField != null && codeField.value != code) codeField.SetValueWithoutNotify(code);
            enteredCode = code;

            // Escribir borra el error, como el mock, salvo que el código siga siendo demasiado largo.
            ShowError(code.Length > LobbyCode.Length ? LengthMessage(code.Length) : null);
        }

        private void Submit()
        {
            // Después de Normalize solo quedan caracteres válidos: lo único que puede fallar es la longitud.
            if (!LobbyCode.IsWellFormed(enteredCode))
            {
                ShowError(LengthMessage(enteredCode.Length));
                return;
            }
            if (joining == null)
            {
                Debug.LogError("JoinScreen: falta asignar la JoiningScreen.", this);
                return;
            }

            joining.Begin(enteredCode, OnJoinFailed);   // solo guarda datos: el intento arranca cuando Joining se muestra
            Stack.Push(joining.Id);
        }

        private void OnJoinFailed(LobbyJoinError error) => ShowError(MessageFor(error));

        private void OnBackClicked() => Stack.Back();

        private void ShowError(string message)
        {
            errorMessage = message;   // estado
            if (errorLabel == null) return;

            // La línea reservada nunca queda vacía: sin error muestra la ayuda, tenue.
            bool hasError = !string.IsNullOrEmpty(message);
            errorLabel.text = hasError ? message : HintText;
            errorLabel.EnableInClassList(HintClass, !hasError);
        }

        /// <summary>true si 'next' es 'previous' con exactamente un carácter insertado: lo que hace una tecla.</summary>
        private static bool IsOneCharInserted(string previous, string next)
        {
            if (next.Length != previous.Length + 1) return false;

            int i = 0;
            while (i < previous.Length && previous[i] == next[i]) i++;   // primer punto de diferencia

            // Desde ahí, el resto de 'next' tiene que ser el resto de 'previous', corrido un lugar.
            return string.CompareOrdinal(previous, i, next, i + 1, previous.Length - i) == 0;
        }

        private static string LengthMessage(int length) =>
            length == 0
                ? $"Enter the lobby code: {LobbyCode.Length} letters or numbers."
                : $"Codes have {LobbyCode.Length} letters or numbers. This one has {length}.";

        private static string MessageFor(LobbyJoinError error) => error switch
        {
            LobbyJoinError.InvalidCode => "Invalid code.",
            LobbyJoinError.NotFound    => "No lobby found with that code.",
            LobbyJoinError.Full        => "That lobby is full.",
            LobbyJoinError.Network     => "Connection failed. Check your network and try again.",
            _                          => "Something went wrong. Try again.",
        };
    }
}