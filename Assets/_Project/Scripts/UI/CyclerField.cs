using System.Collections.Generic;
using UnityEngine.UIElements;

namespace YeetTheMenu.UI
{
    /// <summary>
    /// Elige entre alternativas con dos flechas:  ‹ High ›
    ///
    /// Es UNA sola parada de foco. Las flechas se pueden clicar pero no son enfocables, así que
    /// con teclado o mando hay un solo elemento por fila e izquierda/derecha cambian el valor,
    /// igual que en un slider. Arriba y abajo no se consumen: siguen cambiando de fila.
    ///
    /// No da la vuelta en los extremos: la flecha que ya no sirve queda deshabilitada, lo que
    /// explica sin texto por qué no responde. Pasar de "Off" a "High" con una pulsación
    /// sorprende, sobre todo con mando, donde no se ve venir.
    ///
    /// Implementa INotifyValueChanged&lt;int&gt; para tener la misma superficie que SliderInt y
    /// Toggle: value, SetValueWithoutNotify y RegisterValueChangedCallback. Quien lo use no
    /// necesita saber que es un control propio.
    /// </summary>
    public class CyclerField : VisualElement, INotifyValueChanged<int>
    {
        public const string MainClass = "cycler";
        public const string ArrowClass = "cycler__arrow";
        public const string ValueClass = "cycler__value";

        private readonly Button previousButton;
        private readonly Button nextButton;
        private readonly Label valueLabel;
        private readonly List<string> choices = new();

        private int index;

        public CyclerField()
        {
            AddToClassList(MainClass);

            // Lo que hace que sea una sola parada: el contenedor es enfocable y los hijos no.
            focusable = true;

            previousButton = MakeArrow("‹", -1);
            nextButton = MakeArrow("›", +1);

            // pickingMode Ignore: el texto no se come el clic que debería llegar al ciclador.
            valueLabel = new Label { pickingMode = PickingMode.Ignore };
            valueLabel.AddToClassList(ValueClass);

            Add(previousButton);
            Add(valueLabel);
            Add(nextButton);

            RegisterCallback<NavigationMoveEvent>(OnNavigationMove);
        }

        /// <summary>Las alternativas visibles, en orden. El valor es el índice dentro de esta lista.</summary>
        public IReadOnlyList<string> Choices => choices;

        // ── INotifyValueChanged<int> ──
        // El nombre en minúscula no es un descuido: es el de la interfaz de UI Toolkit.

        public int value
        {
            get => index;
            set
            {
                int valid = Clamp(value);
                if (valid == index) return;

                int before = index;
                SetValueWithoutNotify(valid);

                // Así es como un control avisa un cambio: un ChangeEvent<T> del pool.
                // Es el mismo evento que escucha RegisterValueChangedCallback.
                using ChangeEvent<int> changed = ChangeEvent<int>.GetPooled(before, valid);
                changed.target = this;
                SendEvent(changed);
            }
        }

        /// <summary>Pinta un valor sin avisar. Es lo que se usa al construir o al corregir.</summary>
        public void SetValueWithoutNotify(int newValue)
        {
            index = Clamp(newValue);
            Refresh();
        }

        /// <summary>Define las alternativas. Conserva el índice actual si sigue siendo válido.</summary>
        public void SetChoices(IReadOnlyList<string> newChoices)
        {
            choices.Clear();
            if (newChoices != null) choices.AddRange(newChoices);
            SetValueWithoutNotify(index);   // reajusta y repinta con la lista nueva
        }

        // ── Interacción ──

        private Button MakeArrow(string glyph, int step)
        {
            // focusable = false es la pieza clave: se clica, pero el foco se queda en el ciclador.
            var button = new Button(() => Step(step)) { text = glyph, focusable = false };
            button.AddToClassList(ArrowClass);
            return button;
        }

        private void Step(int delta)
        {
            int target = Clamp(index + delta);
            if (target == index) return;   // en un extremo: no hay vuelta y no se avisa nada
            value = target;                // pasa por el setter, que notifica
        }

        private void OnNavigationMove(NavigationMoveEvent evt)
        {
            int delta = evt.direction switch
            {
                NavigationMoveEvent.Direction.Left => -1,
                NavigationMoveEvent.Direction.Right => +1,
                _ => 0
            };

            // Arriba, abajo, Tab y Shift+Tab se dejan pasar: cambiar de fila es su trabajo.
            if (delta == 0) return;

            Step(delta);

            // Dos pasos, no uno: StopPropagation corta el recorrido del evento, e IgnoreEvent
            // le dice al controlador de foco que tampoco mueva el foco por su cuenta.
            evt.StopPropagation();
            focusController?.IgnoreEvent(evt);
        }

        private int Clamp(int candidate)
        {
            int last = choices.Count - 1;
            if (last < 0) return 0;
            if (candidate < 0) return 0;
            return candidate > last ? last : candidate;
        }

        private void Refresh()
        {
            valueLabel.text = choices.Count > 0 ? choices[index] : string.Empty;

            // La flecha muerta se deshabilita: el USS la apaga y el clic deja de hacer nada.
            previousButton.SetEnabled(index > 0);
            nextButton.SetEnabled(index < choices.Count - 1);
        }
    }
}
