using UnityEngine;

/// <summary>
/// При старте делает пушку неподвижной (Rigidbody — kinematic), чтобы не падала и не проваливалась под землю.
/// После отпускания из рук XR Grab Interactable снова включит физику, и пушка упадёт на пол (если у пола и пушки есть коллайдеры).
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class GunStableSpawn : MonoBehaviour
{
    [Tooltip("Оставить пушку кинематической при старте (не падает). Рекомендуется включено.")]
    public bool kinematicAtSpawn = true;

    void Awake()
    {
        var rb = GetComponent<Rigidbody>();
        if (rb != null && kinematicAtSpawn)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }
    }
}
