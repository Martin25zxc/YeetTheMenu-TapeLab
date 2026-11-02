using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class VhsOverlay : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Si se deja vacío, se busca un RetroClock en la escena.")]
    [SerializeField] private RetroClock clock;

    [Header("Scanlines")]
    [SerializeField, Range(0f, 1f)]
    private float scanlineAlpha = 0.3f;

    [SerializeField, Min(1)]
    private int linesPerScreen = 360;

    [Header("Roll")]
    [SerializeField, Range(0f, 1f)]
    private float rollAlpha = 0.08f;

    [SerializeField, Min(0.5f)]
    private float rollPeriodSeconds = 9f;

    [Tooltip("Alto de la banda como % del alto de pantalla. Es la única fuente de verdad: el USS no lo define.")]
    [SerializeField, Range(5f, 40f)]
    private float rollHeightPercent = 14f;

    [Tooltip("Con Motion = 0 oculta la banda en lugar de dejarla congelada a mitad de pantalla.")]
    [SerializeField]
    private bool hideRollWhenFrozen = true;

    private UIDocument document;
    private VisualElement scanlines;
    private VisualElement roll;
    private Label timeLabel;

    private Texture2D scanlineTexture;
    private Texture2D rollTexture;

    // Scanlines
    private int lastScreenHeight = -1;
    private int lastLineCount = -1;
    private float lastAlpha = -1f;

    // Timecode
    private float startTime;
    private int lastSecond = -1;

    // Roll
    private bool rollVisible;
    private float lastRollTime = float.NaN;
    private float lastRollAlpha = -1f;
    private float lastRollHeight = -1f;

    private void Start()
    {
        document = GetComponent<UIDocument>();

        VisualElement root = document.rootVisualElement;

        if (root == null)
        {
            Debug.LogError("VhsOverlay: no hay un UXML asignado.", this);
            enabled = false;
            return;
        }

        scanlines = root.Q<VisualElement>("scanlines");
        roll = root.Q<VisualElement>("roll");
        Label playLabel = root.Q<Label>("osd-play");
        timeLabel = root.Q<Label>("osd-time");

        if (scanlines == null || roll == null ||
            playLabel == null || timeLabel == null)
        {
            Debug.LogError("VhsOverlay: faltan elementos en el UXML.", this);
            enabled = false;
            return;
        }

        if (clock == null)
            clock = FindFirstObjectByType<RetroClock>();

        // El overlay no debe capturar el puntero.
        root.pickingMode = PickingMode.Ignore;
        root.Query<VisualElement>().ForEach(
            element => element.pickingMode = PickingMode.Ignore);

        // Pistas de uso (experimento): se fijan antes de animar.
        roll.usageHints = UsageHints.DynamicTransform;
        scanlines.usageHints = UsageHints.LargePixelCoverage;

        // La banda empieza oculta; RefreshRoll decide cuándo mostrarla.
        roll.style.display = DisplayStyle.None;

        startTime = Time.unscaledTime;

        BuildRollTexture();
        RefreshScanlines();
    }

    private void Update()
    {
        RefreshScanlines();
        UpdateTimecode();   // 3a.3
        RefreshRoll();      // 3a.4
    }

    // ---------- Scanlines ----------

    private void RefreshScanlines()
    {
        if (scanlines == null)
            return;

        float alpha = Mathf.Clamp01(scanlineAlpha);

        if (!Mathf.Approximately(alpha, lastAlpha))
        {
            scanlines.style.display =
                alpha <= 0f ? DisplayStyle.None : DisplayStyle.Flex;

            scanlines.style.unityBackgroundImageTintColor =
                new Color(1f, 1f, 1f, alpha);

            lastAlpha = alpha;
        }

        if (alpha <= 0f)
            return;

        int height = Mathf.Max(1, Screen.height);
        int count = Mathf.Max(1, linesPerScreen);

        if (scanlineTexture == null ||
            height != lastScreenHeight ||
            count != lastLineCount)
        {
            BuildScanlines(height, count);
        }
    }

    private void BuildScanlines(int height, int count)
    {
        // Periodo mínimo de 3 px: con 2, la mitad de las filas quedaría oscura.
        int period = Mathf.Max(3, Mathf.RoundToInt((float)height / count));
        int thickness = Mathf.Max(1, Mathf.RoundToInt(period / 3f));

        Texture2D newTexture = new Texture2D(1, height, TextureFormat.RGBA32, false);
        newTexture.name = "Vhs_Scanlines_Runtime";
        newTexture.filterMode = FilterMode.Point;
        newTexture.wrapMode = TextureWrapMode.Clamp;

        Color32[] pixels = new Color32[height];
        Color32 dark = new Color32(0, 0, 0, 255);
        Color32 clear = new Color32(0, 0, 0, 0);

        for (int y = 0; y < height; y++)
            pixels[y] = (y % period) < thickness ? dark : clear;

        newTexture.SetPixels32(pixels);
        newTexture.Apply(false, true);

        Texture2D oldTexture = scanlineTexture;
        scanlineTexture = newTexture;
        scanlines.style.backgroundImage = new StyleBackground(scanlineTexture);

        if (oldTexture != null)
            Destroy(oldTexture);

        lastScreenHeight = height;
        lastLineCount = count;
    }

    // ---------- Timecode (3a.3) ----------

    private void UpdateTimecode()
    {
        // Contador de tiempo real: no depende de RetroClock ni de Motion.
        int total = (int)(Time.unscaledTime - startTime);

        if (total == lastSecond)
            return;

        lastSecond = total;

        int h = total / 3600;
        int m = total / 60 % 60;
        int s = total % 60;

        timeLabel.text = $"{h:00}:{m:00}:{s:00}";
    }

    // ---------- Roll (3a.4) ----------

    private void BuildRollTexture()
    {
        const int height = 128;

        rollTexture = new Texture2D(1, height, TextureFormat.RGBA32, false);
        rollTexture.name = "Vhs_Roll_Runtime";
        rollTexture.filterMode = FilterMode.Bilinear;
        rollTexture.wrapMode = TextureWrapMode.Clamp;

        // Equivale a linear-gradient(transparent, color, transparent) del mock.
        Color32[] pixels = new Color32[height];

        for (int y = 0; y < height; y++)
        {
            float t = (y + 0.5f) / height;
            float a = 1f - Mathf.Abs(t * 2f - 1f);   // 0 en los bordes, 1 en el centro
            pixels[y] = new Color32(255, 70, 110, (byte)Mathf.RoundToInt(a * 255f));
        }

        rollTexture.SetPixels32(pixels);
        rollTexture.Apply(false, true);

        roll.style.backgroundImage = new StyleBackground(rollTexture);
    }

    private void RefreshRoll()
    {
        float alpha = Mathf.Clamp01(rollAlpha);
        bool frozen = hideRollWhenFrozen && clock != null && clock.Motion <= 0f;
        bool shouldShow = alpha > 0f && !frozen;

        // Solo se toca el estilo cuando el estado cambia.
        if (shouldShow != rollVisible)
        {
            rollVisible = shouldShow;
            roll.style.display = shouldShow ? DisplayStyle.Flex : DisplayStyle.None;
            lastRollTime = float.NaN;   // fuerza a reposicionar al reaparecer
        }

        if (!rollVisible)
            return;

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

        float time = clock != null ? clock.RetroTime : Time.unscaledTime;

        // Sin cambio de tiempo (Motion = 0 con la banda visible): no hay nada que reescribir.
        if (Mathf.Approximately(time, lastRollTime))
            return;

        lastRollTime = time;

        float phase = Mathf.Repeat(time / rollPeriodSeconds, 1f);

        // translate en % del alto del propio elemento:
        // -100 % = justo encima de la pantalla; 10000 / alto% = justo debajo.
        float endPercent = 10000f / rollHeightPercent;
        float yPercent = Mathf.Lerp(-100f, endPercent, phase);

        roll.style.translate = new StyleTranslate(
            new Translate(new Length(0f), Length.Percent(yPercent)));
    }

    private void OnDestroy()
    {
        if (scanlineTexture != null) Destroy(scanlineTexture);
        if (rollTexture != null) Destroy(rollTexture);
    }
}