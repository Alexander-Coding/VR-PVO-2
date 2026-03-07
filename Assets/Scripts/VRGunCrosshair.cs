using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Показывает прицел по центру экрана, когда захвачена хотя бы одна пушка (m60/m60-1 с M60VRShoot).
/// Прицел виден только в режиме прицеливания головой — куда смотришь, туда и стреляешь.
/// </summary>
[RequireComponent(typeof(Canvas))]
public class VRGunCrosshair : MonoBehaviour
{
    [Header("Внешний вид")]
    [Tooltip("Цвет прицела")]
    public Color crosshairColor = new Color(1f, 0.2f, 0.2f, 0.9f);
    [Tooltip("Размер прицела в пикселях")]
    public float size = 24f;
    [Tooltip("Толщина линий прицела")]
    public float thickness = 3f;

    Canvas _canvas;
    GameObject _crosshairRoot;
    int _grabbedCount;

    void Awake()
    {
        _canvas = GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 32767;
        _canvas.worldCamera = null;
        CreateCrosshairUI();
        _crosshairRoot.SetActive(false);

        // Подписка на все пушки с M60VRShoot в сцене
        var grabInteractables = Object.FindObjectsByType<XRGrabInteractable>(FindObjectsSortMode.None);
        foreach (var grab in grabInteractables)
        {
            if (grab.GetComponent<M60VRShoot>() == null) continue;
            grab.selectEntered.AddListener(OnGunGrabbed);
            grab.selectExited.AddListener(OnGunReleased);
        }
    }

    void OnDestroy()
    {
        var grabInteractables = Object.FindObjectsByType<XRGrabInteractable>(FindObjectsSortMode.None);
        foreach (var grab in grabInteractables)
        {
            if (grab.GetComponent<M60VRShoot>() == null) continue;
            grab.selectEntered.RemoveListener(OnGunGrabbed);
            grab.selectExited.RemoveListener(OnGunReleased);
        }
    }

    void OnGunGrabbed(SelectEnterEventArgs _)
    {
        _grabbedCount++;
        if (_crosshairRoot != null)
            _crosshairRoot.SetActive(true);
    }

    void OnGunReleased(SelectExitEventArgs _)
    {
        _grabbedCount--;
        if (_grabbedCount <= 0)
        {
            _grabbedCount = 0;
            if (_crosshairRoot != null)
                _crosshairRoot.SetActive(false);
        }
    }

    void CreateCrosshairUI()
    {
        _crosshairRoot = new GameObject("Crosshair");
        _crosshairRoot.transform.SetParent(transform, false);

        var rect = _crosshairRoot.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(size * 2f, size * 2f);

        // Горизонтальная линия
        var h = CreateLine("LineH", new Vector2(size * 2f, thickness));
        h.transform.SetParent(_crosshairRoot.transform, false);
        h.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;

        // Вертикальная линия
        var v = CreateLine("LineV", new Vector2(thickness, size * 2f));
        v.transform.SetParent(_crosshairRoot.transform, false);
        v.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;

        // Опционально: точка в центре
        var dot = new GameObject("Dot");
        dot.transform.SetParent(_crosshairRoot.transform, false);
        var dotImage = dot.AddComponent<Image>();
        dotImage.color = crosshairColor;
        var dotRect = dot.GetComponent<RectTransform>();
        dotRect.anchorMin = new Vector2(0.5f, 0.5f);
        dotRect.anchorMax = new Vector2(0.5f, 0.5f);
        dotRect.pivot = new Vector2(0.5f, 0.5f);
        dotRect.anchoredPosition = Vector2.zero;
        dotRect.sizeDelta = new Vector2(thickness, thickness);
    }

    GameObject CreateLine(string name, Vector2 sizeDelta)
    {
        var go = new GameObject(name);
        var image = go.AddComponent<Image>();
        image.color = crosshairColor;
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = sizeDelta;
        return go;
    }
}
