using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using YeetTheMenu.Settings;
using YeetTheMenu.UI;

namespace YeetTheMenu.Menus
{
    /// <summary>
    /// Vista de las preferencias. No guarda nada: lee y escribe a través de GameSettings.
    /// Qué pestañas y filas hay lo dice el catálogo; cómo se ve una fila, SettingsRow.uxml.
    ///
    /// Esta pantalla no conoce ninguna opción en particular. Sabe dibujar los tres tipos de
    /// control y sabe leer el modo de cada fila; nada más. Agregar una opción es escribir una
    /// línea en el catálogo, y acá no se toca nada.
    /// </summary>
    public class SettingsScreen : MenuScreen
    {
        private const string TabClass = "settings-tab";
        private const string TabOnClass = "settings-tab--on";
        private const string RowFocusClass = "settings-row--focus";
        private const string RowDemoClass = "settings-row--demo";
        private const string SliderClass = "settings-slider";
        private const string SliderValueClass = "settings-slider__value";
        private const string ToggleClass = "settings-toggle";
        private const string ChoiceClass = "settings-choice";

        [Tooltip("Dueño de los valores. El catálogo también sale de acá, para que la vista y el dueño nunca miren catálogos distintos.")]
        [SerializeField] private GameSettings settings;

        [Tooltip("Plantilla de una fila: etiqueta, insignia y hueco para el control.")]
        [SerializeField] private VisualTreeAsset rowTemplate;

        [Tooltip("USS exclusivo del popup abierto: se asigna a cada GenericDropdownMenu, NO al panel.")]
        [SerializeField] private StyleSheet choicePopupStyleSheet;

        // Estado que tiene que sobrevivir a que se recree la vista: vive en campos, y la
        // reconstrucción lo vuelve a pintar. Recordar la pestaña durante la sesión alcanza;
        // no se guarda en disco.
        private string activeTabId;

        private VisualElement tabsBar;
        private ScrollView rows;
        private Label description;
        private Button resetButton;

        private VisualElement focusedRow;
        private SettingOption focusedOption;   // la fila que tiene el foco
        private SettingOption hoveredOption;   // la fila bajo el puntero

        protected override void OnBuild(VisualElement root)
        {
            BindClick(root, "btn-back", OnBackClicked);
            resetButton = BindClick(root, "btn-reset", OnResetClicked);

            tabsBar = root.Q("settings-tabs");
            rows = root.Q<ScrollView>("settings-rows");
            description = root.Q<Label>("settings-desc");

            if (tabsBar == null || rows == null || description == null)
            {
                Debug.LogError("SettingsScreen: el UXML necesita 'settings-tabs', un ScrollView 'settings-rows' y un Label 'settings-desc'.", this);
                return;
            }
            if (settings == null || settings.Catalog == null || rowTemplate == null)
            {
                Debug.LogError("SettingsScreen: faltan GameSettings, su catálogo o la plantilla de fila.", this);
                return;
            }

            // UNA sola suscripción para todas las filas: los eventos de foco burbujean, así que
            // el contenedor los ve. Registrar algo por fila sería repetir 23 veces lo mismo.
            rows.RegisterCallback<FocusInEvent>(OnRowFocusIn);
            rows.RegisterCallback<FocusOutEvent>(OnRowFocusOut);

            BuildTabs();
            ShowTab(ResolveTabId());
        }

        protected override void OnUnbind()
        {
            if (rows != null)
            {
                rows.UnregisterCallback<FocusInEvent>(OnRowFocusIn);
                rows.UnregisterCallback<FocusOutEvent>(OnRowFocusOut);
            }

            tabsBar = null;
            rows = null;
            description = null;
            resetButton = null;

            // Punteros a elementos que ya no existen. La pestaña activa NO se borra: es lo único
            // que tiene que sobrevivir para que la vista se repinte igual.
            focusedRow = null;
            focusedOption = null;
            hoveredOption = null;
        }

        // Los cambios ya están en memoria; a disco se escriben acá, una sola vez y con la pantalla
        // ya oculta: guardar en cada movimiento de un slider produce parones perceptibles.
        protected override void OnHide()
        {
            if (settings != null) settings.Save();   // no "settings?.Save()": en objetos de Unity, ?. saltea la comprobación de destruido
        }

        private void OnBackClicked() => Stack.Back();

        // ── Pestañas ──

        private void BuildTabs()
        {
            tabsBar.Clear();

            foreach (SettingsTab tab in settings.Catalog.Tabs)
            {
                string id = tab.Id;
                var button = new Button(() => ShowTab(id)) { text = Upper(tab.Label) };
                button.AddToClassList(TabClass);
                button.userData = id;   // de qué pestaña es este botón, sin comparar textos
                tabsBar.Add(button);
            }
        }

        private string ResolveTabId()
        {
            // La que estaba abierta, si sigue existiendo; si no, la primera del catálogo.
            if (!string.IsNullOrEmpty(activeTabId) && settings.Catalog.TryGetTab(activeTabId, out _))
                return activeTabId;

            return settings.Catalog.Tabs.Count > 0 ? settings.Catalog.Tabs[0].Id : null;
        }

