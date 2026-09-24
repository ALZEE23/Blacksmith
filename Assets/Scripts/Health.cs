using UnityEngine;
using UnityEngine.Events;

// Komponen HP generik, dipakai NPC maupun Enemy. Tinggal panggil TakeDamage() dari script lain.
public class Health : MonoBehaviour
{
    // UnityEvent<T> generik gak muncul bener di Inspector kalau gak dibungkus class konkret kayak gini.
    [System.Serializable] public class DamageEvent : UnityEvent<float> { }

    [SerializeField] private float maxHealth = 100f;
    [Tooltip("Hancurkan object otomatis setelah mati.")]
    [SerializeField] private bool destroyOnDeath = true;
    [Tooltip("Jeda sebelum object dihancurkan, kasih waktu animasi mati main dulu (detik).")]
    [SerializeField] private float destroyDelay = 2f;

    [Tooltip("Dipanggil sekali pas HP sampai 0.")]
    public UnityEvent onDeath;
    [Tooltip("Dipanggil tiap kali kena damage, parameternya jumlah damage yang masuk.")]
    public DamageEvent onDamaged;

    public float MaxHealth => maxHealth;
    public float CurrentHealth { get; private set; }
    public bool IsDead { get; private set; }

    private void Awake()
    {
        CurrentHealth = maxHealth;
    }

    public void TakeDamage(float amount)
    {
        if (IsDead || amount <= 0f) return;

        CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
        onDamaged?.Invoke(amount);

        if (CurrentHealth <= 0f) Die();
    }

    public void Heal(float amount)
    {
        if (IsDead || amount <= 0f) return;
        CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
    }

    private void Die()
    {
        IsDead = true;
        onDeath?.Invoke();

        if (destroyOnDeath)
            Destroy(gameObject, destroyDelay);
    }
}
