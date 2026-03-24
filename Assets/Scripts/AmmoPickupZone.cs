using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Вешается на antiaircraft gun (у которого BoxCollider isTrigger = true).
/// Когда ящик с патронами входит в зону, добавляет патроны всем M60VRShoot-стволам
/// в дочерних объектах и уничтожает ящик.
/// </summary>
public class AmmoPickupZone : MonoBehaviour
{
    [Tooltip("Патронов добавляется каждым ящиком (на ствол).")]
    public int ammoPerBox = 250;

    void OnTriggerEnter(Collider other)
    {
        if (!other.gameObject.name.ToLower().Contains("ammo box")) return;

        // m60 — сиблинг antiaircraft gun, ищем от общего корня иерархии
        var shooters = transform.root.GetComponentsInChildren<M60VRShoot>(true);
        if (shooters.Length == 0)
            shooters = FindObjectsByType<M60VRShoot>(FindObjectsSortMode.None);

        foreach (var s in shooters)
            s.AddAmmo(ammoPerBox);

        // Принудительно отпускаем ящик из рук перед уничтожением
        var grab = other.GetComponentInParent<XRGrabInteractable>();
        if (grab != null && grab.isSelected)
            grab.interactionManager.SelectExit(
                grab.firstInteractorSelecting as UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor,
                grab);

        Destroy(other.gameObject);
    }
}
