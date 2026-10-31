using UnityEngine;

[RequireComponent(typeof(ParticleSystem))]
public class DustLoop : MonoBehaviour
{
    [Header("Camera")]
    [SerializeField] private Camera targetCamera;

    [Header("Particles")]
    [SerializeField, Range(1, 200)]
    private int particleCount = 70;

    [SerializeField] private Vector2 sizeRange =
        new Vector2(0.1f, 0.15f);

    [SerializeField] private Vector2 riseSpeed =
        new Vector2(0.015f, 0.065f);

    [Header("Horizontal sway")]
    [SerializeField] private Vector2 swayAmplitude =
        new Vector2(0.08f, 0.25f);

    [SerializeField] private float swayFrequency = 0.35f;

    private ParticleSystem ps;
    private ParticleSystem.Particle[] particles;

    private float[] speeds;
    private float[] horizontalPositions;
    private float[] amplitudes;
    private float[] phases;

    private float elapsed;

    private void Start()
    {
        ps = GetComponent<ParticleSystem>();

        if (targetCamera == null)
            targetCamera = Camera.main;

        if (targetCamera == null || !targetCamera.orthographic)
        {
            Debug.LogError("DustLoop requiere una camara ortografica.");
            enabled = false;
            return;
        }

        var main = ps.main;
        main.maxParticles = particleCount;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startSpeed = 0f;
        main.gravityModifier = 0f;
        main.useUnscaledTime = true;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.rateOverDistance = 0f;

        var noise = ps.noise;
        noise.enabled = false;

        var velocity = ps.velocityOverLifetime;
        velocity.enabled = false;

        var colorOverLife = ps.colorOverLifetime;
        colorOverLife.enabled = false;

        var sizeOverLife = ps.sizeOverLifetime;
        sizeOverLife.enabled = false;

        particles = new ParticleSystem.Particle[particleCount];
        speeds = new float[particleCount];
        horizontalPositions = new float[particleCount];
        amplitudes = new float[particleCount];
        phases = new float[particleCount];

        float halfHeight = targetCamera.orthographicSize;

        for (int i = 0; i < particleCount; i++)
        {
            speeds[i] = Random.Range(riseSpeed.x, riseSpeed.y);
            horizontalPositions[i] = Random.value;
            amplitudes[i] = Random.Range(
                swayAmplitude.x, swayAmplitude.y);
            phases[i] = Random.Range(0f, Mathf.PI * 2f);

            particles[i] = new ParticleSystem.Particle
            {
                position = new Vector3(
                    0f,
                    Random.Range(-halfHeight, halfHeight),
                    0f),

                startSize = Random.Range(sizeRange.x, sizeRange.y),
                startColor = new Color32(
                    255, 205, 220,
                    (byte)Random.Range(38, 90)),

                startLifetime = 3600f,
                remainingLifetime = 3600f,
                velocity = Vector3.zero
            };
        }

        ps.Clear();
        ps.Play();
        ps.SetParticles(particles, particleCount);
    }

    private void LateUpdate()
    {
        float dt = Time.unscaledDeltaTime;
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

            x = Mathf.Clamp(
                x,
                -halfWidth + 0.1f,
                halfWidth - 0.1f);

            particles[i].position = new Vector3(x, y, 0f);

            // Evita la muerte por tiempo.
            particles[i].remainingLifetime = 3600f;
        }

        ps.SetParticles(particles, particles.Length);
    }
}