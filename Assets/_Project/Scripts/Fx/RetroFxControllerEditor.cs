using UnityEditor;
using UnityEngine;

namespace YeetTheMenu.Fx.EditorTools
{
    /// <summary>Muestra el estado efectivo en vivo, sin guardarlo en la escena.</summary>
    [CustomEditor(typeof(RetroFxController))]
    public class RetroFxControllerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            if (!Application.isPlaying) return;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Estado efectivo", EditorStyles.boldLabel);

            RetroFxState s = RetroFx.State;

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.Toggle("Controlador activo", RetroFx.HasController);
                EditorGUILayout.IntField("Revisión", RetroFx.Revision);
                EditorGUILayout.FloatField("RetroTime", RetroFx.RetroTime);
                EditorGUILayout.Slider("motion", s.motion, 0f, 1f);
                EditorGUILayout.Slider("fog", s.fog, 0f, 1f);
                EditorGUILayout.Slider("dust", s.dust, 0f, 1f);
                EditorGUILayout.Slider("scanlines", s.scanlines, 0f, 1f);
                EditorGUILayout.Slider("roll", s.roll, 0f, 1f);
                EditorGUILayout.Slider("grain", s.grain, 0f, 1f);
            }

            Repaint();   // refresca cada frame mientras se juega
        }
    }
}