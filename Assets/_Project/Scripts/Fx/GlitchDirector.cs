using System;
using UnityEngine;

namespace YeetTheMenu.Fx
{
    /// <summary>Datos de un fallo. Las medidas de la banda son porcentajes de pantalla.</summary>
    public readonly struct GlitchEvent
    {
        public readonly float topPercent;
        public readonly float heightPercent;
        public readonly int shiftPixels;
        public readonly float intensity;

        public GlitchEvent(float topPercent, float heightPercent, int shiftPixels, float intensity)
        {
            this.topPercent = topPercent;
            this.heightPercent = heightPercent;
            this.shiftPixels = shiftPixels;
            this.intensity = intensity;
        }
    }

    /// <summary>
    /// Decide CUÁNDO ocurre un fallo y avisa. No toca ningún elemento visual.
    /// Uno por escena, en el objeto RetroFx.
    /// </summary>
    public class GlitchDirector : RetroFxConsumer
    {
        // Topes de seguridad: ningún valor del Inspector puede superarlos.
        private const float HardMinInterval = 1.5f;
        private const float HardMaxDuration = 0.4f;
        private const int HardMaxShift = 12;

        /// <summary>Empieza un fallo. Suscribirse en OnEnable y desuscribirse en OnDisable.</summary>
        public static event Action<GlitchEvent> Began;

        /// <summary>Termina el fallo. Siempre llega, incluso si se cancela a mitad.</summary>
        public static event Action Ended;

        [Header("Ritmo (segundos)")]
        [SerializeField, Min(HardMinInterval)] private float minInterval = 2.5f;
        [SerializeField, Min(HardMinInterval)] private float maxInterval = 8.5f;
        [SerializeField, Range(0.05f, HardMaxDuration)] private float minDuration = 0.12f;
        [SerializeField, Range(0.05f, HardMaxDuration)] private float maxDuration = 0.32f;

        [Header("Banda (% de pantalla)")]
        [SerializeField, Range(0f, 100f)] private float bandTopMax = 90f;
        [SerializeField, Range(0.5f, 20f)] private float bandMinHeight = 2f;
        [SerializeField, Range(0.5f, 20f)] private float bandMaxHeight = 8f;

        [Header("Desplazamiento (píxeles lógicos)")]
        [SerializeField, Range(0, HardMaxShift)] private int maxShift = 8;

        [Header("Reproducibilidad")]
        [Tooltip("0 = aleatorio en cada ejecución. Otro valor repite la misma secuencia.")]
        [SerializeField] private int seed;

        private System.Random rng;
        private bool active;
        private float timer;

        // Opt-in: sin controlador, o con el canal en 0, no hay fallos.
        // La regla "sin movimiento, sin glitch" ya la aplica el controlador.
        private static bool Enabled => RetroFx.HasController && Fx.glitch > 0f;

        private void OnEnable()
        {
            // Generador propio: no altera el aleatorio global de Unity.
            rng = seed != 0 ? new System.Random(seed) : new System.Random();
            active = false;
            ScheduleNext();
        }

        private void OnDisable()
        {
            // Invariante: nunca dejar un fallo abierto.
            if (active) EndGlitch();
        }

        private void Update()
        {
            if (!Enabled)
            {
                if (active) EndGlitch();
                return;
            }


            // Espera: reloj retro (se congela con el movimiento).
            // Duración: tiempo real limitado, para que el tope de 0,4 s sea real.
            timer -= active
                ? Mathf.Min(Time.unscaledDeltaTime, 0.1f)
                : RetroFx.ScaledDelta;

            
            if (timer > 0f) return;

            if (active) EndGlitch();
            else BeginGlitch(Fx.glitch);
        }

        /// <summary>Prueba manual. Respeta el estado: no dispara si el glitch está desactivado.</summary>
        [ContextMenu("Disparar glitch ahora")]
        private void TriggerNow()
        {
            if (!Application.isPlaying) return;

            if (!Enabled)
            {
                Debug.Log("GlitchDirector: desactivado por el estado actual (reducción, perfil o sin controlador).", this);
                return;
            }

            if (active) EndGlitch();
            BeginGlitch(Fx.glitch);
        }

        private void BeginGlitch(float intensity)
        {
            float top = Range(0f, bandTopMax);
            float height = Range(Mathf.Min(bandMinHeight, bandMaxHeight),
                                 Mathf.Max(bandMinHeight, bandMaxHeight));

            int limit = Mathf.Clamp(maxShift, 0, HardMaxShift);
            int shift = rng.Next(-limit, limit + 1);

            active = true;
            timer = Mathf.Clamp(
                Range(Mathf.Min(minDuration, maxDuration), Mathf.Max(minDuration, maxDuration)),
                0.05f, HardMaxDuration);

            Began?.Invoke(new GlitchEvent(top, height, shift, intensity));
        }

        private void EndGlitch()
        {
            active = false;
            Ended?.Invoke();
            ScheduleNext();
        }

        private void ScheduleNext()
        {
            float min = Mathf.Max(minInterval, HardMinInterval);
            float max = Mathf.Max(maxInterval, min);
            timer = Range(min, max);
        }

        private float Range(float min, float max) => min + (float)rng.NextDouble() * (max - min);

        // Con el Domain Reload desactivado, los eventos estáticos conservarían suscriptores viejos.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Began = null;
            Ended = null;
        }
    }
}