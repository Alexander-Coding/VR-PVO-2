using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Вешается на GunPitchPivot. Управляет наклоном стволов вверх/вниз (pitch).
/// Вращение — по локальной оси Z GunPitchPivot (как требует ориентация модели).
/// VR: следим за дельтой наклона головы.
/// Клавиши ↑/↓ — наклон стволов.
/// </summary>
public class GunPivotFollowHeadPitch : MonoBehaviour
{
    [Header("Камера")]
    [Tooltip("VR-камера головы. Если не задана — Camera.main.")]
    [SerializeField] Camera headCamera;

    [Header("VR слежение — Pitch")]
    [SerializeField] bool vrAlwaysActive = true;

    [Header("Ограничения")]
    [Tooltip("Минимальный угол — 0 (горизонт, ниже нельзя).")]
    [SerializeField] float minPitch = 0f;
    [Tooltip("Максимальный угол наклона вверх (градусы).")]
    [SerializeField] float maxPitch = 55f;
    [Tooltip("Инвертировать направление VR-слежения (если стволы движутся в противоположную сторону).")]
    [SerializeField] bool invertVRPitch = false;

    [Header("Клавиатура — Pitch")]
    [SerializeField] bool keyboardAlwaysActive = true;
    [SerializeField] float keyboardPitchSpeed = 45f;
    [Tooltip("Инвертировать направление клавиш ↑/↓.")]
    [SerializeField] bool invertKeyboardPitch = false;

    float _currentPitch;
    float _prevCamPitch;
    bool _camPitchCaptured;

    Camera GetCam() => headCamera != null ? headCamera : Camera.main;

    void Awake()
    {
        Camera cam = GetCam();
        if (cam != null) { _prevCamPitch = GetCameraPitch(cam); _camPitchCaptured = true; }
        _currentPitch = 0f;
    }

    void LateUpdate()
    {
        // Если игрок не сидит за зениткой — не реагируем ни на что
        if (!TurretSeatController.IsMounted)
        {
            _camPitchCaptured = false; // сбросить, чтобы при посадке не было рывка
            return;
        }

        Camera cam = GetCam();
        if (cam == null) return;

        float camPitch = GetCameraPitch(cam);
        if (!_camPitchCaptured) { _prevCamPitch = camPitch; _camPitchCaptured = true; }

        // VR: дельта наклона головы
        if (vrAlwaysActive)
        {
            float delta = Mathf.DeltaAngle(_prevCamPitch, camPitch);
            _currentPitch += invertVRPitch ? -delta : delta;
        }
        _prevCamPitch = camPitch;

        // Клавиатура ↑/↓
        if (keyboardAlwaysActive)
        {
            float sign = invertKeyboardPitch ? -1f : 1f;
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.upArrowKey.isPressed)   _currentPitch += sign * keyboardPitchSpeed * Time.deltaTime;
                if (kb.downArrowKey.isPressed) _currentPitch -= sign * keyboardPitchSpeed * Time.deltaTime;
            }
#else
            if (UnityEngine.Input.GetKey(KeyCode.UpArrow))   _currentPitch += sign * keyboardPitchSpeed * Time.deltaTime;
            if (UnityEngine.Input.GetKey(KeyCode.DownArrow)) _currentPitch -= sign * keyboardPitchSpeed * Time.deltaTime;
#endif
        }

        _currentPitch = Mathf.Clamp(_currentPitch, minPitch, maxPitch);

        // Применяем как локальный поворот по оси Z (как требует ориентация модели).
        // Если стволы уходят в сторону вместо наклона вверх — включи invertVRPitch / invertKeyboardPitch
        // или смени на Quaternion.Euler(_currentPitch, 0f, 0f).
        transform.localRotation = Quaternion.Euler(0f, 0f, _currentPitch);
    }

    static float GetCameraPitch(Camera cam)
    {
        return Mathf.Asin(Mathf.Clamp(cam.transform.forward.y, -1f, 1f)) * Mathf.Rad2Deg;
    }
}
