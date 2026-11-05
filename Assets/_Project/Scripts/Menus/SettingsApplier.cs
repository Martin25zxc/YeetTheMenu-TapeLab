using UnityEngine;
using YeetTheMenu.Fx;
using YeetTheMenu.Settings;

namespace YeetTheMenu.Menus
{
    /// <summary>
    /// Traduce preferencias a los sistemas de ESTE proyecto. GameSettings no sabe qué significa
    /// "retro-filter"; este componente sí. Es la pieza que cambia al migrar.
    /// Referencias opcionales: si falta un sistema, sus opciones se omiten.
    /// Los ids los toma de SettingIds, la misma constante que usa el catálogo.
    /// </summary>
    [DisallowMultipleComponent]
    public class SettingsApplier : MonoBehaviour
    {
        [SerializeField] private GameSettings settings;

        [Tooltip("Opcional. Sin él, Retro filter no se aplica.")]
        [SerializeField] private RetroFxController retroFx;

        private void OnEnable()
        {
            if (settings != null) settings.Changed += Apply;
        }

        private void OnDisable()
        {
            if (settings != null) settings.Changed -= Apply;
        }

        // Start, no Awake: para entonces ya corrieron todos los Awake de la escena.
        private void Start()
        {
            if (settings == null)
            {
                Debug.LogError("SettingsApplier: falta GameSettings.", this);
                return;
            }
            if (retroFx == null)
                Debug.LogWarning("SettingsApplier: sin RetroFxController, Retro filter no se aplica.", this);

            // Changed no se dispara al cargar: el estado inicial se aplica una vez, acá.
            // Solo Available: Demo se trackea y persiste en GameSettings, pero nunca llega a ningún sistema.
            foreach (SettingOption option in settings.TrackedOptions)
            {
                if (option.Mode != SettingMode.Available) continue;
                if (!settings.TryGetValue(option.Id, out int value)) continue;
                if (!TryApply(option.Id, value))
                    Debug.LogWarning($"SettingsApplier: '{option.Id}' es Available pero nadie la aplica. ¿Id mal escrito?", this);
            }
        }

        private void Apply(string id, int value) => TryApply(id, value);

        /// <summary>true si el id es parte del vocabulario, aunque falte su sistema.</summary>
        private bool TryApply(string id, int value)
        {
            switch (id)
            {
                case SettingIds.RetroFilter:
                    if (retroFx != null) retroFx.ScreenEffectsIntensity = value / 100f;   // S17: el catálogo guarda %, el sistema 0–1
                    return true;

                default:
                    return false;
            }
        }
    }
}
