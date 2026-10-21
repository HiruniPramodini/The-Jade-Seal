using UnityEngine;

namespace JadeSeal.MountainArcher
{
    [CreateAssetMenu(menuName = "Jade Seal/Mountain Archer Settings")]
    public sealed class MountainArcherSettings : ScriptableObject
    {
        [Header("Perception")]
        [Min(1)] public float detectionRange = 28f;
        [Range(1, 360)] public float fieldOfView = 115f;
        [Min(.1f)] public float memorySeconds = 6f;
        [Min(1)] public float attackRange = 24f;

        [Header("Decision Stability")]
        [Min(.05f)] public float thinkInterval = .25f;
        [Min(0)] public float switchMargin = .08f;
        [Min(0)] public float minimumStateSeconds = .5f;
        [Min(1)] public float maximumStateSeconds = 6f;

        [Header("Cover & High Vantage Weights (normalized by their sum)")]
        [Min(1)] public float coverRadius = 22f;
        [Min(0)] public float distanceWeight = 1f;
        [Min(0)] public float pathWeight = 1.5f;
        [Min(0)] public float qualityWeight = 2f;
        [Min(0)] public float exposureWeight = 3f;
        [Min(0)] public float firingWeight = 2.5f;

        [Header("High Mobility & Fast Recovery")]
        [Min(.1f)] public float replanSeconds = .6f;
        [Min(.5f)] public float stuckSeconds = 2.5f;
        [Range(0, 1)] public float retreatThreshold = .25f;
        [Range(0, 1)] public float recoveryTarget = .55f;
        [Min(0)] public float recoveryPerSecond = 5f;
        [Min(0)] public float recoveryDelay = 3.5f;
    }
}
