using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Transformers;

/// <summary>
/// Трансформер захвата: при удержании объекта точка опоры (pivot) остаётся неподвижной в мире.
/// Оружие только поворачивается вокруг этой точки (режим «закреплённой турели»).
/// Добавьте на объект с XRGrabInteractable и укажите Pivot Transform = m60 receiver.
/// Добавьте этот компонент в список Starting Single Grab Transformers у XRGrabInteractable.
/// </summary>
public class GunPivotLockGrabTransformer : XRBaseGrabTransformer
{
    [Tooltip("Точка опоры в мире (например, m60 receiver). Её мировая позиция не меняется при захвате.")]
    [SerializeField] Transform pivotTransform;

    Vector3 _pivotWorldPosition;
    Vector3 _pivotLocalOffsetFromRoot;
    bool _pivotCaptured;

    protected override RegistrationMode registrationMode => RegistrationMode.Single;

    public override void OnLink(XRGrabInteractable grabInteractable)
    {
        base.OnLink(grabInteractable);
        Transform root = grabInteractable.transform;
        Transform pivot = GetPivot(root);
        _pivotCaptured = pivot != null;
        if (_pivotCaptured)
        {
            _pivotWorldPosition = pivot.position;
            _pivotLocalOffsetFromRoot = root.InverseTransformPoint(pivot.position);
        }
    }

    public override void Process(XRGrabInteractable grabInteractable, XRInteractionUpdateOrder.UpdatePhase updatePhase, ref Pose targetPose, ref Vector3 localScale)
    {
        if (!_pivotCaptured) return;

        // Держим точку опоры на месте: позиция корня = pivotWorld - (rotation * pivotLocalOffset)
        targetPose.position = _pivotWorldPosition - (targetPose.rotation * _pivotLocalOffsetFromRoot);
    }

    Transform GetPivot(Transform root)
    {
        if (pivotTransform != null) return pivotTransform;
        var shoot = root.GetComponent<M60VRShoot>();
        if (shoot != null) return shoot.GetAttachPivot();
        return root.Find("m60 receiver");
    }
}
