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

        // Ищем FlyingCubeMovement вверх и вниз по иерархии
        FlyingCubeMovement flying = hitObject.GetComponentInParent<FlyingCubeMovement>(true);
        if (flying == null) flying = hitObject.GetComponentInChildren<FlyingCubeMovement>(true);

        Debug.Log($"[BulletHit] Попал в: '{hitObject.name}' (root: '{hitObject.transform.root.name}') | FlyingCubeMovement: {(flying != null ? "ЕСТЬ → сбиваю!" : "НЕТ")}");

        if (flying != null)
        {
            SpawnEffect(point);
            PlayHitSound(point);

            // Отключаем полётный скрипт
            flying.enabled = false;

            GameObject flyRoot = flying.gameObject;

            // Переключаем ВСЕ Rigidbody в иерархии (prefab'ы могут иметь RB на детях)
            var allRbs = flyRoot.GetComponentsInChildren<Rigidbody>(true);
            Rigidbody mainRb = null;
            foreach (var rb in allRbs)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                if (mainRb == null) mainRb = rb;
            }
            if (mainRb == null)
            {
                mainRb = flyRoot.AddComponent<Rigidbody>();
                mainRb.useGravity = true;
                mainRb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            }

            // Импульс + случайное вращение — самолёт кувыркается при падении
            mainRb.linearVelocity = transform.forward * 8f + Vector3.down * 3f;
            mainRb.angularVelocity = new Vector3(
                Random.Range(-4f, 4f),
                Random.Range(-2f, 2f),
                Random.Range(-4f, 4f));

            Debug.Log($"[BulletHit] Сбит! '{flyRoot.name}', RB на: '{mainRb.gameObject.name}'");

            var explosion = flyRoot.GetComponent<DroppedCubeExplosion>();
            if (explosion == null) explosion = flyRoot.AddComponent<DroppedCubeExplosion>();
            if (hitEffectPrefab != null) explosion.explosionEffectPrefab = hitEffectPrefab;
            if (hitSound != null) explosion.explosionClip = hitSound;
            explosion.explosionScale = 1.5f;
        }
        else
        {
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
