using UnityEngine;

/// <summary>
/// Сброшенная бомба или кубик. Падает под тяжестью; при ударе о землю или объекты — звук, эффект взрыва и уничтожение.
/// </summary>
public class DroppedCubeExplosion : MonoBehaviour
{
    [Tooltip("Звук взрыва при касании земли. Назначь в Inspector или положи в Resources/ExplosionSound.")]
    public AudioClip explosionClip;
    [Tooltip("Префаб эффекта взрыва (например Cartoon explosion BOOM). Спавнится в точке удара.")]
    public GameObject explosionEffectPrefab;
    [Tooltip("Масштаб эффекта взрыва")]
    public float explosionScale = 0.3f;

    bool _exploded;
    Rigidbody _rb;
    float _spawnTime;

    void Start()
    {
        if (explosionClip == null)
            explosionClip = Resources.Load<AudioClip>("ExplosionSound");
        _spawnTime = Time.time;

        // Ищем Rigidbody в иерархии — у сложных prefab'ов он на дочернем объекте
        _rb = GetComponentInChildren<Rigidbody>(true);
        if (_rb != null)
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
    }

    void FixedUpdate()
    {
        if (_exploded) return;
        // Взрыв у земли (на случай если terrain не на y=0)
        if (transform.position.y <= 0.5f)
            Explode(transform.position);
        // Страховочный таймер: если падает >20 сек и не достиг земли — взрываем
        if (Time.time - _spawnTime > 20f)
            Explode(transform.position);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (_exploded) return;
        if (collision.contactCount == 0) return;
        ContactPoint contact = collision.GetContact(0);
        // Взрываемся при любом касании земли/объекта снизу (смягчили порог 0.5→0.25)
        if (contact.normal.y < 0.25f) return;
        Explode(contact.point);
    }

    void Explode(Vector3 point)
    {
        if (_exploded) return;
        _exploded = true;

        if (explosionEffectPrefab != null)
        {
            GameObject effect = Instantiate(explosionEffectPrefab, point, Quaternion.identity);
            effect.transform.localScale = Vector3.one * explosionScale;
            Destroy(effect, 5f);
        }
        if (explosionClip == null) explosionClip = Resources.Load<AudioClip>("ExplosionSound");
        if (explosionClip != null) AudioSource.PlayClipAtPoint(explosionClip, point);
        Destroy(gameObject);
    }
}
