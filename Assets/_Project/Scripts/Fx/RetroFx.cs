using System;
using UnityEngine;
namespace YeetTheMenu.Fx
{
    
    /// <summary>
    /// Estado y reloj de los efectos retro, de solo lectura para los consumidores.
    /// Solo RetroFxController escribe aquí.
    /// </summary>
    public static class RetroFx
    {
        private const float MaxDelta = 0.1f;

        private static float retroTime;
        private static float scaledDelta;

        public static RetroFxState State { get; private set; } = RetroFxState.Full;

        /// <summary>Sube cuando cambia State, o cuando aparece o desaparece el controlador.</summary>
        public static int Revision { get; private set; }

        public static bool HasController { get; private set; }

        /// <summary>Reloj retro: se detiene con motion = 0. Sin controlador, usa tiempo real.</summary>
        public static float RetroTime => HasController ? retroTime : Time.unscaledTime;

        /// <summary>Delta del frame ya multiplicado por motion, y limitado contra parones.</summary>
        public static float ScaledDelta =>
            HasController ? scaledDelta : Mathf.Min(Time.unscaledDeltaTime, MaxDelta);

        internal static void Attach()
        {
            HasController = true;
            Revision++;
        }

        internal static void Detach()
        {
            HasController = false;
            State = RetroFxState.Full;   // política sin controlador: valores de diseño
            retroTime = 0f;
            scaledDelta = 0f;
            Revision++;
        }

        internal static void Publish(in RetroFxState state, float newRetroTime, float newDelta)
        {
            retroTime = newRetroTime;
            scaledDelta = newDelta;

            if (state.Equals(State)) return;

            State = state;
            Revision++;
        }

        // Con el Domain Reload desactivado, los estáticos sobreviven entre ejecuciones.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            State = RetroFxState.Full;
            Revision = 0;
            HasController = false;
            retroTime = 0f;
            scaledDelta = 0f;
        }
    }
}