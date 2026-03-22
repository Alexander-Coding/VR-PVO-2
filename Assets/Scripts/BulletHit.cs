using UnityEngine;

/// <summary>
/// Вешается на каждую выпущенную пулю (m60-bullet Clone).
/// При столкновении с любым объектом:
///  - если цель — летающий объект (FlyingCubeMovement) — уничтожаем его с эффектом
///  - пуля уничтожается в любом случае
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class BulletHit : MonoBehaviour
{
    [Tooltip("Префаб эффекта взрыва при попадании. Если не задан — ищется в Resources/ExplosionEffect.")]
    public GameObject hitEffectPrefab;
    [Tooltip("Масштаб эффекта попадания.")]
    public float hitEffectScale = 0.4f;
    [Tooltip("Звук попадания. Если не задан — загружается Resources/Sound/hit или Resources/ExplosionSound.")]
    public AudioClip hitSound;

    bool _hit;

    void Start()
    {
        if (hitEffectPrefab == null)
            hitEffectPrefab = Resources.Load<GameObject>("ExplosionEffect");
        if (hitSound == null)
            hitSound = Resources.Load<AudioClip>("Sound/hit");
        if (hitSound == null)
            hitSound = Resources.Load<AudioClip>("ExplosionSound");

        // Гарантируем непрерывное обнаружение столкновений — пуля быстрая
        var rb = GetComponent<Rigidbody>();
        if (rb != null)
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (_hit) return;
        HandleHit(collision.gameObject, collision.contacts.Length > 0 ? collision.contacts[0].point : transform.position);
    }

    void OnTriggerEnter(Collider other)
    {
        if (_hit) return;
        HandleHit(other.gameObject, transform.position);
    }

    void HandleHit(GameObject hitObject, Vector3 point)
    {
        _hit = true;

        // Проверяем, является ли цель летающим объектом
        FlyingCubeMovement flying = hitObject.GetComponentInParent<FlyingCubeMovement>();
        if (flying == null)
            flying = hitObject.GetComponent<FlyingCubeMovement>();

        if (flying != null)
        {
            SpawnEffect(point);
            PlayHitSound(point);
            Destroy(flying.gameObject);
        }
        else
        {
            // Попали в статику или другой объект — просто небольшой эффект
            SpawnEffect(point);
            PlayHitSound(point);
        }

        Destroy(gameObject);
    }

    void SpawnEffect(Vector3 point)
    {
        if (hitEffectPrefab == null) return;
        GameObject fx = Instantiate(hitEffectPrefab, point, Quaternion.identity);
        fx.transform.localScale = Vector3.one * hitEffectScale;
        Destroy(fx, 5f);
    }

    void PlayHitSound(Vector3 point)
    {
        if (hitSound != null)
            AudioSource.PlayClipAtPoint(hitSound, point, 0.8f);
    }
}
