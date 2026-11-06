using System.Collections.Generic;
using UnityEngine;

namespace YeetTheMenu.Settings
{
    /// <summary>
    /// Lo que el catálogo y el aplicador tienen que compartir: nada más.
    ///
    /// Solo están acá los identificadores de las opciones que alguien aplica, porque esos sí
    /// viven en dos lugares (la fila y el 'case' del aplicador) y una copia suelta se rompe en
    /// silencio al renombrar. Los identificadores de las filas demostrativas aparecen una sola
    /// vez, en su propia fila, así que una constante no agregaría nada.
    /// </summary>
    public static class SettingIds
    {
        public const string RetroFilter = "retro-filter";
        public const string DisplayMode = "display-mode";
        public const string VSync = "vsync";
        public const string ReducedEffects = "reduced-effects";

        /// <summary>
        /// Las alternativas del modo de pantalla. Viven acá porque el ORDEN es contrato:
        /// el valor guardado es el índice, y el aplicador lo traduce por posición.
        /// Reordenar esta lista cambia el significado de lo que ya está guardado.
        /// </summary>
        public static readonly string[] DisplayModes = { "Fullscreen", "Borderless", "Windowed" };
    }

    /// <summary>
    /// El catálogo de configuración de TapeLab: el único archivo de Settings que es de ESTE proyecto.
    ///
    /// Cada fila declara su modo:
    ///   Available  se aplica de verdad y se guarda
    ///   Demo       se mueve y se guarda, pero no llega a ningún sistema
    ///   Hidden     no se dibuja; queda escrita como documentación del juego terminado
    ///
    /// Hay 24 filas y solo cuatro aplicables. Eso no es una carencia del menú: es lo que el
    /// proyecto puede cumplir hoy, dicho en voz alta en lugar de simulado.
    /// </summary>
    [CreateAssetMenu(menuName = "TapeLab/Settings Catalog", fileName = "SC_TapeLab")]
    public sealed class TapeLabCatalog : SettingsCatalog
    {
        private const SettingMode Demo = SettingMode.Demo;
        private const SettingMode Hidden = SettingMode.Hidden;

        protected override IReadOnlyList<SettingsTab> Build() => new[]
        {
            Tab("sound", "Sound",
                Slider("master-volume", "Master volume", 0, 100, 80,
                       "No effect yet — TapeLab has no audio. The slider still remembers where you left it.", Demo),
                Slider("ui-volume", "UI volume", 0, 100, 70,
                       "No effect yet — there are no interface sounds.", Demo)),

            Tab("graphics", "Graphics",
                Choice("quality-preset", "Quality preset", new[] { "Low", "Medium", "High", "Custom" }, 2,
                       "No effect yet — this project's quality levels are Mobile and PC, not Low to High.", Demo),

                Choice(SettingIds.DisplayMode, "Display mode", SettingIds.DisplayModes, 0,
                       "Fullscreen, a borderless window, or a resizable window."),

                Toggle("motion-blur", "Motion blur", on: true,
                       "No effect yet — motion blur belongs to the render pipeline asset.", Demo),

                // Nunca se dibuja. Se escribe igual para que el catálogo diga qué tendría el juego
                // terminado, y porque prometer una descarga de 4 GB que no existe induce a error.
                Toggle("hd-texture-pack", "HD texture pack", on: false,
                       "Downloads an extra texture pack that takes about 4 GB of disk space.", Hidden),

                Toggle(SettingIds.VSync, "VSync", on: true,
                       "Matches the monitor refresh rate so the image does not tear."),

                Slider("framerate-cap", "Framerate cap", 30, 240, 144,
                       "No effect yet — a frame rate limit can affect a hosted match, and the networking is undecided.", Demo),

                Choice("shadows", "Shadows", new[] { "High", "Medium", "Low", "Off" }, 0,
                       "No effect yet — shadow quality belongs to the render pipeline asset.", Demo),

                Choice("geometry-detail", "Geometry detail", new[] { "High", "Medium", "Low" }, 0,
                       "No effect yet — there is no level geometry to simplify.", Demo),

                Slider(SettingIds.RetroFilter, "Retro filter", 0, 100, 100,
                       "Scanlines, tape roll, grain and glitch intensity. Fog and dust are not affected.")),

            Tab("gameplay", "Gameplay",
                Slider("mouse-sensitivity", "Mouse sensitivity", 1, 100, 50,
                       "No effect yet — there is no player camera.", Demo),
                Toggle("invert-y", "Invert Y axis", on: false,
                       "No effect yet — there is no player camera.", Demo),
                Slider("field-of-view", "Field of view", 70, 110, 90,
                       "No effect yet — there is no player camera.", Demo),
                Toggle("tutorial-hints", "Show tutorial hints", on: true,
                       "No effect yet — there are no tutorial hints.", Demo)),

            Tab("accessibility", "Accessibility",
                // Esta fila no está en el diseño original. Se agrega porque sí puede hacer algo:
                // es la que conecta los efectos retro con las transiciones del menú.
                Toggle(SettingIds.ReducedEffects, "Reduced effects", on: false,
                       "Stops the moving effects and shortens menu transitions."),

                Slider("camera-shake", "Camera shake", 0, 100, 100,
                       "No effect yet — there is no gameplay camera.", Demo),
                Toggle("subtitles", "Subtitles", on: true,
                       "No effect yet — there is no dialogue or audio.", Demo),
                Slider("hud-scale", "HUD scale", 50, 150, 100,
                       "No effect yet — there is no HUD.", Demo),
                Slider("font-scale", "Font scale", 80, 150, 100,
                       "No effect yet — scaling the interface writes to a project asset and needs its own pass.", Demo),
                Slider("contrast", "Contrast", 50, 150, 100,
                       "No effect yet — contrast has to be global, and only the menu has a colour volume.", Demo),
                Slider("gamma", "Gamma", 50, 150, 100,
                       "No effect yet — gamma has to be global, and only the menu has a colour volume.", Demo),
                Toggle("simplified-input", "Simplified input", on: false,
                       "No effect yet — there are no held or repeated actions.", Demo),
                Toggle("disable-gore", "Disable gore and blood", on: false,
                       "No effect yet — there is no gore to disable.", Demo)),
        };
    }
}
