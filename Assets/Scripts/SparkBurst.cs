using UnityEngine;

// Percikan sederhana buat efek palu kena anvil. ParticleSystem-nya dibikin sendiri lewat kode
// (gak butuh particle prefab/gambar apapun), pakai shader "Sprites/Default" yang aman & selalu
// kompatibel di URP. Tinggal panggil Play() tiap kali mau munculin percikan.
[RequireComponent(typeof(ParticleSystem))]
public class SparkBurst : MonoBehaviour
{
    [SerializeField] private Color startColor = new Color(1f, 0.8f, 0.25f, 1f);
    [SerializeField] private Color endColor = new Color(1f, 0.3f, 0f, 0f);
    [SerializeField] private int burstCount = 24;
    [SerializeField] private float speed = 6f;
    [SerializeField] private float lifetime = 0.45f;
    [SerializeField] private float size = 0.18f;
    [SerializeField] private float coneAngle = 35f;

    private ParticleSystem ps;

    private void Awake()
    {
        ps = GetComponent<ParticleSystem>();
        Configure();
    }

    private void Configure()
    {
        // ParticleSystem baru ditambahin defaultnya "Play On Awake" = true, jadi udah langsung
        // main duluan. Harus di-stop dulu sebelum ubah main.duration, Unity gak ngebolehin ubah
        // durasi selagi sistemnya masih playing.
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = lifetime;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = lifetime;
        main.startSpeed = speed;
        main.startSize = size;
        main.startColor = startColor;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 1.5f; // percikannya jatuh ke bawah kayak bunga api beneran

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)burstCount) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = coneAngle;
        shape.radius = 0.05f;

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(startColor, 0f), new GradientColorKey(endColor, 1f) },
            new[] { new GradientAlphaKey(startColor.a, 0f), new GradientAlphaKey(0f, 1f) });
        colorOverLifetime.color = gradient;

        ParticleSystemRenderer psRenderer = ps.GetComponent<ParticleSystemRenderer>();
        if (psRenderer.sharedMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null) psRenderer.material = new Material(shader);
        }

        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    public void Play()
    {
        if (ps == null) return;
        ps.Play(true);
    }
}
