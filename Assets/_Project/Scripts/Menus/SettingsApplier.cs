using UnityEngine;
using YeetTheMenu.Fx;
using YeetTheMenu.Settings;
using YeetTheMenu.UI;

namespace YeetTheMenu.Menus
{
    /// <summary>
    /// Traduce preferencias a los sistemas de ESTE proyecto. GameSettings no sabe qué significa
    /// "retro-filter"; este componente sí. Es la pieza que cambia al migrar, y es el único lugar
    /// con permiso para conocer las dos orillas: la preferencia y el sistema que la cumple.
    ///
    /// Referencias opcionales: si falta un sistema, sus opciones simplemente no se aplican y la
    /// pantalla sigue siendo navegable.
    /// </summary>
    [DisallowMultipleComponent]
    public class SettingsApplier : MonoBehaviour
    {
        [SerializeField] private GameSettings settings;

        [Tooltip("Opcional. Sin él no se aplican Retro filter ni Reduced effects.")]
        [SerializeField] private RetroFxController retroFx;

        [Tooltip("Opcional. Sin él, Reduced effects no acorta las transiciones del menú.")]
        [SerializeField] private ScreenStack stack;

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
                Debug.LogWarning("SettingsApplier: sin RetroFxController no se aplican Retro filter ni Reduced effects.", this);
            if (stack == null)
                Debug.LogWarning("SettingsApplier: sin ScreenStack, Reduced effects no acorta las transiciones.", this);

            // Changed no se dispara al cargar, así que el estado inicial se aplica una vez, acá.
            // Solo las aplicables: las demostrativas se guardan en GameSettings, pero no llegan
            // a ningún sistema. Esa es toda la diferencia entre los dos modos.
            foreach (SettingOption option in settings.TrackedOptions)
            {
                if (option.Mode != SettingMode.Available) continue;
                if (!settings.TryGetValue(option.Id, out int value)) continue;

                if (!TryApply(option.Id, value))
                    Debug.LogWarning($"SettingsApplier: '{option.Id}' es aplicable pero nadie la aplica. ¿Id mal escrito?", this);
            }
        }

        private void Apply(string id, int value) => TryApply(id, value);

        /// <summary>true si el id es parte del vocabulario, aunque falte su sistema.</summary>
        private bool TryApply(string id, int value)
        {
            switch (id)
            {
                case SettingIds.RetroFilter:
                    // El catálogo guarda porcentaje; el controlador espera 0–1.
                    if (retroFx != null) retroFx.ScreenEffectsIntensity = value / 100f;
                    return true;

                case SettingIds.DisplayMode:
                    Screen.fullScreenMode = ToFullScreenMode(value);
                    return true;

                case SettingIds.VSync:
                    // 1 = una imagen por refresco del monitor. Con esto activo Unity ignora
                    // Application.targetFrameRate, que es la razón por la que el límite de
                    // cuadros no se ofrece como opción aplicable todavía.
                    QualitySettings.vSyncCount = value != 0 ? 1 : 0;
                    return true;

                case SettingIds.ReducedEffects:
                    ApplyReducedEffects(value != 0);
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Un interruptor del jugador, dos destinos. Los escribe el mismo componente, que es
        /// además el único que escribe cada uno: el stack no conoce los efectos y los efectos
        /// no conocen la interfaz.
        ///
        /// Interponer un componente que leyera el estado de los efectos para deducir este mismo
        /// booleano no desacoplaría nada: entregaría exactamente el valor que entró.
        /// </summary>
        private void ApplyReducedEffects(bool on)
        {
            // Pone el movimiento en cero, y con el movimiento detenido el controlador además
            // apaga grano, glitch y barrido, porque una banda quieta parece una mancha.
            if (retroFx != null) retroFx.ReducedEffects = on;

            // Transiciones sin escala y más cortas: molesta el movimiento, no el cambio.
            if (stack != null) stack.ReducedMotion = on;
        }

        /// <summary>
        /// El valor guardado es el índice dentro de SettingIds.DisplayModes, así que esta
        /// traducción es por POSICIÓN: el orden de esa lista es contrato con este método.
        /// </summary>
        private static FullScreenMode ToFullScreenMode(int index) => index switch
        {
            0 => FullScreenMode.ExclusiveFullScreen,   // Fullscreen
            1 => FullScreenMode.FullScreenWindow,      // Borderless
            _ => FullScreenMode.Windowed               // Windowed
        };
    }
}
