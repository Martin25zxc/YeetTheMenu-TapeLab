using System.Collections.Generic;
using UnityEngine.UIElements;

namespace YeetTheMenu.UI
{
    /// <summary>
    /// Choice compacto: una sola parada de foco, selección por índice y menú nativo
    /// estilizado por instancia. No agrega estilos al panel ni al Theme Style Sheet.
    /// </summary>
    public sealed class ChoiceMenuField : Button, INotifyValueChanged<int>
    {
        public const string MainClass = "choice-menu-field";
        private const string ArrowClass = "choice-menu-field__arrow";
        private const string PopupClass = "ytm-choice-popup";

        private readonly List<string> choices = new();
        private int index;

        public StyleSheet PopupStyleSheet { get; set; }
        public IReadOnlyList<string> Choices => choices;

        public ChoiceMenuField()
        {
            AddToClassList(MainClass);
            // Button es el único elemento enfocable. El indicador no captura puntero ni foco.
            var arrow = new Label("▾") { pickingMode = PickingMode.Ignore, focusable = false };
            arrow.AddToClassList(ArrowClass);
            Add(arrow);
            clicked += OpenMenu;
            Refresh();
        }

        // El contrato es el mismo que el del antiguo CyclerField: un índice entero.
        public int value
        {
            get => index;
            set
            {
                int valid = Clamp(value);
                if (valid == index) return;

                int old = index;
                SetValueWithoutNotify(valid);

                using ChangeEvent<int> change = ChangeEvent<int>.GetPooled(old, valid);
                change.target = this;
                SendEvent(change);
            }
        }

        public void SetValueWithoutNotify(int newValue)
        {
            index = Clamp(newValue);
            Refresh();
        }

        public void SetChoices(IReadOnlyList<string> newChoices)
        {
            choices.Clear();
            if (newChoices != null)
                for (int i = 0; i < newChoices.Count; i++) choices.Add(newChoices[i]);
            SetValueWithoutNotify(index);
        }

        private int Clamp(int candidate)
        {
            if (choices.Count == 0) return 0;
            if (candidate < 0) return 0;
            if (candidate >= choices.Count) return choices.Count - 1;
            return candidate;
        }

        private void Refresh()
        {
            text = choices.Count > 0 ? choices[index] : string.Empty;
            SetEnabled(choices.Count > 0);
        }

        private void OpenMenu()
        {
            if (panel == null || choices.Count == 0) return;

            var menu = new GenericDropdownMenu();
            for (int i = 0; i < choices.Count; i++)
            {
                int selected = i; // cada callback conserva su índice
                menu.AddItem(choices[i], i == index, () => value = selected);
            }

            // contentContainer pertenece a un árbol que Unity ya creó, aunque todavía
            // no lo haya agregado al panel. Subimos hasta la raíz DE ESTA instancia.
            VisualElement popupRoot = menu.contentContainer;
            while (popupRoot.parent != null) popupRoot = popupRoot.parent;
            popupRoot.AddToClassList(PopupClass);
            if (PopupStyleSheet != null) popupRoot.styleSheets.Add(PopupStyleSheet);

            // La interacción vuelve al mismo selector después de elegir o cancelar.
            menu.onClose += () =>
            {
                if (panel != null && canGrabFocus) Focus();
            };

            // Fixed: ancho del popup = ancho del botón. No se expande a todo el panel.
            menu.DropDown(worldBound, this, DropdownMenuSizeMode.Fixed);
        }
    }
}
