using UnityEngine;

namespace JadeSeal.MountainArcher
{
    public sealed class MountainArcherTactics : MonoBehaviour
    {
        public MountainArcherCoverPoint[] coverPoints;
        public MountainArcherCoverPoint Selected { get; private set; }
        public float Score { get; private set; }

        private MountainArcherCoverPoint rejected;
        private float retryAt;

        public void Reject()
        {
            if (Selected)
            {
                rejected = Selected;
                retryAt = Time.time + 4f;
            }
            Release();
        }

        public void Release()
        {
            if (Selected) Selected.Release(GetComponent<MountainArcherBrain>());
            Selected = null;
            Score = 0;
        }

        public void Select(MountainArcherNavigation nav, MountainArcherPerception sense, MountainArcherSettings settings, bool defensive)
        {
            MountainArcherBrain owner = GetComponent<MountainArcherBrain>();
            MountainArcherCoverPoint best = null;
            float bestScore = -1f;

            if (coverPoints == null || coverPoints.Length == 0)
            {
                coverPoints = Object.FindObjectsByType<MountainArcherCoverPoint>(FindObjectsSortMode.None);
            }

            if (coverPoints == null) { Release(); return; }

            foreach (var cover in coverPoints)
            {
                if (!cover || !cover.isActiveAndEnabled || !cover.IsAvailableFor(owner) || (cover == rejected && Time.time < retryAt)) continue;

                float distance = Vector3.Distance(transform.position, cover.HidePosition);
                if (distance > settings.coverRadius || !nav.Validate(cover.HidePosition, out _, out float length)) continue;

                Vector3 threat = sense.LastPosition + Vector3.up;

                // Three body-height probes estimate exposure using the last observed threat position
                float exposed = 0f;
                for (int i = 0; i < 3; i++)
                {
                    if (sense.Clear(threat, cover.HidePosition + Vector3.up * (.5f + .55f * i))) exposed += 1f / 3f;
                }
                if (exposed > .67f) continue;

                bool peekReachable = nav.Validate(cover.PeekPosition, out _, out _);
                bool firing = peekReachable && Vector3.Distance(cover.PeekPosition, sense.LastPosition) <= settings.attackRange &&
                    sense.Clear(cover.PeekPosition + Vector3.up * 1.6f, threat);

                if (!defensive && !firing) continue;

                float score = MountainArcherUtility.CoverScore(distance, length, cover.Quality, exposed, firing ? 1 : 0, settings);
                if (cover == Selected) score += settings.switchMargin;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = cover;
                }
            }

            if (best != Selected)
            {
                Release();
                if (best && best.Reserve(owner)) Selected = best;
            }

            Score = Selected ? Mathf.Clamp01(bestScore) : 0f;
        }

        private void OnDisable()
        {
            Release();
        }
    }
}
