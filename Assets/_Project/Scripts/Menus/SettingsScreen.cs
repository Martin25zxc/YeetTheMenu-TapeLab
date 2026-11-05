using UnityEngine;
using UnityEngine.UIElements;
using YeetTheMenu.Settings;
using YeetTheMenu.UI;

namespace YeetTheMenu.Menus
{
    /// <summary>
    /// Vista de las preferencias. No guarda nada: lee y escribe a través de GameSettings.
    /// Qué filas hay lo dice el catálogo; cómo se ve una fila, SettingsRow.uxml.
    /// </summary>
    public class SettingsScreen : MenuScreen
    {
        private const string SliderClass = "settings-slider";
        private const string ValueClass = "settings-slider__value";

        [Tooltip("Dueño de los valores. El catálogo también sale de acá, para que la vista y el dueño nunca miren catálogos distintos.")]
        [SerializeField] private GameSettings settings;

        [Tooltip("Plantilla de una fila: etiqueta + hueco para el control.")]
        [SerializeField] private VisualTreeAsset rowTemplate;

        [Tooltip("Pestaña que se muestra. Provisorio: todavía no hay selector de pestañas.")]
        [SerializeField] private string tabId = "graphics";

        private ScrollView rows;

        protected override void OnBuild(VisualElement root)
        {
            BindClick(root, "btn-back", OnBackClicked);

            rows = root.Q<ScrollView>("settings-rows");
            if (rows == null || settings == null || rowTemplate == null)
            {
                Debug.LogError("SettingsScreen: faltan 'settings-rows' en el UXML, GameSettings o la plantilla de fila.", this);
                return;
            }

            BuildTab(tabId);
        }

        protected override void OnUnbind() => rows = null;

        // Los cambios ya están en memoria; a disco se escriben acá, una sola vez y con la pantalla
        // ya oculta: guardar en cada movimiento de un slider produce parones perceptibles.
        protected override void OnHide()
        {
            if (settings != null) settings.Save();   // no "settings?.Save()": en objetos de Unity, ?. saltea la comprobación de destruido
        }

        private void OnBackClicked() => Stack.Back();

        // ── Construcción desde datos ──

        private void BuildTab(string id)
        {
            rows.Clear();   // se lleva también la fila de muestra del UXML, que solo existe para diseñar

            if (settings.Catalog == null || !settings.Catalog.TryGetTab(id, out SettingsTab tab))
            {
                Debug.LogWarning($"SettingsScreen: el catálogo no tiene la pestaña '{id}'.", this);
                return;
            }

            foreach (SettingOption option in tab.Options)
            {
                if (option.Mode == SettingMode.Hidden) continue;   // lo oculto ni se construye: sin fila, sin foco y sin costo

                TemplateContainer row = rowTemplate.Instantiate();
                Label label = row.Q<Label>("row-label");
                VisualElement slot = row.Q("row-control");

                if (label == null || slot == null)
                {
                    Debug.LogError("SettingsScreen: la plantilla de fila necesita 'row-label' y 'row-control'.", this);
                    return;
                }

                label.text = option.Label;
                if (!TryFillControl(option, slot)) continue;   // sin control, no se agrega la fila

                rows.Add(row);
            }
        }

        private bool TryFillControl(SettingOption option, VisualElement slot)
        {
            switch (option.Kind)
            {
                case SettingKind.Slider:
                    FillSlider(option, slot);
                    return true;

                default:   // Toggle y Choice todavía no tienen control
                    Debug.LogWarning($"SettingsScreen: '{option.Id}' es {option.Kind}, que todavía no tiene control.", this);
                    return false;
            }
        }

        /// <summary>
        /// Slider + número de SOLO LECTURA. Nada de showInputField: un campo de texto en una fila
        /// de Settings es una trampa de foco con mando, y obliga a una política para texto no numérico.
        /// </summary>
        private void FillSlider(SettingOption option, VisualElement slot)
        {
            int current = ReadValue(option);

            var slider = new SliderInt(option.Min, option.Max);
            slider.AddToClassList(SliderClass);

            var value = new Label(current.ToString()) { pickingMode = PickingMode.Ignore };   // no roba foco ni clics
            value.AddToClassList(ValueClass);

            // Pintar el estado SIN notificar: abrir la pantalla no es cambiar una preferencia.
            slider.SetValueWithoutNotify(current);

            slider.RegisterValueChangedCallback(evt =>
            {
                int stored = settings.SetValue(option.Id, evt.newValue);
                if (stored != evt.newValue) slider.SetValueWithoutNotify(stored);   // el dueño corrigió el valor: la vista obedece
                value.text = stored.ToString();
            });

            slot.Add(slider);
            slot.Add(value);
        }

        private int ReadValue(SettingOption option) =>
            settings.TryGetValue(option.Id, out int value) ? value : option.DefaultValue;
    }
}
