using System;
using System.Collections.Generic;
using UnityEngine;

namespace YeetTheMenu.Settings
{
    /// <summary>Una pestaña de Settings y sus opciones, en el orden en que se muestran.</summary>
    [Serializable]
    public class SettingsTab
    {
        [Tooltip("Identificador estable (por ejemplo: graphics).")]
        [SerializeField] private string id;

        [Tooltip("Texto de la pestaña.")]
        [SerializeField] private string label;

        [SerializeField] private List<SettingOption> options = new();

        public string Id => id;
        public string Label => label;
        public IReadOnlyList<SettingOption> Options => options;   // de solo lectura hacia afuera (S13)
    }

    /// <summary>
    /// Qué opciones existen en este proyecto, agrupadas en pestañas.
    /// Es un asset de diseño: ningún script lo escribe en ejecución (S13).
    /// Otro proyecto usa el mismo código con su propio catálogo: eso es lo "adaptable" (S31).
    /// </summary>
    [CreateAssetMenu(menuName = "TapeLab/Settings Catalog", fileName = "SC_New")]
    public class SettingsCatalog : ScriptableObject
    {
        [SerializeField] private List<SettingsTab> tabs = new();

        public IReadOnlyList<SettingsTab> Tabs => tabs;

        /// <summary>Todas las opciones de todas las pestañas, en orden.</summary>
        public IEnumerable<SettingOption> AllOptions
        {
            get
            {
                foreach (SettingsTab tab in tabs)
                    foreach (SettingOption option in tab.Options)
                        yield return option;
            }
        }

        public bool TryGetTab(string tabId, out SettingsTab tab)
        {
            foreach (SettingsTab candidate in tabs)
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

        // Unity solo llama OnValidate en el Editor, al cargar o editar el asset.
        private void OnValidate()
        {
            var tabIds = new HashSet<string>();
            var optionIds = new HashSet<string>();

            foreach (SettingsTab tab in tabs)
            {
                if (string.IsNullOrWhiteSpace(tab.Id) || !tabIds.Add(tab.Id))
                    Debug.LogWarning($"{name}: la pestaña '{tab.Label}' no tiene id o lo repite.", this);

                foreach (SettingOption option in tab.Options)
                {
                    string problem = option.Validate();
                    if (problem != null)
                        Debug.LogWarning($"{name}: la opción '{option.Id}' ({tab.Id}) {problem}.", this);

                    // El id es la clave de guardado: dos opciones con el mismo id se pisarían.
                    if (!string.IsNullOrWhiteSpace(option.Id) && !optionIds.Add(option.Id))
                        Debug.LogWarning($"{name}: id repetido '{option.Id}'. Tiene que ser único.", this);
                }
            }
        }
    }
}
