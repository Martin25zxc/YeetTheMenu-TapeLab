using UnityEngine;
using YeetTheMenu.Settings;

namespace YeetTheMenu.Lab
{
    /// <summary>
    /// Herramienta de laboratorio: prueba GameSettings sin interfaz (etapa 1) y registra cada Changed.
    /// Se usa en Play desde el menú ⋮ del componente. Vive en Lab porque no existe en el juego real.
    /// </summary>
    public class SettingsProbe : MonoBehaviour
    {
        [SerializeField] private GameSettings settings;
        [SerializeField] private string id = "retro-filter";
        [SerializeField] private int value = 30;

        private void OnEnable()
        {
            if (settings != null) settings.Changed += OnChanged;
        }

        private void OnDisable()
        {
            if (settings != null) settings.Changed -= OnChanged;
        }

        private void OnChanged(string changedId, int newValue) =>
            Debug.Log($"[Probe] Changed: {changedId} = {newValue}", this);

        [ContextMenu("Set value")]
        private void SetValue()
        {
            if (!CanUse()) return;
            int stored = settings.SetValue(id, value);
            Debug.Log($"[Probe] '{id}': pedido {value}, quedó {stored}.", this);
        }

        [ContextMenu("Log all")]
        private void LogAll()
        {
            if (!CanUse()) return;
            foreach (SettingOption option in settings.TrackedOptions)
            {
                settings.TryGetValue(option.Id, out int current);
                Debug.Log($"[Probe] {option.Id} = {current} (default {option.DefaultValue}, rango {option.Min}–{option.Max}, {option.Mode})", this);
            }
        }

        [ContextMenu("Save")]
        private void Save()
        {
            if (!CanUse()) return;
            settings.Save();
            Debug.Log("[Probe] Save() llamado.", this);
        }

        private bool CanUse()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[Probe] Usala en Play.", this);
                return false;
            }
            if (settings == null)
            {
                Debug.LogWarning("[Probe] Falta asignar GameSettings.", this);
                return false;
            }
            return true;
        }
    }
}
