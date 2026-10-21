using UnityEngine;
namespace JadeSeal.RoyalGuard
{
    [CreateAssetMenu(menuName = "Jade Seal/Royal Guard Settings")]
    public sealed class RoyalGuardSettings : ScriptableObject
    {
        [Header("Perception")]
        [Min(1)] public float detectionRange = 28;
        [Range(1, 360)] public float fieldOfView = 115;
        [Min(.1f)] public float memorySeconds = 6;
        [Min(1)] public float attackRange = 21;
        [Header("Decision stability")]
        [Min(.05f)] public float thinkInterval = .25f;
        [Min(0)] public float switchMargin = .08f;
        [Min(0)] public float minimumStateSeconds = .6f;
        [Min(1)] public float maximumStateSeconds = 8;
        [Header("Cover score weights (normalized by their sum)")]
        [Min(1)] public float coverRadius = 20;
        [Min(0)] public float distanceWeight = 1;
        [Min(0)] public float pathWeight = 1.5f;
        [Min(0)] public float qualityWeight = 2;
        [Min(0)] public float exposureWeight = 3;
        [Min(0)] public float firingWeight = 2;
        [Header("Movement and recovery")]
        [Min(.1f)] public float replanSeconds = .8f;
        [Min(.5f)] public float stuckSeconds = 3;
        [Range(0, 1)] public float retreatThreshold = .28f;
        [Range(0, 1)] public float recoveryTarget = .55f;
        [Min(0)] public float recoveryPerSecond = 4;
        [Min(0)] public float recoveryDelay = 4;
    }
}
