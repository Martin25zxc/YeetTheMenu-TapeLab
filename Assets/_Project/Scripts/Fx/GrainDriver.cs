using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace YeetTheMenu.Fx
{
    /// <summary>Escala la intensidad del Film Grain del Volume.</summary>
    [RequireComponent(typeof(Volume))]
    public class GrainDriver : RetroFxConsumer
    {
        private FilmGrain grain;
        private float baseIntensity;
        private bool ready;

        private void OnEnable()
        {
            // volume.profile devuelve una copia en ejecución: no modifica el asset del proyecto.
            Volume volume = GetComponent<Volume>();

            if (volume.profile == null || !volume.profile.TryGet(out grain))
            {
                Debug.LogError("GrainDriver: el Volume no tiene un override de Film Grain.", this);
                enabled = false;
                return;
            }

            baseIntensity = grain.intensity.value;
            ready = true;
            InvalidateFx();
        }

        private void Update()
        {
            if (!ready || !FxChanged) return;

            grain.intensity.value = baseIntensity * Fx.grain;
            MarkFxApplied();
        }
    }
}