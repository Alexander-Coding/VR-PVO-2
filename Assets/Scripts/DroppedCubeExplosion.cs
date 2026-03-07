using UnityEngine;

/// <summary>
/// Маленький кубик, сброшенный с летящего куба. Падает под тяжестью, при ударе о землю — звук взрыва и уничтожение.
/// </summary>
public class DroppedCubeExplosion : MonoBehaviour
{
    [Tooltip("Звук взрыва при касании земли. Назначь в Inspector или положи в Resources/ExplosionSound.")]
    public AudioClip explosionClip;

    AudioSource _oneShotSource;
    bool _exploded;

    void Start()
    {
        if (explosionClip == null)
            explosionClip = Resources.Load<AudioClip>("ExplosionSound");
    }

    void OnCollisionEnter(Collision collision)
    {
        if (_exploded) return;
        // Взрыв только при ударе о землю (нормаль снизу вверх или объект — Terrain/земля)
        if (collision.contactCount == 0) return;
        ContactPoint contact = collision.GetContact(0);
        if (contact.normal.y < 0.3f) return; // не «земля» снизу

        _exploded = true;
        PlayExplosionSound();
        Destroy(gameObject, 0.1f);
    }

    void PlayExplosionSound()
    {
        if (explosionClip == null) return;
        var src = GetComponent<AudioSource>();
        if (src == null) src = gameObject.AddComponent<AudioSource>();
        src.spatialBlend = 1f;
        src.playOnAwake = false;
        src.PlayOneShot(explosionClip);
    }
}
