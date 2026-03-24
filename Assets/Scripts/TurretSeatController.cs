using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Unity.XR.CoreUtils;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Управляет посадкой/высадкой игрока за зенитку GunPlatformTurrel.
/// При старте игрок сидит за зениткой — всё управление работает.
/// F — высадиться (зенитка не реагирует, ходьба разблокирована, контроллеры видны).
/// Подойти к зенитке и нажать F — снова сесть (телепорт на spawn-point).
/// </summary>
public class TurretSeatController : MonoBehaviour
{
    /// <summary>Игрок сидит за зениткой — управление активно.</summary>
    public static bool IsMounted { get; private set; } = true;

    [Header("Зенитка")]
    [Tooltip("Transform GunPlatformTurrel. Если не задан — ищется по имени в сцене.")]
    [SerializeField] Transform turret;

    [Header("Посадка")]
    [Tooltip("Точка телепортации при посадке за зенитку. Если не задана — ищется объект 'spawn-point'.")]
    [SerializeField] Transform spawnPoint;
    [Tooltip("Максимальное расстояние до зенитки для повторной посадки (м).")]
    [SerializeField] float mountDistance = 5f;

    // XR Origin
    Transform  _xrOrigin;
    XROrigin   _xrOriginComponent;
    Vector3    _mountedXROriginPos; // позиция XR Origin при посадке — принудительно удерживаем

    // Начальные состояния
    Vector3    _initialCameraForward;   // захватывается в первый кадр (после VR-трекинга)
    bool       _initialCameraForwardCaptured;
    Transform  _antiAircraftGun;
    Vector3    _initialGunPos;
    Quaternion _initialGunRot;

    GameObject  _locomotionGO;        // объект Locomotion — отключаем целиком при посадке
    Behaviour[] _locomotion;          // fallback: отдельные провайдеры если объект не найден
    Renderer[]  _controllerRenderers; // модели контроллеров — скрываем при посадке

    Text   _promptText;
    Camera _cam;

    const string TurretName = "GunPlatformTurrel";

    void Awake()
    {
        IsMounted = true;

        // Захватываем положение зенитки в Awake — до любых скриптов и трекинга
        var gun = GameObject.Find("antiaircraft gun");
        if (gun != null)
        {
            _antiAircraftGun = gun.transform;
            _initialGunPos   = gun.transform.position;
            _initialGunRot   = gun.transform.rotation;
        }
    }

    void Start()
    {
        _cam      = Camera.main;
        _xrOrigin = FindXROrigin();
        if (_xrOrigin != null)
            _xrOriginComponent = _xrOrigin.GetComponent<XROrigin>();

        if (turret == null) turret = FindTurret();
        if (spawnPoint == null)
        {
            var sp = GameObject.Find("spawn-point");
            if (sp != null) spawnPoint = sp.transform;
        }

        _locomotionGO        = FindLocomotionGO();
        _locomotion          = FindLocomotion();
        _controllerRenderers = FindControllerRenderers();
        _promptText          = CreatePromptUI();

        // Сразу применяем начальное состояние (сидим за зениткой)
        if (_xrOrigin != null) _mountedXROriginPos = _xrOrigin.position;
        ApplyMountedState();
    }

    void Update()
    {
        bool pressed;
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        pressed = kb != null && kb.fKey.wasPressedThisFrame;
#else
        pressed = Input.GetKeyDown(KeyCode.F);
#endif

        // Захватываем направление камеры в первый кадр (VR-трекинг уже устоялся)
        if (!_initialCameraForwardCaptured && _cam != null)
        {
            Vector3 fwd = _cam.transform.forward; fwd.y = 0f;
            _initialCameraForward            = fwd.sqrMagnitude > 0.001f ? fwd.normalized : Vector3.forward;
            _initialCameraForwardCaptured    = true;
        }

        if (pressed)
        {
            if (IsMounted)
                Dismount();
            else if (NearTurret())
                Mount();
        }

        RefreshPrompt();
    }

    void LateUpdate()
    {
        // Жёстко фиксируем позицию XR Origin при посадке.
        // Блокирует любое движение: XR Device Simulator, joystick, всё.
        if (IsMounted && _xrOrigin != null)
            _xrOrigin.position = _mountedXROriginPos;
    }

    // ── Посадка / Высадка ────────────────────────────────────────────────────

    void Mount()
    {
        // Телепортируем на spawn-point
        if (spawnPoint != null && _xrOrigin != null)
        {
            if (_xrOriginComponent != null)
            {
                // Возвращаем antiaircraft gun в исходное положение
                if (_antiAircraftGun != null)
                {
                    _antiAircraftGun.SetPositionAndRotation(_initialGunPos, _initialGunRot);
                    // Сбрасываем внутренний yaw, чтобы LateUpdate не перебил сброс
                    _antiAircraftGun.GetComponentInChildren<GunPlatformTurrelFollowYaw>()
                        ?.ResetToInitialYaw();
                }

                // Корректный XR-телепорт: учитывает смещение камеры
                _xrOriginComponent.MoveCameraToWorldLocation(spawnPoint.position);

                // Поворот камеры: возвращаем исходное направление взгляда при старте
                Vector3 targetForward = _initialCameraForwardCaptured ? _initialCameraForward : spawnPoint.forward;
                _xrOriginComponent.MatchOriginUpCameraForward(Vector3.up, targetForward);
            }
            else
            {
                // Fallback: прямое перемещение
                _xrOrigin.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);
            }
            Debug.Log($"[TurretSeat] Телепорт на {spawnPoint.position}");
        }
        else
        {
            Debug.LogWarning("[TurretSeat] spawn-point или XR Origin не найден — телепорт пропущен.");
        }

