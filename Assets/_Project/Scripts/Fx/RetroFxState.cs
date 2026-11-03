using System;

namespace YeetTheMenu.Fx
{
    /// <summary>
    /// Valores efectivos de los efectos retro, ya combinados (perfil, preferencias, reducción).
    /// Intensidades: 0 = invisible, 1 = valor de diseño. Movimiento: 0 = detenido, 1 = normal.
    /// </summary>
    [Serializable]
    public struct RetroFxState : IEquatable<RetroFxState>
    {
        public float motion;
        public float fog;
        public float dust;
        public float scanlines;
        public float roll;
        public float grain;
        public float glitch;  

        /// <summary>Todo al 100 %. Valor por defecto cuando no hay controlador.</summary>
        public static readonly RetroFxState Full = new RetroFxState
        {
            motion = 1f, fog = 1f, dust = 1f, scanlines = 1f, roll = 1f, grain = 1f, glitch = 1f
        };

        // Comparación exacta a propósito: detecta cambios, no mide parecido.
        public bool Equals(RetroFxState o) =>
            motion == o.motion && fog == o.fog && dust == o.dust &&
            scanlines == o.scanlines && roll == o.roll && grain == o.grain && glitch == o.glitch;

        public override bool Equals(object obj) => obj is RetroFxState o && Equals(o);

        public override int GetHashCode() =>
            HashCode.Combine(motion, fog, dust, scanlines, roll, grain, glitch);
    }
}