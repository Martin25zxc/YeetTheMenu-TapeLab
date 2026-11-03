using UnityEngine;
using UnityEngine.UIElements;

namespace YeetTheMenu.Fx
{
    /// <summary>
    /// Desplaza horizontalmente un elemento con nombre mientras dura un fallo.
    /// El elemento queda RESERVADO al glitch: ningún otro sistema debe escribir su translate.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class GlitchShift : MonoBehaviour
    {
        [Tooltip("Nombre del elemento a desplazar en el UXML (por ejemplo, root o vhs-root).")]
        [SerializeField] private string targetName = "root";

        [Tooltip("Multiplica el desplazamiento del director. 0 = no tiembla.")]
        [SerializeField, Range(0f, 2f)] private float scale = 1f;

        private UIDocument document;
        private VisualElement shiftedTarget;   // solo mientras dura un fallo
        private bool warned;

        private void Awake()
        {
            document = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            GlitchDirector.Began += OnGlitchBegan;
            GlitchDirector.Ended += OnGlitchEnded;
        }

        private void OnDisable()
        {
            GlitchDirector.Began -= OnGlitchBegan;
            GlitchDirector.Ended -= OnGlitchEnded;
            Restore();                          // invariante: nunca dejar la UI desplazada
        }

        private void OnGlitchBegan(GlitchEvent e)
        {
            Restore();

            // Se busca en cada fallo: si el árbol se recreó, encontramos el elemento vigente.
            VisualElement target = document.rootVisualElement?.Q<VisualElement>(targetName);
            if (target == null)
            {
                if (!warned)
                {
                    Debug.LogWarning($"GlitchShift: no existe el elemento '{targetName}' en el UXML.", this);
                    warned = true;
                }
                return;
            }

            // Entero: un desplazamiento fraccionario desenfocaría el texto.
            int px = Mathf.RoundToInt(e.shiftPixels * scale * e.intensity);
            if (px == 0) return;

            target.style.translate = new StyleTranslate(new Translate(new Length(px), new Length(0f)));
            shiftedTarget = target;
        }

        private void OnGlitchEnded() => Restore();

        private void Restore()
        {
            if (shiftedTarget == null) return;

            // Si el árbol se recreó, el elemento viejo ya no está en un panel y el nuevo
            // nace sin desplazamiento: no hay nada que restaurar (política: cancelar).
            if (shiftedTarget.panel != null)
                shiftedTarget.style.translate = StyleKeyword.Null;   // quita el inline; el USS vuelve a mandar

            shiftedTarget = null;
        }
    }
}