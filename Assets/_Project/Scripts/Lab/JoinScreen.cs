using UnityEngine;
using UnityEngine.UIElements;
using YeetTheMenu.Online;
using YeetTheMenu.UI;

namespace YeetTheMenu.Lab
{
    /// <summary>
    /// Pide el código, lo valida y abre Joining. Muestra los errores de formato y de conexión.
    /// El ESTADO (código y error) vive en campos: la vista es desechable y OnBuild la repinta.
    /// </summary>
    public class JoinScreen : MenuScreen
    {
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

            // Sin maxLength a propósito: truncaría en silencio un código pegado demasiado largo.
            codeField.SetValueWithoutNotify(enteredCode);   // repinta el estado en la vista nueva
            codeField.RegisterValueChangedCallback(OnCodeChanged);
            // TrickleDown: el TextField no deja subir el Enter (NavigationSubmitEvent); hay que verlo en la bajada.
            codeField.RegisterCallback<NavigationSubmitEvent>(OnCodeSubmit, TrickleDown.TrickleDown);

            BindClick(root, "btn-paste", OnPaste);
            BindClick(root, "btn-confirm", Submit);
            BindClick(root, "btn-back", OnBackClicked);

            ShowError(errorMessage);   // repinta el error, si había uno
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
            // USS no tiene text-transform. Lo que se ve en el campo es exactamente lo que se valida.
            string normalized = LobbyCode.Normalize(evt.newValue);
            if (normalized != evt.newValue) codeField.SetValueWithoutNotify(normalized);

            enteredCode = normalized;
            ShowError(null);   // como el mock: escribir borra el error
        }

        private void OnCodeSubmit(NavigationSubmitEvent evt) => Submit();   // Enter dentro del campo

        private void OnPaste()
        {
            string pasted = LobbyCode.Normalize(GUIUtility.systemCopyBuffer);
            if (pasted.Length == 0)
            {
                ShowError("Clipboard is empty.");
                return;
            }
            codeField.value = pasted;   // con notificación: actualiza el estado y borra el error
        }

        private void Submit()
        {
            if (!LobbyCode.IsWellFormed(enteredCode))
            {
                ShowError(MessageFor(LobbyJoinError.InvalidCode));
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
            errorMessage = message;                                       // estado
            if (errorLabel != null) errorLabel.text = message ?? string.Empty;   // vista (espacio reservado en USS)
        }

        private static string MessageFor(LobbyJoinError error) => error switch
        {
            LobbyJoinError.InvalidCode => "Invalid code. Use exactly 6 letters or numbers, no spaces or symbols.",
            LobbyJoinError.NotFound    => "No lobby found with that code.",
            LobbyJoinError.Full        => "That lobby is full.",
            LobbyJoinError.Network     => "Connection failed. Check your network and try again.",
            _                          => "Something went wrong. Try again.",
        };
    }
}