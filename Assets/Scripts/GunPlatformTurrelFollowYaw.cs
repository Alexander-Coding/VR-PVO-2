using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Вешается на antiaircraft gun. Только YAW (горизонтальный поворот).
/// VR: платформа следует за поворотом головы. XR Origin НЕ вращается от головы
///     (VR-шлем сам отслеживает ориентацию — двойной поворот не нужен).
/// Клавиатура ←/→: вращают и платформу, и XR Origin (игрок "едет" вместе с пушкой).
/// Pitch (наклон стволов вверх/вниз) — в GunPivotFollowHeadPitch на GunPitchPivot.
/// </summary>
public class GunPlatformTurrelFollowYaw : MonoBehaviour
{
    [Header("Камера")]
    [Tooltip("VR-камера головы. Если не задана — Camera.main.")]
    [SerializeField] Camera headCamera;

    [Header("XR Origin")]
    [Tooltip("Корень XR Rig. Если не задан — ищется по иерархии камеры.")]
    [SerializeField] Transform xrOriginOverride;
    [Tooltip("При повороте клавишами ←/→ XR Origin тоже вращается (игрок едет вместе с пушкой).")]
    [SerializeField] bool rotateXROriginWithKeyboard = true;

    [Header("VR слежение — Yaw")]
    [Tooltip("Всегда следить за yaw головы, даже без захвата пушки.")]
    [SerializeField] bool vrAlwaysActive = true;

    [Header("Клавиатура — Yaw")]
    [SerializeField] bool keyboardAlwaysActive = true;
    [Tooltip("Скорость поворота клавишами (градусов/сек).")]
    [SerializeField] float keyboardYawSpeed = 60f;

    int _grabbedCount;
    float _prevCamYaw;
    bool _camYawCaptured;
    float _currentYaw;
    Transform _xrOriginCached;

    void Awake()
    {
        _currentYaw = transform.eulerAngles.y;

        var grabs = Object.FindObjectsByType<XRGrabInteractable>(FindObjectsSortMode.None);
        foreach (var g in grabs)
        {
            if (g.GetComponent<M60VRShoot>() == null) continue;
            g.selectEntered.AddListener(OnGunGrabbed);
            g.selectExited.AddListener(OnGunReleased);
        }
    }

    void OnDestroy()
    {
        var grabs = Object.FindObjectsByType<XRGrabInteractable>(FindObjectsSortMode.None);
        foreach (var g in grabs)
        {
            if (g.GetComponent<M60VRShoot>() == null) continue;
            g.selectEntered.RemoveListener(OnGunGrabbed);
            g.selectExited.RemoveListener(OnGunReleased);
        }
    }

    void OnGunGrabbed(SelectEnterEventArgs _) => _grabbedCount++;
    void OnGunReleased(SelectExitEventArgs _) { if (--_grabbedCount < 0) _grabbedCount = 0; }

    Camera GetCam() => headCamera != null ? headCamera : Camera.main;

    Transform GetXROrigin()
    {
        if (xrOriginOverride != null) return xrOriginOverride;
        if (_xrOriginCached != null) return _xrOriginCached;
        Camera cam = GetCam();
        if (cam == null) return null;
        Transform t = cam.transform;
        while (t.parent != null) t = t.parent;
        _xrOriginCached = t;
        return _xrOriginCached;
    }

    void LateUpdate()
    {
        Camera cam = GetCam();
        if (cam == null) return;

        float camYaw = cam.transform.eulerAngles.y;
        if (!_camYawCaptured) { _prevCamYaw = camYaw; _camYawCaptured = true; }

        bool vrActive = vrAlwaysActive || _grabbedCount > 0;

        // --- VR-поворот: только платформа, XR Origin НЕ трогаем ---
        // Вращение XR Origin от головы вызывало двойной счёт: на каждый 1 оборот
        // головы платформа вращалась ~0.5 оборота. Теперь 1:1.
        float vrDelta = 0f;
        if (vrActive)
            vrDelta = Mathf.DeltaAngle(_prevCamYaw, camYaw);

        // --- Клавиатура: вращаем платформу И XR Origin ---
        float keyDelta = 0f;
        if (keyboardAlwaysActive || vrActive)
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.rightArrowKey.isPressed) keyDelta += keyboardYawSpeed * Time.deltaTime;
                if (kb.leftArrowKey.isPressed)  keyDelta -= keyboardYawSpeed * Time.deltaTime;
            }
#else
            if (UnityEngine.Input.GetKey(KeyCode.RightArrow)) keyDelta += keyboardYawSpeed * Time.deltaTime;
            if (UnityEngine.Input.GetKey(KeyCode.LeftArrow))  keyDelta -= keyboardYawSpeed * Time.deltaTime;
#endif
        }

        float totalDelta = vrDelta + keyDelta;
        _currentYaw += totalDelta;

        if (Mathf.Abs(totalDelta) > 0.0001f)
        {
            Vector3 pivot = cam.transform.position;
            pivot.y = transform.position.y;
            transform.RotateAround(pivot, Vector3.up, totalDelta);
        }

        // Фиксируем только YAW на уровне antiaircraft gun (pitch — в GunPivotFollowHeadPitch)
        transform.rotation = Quaternion.Euler(0f, _currentYaw, 0f);

        // Клавишный поворот также двигает игрока
        if (Mathf.Abs(keyDelta) > 0.0001f && rotateXROriginWithKeyboard)
        {
            Transform xrOrigin = GetXROrigin();
            if (xrOrigin != null)
                xrOrigin.RotateAround(cam.transform.position, Vector3.up, keyDelta);
        }

        // Обновляем ПОСЛЕ всех поворотов, чтобы не считать наш же поворот повторно
        _prevCamYaw = cam.transform.eulerAngles.y;
    }
}
