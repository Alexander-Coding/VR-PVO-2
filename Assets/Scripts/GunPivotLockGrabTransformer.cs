using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Transformers;

/// <summary>
/// Трансформер захвата: точка опоры (центр вращения) неподвижна. Горизонтальный поворот (yaw) — только вместе с платформой GunPlatformTurrel.
/// Пушка наклоняется только вверх/вниз (pitch) по камере; влево/вправо вращается платформа.
/// </summary>
public class GunPivotLockGrabTransformer : XRBaseGrabTransformer
{
    /// <summary>Ось, вокруг которой пушка наклоняется вверх/вниз (в локальном пространстве платформы).</summary>
    public enum PitchAxisType
    {
        Right,   // X — по умолчанию для наклона ствола вверх/вниз
        Up,      // Y
        Forward, // Z
        Custom   // customPitchAxis
    }

    [Header("Центр вращения пушки")]
    [Tooltip("Элемент, относительно которого вращается пушка (мировая позиция остаётся на месте). Обычно m60 receiver или точка крепления. Если не задан — ищется дочерний «m60 receiver» или pivot из M60VRShoot.")]
    [SerializeField] Transform pivotTransform;
    [Header("Ось наклона (вверх/вниз)")]
    [Tooltip("Ось, вокруг которой пушка наклоняется вверх/вниз (в локальных осях платформы). Right (X) — типично для ствола.")]
    [SerializeField] PitchAxisType pitchAxis = PitchAxisType.Right;
    [Tooltip("При Pitch Axis = Custom: направление оси в локальном пространстве платформы (нормализуется).")]
    [SerializeField] Vector3 customPitchAxis = Vector3.right;

    [Header("Платформа и камера")]

    [Tooltip("Платформа для горизонтального поворота (GunPlatformTurrel). Если не задана — ищется среди соседей по иерархии.")]
    [SerializeField] Transform platformTransform;
    [Tooltip("Камера головы (VR). Если не задана — Camera.main.")]
    [SerializeField] Camera headCamera;
    [Tooltip("Плавный переход при захвате (сек), чтобы пушка не дёргалась в сторону. 0 — без сглаживания.")]
    [SerializeField, Min(0f)] float grabBlendDuration = 0.15f;

    Vector3 _pivotLocalInPlatform;
    Vector3 _pivotLocalOffsetFromRoot;
    Transform _platform;
    bool _pivotCaptured;
    bool _hadSelected;
    float _initialGunPitch;
    float _initialCameraPitch;
    float _grabStartTime;
    Quaternion _initialRotation;
    /// <summary>Ориентация пушки в последний момент, когда она не была захвачена — используем при начале захвата, чтобы не дёргаться.</summary>
    Quaternion _lastRotationWhenNotSelected;

    protected override RegistrationMode registrationMode => RegistrationMode.Single;

    public override void OnLink(XRGrabInteractable grabInteractable)
    {
        base.OnLink(grabInteractable);
        Transform root = grabInteractable.transform;
        Transform pivot = GetPivot(root);
        _platform = platformTransform != null ? platformTransform : FindGunPlatformTurrel(root);
        _pivotCaptured = pivot != null && _platform != null;
        if (_pivotCaptured)
        {
            _pivotLocalOffsetFromRoot = root.InverseTransformPoint(pivot.position);
            _pivotLocalInPlatform = _platform.InverseTransformPoint(pivot.position);
            _lastRotationWhenNotSelected = root.rotation;
        }
        _hadSelected = false;
    }

