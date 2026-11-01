
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class VhsOverlay : MonoBehaviour
{
    [Header("Scanlines")]
    [SerializeField, Range(0f, 1f)]
    private float scanlineAlpha = 0.55f;

    [SerializeField, Min(1)]
    private int linesPerScreen = 360;

    private UIDocument document;
    private VisualElement scanlines;
    private Texture2D scanlineTexture;

    private int lastScreenHeight = -1;
    private int lastLineCount = -1;
    private float lastAlpha = -1f;

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

        VisualElement roll = root.Q<VisualElement>("roll");
        Label playLabel = root.Q<Label>("osd-play");
        Label timeLabel = root.Q<Label>("osd-time");

        if (scanlines == null || roll == null ||
            playLabel == null || timeLabel == null)
        {
            Debug.LogError(
                "VhsOverlay: faltan elementos en el UXML.",
                this);

            enabled = false;
            return;
        }

        // El overlay no debe capturar el puntero.
        root.pickingMode = PickingMode.Ignore;

        root.Query<VisualElement>().ForEach(
            element => element.pickingMode = PickingMode.Ignore);

        // El roll se implementará en 3a.4.
        roll.style.display = DisplayStyle.None;

        RefreshScanlines();
    }

    private void Update()
    {
        RefreshScanlines();
    }

    private void RefreshScanlines()
    {
        if (scanlines == null)
            return;

        float alpha = Mathf.Clamp01(scanlineAlpha);

        // No modificar estilos si el valor no cambió.
        if (!Mathf.Approximately(alpha, lastAlpha))
        {
            scanlines.style.display =
                alpha <= 0f
                    ? DisplayStyle.None
                    : DisplayStyle.Flex;

            scanlines.style.unityBackgroundImageTintColor =
                new Color(1f, 1f, 1f, alpha);

            lastAlpha = alpha;
        }

        // Si está desactivado, no necesita reconstruirse.
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
        // Aproximadamente 360 líneas para cualquier resolución.
        int period = Mathf.Max(
            2,
            Mathf.RoundToInt((float)height / count));

        int thickness = Mathf.Max(
            1,
            Mathf.RoundToInt(period / 3f));

        Texture2D newTexture = new Texture2D(
            1,
            height,
            TextureFormat.RGBA32,
            false);

        newTexture.name = "Vhs_Scanlines_Runtime";
        newTexture.filterMode = FilterMode.Point;
        newTexture.wrapMode = TextureWrapMode.Clamp;

        Color32[] pixels = new Color32[height];

        Color32 dark = new Color32(0, 0, 0, 255);
        Color32 clear = new Color32(0, 0, 0, 0);

        for (int y = 0; y < height; y++)
        {
            pixels[y] =
                (y % period) < thickness ? dark : clear;
        }

        newTexture.SetPixels32(pixels);
        newTexture.Apply(false, true);

        Texture2D oldTexture = scanlineTexture;
        scanlineTexture = newTexture;

        scanlines.style.backgroundImage =
            new StyleBackground(scanlineTexture);

        if (oldTexture != null)
            Destroy(oldTexture);

        lastScreenHeight = height;
        lastLineCount = count;
    }

    private void OnDestroy()
    {
        if (scanlineTexture != null)
        {
            Destroy(scanlineTexture);
            scanlineTexture = null;
        }
    }
}
