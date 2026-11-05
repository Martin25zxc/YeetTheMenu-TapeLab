using System;
using System.Collections.Generic;
using UnityEngine;

namespace YeetTheMenu.Settings
{
    /// <summary>
    /// Dueño de las preferencias del jugador: valores actuales, validación, persistencia y aviso de cambios.
    /// No sabe qué significa ningún valor (S14): aplicarlos es trabajo de quien escuche Changed.
    /// Uno por escena, no singleton (S18): el almacén de verdad es PlayerPrefs; esto es su caché validada.
    /// Guarda el valor de toda opción que tiene fila (Available y Demo): si se ve y se mueve, se recuerda,
    /// aunque Demo no lo aplique a ningún sistema (S1). Solo Hidden queda sin valor, porque ni se construye.
    /// </summary>
    [DisallowMultipleComponent]
    public class GameSettings : MonoBehaviour
    {
        private const string KeyPrefix = "settings.";   // separa nuestras claves de cualquier otra en PlayerPrefs

        [Tooltip("Qué opciones existen en este proyecto.")]
        [SerializeField] private SettingsCatalog catalog;

        private readonly Dictionary<string, SettingOption> tracked = new();
        private readonly Dictionary<string, int> values = new();
        private bool loaded;
        private bool dirty;   // hay cambios en memoria que todavía no están en disco

        /// <summary>Un valor cambió de verdad: (id, valor ya validado). No se dispara al cargar.</summary>
        public event Action<string, int> Changed;

        public SettingsCatalog Catalog => catalog;

        /// <summary>
        /// Toda opción con fila y con valor guardado: Available y Demo. Available y Demo se distinguen
        /// por Mode; quien aplica (SettingsApplier) filtra por eso, no este servicio.
        /// </summary>
        public IEnumerable<SettingOption> TrackedOptions
        {
            get
            {
                EnsureLoaded();
                return tracked.Values;
            }
        }

        private void Awake() => EnsureLoaded();

        // Red de seguridad: salir del juego o de la escena guarda lo pendiente (S3).
        private void OnApplicationQuit() => Save();
        private void OnDestroy() => Save();

        public bool TryGetValue(string id, out int value)
        {
            EnsureLoaded();
            return values.TryGetValue(id, out value);
        }

        /// <summary>
        /// Pide un valor nuevo. Devuelve el que quedó, que puede diferir si el pedido estaba
        /// fuera de rango: el dueño del valor tiene la última palabra (S29).
        /// </summary>
        public int SetValue(string id, int value)
        {
            EnsureLoaded();

            if (!tracked.TryGetValue(id, out SettingOption option))
            {
                Debug.LogWarning($"GameSettings: '{id}' no es una opción con fila (¿Hidden, o no está en el catálogo?); no se guarda.", this);
                return value;
            }

            int valid = option.Clamp(value);
            if (values[id] == valid) return valid;   // sin cambio real: ni aviso ni guardado

            values[id] = valid;
            dirty = true;
            Changed?.Invoke(id, valid);
            return valid;
        }

        /// <summary>Escribe a disco lo pendiente. No hace nada si no hubo cambios.</summary>
        public void Save()
        {
            if (!dirty) return;

            foreach (KeyValuePair<string, int> pair in values)
                PlayerPrefs.SetInt(KeyPrefix + pair.Key, pair.Value);

            PlayerPrefs.Save();   // lo caro: en momentos definidos, nunca mientras se arrastra un slider
            dirty = false;
        }

        // Carga perezosa: funciona aunque alguien pregunte antes de que corra este Awake.
        private void EnsureLoaded()
        {
            if (loaded) return;
            loaded = true;

            if (catalog == null)
            {
                Debug.LogError("GameSettings: falta el catálogo.", this);
                return;
            }

            foreach (SettingOption option in catalog.AllOptions)
            {
                if (option.Mode == SettingMode.Hidden) continue;        // Hidden no se construye: sin fila, sin valor
                if (string.IsNullOrWhiteSpace(option.Id)) continue;     // el catálogo ya avisa
                if (!tracked.TryAdd(option.Id, option)) continue;       // id repetido: el catálogo ya avisa

                int stored = PlayerPrefs.GetInt(KeyPrefix + option.Id, option.DefaultValue);
                int valid = option.Clamp(stored);

                // Un valor guardado puede quedar inválido si el catálogo cambió (por ejemplo, otro rango).
                if (valid != stored)
                {
                    Debug.LogWarning($"GameSettings: '{option.Id}' tenía {stored} guardado, fuera de {option.Min}–{option.Max}; se usa {valid}.", this);
                    dirty = true;
                }

                values[option.Id] = valid;
            }
        }
    }
}
