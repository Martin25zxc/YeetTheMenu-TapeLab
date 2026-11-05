using System.Collections.Generic;
using UnityEngine;

namespace YeetTheMenu.Settings
{
    /// <summary>
    /// Las claves que comparten el catálogo y el aplicador. Una sola copia de cada string:
    /// antes el id vivía dos veces, en el asset y en el 'case' del aplicador, sin relación entre sí.
    /// </summary>
    public static class SettingIds
    {
        public const string RetroFilter = "retro-filter";
        public const string MasterVolume = "master-volume";

        // Cada opción nueva agrega su constante acá y su fila en Build(), con el mismo id.
    }

    /// <summary>
    /// El catálogo de configuración de TapeLab: el único archivo de Settings que es de ESTE proyecto.
    /// </summary>
    [CreateAssetMenu(menuName = "TapeLab/Settings Catalog", fileName = "SC_TapeLab")]
    public sealed class TapeLabCatalog : SettingsCatalog
    {
        protected override IReadOnlyList<SettingsTab> Build() => new[]
        {
            Tab("graphics", "Graphics",
                Slider(SettingIds.RetroFilter, "Retro filter", 0, 100, 100,
                       "Intensity of scanlines, tape roll, grain and glitches. Fog and dust are not affected.")),

            Tab("sound", "Sound",
                Slider(SettingIds.MasterVolume, "Master volume", 0, 100, 80,
                       "TapeLab has no audio yet: the slider remembers where you left it, but nothing is routed.",
                       SettingMode.Demo)),
        };
    }
}
