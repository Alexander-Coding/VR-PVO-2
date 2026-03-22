using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Прицел, привязанный к направлению ствола.
/// Вокруг прицела — дуга перегрева: пустая → заполняется красной полоской по часовой.
/// </summary>
public class CrosshairUI : MonoBehaviour
{
    [Header("Прицел")]
    [SerializeField] Color crosshairColor = Color.white;
    [SerializeField] float lineThickness = 2f;
    [SerializeField] float lineLength = 20f;
    [SerializeField] float gap = 5f;

    [Header("Кольцо перегрева")]
    [SerializeField] float ringDiameter  = 72f;
    [Tooltip("Толщина кольца в пикселях текстуры (из 128)")]
    [SerializeField] float ringThickness = 14f;
    [SerializeField] Color heatColor     = new Color(1f, 0.15f, 0f, 1f);
    [SerializeField] Color overheatColor = new Color(1f, 0f, 0f, 1f);

    [Header("Прицел — аим")]
    [SerializeField] float aimDistance = 500f;
    [Tooltip("Ствол. Если не задан — ищется 'm60 barrel 22in' в сцене.")]
    [SerializeField] Transform barrelTransform;

    Canvas        _canvas;
    RectTransform _canvasRect;
    RectTransform _crosshairRoot;
    Image         _heatArc;
    Camera        _cam;
    M60VRShoot[]  _guns;

    void Awake()
    {
        _canvas = GetComponent<Canvas>();
        if (_canvas == null) _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 100;

        if (GetComponent<CanvasScaler>() == null)
        {
            var s = gameObject.AddComponent<CanvasScaler>();
            s.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            s.referenceResolution = new Vector2(1920, 1080);
            s.matchWidthOrHeight  = 0.5f;
        }
        if (GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();

        _canvasRect = GetComponent<RectTransform>();

        // Корневой контейнер прицела
        var rootGO = new GameObject("CrosshairRoot");
        rootGO.transform.SetParent(transform, false);
        _crosshairRoot = rootGO.AddComponent<RectTransform>();
        _crosshairRoot.anchorMin        = new Vector2(0.5f, 0.5f);
        _crosshairRoot.anchorMax        = new Vector2(0.5f, 0.5f);
        _crosshairRoot.pivot            = new Vector2(0.5f, 0.5f);
        _crosshairRoot.anchoredPosition = Vector2.zero;
        _crosshairRoot.sizeDelta        = Vector2.zero;

        // Дуга перегрева (процедурный кольцевой спрайт)
        var arcGO = new GameObject("HeatArc");
        arcGO.transform.SetParent(_crosshairRoot, false);
        _heatArc = arcGO.AddComponent<Image>();
        _heatArc.sprite      = CreateRingSprite(128, ringThickness);
        _heatArc.type        = Image.Type.Filled;
        _heatArc.fillMethod  = Image.FillMethod.Radial360;
        _heatArc.fillOrigin  = (int)Image.Origin360.Top;
        _heatArc.fillClockwise = true;
        _heatArc.fillAmount  = 0f;
        _heatArc.color       = heatColor;
        var arcRect = _heatArc.rectTransform;
        arcRect.anchorMin        = new Vector2(0.5f, 0.5f);
        arcRect.anchorMax        = new Vector2(0.5f, 0.5f);
        arcRect.pivot            = new Vector2(0.5f, 0.5f);
        arcRect.sizeDelta        = new Vector2(ringDiameter, ringDiameter);
        arcRect.anchoredPosition = Vector2.zero;
        _heatArc.gameObject.SetActive(false);

        // Линии прицела (поверх дуги)
        float half = gap * 0.5f + lineLength * 0.5f;
        CreateBar("CH_Left",  new Vector2(lineLength, lineThickness), new Vector2(-half,  0f));
        CreateBar("CH_Right", new Vector2(lineLength, lineThickness), new Vector2( half,  0f));
        CreateBar("CH_Up",    new Vector2(lineThickness, lineLength), new Vector2(  0f,  half));
        CreateBar("CH_Down",  new Vector2(lineThickness, lineLength), new Vector2(  0f, -half));
    }

    void Start()
    {
        _cam  = Camera.main;
        _guns = Object.FindObjectsByType<M60VRShoot>(FindObjectsSortMode.None);
        if (barrelTransform == null)
        {
            var go = GameObject.Find("m60 barrel 22in");
            if (go != null) barrelTransform = go.transform;
        }
    }

    void LateUpdate()
    {
        // Двигаем прицел по экрану
        if (_cam == null) _cam = Camera.main;
        if (_cam != null && barrelTransform != null)
        {
            Vector3 aimWorld = barrelTransform.position + (-barrelTransform.right) * aimDistance;
            Vector3 screen   = _cam.WorldToScreenPoint(aimWorld);
            bool inFront     = screen.z > 0f;
            _crosshairRoot.gameObject.SetActive(inFront);
            if (inFront)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _canvasRect, new Vector2(screen.x, screen.y), null, out Vector2 local);
                _crosshairRoot.anchoredPosition = local;
            }
        }

        // Обновляем дугу
        UpdateHeatArc();
    }

    void UpdateHeatArc()
    {
        if (_heatArc == null || _guns == null) return;

        float heat      = 0f;
        bool overheated = false;
        foreach (var g in _guns)
        {
            if (g == null) continue;
            float h = g.HeatFraction;
            if (h > heat) heat = h;
            if (g.IsOverheated) overheated = true;
        }

        bool show = heat > 0.005f;
        _heatArc.gameObject.SetActive(show);
        if (!show) return;

        _heatArc.fillAmount = heat;

        if (overheated)
        {
            // Мигание при перегреве
            float blink = Mathf.PingPong(Time.time * 5f, 1f);
            _heatArc.color = Color.Lerp(heatColor, overheatColor, blink);
        }
        else
        {
            _heatArc.color = heatColor;
        }
    }

    /// <summary>
    /// Создаёт белый кольцевой спрайт с мягкими краями.
    /// </summary>
    static Sprite CreateRingSprite(int resolution, float thickness)
    {
        var tex = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode   = TextureWrapMode.Clamp;

        float center  = resolution * 0.5f;
        float outerR  = center - 1f;
        float innerR  = outerR - thickness;
        float feather = 1.5f;

        var pixels = new Color32[resolution * resolution];
        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float dx   = x - center + 0.5f;
                float dy   = y - center + 0.5f;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                float outerAlpha = Mathf.Clamp01((outerR - dist) / feather);
                float innerAlpha = Mathf.Clamp01((dist - innerR) / feather);
                byte  a          = (byte)(Mathf.Min(outerAlpha, innerAlpha) * 255f);
                pixels[y * resolution + x] = new Color32(255, 255, 255, a);
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, resolution, resolution), new Vector2(0.5f, 0.5f));
    }

    void CreateBar(string barName, Vector2 size, Vector2 offset)
    {
        var go = new GameObject(barName);
        go.transform.SetParent(_crosshairRoot, false);
        var img = go.AddComponent<Image>();
        img.color = crosshairColor;
        var rect = img.rectTransform;
        rect.anchorMin        = new Vector2(0.5f, 0.5f);
        rect.anchorMax        = new Vector2(0.5f, 0.5f);
        rect.pivot            = new Vector2(0.5f, 0.5f);
        rect.sizeDelta        = size;
        rect.anchoredPosition = offset;
    }
}
