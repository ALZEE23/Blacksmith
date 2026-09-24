using UnityEngine;

// Nama & hash parameter Animator yang dipakai BARENG antara NPC (SplineFollower) dan Enemy,
// biar dua-duanya make skema animasi yang sama persis: Blend (float, 0 idle/0.5 walk/1 run),
// Attack (trigger), Death (bool). Animator Controller keduanya harus punya parameter ini.
public static class CombatAnimatorParams
{
    public static readonly int Blend = Animator.StringToHash("Blend");
    public static readonly int Attack = Animator.StringToHash("Attack");
    public static readonly int Death = Animator.StringToHash("Death");

    public const float BlendIdle = 0f;
    public const float BlendWalk = 0.5f;
    public const float BlendRun = 1f;
}
