using UnityEngine;

namespace YeetTheMenu.Fx
{
    /// <summary>
    /// Valores de los efectos para un contexto (menú, pausa, gameplay).
    /// Es un asset del proyecto: ningún script debe escribir en él durante la ejecución.
    /// </summary>
    [CreateAssetMenu(menuName = "TapeLab/Retro Fx Profile", fileName = "FX_New")]
    public class RetroFxProfile : ScriptableObject
    {
        [Header("Movimiento (0 = detenido, 1 = normal)")]
        [SerializeField, Range(0f, 1f)] private float motion = 1f;

        [Header("Intensidad (0 = invisible, 1 = valor de diseño)")]
        [SerializeField, Range(0f, 1f)] private float fog = 1f;
        [SerializeField, Range(0f, 1f)] private float dust = 1f;
        [SerializeField, Range(0f, 1f)] private float scanlines = 1f;
        [SerializeField, Range(0f, 1f)] private float roll = 1f;
        [SerializeField, Range(0f, 1f)] private float grain = 1f;
        [SerializeField, Range(0f, 1f)] private float glitch = 1f;


        public RetroFxState ToState() => new RetroFxState
        {
            motion = motion, fog = fog, dust = dust,
            scanlines = scanlines, roll = roll, grain = grain, glitch = glitch
        };
    }
}