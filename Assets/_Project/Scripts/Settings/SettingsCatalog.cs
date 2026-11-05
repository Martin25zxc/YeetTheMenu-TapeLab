using System.Collections.Generic;
using UnityEngine;

namespace YeetTheMenu.Settings
{
    /// <summary>Una pestaña de Settings y sus opciones, en el orden en que se muestran.</summary>
    public sealed class SettingsTab
    {
        public string Id { get; }
        public string Label { get; }
        public IReadOnlyList<SettingOption> Options { get; }   // de solo lectura hacia afuera: nadie de afuera puede agregar ni quitar opciones

        public SettingsTab(string id, string label, IReadOnlyList<SettingOption> options)
        {
            Id = id;
            Label = label;
            Options = options ?? System.Array.Empty<SettingOption>();
        }
    }

    /// <summary>
    /// Qué opciones existen en este proyecto, agrupadas en pestañas.
    /// Es el punto de enganche: la escena apunta a un asset, pero el contenido lo escribe
    /// una subclase en código, donde se ve entero, se diffea y un id mal escrito no compila.
    /// Otro proyecto usa el mismo código con su propia subclase: eso es lo que lo hace adaptable.
    /// Ningún script lo escribe en ejecución.
    /// </summary>
    public abstract class SettingsCatalog : ScriptableObject
    {
        private IReadOnlyList<SettingsTab> tabs;

        /// <summary>Se construye una vez y se reusa. Cambia solo al recompilar, porque es código.</summary>
        public IReadOnlyList<SettingsTab> Tabs => tabs ??= Validate(Build());

        /// <summary>Acá se escribe el catálogo. Ver TapeLabCatalog.</summary>
        protected abstract IReadOnlyList<SettingsTab> Build();

        /// <summary>Todas las opciones de todas las pestañas, en orden.</summary>
        public IEnumerable<SettingOption> AllOptions
        {
            get
            {
                foreach (SettingsTab tab in Tabs)
                    foreach (SettingOption option in tab.Options)
                        yield return option;
            }
        }

        public bool TryGetTab(string tabId, out SettingsTab tab)
        {
            foreach (SettingsTab candidate in Tabs)
            {
                if (candidate.Id == tabId)
                {
                    tab = candidate;
                    return true;
                }
            }

            tab = null;
            return false;
        }

        // ── Ayudas para escribir un catálogo legible ──
        // Están acá, en la base, para que cualquier catálogo las tenga sin repetirlas.

        protected static SettingsTab Tab(string id, string label, params SettingOption[] options) =>
            new(id, label, options);

        protected static SettingOption Slider(string id, string label, int min, int max, int defaultValue,
                                              string description, SettingMode mode = SettingMode.Available) =>
            new(id, label, SettingKind.Slider, mode, min, max, defaultValue, description);

        protected static SettingOption Toggle(string id, string label, bool on,
                                              string description, SettingMode mode = SettingMode.Available) =>
            new(id, label, SettingKind.Toggle, mode, 0, 1, on ? 1 : 0, description);

        protected static SettingOption Choice(string id, string label, string[] choices, int defaultIndex,
                                              string description, SettingMode mode = SettingMode.Available) =>
            new(id, label, SettingKind.Choice, mode, 0, 0, defaultIndex, description, choices);

        // El id es la clave de guardado: dos opciones con el mismo id se pisarían.
        // Esta comprobación reemplaza al OnValidate del asset.
        private IReadOnlyList<SettingsTab> Validate(IReadOnlyList<SettingsTab> built)
        {
            var tabIds = new HashSet<string>();
            var optionIds = new HashSet<string>();

            foreach (SettingsTab tab in built)
            {
                if (string.IsNullOrWhiteSpace(tab.Id) || !tabIds.Add(tab.Id))
                    Debug.LogError($"{name}: la pestaña '{tab.Label}' no tiene id o lo repite.", this);

                foreach (SettingOption option in tab.Options)
                {
                    if (!string.IsNullOrWhiteSpace(option.Id) && !optionIds.Add(option.Id))
                        Debug.LogError($"{name}: id repetido '{option.Id}'. Tiene que ser único.", this);
                }
            }

            return built;
        }
    }
}
