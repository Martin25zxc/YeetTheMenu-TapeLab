using UnityEngine;

namespace YeetTheMenu.Fx
{
    /// <summary>
    /// Polvo en suspensión del fondo del menú: partículas persistentes que suben,
    /// oscilan y reaparecen por abajo. El ParticleSystem solo dibuja; las posiciones
    /// las calcula este script.
    /// Requiere una cámara ortográfica, así que pertenece al menú, no al gameplay.
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public class DustLoop : RetroFxConsumer
    {
        // Se refresca en cada frame activo; solo cuenta mientras el polvo está congelado,
        // y el sistema se pausa en ese caso, así que no llega a agotarse.
        private const float ParticleLifetime = 3600f;

        [Header("Camera")]
        [SerializeField] private Camera targetCamera;

        [Header("Particles")]
        [SerializeField, Range(1, 200)]
        private int particleCount = 70;

        [SerializeField] private Vector2 sizeRange = new Vector2(0.1f, 0.15f);
        [SerializeField] private Vector2 riseSpeed = new Vector2(0.015f, 0.065f);

        [Header("Opacidad a intensidad 100 % (0-255)")]
        [SerializeField] private Vector2Int alphaRange = new Vector2Int(38, 90);

        [Header("Horizontal sway")]
        [SerializeField] private Vector2 swayAmplitude = new Vector2(0.08f, 0.25f);
        [SerializeField] private float swayFrequency = 0.35f;

        private ParticleSystem ps;
        private ParticleSystemRenderer psRenderer;
        private ParticleSystem.Particle[] particles;

        private float[] speeds;
        private float[] horizontalPositions;
        private float[] amplitudes;
        private float[] phases;
        private byte[] baseAlpha;

        private float elapsed;
        private bool seeded;
        private bool frozen;
        private bool ready;

        private void Awake()
        {
            ps = GetComponent<ParticleSystem>();
            psRenderer = GetComponent<ParticleSystemRenderer>();

            if (targetCamera == null)
                targetCamera = Camera.main;

            if (targetCamera == null || !targetCamera.orthographic)
            {
                Debug.LogError("DustLoop requiere una camara ortografica. " +
                               "Este efecto pertenece al fondo del menu.", this);
                enabled = false;
                return;
            }

            ConfigureSystem();
            ready = true;
        }

        // En OnEnable, no en Start: el objeto puede desactivarse y reactivarse
        // (abrir y cerrar un menu), y al reactivar un ParticleSystem su buffer se vacia.
        private void OnEnable()
        {
            if (!ready) return;

            if (!seeded)
            {
                Seed();
                seeded = true;
            }

            frozen = false;
            InvalidateFx();

            ps.Clear();
            ps.Play();
            Simulate(0f);   // reparte las partículas en x aun si el movimiento está detenido
            ps.SetParticles(particles, particles.Length);
        }

        private void LateUpdate()
        {
            bool intensityChanged = FxChanged;
            if (intensityChanged)
                ApplyIntensity();

            float dt = RetroFx.ScaledDelta;
            bool visible = psRenderer.enabled;

            // Invisible o quieto: el sistema se pausa para que no envejezca las partículas.
            SetFrozen(!visible || dt <= 0f);

            if (!visible)
                return;

            // Quieto y sin cambio de intensidad: no hace falta reescribir las particulas.
            if (frozen && !intensityChanged)
                return;

            if (dt > 0f)
                Simulate(dt);

            ps.SetParticles(particles, particles.Length);
        }

        // ---------- Configuracion ----------

        private void ConfigureSystem()
        {
            var main = ps.main;
            main.maxParticles = particleCount;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startSpeed = 0f;
            main.gravityModifier = 0f;
            main.useUnscaledTime = true;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.rateOverDistance = 0f;

            // Los modulos son structs: hay que guardarlos en una variable para modificarlos.
            var noise = ps.noise;
            noise.enabled = false;

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = false;

            var colorOverLife = ps.colorOverLifetime;
            colorOverLife.enabled = false;

            var sizeOverLife = ps.sizeOverLifetime;
            sizeOverLife.enabled = false;
        }

        private void Seed()
        {
            particles = new ParticleSystem.Particle[particleCount];
            speeds = new float[particleCount];
            horizontalPositions = new float[particleCount];
            amplitudes = new float[particleCount];
            phases = new float[particleCount];
            baseAlpha = new byte[particleCount];

            float halfHeight = targetCamera.orthographicSize;

            for (int i = 0; i < particleCount; i++)
            {
                speeds[i] = Random.Range(riseSpeed.x, riseSpeed.y);
                horizontalPositions[i] = Random.value;
                amplitudes[i] = Random.Range(swayAmplitude.x, swayAmplitude.y);
                phases[i] = Random.Range(0f, Mathf.PI * 2f);
                baseAlpha[i] = (byte)Random.Range(alphaRange.x, alphaRange.y + 1);

                particles[i] = new ParticleSystem.Particle
                {
                    position = new Vector3(0f, Random.Range(-halfHeight, halfHeight), 0f),
                    startSize = Random.Range(sizeRange.x, sizeRange.y),
                    startColor = new Color32(255, 205, 220, baseAlpha[i]),
                    startLifetime = ParticleLifetime,
                    remainingLifetime = ParticleLifetime,
                    velocity = Vector3.zero
                };
            }
        }

        // ---------- Simulacion ----------

        private void Simulate(float dt)
        {
            elapsed += dt;

            float halfHeight = targetCamera.orthographicSize;
            float halfWidth = halfHeight * targetCamera.aspect;

            for (int i = 0; i < particles.Length; i++)
            {
                float y = particles[i].position.y + speeds[i] * dt;

                // Cuando sale por arriba, reaparece debajo.
                if (y > halfHeight + 0.2f)
                    y = -halfHeight - 0.2f;

                // Movimiento horizontal suave y limitado.
                float centerX = Mathf.Lerp(
                    -halfWidth + 0.5f,
                    halfWidth - 0.5f,
                    horizontalPositions[i]);

                float x = centerX + amplitudes[i] *
                    Mathf.Sin(elapsed * swayFrequency + phases[i]);

                x = Mathf.Clamp(x, -halfWidth + 0.1f, halfWidth - 0.1f);

                particles[i].position = new Vector3(x, y, 0f);

                // Evita la muerte por tiempo.
                particles[i].remainingLifetime = ParticleLifetime;
            }
        }

        // Congelado: se pausa el sistema para que no envejezca las particulas.
        // Sin esto, una congelacion larga (1 hora) las mataria y no volverian,
        // porque la emision esta en 0.
        private void SetFrozen(bool value)
        {
            if (value == frozen) return;
            frozen = value;

            if (value) ps.Pause();
            else ps.Play();
        }

        private void ApplyIntensity()
        {
            float k = Fx.dust;
            psRenderer.enabled = k > 0f;

            for (int i = 0; i < particles.Length; i++)
            {
                byte alpha = (byte)Mathf.Clamp(Mathf.RoundToInt(baseAlpha[i] * k), 0, 255);
                particles[i].startColor = new Color32(255, 205, 220, alpha);
            }

            MarkFxApplied();
        }
    }
}