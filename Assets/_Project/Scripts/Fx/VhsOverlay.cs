using UnityEngine;
using UnityEngine.UIElements;

namespace YeetTheMenu.Fx
{
    /// <summary>
    /// Overlay estilo cinta VHS: scanlines, OSD, timecode y banda de roll.
    /// Los elementos se definen en VhsOverlay.uxml; aquí solo se animan.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class VhsOverlay : RetroFxConsumer
    {
        // Espera a que el tamaño de ventana se estabilice antes de rehacer la textura.
        private const float ResizeSettleSeconds = 0.25f;

        [Header("Scanlines (valores a intensidad 100 %)")]
        [SerializeField, Range(0f, 1f)] private float scanlineAlpha = 0.3f;
        [SerializeField, Min(1)] private int linesPerScreen = 360;

        [Header("Roll (valores a intensidad 100 %)")]
        [SerializeField, Range(0f, 1f)] private float rollAlpha = 0.08f;
        [SerializeField, Min(0.5f)] private float rollPeriodSeconds = 9f;

        [Tooltip("Alto de la banda como % del alto de pantalla. Única fuente de verdad: el USS no lo define.")]
        [SerializeField, Range(5f, 40f)] private float rollHeightPercent = 14f;

        [Tooltip("1 = curva triangular (como el mock). 3 = cúbica, más concentrada.")]
        [SerializeField, Range(0.5f, 4f)] private float rollCurveExponent = 3f;

        private VisualElement glitchBand;

        private UIDocument document;
        private VisualElement scanlines;
        private VisualElement roll;
        private Label timeLabel;

        private Texture2D scanlineTexture;
        private Texture2D rollTexture;

        private bool bound;
        private int bindAttempts;

        // Cachés de estilo aplicado
        private float lastScanAlpha = -1f;
        private int lastScreenHeight = -1;
        private int lastLineCount = -1;
        private float lastRollAlpha = -1f;
        private float lastRollHeight = -1f;
        private float lastRollTime = float.NaN;
        private float lastCurveExponent = -1f;
        private bool rollVisible;

        // Debounce de redimensionado
        private int pendingHeight = -1;
        private float pendingSince;

        // Timecode
        private float startTime;
        private int lastSecond = -1;

        private void OnEnable()
        {
            startTime = Time.unscaledTime;
            GlitchDirector.Began += OnGlitchBegan;
            GlitchDirector.Ended += OnGlitchEnded;
            TryBind();                 // si el árbol aún no existe, Update lo reintenta
        }

        private void OnDisable()
        {
            GlitchDirector.Began -= OnGlitchBegan;
            GlitchDirector.Ended -= OnGlitchEnded;
            Unbind();
        }

        private void Update()
        {
            // El árbol de UI puede recrearse (UIDocument reactivado, cambio de UXML,
            // recarga de scripts). Los elementos viejos quedan sin panel y no se dibujan.
            if (!bound || scanlines.panel == null)
            {
                if (!TryBind()) return;
            }

            RefreshScanlines();
            UpdateTimecode();
            RefreshRoll();
        }

        private void OnDestroy()
        {
            if (scanlineTexture != null) Destroy(scanlineTexture);
            if (rollTexture != null) Destroy(rollTexture);
        }

        // ---------- Enlace ----------

        private bool TryBind()
        {
            Unbind();

            if (document == null) document = GetComponent<UIDocument>();
            VisualElement root = document != null ? document.rootVisualElement : null;

            scanlines = root?.Q<VisualElement>("scanlines");
            roll = root?.Q<VisualElement>("roll");
            timeLabel = root?.Q<Label>("osd-time");
            glitchBand = root?.Q<VisualElement>("glitch-band");

            if (scanlines == null || roll == null || timeLabel == null || glitchBand == null)
            {
                Unbind();

                if (++bindAttempts == 120)
                    Debug.LogError("VhsOverlay: no se encuentran los elementos del UXML " +
                                   "(scanlines, roll, osd-time, glitch-band).", this);
                return false;
            }
            // Política: si el árbol se recrea durante un fallo, el fallo se cancela. ToDo Revisar 
            glitchBand.style.display = DisplayStyle.None;

            root.pickingMode = PickingMode.Ignore;
            root.Query<VisualElement>().ForEach(e => e.pickingMode = PickingMode.Ignore);

            // Los elementos son nuevos: no tienen ningún estilo aplicado.
            lastScanAlpha = -1f;
            lastScreenHeight = -1;
            lastLineCount = -1;
            lastRollAlpha = -1f;
            lastRollHeight = -1f;
            lastRollTime = float.NaN;
            lastSecond = -1;
            rollVisible = false;
            pendingHeight = -1;
            InvalidateFx();

            roll.style.display = DisplayStyle.None;
            EnsureRollTexture();

            bound = true;
            bindAttempts = 0;
            return true;
        }

        private void Unbind()
        {
            bound = false;
            scanlines = null;
            roll = null;
            timeLabel = null;
            glitchBand = null;
        }

        // ---------- Scanlines ----------

        private void RefreshScanlines()
        {
            float alpha = Mathf.Clamp01(scanlineAlpha * Fx.scanlines);

            if (!Mathf.Approximately(alpha, lastScanAlpha))
            {
                scanlines.style.display = alpha <= 0f ? DisplayStyle.None : DisplayStyle.Flex;
                scanlines.style.unityBackgroundImageTintColor = new Color(1f, 1f, 1f, alpha);
                lastScanAlpha = alpha;
            }

            if (alpha <= 0f) return;

            int height = Mathf.Max(1, Screen.height);
            int count = Mathf.Max(1, linesPerScreen);

            bool needsRebuild = scanlineTexture == null ||
                                height != lastScreenHeight ||
                                count != lastLineCount;

            if (!needsRebuild) return;

            // Debounce: durante un redimensionado, Screen.height cambia cada frame.
            // Sin esto se crearía y destruiría una textura por frame.
            if (height != lastScreenHeight)
            {
                if (height != pendingHeight)
                {
                    pendingHeight = height;
                    pendingSince = Time.unscaledTime;
                    return;
                }

                if (Time.unscaledTime - pendingSince < ResizeSettleSeconds)
                    return;
            }

            BuildScanlines(height, count);
        }

        private void BuildScanlines(int height, int count)
        {
            // Periodo mínimo de 3 px: con 2, la mitad de las filas quedaría oscura.
            int period = Mathf.Max(3, Mathf.RoundToInt((float)height / count));
            int thickness = Mathf.Max(1, Mathf.RoundToInt(period / 3f));

            var texture = new Texture2D(1, height, TextureFormat.RGBA32, false)
            {
                name = "Vhs_Scanlines_Runtime",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color32[height];
            var dark = new Color32(0, 0, 0, 255);
            var clear = new Color32(0, 0, 0, 0);

            for (int y = 0; y < height; y++)
                pixels[y] = (y % period) < thickness ? dark : clear;

            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            Texture2D old = scanlineTexture;
            scanlineTexture = texture;
            scanlines.style.backgroundImage = new StyleBackground(scanlineTexture);

            if (old != null) Destroy(old);

            lastScreenHeight = height;
            lastLineCount = count;
            pendingHeight = -1;
        }

        // ---------- Timecode ----------

        private void UpdateTimecode()
        {
            // Contador de tiempo real: no depende del reloj retro ni de motion.
            int total = (int)(Time.unscaledTime - startTime);
            if (total == lastSecond) return;

            lastSecond = total;
            timeLabel.text = $"{total / 3600:00}:{total / 60 % 60:00}:{total % 60:00}";
        }

        // ---------- Roll ----------

        private void EnsureRollTexture()
        {
            if (rollTexture == null || !Mathf.Approximately(rollCurveExponent, lastCurveExponent))
                BuildRollTexture();

            roll.style.backgroundImage = new StyleBackground(rollTexture);
        }

        private void BuildRollTexture()
        {
            const int height = 128;

            var texture = new Texture2D(1, height, TextureFormat.RGBA32, false)
            {
                name = "Vhs_Roll_Runtime",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color32[height];

            for (int y = 0; y < height; y++)
            {
                float t = (y + 0.5f) / height;
                float a = 1f - Mathf.Abs(t * 2f - 1f);        // triangular
                a = Mathf.Pow(a, rollCurveExponent);          // 1 = mock, 3 = cúbica
                pixels[y] = new Color32(255, 70, 110, (byte)Mathf.RoundToInt(a * 255f));
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            Texture2D old = rollTexture;
            rollTexture = texture;
            if (old != null) Destroy(old);

            lastCurveExponent = rollCurveExponent;
        }

        private void RefreshRoll()
        {
            if (!Mathf.Approximately(rollCurveExponent, lastCurveExponent))
                EnsureRollTexture();

            float alpha = Mathf.Clamp01(rollAlpha * Fx.roll);
            bool shouldShow = alpha > 0f;

            if (shouldShow != rollVisible)
            {
                rollVisible = shouldShow;
                roll.style.display = shouldShow ? DisplayStyle.Flex : DisplayStyle.None;
                lastRollTime = float.NaN;      // fuerza a reposicionar al reaparecer
            }

            if (!rollVisible) return;

            if (!Mathf.Approximately(alpha, lastRollAlpha))
            {
                roll.style.unityBackgroundImageTintColor = new Color(1f, 1f, 1f, alpha);
                lastRollAlpha = alpha;
            }

            if (!Mathf.Approximately(rollHeightPercent, lastRollHeight))
            {
                roll.style.height = Length.Percent(rollHeightPercent);
                lastRollHeight = rollHeightPercent;
                lastRollTime = float.NaN;
            }

            float time = RetroFx.RetroTime;

            // Sin cambio de tiempo (motion = 0 con la banda visible): nada que reescribir.
            if (Mathf.Approximately(time, lastRollTime)) return;
            lastRollTime = time;

            float phase = Mathf.Repeat(time / rollPeriodSeconds, 1f);

            // translate en % del alto del propio elemento:
            // -100 % = justo encima de la pantalla; 10000 / alto% = justo debajo.
            float yPercent = Mathf.Lerp(-100f, 10000f / rollHeightPercent, phase);

            roll.style.translate = new StyleTranslate(
                new Translate(new Length(0f), Length.Percent(yPercent)));
        }


        // ---------- Glitch ----------
        private void OnGlitchBegan(GlitchEvent e)
        {
            if (!bound || glitchBand == null)
                return;

            glitchBand.style.top = Length.Percent(e.topPercent);
            glitchBand.style.height = Length.Percent(e.heightPercent);
            glitchBand.style.opacity = e.intensity;
            glitchBand.style.display = DisplayStyle.Flex;
        }

        private void OnGlitchEnded()
        {
            if (bound) glitchBand.style.display = DisplayStyle.None;
        }
    }
}