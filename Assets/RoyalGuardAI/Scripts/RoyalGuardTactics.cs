using UnityEngine;
namespace JadeSeal.RoyalGuard
{
    public sealed class RoyalGuardTactics : MonoBehaviour
    {
        public RoyalGuardCoverPoint[] coverPoints;
        public RoyalGuardCoverPoint Selected { get; private set; }
        public float Score { get; private set; }
        RoyalGuardCoverPoint rejected;
        float retryAt;
        public void Reject() { if (Selected) { rejected = Selected; retryAt = Time.time + 5; } Release(); }
        public void Release()
        {
            if (Selected) Selected.Release(GetComponent<RoyalGuardBrain>());
            Selected = null; Score = 0;
        }
        public void Select(RoyalGuardNavigation nav, RoyalGuardPerception sense, RoyalGuardSettings settings, bool defensive)
        {
            RoyalGuardBrain owner = GetComponent<RoyalGuardBrain>();
            RoyalGuardCoverPoint best = null;
            float bestScore = -1;
            if (coverPoints == null) { Release(); return; }
            foreach (var cover in coverPoints)
            {
                if (!cover || !cover.isActiveAndEnabled || !cover.IsAvailableFor(owner) || (cover == rejected && Time.time < retryAt)) continue;
                float distance = Vector3.Distance(transform.position, cover.HidePosition);
                if (distance > settings.coverRadius || !nav.Validate(cover.HidePosition, out _, out float length)) continue;
                Vector3 threat = sense.LastPosition + Vector3.up;
                // Three body-height probes estimate exposure using the last observed threat position.
                float exposed = 0;
                for (int i = 0; i < 3; i++)
                    if (sense.Clear(threat, cover.HidePosition + Vector3.up * (.5f + .55f * i))) exposed += 1f / 3;
                if (exposed > .67f) continue;
                bool peekReachable = nav.Validate(cover.PeekPosition, out _, out _);
                bool firing = peekReachable && Vector3.Distance(cover.PeekPosition, sense.LastPosition) <= settings.attackRange &&
                    sense.Clear(cover.PeekPosition + Vector3.up * 1.6f, threat);
                if (!defensive && !firing) continue;
                float score = RoyalGuardUtility.CoverScore(distance, length, cover.Quality, exposed, firing ? 1 : 0, settings);
                if (cover == Selected) score += settings.switchMargin; // stable reservation, still revalidated
                if (score > bestScore) { bestScore = score; best = cover; }
            }
            if (best != Selected) { Release(); if (best && best.Reserve(owner)) Selected = best; }
            Score = Selected ? Mathf.Clamp01(bestScore) : 0;
        }
        void OnDisable() { Release(); }
    }
}