        private void ShowTab(string id)
        {
            if (id == null || !settings.Catalog.TryGetTab(id, out SettingsTab tab))
            {
                Debug.LogWarning($"SettingsScreen: el catálogo no tiene la pestaña '{id}'.", this);
                return;
            }

            activeTabId = id;

            // Cada botón se mira a sí mismo: ninguno sabe de los demás.
            foreach (VisualElement button in tabsBar.Children())
                button.EnableInClassList(TabOnClass, (button.userData as string) == id);

            if (resetButton != null) resetButton.text = $"RESET {Upper(tab.Label)}";

            BuildRows(tab);
        }

        private void FocusActiveTab()
        {
            foreach (VisualElement button in tabsBar.Children())
            {
                if ((button.userData as string) != activeTabId) continue;
                button.Focus();
                return;
            }
        }

        // USS no tiene text-transform, así que las mayúsculas se hacen acá.
        private static string Upper(string text) => text == null ? string.Empty : text.ToUpperInvariant();

        // ── Filas ──

        private void BuildRows(SettingsTab tab)
        {
            // Si el foco estaba en una fila, esa fila está por desaparecer. Hay que devolverlo
            // a algo que siga existiendo, o queda en la nada y el teclado deja de responder.
            bool focusWasInList = rows.focusController?.focusedElement is VisualElement focused
                                  && (focused == rows || rows.Contains(focused));

            ClearFocusedRow();
            hoveredOption = null;

            rows.Clear();   // se lleva también las filas de muestra del UXML, que solo existen para diseñar

            foreach (SettingOption option in tab.Options)
            {
                if (option.Mode == SettingMode.Hidden) continue;   // lo oculto ni se construye: sin fila, sin foco y sin costo

                VisualElement row = InstantiateRow();
                if (row == null) return;

                Label label = row.Q<Label>("row-label");
                VisualElement slot = row.Q("row-control");
                VisualElement demoTag = row.Q("row-demo");

                if (label == null || slot == null)
                {
                    Debug.LogError("SettingsScreen: la plantilla de fila necesita 'row-label' y 'row-control'.", this);
                    return;
                }

                label.text = option.Label;

                // La insignia no se esconde: en una fila que sí se aplica, se saca del árbol.
                if (option.Mode == SettingMode.Demo) row.AddToClassList(RowDemoClass);
                else demoTag?.RemoveFromHierarchy();

                if (!TryFillControl(option, slot)) continue;   // sin control, la fila no se agrega

                // De la fila a su opción. Es lo que usan el foco, el puntero y la descripción,
                // y lo que hace que no haga falta un diccionario aparte.
                row.userData = option;
                row.RegisterCallback<PointerEnterEvent>(_ => SetHovered(option));
                row.RegisterCallback<PointerLeaveEvent>(_ => ClearHovered(option));

                rows.Add(row);
            }

            RefreshDescription();
            if (focusWasInList) FocusActiveTab();
        }

        private VisualElement InstantiateRow()
        {
            // Instantiate() devuelve un TemplateContainer que envuelve la plantilla. Sacamos la
            // fila de adentro y descartamos la envoltura, para que el padre de cada fila sea el
            // contenido del scroll y nada más: así el USS no tiene que esquivar una capa extra.
            TemplateContainer container = rowTemplate.Instantiate();
            VisualElement row = container.Q("row");

            if (row == null)
            {
                Debug.LogError("SettingsScreen: la plantilla de fila necesita un elemento llamado 'row'.", this);
                return null;
            }

            row.RemoveFromHierarchy();
            return row;
        }

        private bool TryFillControl(SettingOption option, VisualElement slot)
        {
            switch (option.Kind)
            {
                case SettingKind.Slider:
                    FillSlider(option, slot);
                    return true;

                case SettingKind.Toggle:
                    FillToggle(option, slot);
                    return true;

                case SettingKind.Choice:
                    FillChoice(option, slot);
                    return true;

                default:
                    Debug.LogWarning($"SettingsScreen: '{option.Id}' es {option.Kind} y no hay control para ese tipo.", this);
                    return false;
            }
        }