    public override void Process(XRGrabInteractable grabInteractable, XRInteractionUpdateOrder.UpdatePhase updatePhase, ref Pose targetPose, ref Vector3 localScale)
    {
        if (!_pivotCaptured) return;

        bool isSelected = grabInteractable.isSelected;
        if (!isSelected)
        {
            _lastRotationWhenNotSelected = grabInteractable.transform.rotation;
            _hadSelected = false;
            return;
        }

        Vector3 pivotWorldPos = _platform.TransformPoint(_pivotLocalInPlatform);
        Vector3 localAxis = GetPitchAxisLocal();
        Camera cam = GetHeadCamera();

        if (cam != null)
        {
            if (!_hadSelected)
            {
                _hadSelected = true;
                _initialRotation = _lastRotationWhenNotSelected;
                Vector3 gunForwardWorld = _lastRotationWhenNotSelected * Vector3.forward;
                _initialGunPitch = GetPitchInPlatformLocalSpace(gunForwardWorld, localAxis);
                _initialCameraPitch = GetPitchInPlatformLocalSpace(cam.transform.forward, localAxis);
                _grabStartTime = Time.time;
                targetPose.rotation = _lastRotationWhenNotSelected;
            }
            else
            {
                float currentCameraPitch = GetPitchInPlatformLocalSpace(cam.transform.forward, localAxis);
                float deltaPitch = Mathf.DeltaAngle(_initialCameraPitch, currentCameraPitch);
                float targetPitch = _initialGunPitch + deltaPitch;
                Quaternion targetRotation = _platform.rotation * Quaternion.AngleAxis(targetPitch, localAxis);

                if (grabBlendDuration > 0f)
                {
                    float t = Mathf.Clamp01((Time.time - _grabStartTime) / grabBlendDuration);
                    targetPose.rotation = Quaternion.Slerp(_initialRotation, targetRotation, t);
                }
                else
                {
                    targetPose.rotation = targetRotation;
                }
            }
        }
        else
        {
            if (!_hadSelected)
            {
                _hadSelected = true;
                targetPose.rotation = _lastRotationWhenNotSelected;
            }
            else
            {
                targetPose.rotation = _platform.rotation;
            }
        }

        targetPose.position = pivotWorldPos - (targetPose.rotation * _pivotLocalOffsetFromRoot);
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

    Camera GetHeadCamera()
    {
        if (headCamera != null) return headCamera;
        // В VR Camera.main — камера головы (центр взгляда)
        return Camera.main;
    }

    Vector3 GetPitchAxisLocal()
    {
        switch (pitchAxis)
        {
            case PitchAxisType.Right:   return Vector3.right;
            case PitchAxisType.Up:      return Vector3.up;
            case PitchAxisType.Forward: return Vector3.forward;
            case PitchAxisType.Custom:  return customPitchAxis.sqrMagnitude > 0.0001f ? customPitchAxis.normalized : Vector3.right;
            default: return Vector3.right;
        }
    }

    /// <summary>Угол наклона вверх/вниз в локальном пространстве платформы: направление камеры → угол вокруг pitchAxis.</summary>
    float GetPitchInPlatformLocalSpace(Vector3 cameraForwardWorld, Vector3 pitchAxisLocal)
    {
        Vector3 camInPlatform = _platform.InverseTransformDirection(cameraForwardWorld);
        if (camInPlatform.sqrMagnitude < 0.0001f) return 0f;
        camInPlatform.Normalize();
        Vector3 forwardLocal = Vector3.forward;
        Vector3 inPlane = Vector3.ProjectOnPlane(camInPlatform, pitchAxisLocal);
        if (inPlane.sqrMagnitude < 0.0001f) return 0f;
        inPlane.Normalize();
        Vector3 refInPlane = Vector3.ProjectOnPlane(forwardLocal, pitchAxisLocal);
        if (refInPlane.sqrMagnitude < 0.0001f) refInPlane = Vector3.ProjectOnPlane(Vector3.right, pitchAxisLocal);
        refInPlane.Normalize();
        return Vector3.SignedAngle(refInPlane, inPlane, pitchAxisLocal);
    }

    Transform GetPivot(Transform root)
    {
        if (pivotTransform != null) return pivotTransform;
        var shoot = root.GetComponent<M60VRShoot>();
        if (shoot != null) return shoot.GetAttachPivot();
        return root.Find("m60 receiver");
    }
}
