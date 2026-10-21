using UnityEngine;

namespace JadeSeal.MountainArcher
{
    /// <summary>
    /// Deterministic decision policy scoring equations for the Mountain Archer AI.
    /// Pure mathematical evaluation with no direct scene/animation dependencies.
    /// </summary>
    public static class MountainArcherUtility
    {
        public enum State { Patrol, Investigate, SeekCover, Reposition, Attack, Reload, Retreat }

        public struct Context
        {
            public bool visible, memory, lowHealth, cover, atHide, atPeek, inRange, firingLine, reload;
            public float coverScore;
        }

        public static float Score(State state, Context c)
        {
            bool threat = c.visible || c.memory;
            switch (state)
            {
                case State.Patrol: return threat ? 0f : .35f;
                case State.Investigate: return !c.visible && c.memory ? .72f : 0f;
                case State.Retreat: return c.lowHealth && threat ? 1f : 0f;
                case State.Reload: return c.reload ? .95f : 0f;
                case State.SeekCover: return threat && c.cover && !c.atHide && !c.atPeek && !c.lowHealth ? .66f + .24f * c.coverScore : 0f;
                case State.Reposition: return c.visible && (!c.inRange || !c.firingLine || c.atHide) && !c.reload ? .85f : 0f;
                case State.Attack: return c.visible && c.inRange && c.firingLine && !c.reload && (!c.cover || c.atPeek) ? .94f : 0f;
                default: return 0f;
            }
        }

        public static State Choose(Context c, State current, float elapsed, MountainArcherSettings s)
        {
            // Emergency retreat and empty-quiver restock interrupt dwell time
            if (c.lowHealth && (c.visible || c.memory)) return State.Retreat;
            if (c.reload) return State.Reload;

            float existing = Score(current, c);
            State best = State.Patrol;
            float highest = 0f;

            for (int i = 0; i < 7; i++)
            {
                State candidate = (State)i;
                float score = Score(candidate, c);
                if (score > highest)
                {
                    highest = score;
                    best = candidate;
                }
            }

            if (existing > 0 && elapsed < s.minimumStateSeconds) return current;
            if (existing > 0 && elapsed < s.maximumStateSeconds && highest <= existing + s.switchMargin) return current;
            return best;
        }

        public static float CoverScore(float distance, float length, float quality, float exposure, float firing, MountainArcherSettings s)
        {
            float sum = s.distanceWeight + s.pathWeight + s.qualityWeight + s.exposureWeight + s.firingWeight;
            if (sum <= 0) return 0f;
            return (s.distanceWeight * (1f - Mathf.Clamp01(distance / Mathf.Max(1f, s.coverRadius))) +
                    s.pathWeight * (1f - Mathf.Clamp01(length / Mathf.Max(1f, s.coverRadius * 2f))) +
                    s.qualityWeight * Mathf.Clamp01(quality) + s.exposureWeight * (1f - Mathf.Clamp01(exposure)) +
                    s.firingWeight * Mathf.Clamp01(firing)) / sum;
        }
    }
}
