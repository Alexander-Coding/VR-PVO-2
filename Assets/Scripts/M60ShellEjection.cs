using UnityEngine;

/// <summary>
/// Выброс гильз из двух стволов M60 при стрельбе.
/// Назначь точки выброса (Ejection Point 1/2) и префаб гильзы (m60-case).
/// Вызов Fire() — из кода, по Input или из Animation Event.
/// </summary>
public class M60ShellEjection : MonoBehaviour
{
    [Header("Точки выброса (оба ствола)")]
    [Tooltip("Точка выброса гильзы первого ствола (например, m60 receiver)")]
    public Transform ejectionPoint1;
    [Tooltip("Точка выброса гильзы второго ствола (на m60-1)")]
    public Transform ejectionPoint2;

    [Header("Гильза")]
    [Tooltip("Префаб или объект гильзы (m60-case). Можно перетащить объект m60-case из сцены.")]
    public GameObject shellPrefab;
    [Tooltip("Масштаб гильзы (1 = как в префабе, 0.1 = в 10 раз меньше)")]
    [Range(0.01f, 1f)]
    public float shellScale = 0.12f;

    [Header("Направление выброса (первый ствол)")]
    [Tooltip("Направление вылета гильзы в локальных осях точки выброса. Для второго ствола ось Z автоматически отзеркаливается.")]
    public Vector3 ejectionDirection = new Vector3(1f, -0.2f, 0f);

    [Header("Параметры выброса")]
    [Tooltip("Сила выброса (небольшая — гильзы должны спокойно падать)")]
    public float ejectionForce = 0.025f;
    [Tooltip("Случайный разброс силы")]
    public float forceRandom = 0.008f;
    [Tooltip("Интервал между выстрелами (сек) — больше значение = меньше гильз")]
    public float fireInterval = 0.9f;
    [Tooltip("Удалить гильзу через N секунд (0 = не удалять)")]
    public float shellLifetime = 4f;

    float _lastFireTime;

    void Start()
    {
        // Автопоиск точек выброса, если не назначены вручную (скрипт висит на m60)
        if (ejectionPoint1 == null)
            ejectionPoint1 = transform.Find("m60 receiver");
        if (ejectionPoint2 == null)
        {
            Transform m60_1 = transform.Find("m60-1");
            if (m60_1 != null)
                ejectionPoint2 = m60_1.Find("m60 receiver");
        }
    }

    // Update больше не вызывает Fire() — стрельба только по вызову из VR (M60VRShoot) или Animation Event.

    /// <summary>
    /// Один "выстрел" — вылет двух гильз из обоих стволов.
    /// Можно вызывать из Animation Event или из XR input.
    /// </summary>
    GameObject GetShellTemplate()
    {
        if (shellPrefab != null) return shellPrefab;
        var fallback = GameObject.Find("m60-case");
        return fallback != null ? fallback : null;
    }

    public void Fire()
    {
        GameObject template = GetShellTemplate();
        if (template == null) return;
        if (Time.time - _lastFireTime < fireInterval) return;
        _lastFireTime = Time.time;

        EjectShell(ejectionPoint1, template, mirrorDirectionZ: false);
        EjectShell(ejectionPoint2, template, mirrorDirectionZ: true);
    }

    void EjectShell(Transform point, GameObject template, bool mirrorDirectionZ)
    {
        if (point == null) return;

        GameObject shell = Instantiate(template, point.position, point.rotation);
        shell.name = "m60-case(Clone)";

        Vector3 baseScale = template.transform.lossyScale;
        shell.transform.localScale = new Vector3(baseScale.x * shellScale, baseScale.y * shellScale, baseScale.z * shellScale);

        Rigidbody rb = shell.GetComponent<Rigidbody>();
        if (rb == null) rb = shell.AddComponent<Rigidbody>();
        rb.isKinematic = false;
        rb.useGravity = true;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.mass = 0.02f;
        rb.linearDamping = 0.5f;
        rb.angularDamping = 0.5f;

        if (shell.GetComponent<Collider>() == null)
        {
            var col = shell.AddComponent<CapsuleCollider>();
            col.radius = 0.005f;
            col.height = 0.02f;
            col.direction = 2;
        }

        Vector3 dir = point.TransformDirection(ejectionDirection.normalized);
        if (mirrorDirectionZ)
            dir = new Vector3(dir.x, dir.y, -dir.z);
        float force = ejectionForce + Random.Range(-forceRandom, forceRandom);
        rb.AddForce(dir * force, ForceMode.Impulse);
        rb.AddTorque(Random.insideUnitSphere * 0.3f, ForceMode.Impulse);

        if (shellLifetime > 0f)
            Destroy(shell, shellLifetime);
    }
}
