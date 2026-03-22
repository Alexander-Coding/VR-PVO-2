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
            // Небольшой эффект попадания
            SpawnEffect(point);
            PlayHitSound(point);

            // Отключаем полётный скрипт — цель начинает падать
            flying.enabled = false;

            // Включаем физику
            Rigidbody rb = flying.GetComponent<Rigidbody>();
            if (rb == null) rb = flying.gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            // Небольшой импульс от попадания
            rb.linearVelocity = transform.forward * 5f + Vector3.up * 2f;

            // DroppedCubeExplosion — взрыв при падении на землю
            var explosion = flying.GetComponent<DroppedCubeExplosion>();
            if (explosion == null) explosion = flying.gameObject.AddComponent<DroppedCubeExplosion>();
            if (hitEffectPrefab != null) explosion.explosionEffectPrefab = hitEffectPrefab;
            if (hitSound != null) explosion.explosionClip = hitSound;
            explosion.explosionScale = 0.6f;
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
