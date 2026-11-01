using UnityEngine;

/// <summary>
/// Reloj propio de los efectos retro.
/// Avanza con tiempo real (ignora Time.timeScale), multiplicado por "motion".
/// Con motion = 0 se detiene sin saltos y, al subir otra vez, continúa donde estaba.
/// </summary>
public class RetroClock : MonoBehaviour
{
    [SerializeField, Range(0f, 1f)] private float motion = 1f;

    private static readonly int RetroTimeId = Shader.PropertyToID("_RetroTime");

    /// <summary>Segundos "retro" acumulados.</summary>
    public float RetroTime { get; private set; }

    /// <summary>Delta de este frame, ya multiplicado por el movimiento.</summary>
    public float ScaledDelta { get; private set; }

    public float Motion
    {
        get => motion;
        set => motion = Mathf.Clamp01(value);
    }

    private void Update()
    {
        ScaledDelta = Time.unscaledDeltaTime * motion;
        RetroTime += ScaledDelta;
        Shader.SetGlobalFloat(RetroTimeId, RetroTime);
    }

    private void OnDisable()
    {
        // Al salir de Play, deja el shader en su estado de reposo.
        Shader.SetGlobalFloat(RetroTimeId, 0f);
    }
}