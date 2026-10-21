using UnityEngine;
namespace JadeSeal.RoyalGuard
{
    // No scene, weapon, animation or navigation dependencies: deterministic decision policy.
    public static class RoyalGuardUtility
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
                case State.Patrol: return threat ? 0 : .35f;
                case State.Investigate: return !c.visible && c.memory ? .72f : 0;
                case State.Retreat: return c.lowHealth && threat ? 1 : 0;
                case State.Reload: return c.reload ? .94f : 0;
                case State.SeekCover: return threat && c.cover && !c.atHide && !c.atPeek && !c.lowHealth ? .66f + .24f * c.coverScore : 0;
                case State.Reposition: return c.visible && (!c.inRange || !c.firingLine || c.atHide) && !c.reload ? .82f : 0;
                case State.Attack: return c.visible && c.inRange && c.firingLine && !c.reload && (!c.cover || c.atPeek) ? .92f : 0;
                default: return 0;
            }
        }
        public static State Choose(Context c, State current, float elapsed, RoyalGuardSettings s)
        {
            // Emergency retreat and empty-magazine reload interrupt dwell time.
            if (c.lowHealth && (c.visible || c.memory)) return State.Retreat;
            if (c.reload) return State.Reload;
            float existing = Score(current, c);
            State best = State.Patrol;
            float highest = 0;
            for (int i = 0; i < 7; i++)
            {
                State candidate = (State)i;
                float score = Score(candidate, c);
                if (score > highest) { highest = score; best = candidate; }
            }
            if (existing > 0 && elapsed < s.minimumStateSeconds) return current;
            if (existing > 0 && elapsed < s.maximumStateSeconds && highest <= existing + s.switchMargin) return current;
            return best;
        }
        public static float CoverScore(float distance, float length, float quality, float exposure, float firing, RoyalGuardSettings s)
        {
            float sum = s.distanceWeight + s.pathWeight + s.qualityWeight + s.exposureWeight + s.firingWeight;
            if (sum <= 0) return 0;
            return (s.distanceWeight * (1 - Mathf.Clamp01(distance / Mathf.Max(1, s.coverRadius))) +
                    s.pathWeight * (1 - Mathf.Clamp01(length / Mathf.Max(1, s.coverRadius * 2))) +
                    s.qualityWeight * Mathf.Clamp01(quality) + s.exposureWeight * (1 - Mathf.Clamp01(exposure)) +
                    s.firingWeight * Mathf.Clamp01(firing)) / sum;
        }
    }
}
