using System;
using System.Collections.Generic;
using UnityEngine;

namespace YeetTheMenu.Settings
{
    /// <summary>Cómo se controla una opción. Define también qué significa su valor entero.</summary>
    public enum SettingKind
    {
        Slider,   // un número entre Min y Max
        Toggle,   // 0 = apagado, 1 = encendido
        Choice    // índice dentro de Choices
    }

    /// <summary>Qué puede hacer este proyecto con la opción.</summary>
    public enum SettingMode
    {
        Available,   // se aplica y se guarda
        Demo,        // se muestra y se mueve, pero no aplica ni se guarda
        Hidden       // no se dibuja: queda como documentación del juego terminado
    }

    /// <summary>
    /// Descripción de UNA opción: qué es, cómo se controla y qué valores admite.
    /// Todos los valores son enteros (S17, S27): un slider guarda el número que muestra,
    /// un toggle guarda 0 o 1, un choice guarda el índice de la alternativa elegida.
    /// Es dato de diseño: nunca guarda el valor actual del jugador (S13).
    /// </summary>
    [Serializable]
    public class SettingOption
    {
        [Tooltip("Clave estable de guardado y de aplicación. Cambiarla hace perder lo que el jugador guardó.")]
        [SerializeField] private string id;

        [Tooltip("Texto de la fila.")]
        [SerializeField] private string label;

        [Tooltip("Qué hace la opción. Se mostrará en la línea de descripción.")]
        [SerializeField, TextArea(1, 3)] private string description;

        [SerializeField] private SettingKind kind;
        [SerializeField] private SettingMode mode;

        [Tooltip("Solo Slider: valor mínimo.")]
        [SerializeField] private int min;

        [Tooltip("Solo Slider: valor máximo.")]
        [SerializeField] private int max = 100;

        [Tooltip("Slider: el número. Toggle: 0 o 1. Choice: el índice en Choices.")]
        [SerializeField] private int defaultValue;

        [Tooltip("Solo Choice: nombres legibles, en orden.")]
        [SerializeField] private string[] choices;

        public string Id => id;
        public string Label => label;
        public string Description => description;
        public SettingKind Kind => kind;
        public SettingMode Mode => mode;

        public int Min => kind == SettingKind.Slider ? min : 0;

        public int Max => kind switch
        {
            SettingKind.Slider => max,
            SettingKind.Toggle => 1,
            SettingKind.Choice => Mathf.Max(0, Choices.Count - 1),
            _ => 0
        };

        public int DefaultValue => Clamp(defaultValue);

        public IReadOnlyList<string> Choices => choices ?? Array.Empty<string>();

        /// <summary>Lleva cualquier entero al rango válido de esta opción.</summary>
        public int Clamp(int value) => Mathf.Clamp(value, Min, Max);

        /// <summary>Describe el problema de configuración, o devuelve null si está bien.</summary>
        internal string Validate()
        {
            if (string.IsNullOrWhiteSpace(id)) return "no tiene id";
            if (kind == SettingKind.Slider && min > max) return $"tiene min ({min}) mayor que max ({max})";
            if (kind == SettingKind.Choice && Choices.Count == 0) return "es Choice y no tiene alternativas";
            if (defaultValue != Clamp(defaultValue))
                return $"tiene el default {defaultValue} fuera de {Min}–{Max}; se usará {Clamp(defaultValue)}";
            return null;
        }
    }
}
