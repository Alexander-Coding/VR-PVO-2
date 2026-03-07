using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Вешается на объект с XRGrabInteractable (m60 или m60-1).
/// На активацию (нажатие триггера) — вызов выброса гильз и случайный звук выстрела.
/// Поддерживает точку опоры (pivot) для движения оружия относительно неё.
/// </summary>
[RequireComponent(typeof(XRGrabInteractable))]
public class M60VRShoot : MonoBehaviour
{
    [Header("Выстрел и гильзы")]
    [Tooltip("Скрипт выброса гильз (на корне m60 или на этом объекте). Если не задан — ищется на этом объекте или у родителя.")]
    public M60ShellEjection shellEjection;
    [Tooltip("Интервал между выстрелами (сек), чтобы не спамить гильзами")]
    public float fireRate = 0.12f;

    [Header("Звук выстрела")]
    [Tooltip("Клипы выстрела (tush_net_1..4). Если пусто — загружаются из Resources/Sound/ по именам tush_net_1..4.")]
    public AudioClip[] shootClips;
    [Tooltip("Громкость выстрела")]
    [Range(0f, 1f)]
    public float shootVolume = 0.8f;

    [Header("Точка опоры (pivot)")]
    [Tooltip("Точка опоры и захвата (рекомендуется m60 receiver). Мировая позиция не меняется при захвате, если используется GunPivotLockGrabTransformer. Если не задана — ищется дочерний «m60 receiver».")]
    public Transform pivotPoint;
    [Tooltip("Если true, при захвате оружие смещается так, чтобы pivot совпадал с точкой захвата контроллера.")]
    public bool usePivotForGrab = true;

    XRGrabInteractable _grab;
    AudioSource _audioSource;
    float _lastFireTime;
    static readonly string[] TushNetNames = { "tush_net_1", "tush_net_2", "tush_net_3", "tush_net_4" };

    void Awake()
    {
        _grab = GetComponent<XRGrabInteractable>();
        if (shellEjection == null)
        {
            shellEjection = GetComponent<M60ShellEjection>();
            if (shellEjection == null) shellEjection = GetComponentInParent<M60ShellEjection>();
        }
        var receiver = transform.Find("m60 receiver");
        if (receiver != null)
            pivotPoint = receiver;
        EnsureShootClips();
        ApplyPivotToGrab();
    }

    void ApplyPivotToGrab()
    {
        if (!usePivotForGrab || pivotPoint == null || _grab == null) return;
        _grab.attachTransform = pivotPoint;
    }

    void OnEnable()
    {
        if (_grab != null)
            _grab.activated.AddListener(OnActivated);
    }

    void OnDisable()
    {
        if (_grab != null)
            _grab.activated.RemoveListener(OnActivated);
    }

    void EnsureShootClips()
    {
        if (shootClips != null && shootClips.Length > 0) return;
        shootClips = new AudioClip[TushNetNames.Length];
        for (int i = 0; i < TushNetNames.Length; i++)
            shootClips[i] = Resources.Load<AudioClip>("Sound/" + TushNetNames[i]);
    }

    void OnActivated(ActivateEventArgs args)
    {
        if (Time.time - _lastFireTime < fireRate) return;
        _lastFireTime = Time.time;

        if (shellEjection != null)
            shellEjection.Fire();

        PlayRandomShootSound();
    }

    void PlayRandomShootSound()
    {
        AudioClip clip = null;
        if (shootClips != null && shootClips.Length > 0)
        {
            int i = Random.Range(0, shootClips.Length);
            if (shootClips[i] != null) clip = shootClips[i];
        }
        if (clip == null)
        {
            EnsureShootClips();
            foreach (var c in shootClips)
                if (c != null) { clip = c; break; }
        }
        if (clip == null) return;

        if (_audioSource == null)
        {
            _audioSource = gameObject.GetComponent<AudioSource>();
            if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 1f;
        }
        _audioSource.PlayOneShot(clip, shootVolume);
    }

    /// <summary>
    /// Точка опоры для захвата: возвращает pivot, если задан, иначе transform.
    /// В Inspector на XRGrabInteractable можно выставить Attach Transform в этот transform (дочерний pivot), чтобы оружие двигалось относительно рукояти.
    /// </summary>
    public Transform GetAttachPivot()
    {
        return pivotPoint != null ? pivotPoint : transform;
    }
}
