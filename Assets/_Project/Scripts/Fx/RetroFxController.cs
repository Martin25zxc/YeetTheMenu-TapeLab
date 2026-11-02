using UnityEngine;

namespace YeetTheMenu.Fx
{
    /// <summary>
    /// Único dueño del estado y del reloj de los efectos retro.
    /// Hace todo en su Update, antes que cualquier consumidor.
    /// </summary>
    [DefaultExecutionOrder(RetroFxOrder.Controller)]
    [DisallowMultipleComponent]
    public class RetroFxController : MonoBehaviour
    {
        private const float MaxDelta = 0.1f;   // evita saltos tras alt-tab, carga o breakpoint

        private static readonly int RetroTimeId = Shader.PropertyToID("_RetroTime");
        private static readonly int FogIntensityId = Shader.PropertyToID("_FogIntensity");
        private static RetroFxController active;

        [Header("Perfil del contexto actual")]
        [SerializeField] private RetroFxProfile profile;

        [Header("Preferencias del jugador (las alimentará Settings)")]
        [Tooltip("Multiplica solo los efectos de pantalla: scanlines, roll y grano.")]
        [SerializeField, Range(0f, 1f)] private float screenEffectsIntensity = 1f;
        [SerializeField] private bool reducedEffects;

        [Header("Reglas con el movimiento detenido")]
        [SerializeField] private bool hideRollWhenFrozen = true;
        [SerializeField] private bool hideDustWhenFrozen;

        private RetroFxState effective = RetroFxState.Full;   // sin [SerializeField]: no ensucia la escena
        private float retroTime;

        public RetroFxProfile Profile => profile;

        public float ScreenEffectsIntensity
        {
            get => screenEffectsIntensity;
            set => screenEffectsIntensity = Mathf.Clamp01(value);
        }

        public bool ReducedEffects
        {
            get => reducedEffects;
            set => reducedEffects = value;
        }

        /// <summary>Punto de entrada para cambiar de contexto (menú, pausa, gameplay).</summary>
        public void SetProfile(RetroFxProfile newProfile) => profile = newProfile;

        private void OnEnable()
        {
            if (active != null && active != this)
            {
                Debug.LogWarning("Ya hay un RetroFxController activo; este se desactiva.", this);
                enabled = false;
                return;
            }

            active = this;
            RetroFx.Attach();
            Recompute();
            PublishAll(0f);      // valores correctos desde el primer frame
        }

        private void OnDisable()
        {
            if (active != this) return;

            active = null;
            Shader.SetGlobalFloat(RetroTimeId, 0f);
            Shader.SetGlobalFloat(FogIntensityId, 1f);
            RetroFx.Detach();
        }

        private void Update()
        {
            Recompute();

            float delta = Mathf.Min(Time.unscaledDeltaTime, MaxDelta) * effective.motion;
            retroTime += delta;

            PublishAll(delta);
        }

        private void PublishAll(float delta)
        {
            Shader.SetGlobalFloat(RetroTimeId, retroTime);
            Shader.SetGlobalFloat(FogIntensityId, effective.fog);
            RetroFx.Publish(effective, retroTime, delta);
        }

        private void Recompute()
        {
            RetroFxState next = profile != null ? profile.ToState() : RetroFxState.Full;

            // Preferencia del jugador: solo efectos de pantalla.
            float user = Mathf.Clamp01(screenEffectsIntensity);
            next.scanlines *= user;
            next.roll *= user;
            next.grain *= user;

            if (reducedEffects)
                next.motion = 0f;

            // Reglas con el movimiento detenido.
            if (next.motion <= 0f)
            {
                next.grain = 0f;                          // el grano de URP no se puede congelar
                if (hideRollWhenFrozen) next.roll = 0f;   // una banda quieta parece una mancha
                if (hideDustWhenFrozen) next.dust = 0f;
            }

            effective = next;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => active = null;
    }
}