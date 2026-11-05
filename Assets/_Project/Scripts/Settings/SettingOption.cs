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
        Demo,        // se muestra, se mueve y se guarda, pero no se aplica a ningún sistema
        Hidden       // no se dibuja: queda como documentación del juego terminado
    }

    /// <summary>
    /// Descripción de UNA opción: qué es, cómo se controla y qué valores admite.
    /// Inmutable y construida en código, no en el Inspector: el id, el tipo y el rango son
    /// contrato con el aplicador, no datos de diseño. El catálogo las agrupa en pestañas.
    /// Todos los valores son enteros: un slider guarda el número que muestra,
    /// un toggle guarda 0 o 1, un choice guarda el índice de la alternativa elegida.
    /// Es dato de diseño: nunca guarda el valor actual del jugador.
    /// </summary>
    public sealed class SettingOption
    {
        private static readonly string[] NoChoices = Array.Empty<string>();

        /// <summary>Clave estable de guardado y de aplicación. Cambiarla pierde lo que el jugador guardó.</summary>
        public string Id { get; }

        public string Label { get; }

        /// <summary>Qué hace la opción. Lo muestra la línea de descripción.</summary>
        public string Description { get; }

        public SettingKind Kind { get; }
        public SettingMode Mode { get; }

        public int Min { get; }
        public int Max { get; }
        public int DefaultValue { get; }

        /// <summary>Solo Choice: nombres legibles, en orden.</summary>
        public IReadOnlyList<string> Choices { get; }

        public SettingOption(string id, string label, SettingKind kind, SettingMode mode,
                             int min, int max, int defaultValue, string description, string[] choices = null)
        {
            Id = id;
            Label = label;
            Description = description ?? string.Empty;
            Kind = kind;
            Mode = mode;
            Choices = choices ?? NoChoices;

            // El rango no se pide dos veces: cada tipo sabe el suyo.
            switch (kind)
            {
                case SettingKind.Toggle:
                    Min = 0;
                    Max = 1;
                    break;
                case SettingKind.Choice:
                    Min = 0;
                    Max = Mathf.Max(0, Choices.Count - 1);
                    break;
                default:
                    Min = Mathf.Min(min, max);
                    Max = Mathf.Max(min, max);
                    break;
            }

            DefaultValue = Clamp(defaultValue);

            // Un catálogo en código no se puede validar al compilar; esto es lo más temprano posible.
            // Antes esta red la tendía OnValidate sobre el asset.
            if (string.IsNullOrWhiteSpace(Id))
                Debug.LogError($"SettingOption '{label}': sin id. Es la clave de guardado y de aplicación.");

            if (kind == SettingKind.Choice && Choices.Count == 0)
                Debug.LogError($"SettingOption '{id}': es Choice y no tiene alternativas.");

            if (DefaultValue != defaultValue)
                Debug.LogWarning($"SettingOption '{id}': el default {defaultValue} está fuera de {Min}–{Max}; se usa {DefaultValue}.");
        }

        /// <summary>Lleva cualquier entero al rango válido de esta opción.</summary>
        public int Clamp(int value) => Mathf.Clamp(value, Min, Max);
    }
}
