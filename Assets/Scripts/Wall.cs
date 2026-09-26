using UnityEngine;

// Base/tembok utama. Ini jadi sasaran DEFAULT si Enemy kalau lagi gak ada NPC di deket dia
// (lihat Enemy.cs) — begitu ada NPC lagi, Enemy otomatis ganti sasaran ke NPC dulu.
[RequireComponent(typeof(Health))]
public class Wall : MonoBehaviour
{
    private Health health;
    public Health Health => health;

    private void Awake()
    {
        health = GetComponent<Health>();
    }

    // Otomatis kasih tag "Wall" pas komponen ini ditambahin di Editor, biar gak lupa set manual.
    private void Reset()
    {
        gameObject.tag = "Wall";
    }
}