        /// <summary>
        /// Slider más el número, de SOLO LECTURA. Nada de showInputField: un campo de texto en una
        /// fila de Settings es una trampa de foco con mando, y obliga a una política para texto no
        /// numérico, vacío y fuera de rango. Nadie necesita tipear un 73 exacto en Retro filter.
        /// </summary>
        private void FillSlider(SettingOption option, VisualElement slot)
        {
            int current = ReadValue(option);

            var slider = new SliderInt(option.Min, option.Max);
            slider.AddToClassList(SliderClass);

            var value = new Label(current.ToString()) { pickingMode = PickingMode.Ignore };   // no roba foco ni clics
            value.AddToClassList(SliderValueClass);

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

        private void FillToggle(SettingOption option, VisualElement slot)
        {
            bool current = ReadValue(option) != 0;

            var toggle = new Toggle();
            toggle.AddToClassList(ToggleClass);
            toggle.SetValueWithoutNotify(current);

            toggle.RegisterValueChangedCallback(evt =>
            {
                int stored = settings.SetValue(option.Id, evt.newValue ? 1 : 0);
                if ((stored != 0) != evt.newValue) toggle.SetValueWithoutNotify(stored != 0);
            });

            slot.Add(toggle);
        }

        /// <summary>
        /// Choice compacto y popup nativo con estilos locales a su propia instancia.
        /// El catálogo y GameSettings continúan usando índices enteros.
        /// </summary>
        private void FillChoice(SettingOption option, VisualElement slot)
        {
            // Copia local: funciona si Choices es string[] o IReadOnlyList<string>.
            var alternatives = option.Choices == null ? new List<string>() : new List<string>(option.Choices);
            if (alternatives.Count == 0)
            {
                Debug.LogWarning($"SettingsScreen: '{option.Id}' es Choice sin alternativas.", this);
                return;
            }

            var choice = new ChoiceMenuField();
            choice.AddToClassList(ChoiceClass);
            choice.PopupStyleSheet = choicePopupStyleSheet;
            choice.SetChoices(alternatives);
            choice.SetValueWithoutNotify(ReadValue(option));

            choice.RegisterCallback<ChangeEvent<int>>(evt =>
            {
                int stored = settings.SetValue(option.Id, evt.newValue);

                if (stored != evt.newValue)
                    choice.SetValueWithoutNotify(stored);
            });

            slot.Add(choice);
        }

        private int ReadValue(SettingOption option) =>
            settings.TryGetValue(option.Id, out int value) ? value : option.DefaultValue;

        // ── Foco, puntero y la línea de descripción ──

        private void OnRowFocusIn(FocusInEvent evt)
        {
            if (evt.target is not VisualElement target) return;

            VisualElement row = FindRow(target);
            if (row == null) return;

            if (focusedRow == row) return;

            focusedRow?.RemoveFromClassList(RowFocusClass);
            focusedRow = row;
            focusedRow.AddToClassList(RowFocusClass);

            focusedOption = row.userData as SettingOption;
            RefreshDescription();

            // Que el foco no se salga de la vista es requisito, no mejora: un menú con mando
            // donde el foco avanza fuera de pantalla está roto. Se desplaza a la FILA y no al
            // control, porque el foco cae en el control o en un hijo interno suyo, y moverse
            // hasta ahí puede dejar la etiqueta cortada arriba.
            rows.ScrollTo(row);
        }

        private void OnRowFocusOut(FocusOutEvent evt)
        {
            // Al pasar de una fila a otra llegan los dos eventos. Si el foco se queda dentro de
            // la lista no hay nada que apagar: el FocusIn que viene detrás se encarga.
            if (evt.relatedTarget is VisualElement next && FindRow(next) != null) return;

            ClearFocusedRow();
            RefreshDescription();
        }

        private void ClearFocusedRow()
        {
            focusedRow?.RemoveFromClassList(RowFocusClass);
            focusedRow = null;
            focusedOption = null;
        }

        private void SetHovered(SettingOption option)
        {
            hoveredOption = option;
            RefreshDescription();
        }

        private void ClearHovered(SettingOption option)
        {
            if (hoveredOption != option) return;   // el puntero ya entró en otra fila
            hoveredOption = null;
            RefreshDescription();
        }

        /// <summary>
        /// Con puntero manda el puntero; con teclado o mando, el foco. Es la misma regla que ya
        /// sigue el resto del menú, y resuelve el tooltip del diseño original, que solo funcionaba
        /// con mouse: acá la explicación también aparece navegando con mando.
        /// </summary>
        private void RefreshDescription()
        {
            if (description == null) return;

            SettingOption shown = hoveredOption ?? focusedOption;
            description.text = shown != null ? shown.Description : string.Empty;
        }

        /// <summary>De cualquier elemento a la fila que lo contiene. La marca es su userData.</summary>
        private VisualElement FindRow(VisualElement element)
        {
            while (element != null && element != rows)
            {
                if (element.userData is SettingOption) return element;
                element = element.parent;
            }

            return null;
        }

        // ── Restablecer ──

        private void OnResetClicked()
        {
            // El botón se conecta antes de comprobar el resto, así que acá se vuelve a comprobar:
            // si algo falta, el clic no tiene que tirar una excepción.
            if (settings == null || settings.Catalog == null) return;
            if (!settings.Catalog.TryGetTab(activeTabId, out SettingsTab tab)) return;

            // Solo la pestaña activa, y sin confirmación: es de bajo riesgo y se deshace a mano.
            // Un diálogo modal exigiría un sistema que el proyecto no tiene.
            foreach (SettingOption option in tab.Options)
            {
                if (option.Mode == SettingMode.Hidden) continue;   // sin fila, no hay valor que restablecer
                settings.SetValue(option.Id, option.DefaultValue); // cada uno avisa, y quien aplique aplica
            }

            ShowTab(activeTabId);   // repinta los controles con los valores nuevos
        }
    }
}
