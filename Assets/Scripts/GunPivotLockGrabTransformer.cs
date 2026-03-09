using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Transformers;

/// <summary>
/// Трансформер захвата: блокирует дефолтное вращение XR Grab и фиксирует пушку на платформе.
/// Вращение исключительно по оси Y — только вместе с платформой (GunPlatformTurrel / antiaircraft gun).
/// Позиция пушки не меняется — она остаётся на месте как дочерний объект платформы.
/// </summary>
[DefaultExecutionOrder(100)]
public class GunPivotLockGrabTransformer : XRBaseGrabTransformer
{
    [Header("Платформа")]
    [Tooltip("Платформа для горизонтального поворота (GunPlatformTurrel). Если не задана — ищется среди соседей по иерархии.")]
    [SerializeField] Transform platformTransform;

    [Header("Сглаживание")]
    [Tooltip("Плавный переход при захвате (сек), чтобы пушка не дёргалась. 0 — без сглаживания.")]
    [SerializeField, Min(0f)] float grabBlendDuration = 0.15f;

    Transform _platform;
    bool _platformFound;
    bool _hadSelected;
    float _grabStartTime;
    Quaternion _initialRotation;
    Quaternion _gunLocalRotAtGrab;

    protected override RegistrationMode registrationMode => RegistrationMode.Single;

    public override void OnLink(XRGrabInteractable grabInteractable)
    {
        base.OnLink(grabInteractable);
        Transform root = grabInteractable.transform;
        _platform = platformTransform != null ? platformTransform : FindGunPlatformTurrel(root);
        _platformFound = _platform != null;
        _hadSelected = false;
    }

    public override void Process(XRGrabInteractable grabInteractable, XRInteractionUpdateOrder.UpdatePhase updatePhase, ref Pose targetPose, ref Vector3 localScale)
    {
        if (!_platformFound) return;

        bool isSelected = grabInteractable.isSelected;
        if (!isSelected)
        {
            _hadSelected = false;
            return;
        }

        if (!_hadSelected)
        {
            _hadSelected = true;
            _initialRotation = grabInteractable.transform.rotation;
            _gunLocalRotAtGrab = Quaternion.Inverse(_platform.rotation) * _initialRotation;
            _grabStartTime = Time.time;
        }

        Quaternion targetRotation = _platform.rotation * _gunLocalRotAtGrab;

        if (grabBlendDuration > 0f && (Time.time - _grabStartTime) < grabBlendDuration)
        {
            float t = Mathf.Clamp01((Time.time - _grabStartTime) / grabBlendDuration);
            targetPose.rotation = Quaternion.Slerp(_initialRotation, targetRotation, t);
        }
        else
        {
            targetPose.rotation = targetRotation;
        }

        targetPose.position = grabInteractable.transform.position;
    }

    static Transform FindGunPlatformTurrel(Transform root)
    {
        for (Transform t = root; t != null; t = t.parent)
        {
            if (t.parent != null)
            {
                Transform platform = t.parent.Find("GunPlatformTurrel");
                if (platform != null) return platform;
            }
        }
        return null;
    }
}