        IsMounted = true;
        if (_xrOrigin != null) _mountedXROriginPos = _xrOrigin.position;
        ApplyMountedState();
        Debug.Log("[TurretSeat] Сел за зенитку.");
    }

    void Dismount()
    {
        IsMounted = false;
        ApplyMountedState();
        Debug.Log("[TurretSeat] Покинул зенитку.");
    }

    /// <summary>Включает/выключает ходьбу и видимость контроллеров по состоянию IsMounted.</summary>
    void ApplyMountedState()
    {
        // Ходьба и повороты — отключаем весь объект Locomotion (LocomotionMediator + все провайдеры)
        if (_locomotionGO != null)
        {
            _locomotionGO.SetActive(!IsMounted);
        }
        else
        {
            // Fallback: отключаем провайдеры по отдельности
            foreach (var b in _locomotion)
                if (b != null) b.enabled = !IsMounted;
        }

        // Модели контроллеров — скрыты когда сидим
        foreach (var r in _controllerRenderers)
            if (r != null) r.enabled = !IsMounted;
    }

    // ── Вспомогательное ─────────────────────────────────────────────────────

    bool NearTurret()
    {
        if (turret == null || _cam == null) return true;
        Vector3 a = _cam.transform.position; a.y = 0f;
        Vector3 b = turret.position;          b.y = 0f;
        return Vector3.Distance(a, b) <= mountDistance;
    }

    void RefreshPrompt()
    {
        if (_promptText == null) return;
        bool show = !IsMounted && NearTurret();
        if (_promptText.enabled != show)
            _promptText.enabled = show;
    }

    Transform FindXROrigin()
    {
        // Пробуем оба возможных имени
        var go = GameObject.Find("XR Origin (XR Rig)") ?? GameObject.Find("XR Origin");
        if (go != null) return go.transform;
        // Fallback: ищем по компоненту XROrigin
        var xrOrigin = FindFirstObjectByType<XROrigin>();
        return xrOrigin != null ? xrOrigin.transform : null;
    }

    Transform FindTurret()
    {
        var go = GameObject.Find(TurretName);
        if (go != null) return go.transform;
        foreach (var t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
            if (t.name == TurretName) return t;
        return null;
    }

    /// <summary>Находит объект Locomotion — дочерний к XR Origin, содержит LocomotionMediator и всех провайдеров.</summary>
    GameObject FindLocomotionGO()
    {
        if (_xrOrigin != null)
        {
            var t = _xrOrigin.Find("Locomotion");
            if (t != null) return t.gameObject;
        }
        var go = GameObject.Find("Locomotion");
        if (go != null) return go;
        Debug.LogWarning("[TurretSeat] Объект Locomotion не найден — движение не будет заблокировано.");
        return null;
    }

    /// <summary>Находит все провайдеры движения и поворота XR Interaction Toolkit (fallback).</summary>
    Behaviour[] FindLocomotion()
    {
        var result = new List<Behaviour>();
        foreach (var b in FindObjectsByType<Behaviour>(FindObjectsSortMode.None))
        {
            if (b == null) continue;
            string n = b.GetType().Name;
            if (n.Contains("MoveProvider")
             || n.Contains("TeleportationProvider")
             || n.Contains("SnapTurnProvider")
             || n.Contains("ContinuousTurnProvider"))
                result.Add(b);
        }
        return result.ToArray();
    }

    /// <summary>
    /// Ищет рендереры моделей контроллеров/рук внутри XR Origin.
    /// Исключает камеру и сам корневой объект.
    /// </summary>
    Renderer[] FindControllerRenderers()
    {
        if (_xrOrigin == null) return new Renderer[0];

        var result = new List<Renderer>();
        foreach (var r in _xrOrigin.GetComponentsInChildren<Renderer>(true))
        {
            if (r.gameObject == _xrOrigin.gameObject) continue; // корень — пропуск
            if (r.GetComponent<Camera>() != null) continue;      // камера — пропуск
            result.Add(r);
        }
        return result.ToArray();
    }

    Text CreatePromptUI()
    {
        var canvasGo = new GameObject("TurretPromptCanvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        canvasGo.AddComponent<CanvasScaler>();
        canvasGo.AddComponent<GraphicRaycaster>();

        var textGo = new GameObject("PromptText");
        textGo.transform.SetParent(canvasGo.transform, false);

        var txt = textGo.AddComponent<Text>();
        txt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize  = 28;
        txt.color     = Color.white;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.text      = "F — сесть за зенитку";
        txt.enabled   = false;

        var rt = textGo.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.2f, 0.08f);
        rt.anchorMax = new Vector2(0.8f, 0.16f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        return txt;
    }
}
