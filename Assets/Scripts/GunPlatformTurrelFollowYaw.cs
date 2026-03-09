using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Вешается на платформу GunPlatformTurrel. Когда захвачена хотя бы одна пушка (m60/m60-1),
/// платформа поворачивается по горизонтали (yaw) за камерой — влево/вправо куда смотришь.
/// Пушки при этом могут наклоняться только вверх/вниз; горизонтальный поворот только вместе с платформой.
/// </summary>
public class GunPlatformTurrelFollowYaw : MonoBehaviour
{
    public enum RotationAxisType
    {
        WorldY,
        WorldX,
        WorldZ,
        LocalY,
        LocalX,
        LocalZ,
        Custom
    }

    [Header("Центр и ось вращения")]
    [Tooltip("Объект, относительно которого вращается платформа (мировая позиция остаётся неподвижной). Если не задан — вращение вокруг собственной позиции платформы.")]
    [SerializeField] Transform rotationCenter;
    [Tooltip("Ось вращения: World — глобальные оси; Local — локальные оси платформы (transform.up/right/forward).")]
    [SerializeField] RotationAxisType rotationAxis = RotationAxisType.LocalY;
    [Tooltip("Используется при Custom: направление оси вращения (нормализуется). Либо задайте Transform в customAxisTransform.")]
    [SerializeField] Vector3 customAxis = Vector3.up;
    [Tooltip("При Custom: ось берётся как forward этого объекта (если задан). Иначе используется customAxis.")]
    [SerializeField] Transform customAxisTransform;

    [Header("Камера")]
    [Tooltip("Камера головы (VR). Если не задана — Camera.main.")]
    [SerializeField] Camera headCamera;

    int _grabbedCount;
    bool _capturedInitialAngles;
    float _initialPlatformAngle;
    float _initialCameraAngle;

    void Awake()
    {
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

    void OnGunGrabbed(SelectEnterEventArgs _) => _grabbedCount++;
    void OnGunReleased(SelectExitEventArgs _)
    {
        _grabbedCount--;
        if (_grabbedCount <= 0)
        {
            _grabbedCount = 0;
            _capturedInitialAngles = false;
        }
    }

    void LateUpdate()
    {
        if (_grabbedCount <= 0) return;

        Camera cam = headCamera != null ? headCamera : Camera.main;
        if (cam == null) return;

        Vector3 axis = GetRotationAxis();
        if (axis.sqrMagnitude < 0.0001f) return;
        axis.Normalize();

        if (!_capturedInitialAngles)
        {
            _initialPlatformAngle = GetYawAngleInPlane(transform.forward, axis);
            _initialCameraAngle = GetYawAngleInPlane(cam.transform.forward, axis);
            _capturedInitialAngles = true;
        }

        float currentCameraAngle = GetYawAngleInPlane(cam.transform.forward, axis);
        float deltaAngle = Mathf.DeltaAngle(_initialCameraAngle, currentCameraAngle);
        float targetAngle = _initialPlatformAngle + deltaAngle;
        Quaternion newRotation = Quaternion.AngleAxis(targetAngle, axis);

        if (rotationCenter != null)
        {
            Vector3 centerWorldPos = rotationCenter.position;
            Vector3 localOffset = transform.InverseTransformPoint(centerWorldPos);
            transform.rotation = newRotation;
            transform.position = centerWorldPos - transform.TransformVector(localOffset);
        }
        else
        {
            transform.rotation = newRotation;
        }
    }

    Vector3 GetRotationAxis()
    {
        switch (rotationAxis)
        {
            case RotationAxisType.WorldY:  return Vector3.up;
            case RotationAxisType.WorldX:   return Vector3.right;
            case RotationAxisType.WorldZ:   return Vector3.forward;
            case RotationAxisType.LocalY:   return transform.up;
            case RotationAxisType.LocalX:   return transform.right;
            case RotationAxisType.LocalZ:   return transform.forward;
            case RotationAxisType.Custom:
                if (customAxisTransform != null) return customAxisTransform.forward;
                return customAxis;
            default: return transform.up;
        }
    }

    /// <summary>Угол в плоскости, перпендикулярной оси: от мировой «вперёд» к направлению взгляда.</summary>
    static float GetYawAngleInPlane(Vector3 lookDirection, Vector3 rotationAxis)
    {
        Vector3 proj = Vector3.ProjectOnPlane(lookDirection, rotationAxis);
        if (proj.sqrMagnitude < 0.0001f) return 0f;
        proj.Normalize();
        Vector3 refInPlane = Vector3.ProjectOnPlane(Vector3.forward, rotationAxis);
        if (refInPlane.sqrMagnitude < 0.0001f) refInPlane = Vector3.ProjectOnPlane(Vector3.right, rotationAxis);
        refInPlane.Normalize();
        return Vector3.SignedAngle(refInPlane, proj, rotationAxis);
    }
}
