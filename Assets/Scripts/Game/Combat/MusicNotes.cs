using UnityEngine;

namespace KingsRescue.Combat
{
    /// <summary>
    /// Scales the authored note burst with the music field. The prefab values match the smallest field.
    /// </summary>
    public class MusicNotes : MonoBehaviour
    {
        private ParticleSystem[] systems;
        private ParticleSystem.MinMaxCurve[] lifetimes;
        private ParticleSystem.MinMaxCurve[] speeds;
        private ParticleSystem.MinMaxCurve[] rates;

        private void Awake()
        {
            systems = GetComponentsInChildren<ParticleSystem>(true);
            lifetimes = new ParticleSystem.MinMaxCurve[systems.Length];
            speeds = new ParticleSystem.MinMaxCurve[systems.Length];
            rates = new ParticleSystem.MinMaxCurve[systems.Length];
            for (int i = 0; i < systems.Length; i++)
            {
                ParticleSystem system = systems[i];
                if (system == null)
                {
                    continue;
                }

                lifetimes[i] = system.main.startLifetime;
                speeds[i] = system.main.startSpeed;
                rates[i] = system.emission.rateOverTime;
            }
        }

        /// <summary>
        /// <paramref name="radius"/> is the field's world radius. <paramref name="baselineRadius"/> is the field the prefab was authored for.
        /// </summary>
        public void ApplyFieldRadius(float radius, float baselineRadius)
        {
            if (systems == null)
            {
                return;
            }

            float scale = Mathf.Max(0.05f, radius) / Mathf.Max(0.05f, baselineRadius);
            for (int i = 0; i < systems.Length; i++)
            {
                ParticleSystem system = systems[i];
                if (system == null)
                {
                    continue;
                }

                ParticleSystem.MainModule main = system.main;
                main.startLifetime = Scale(lifetimes[i], scale);
                main.startSpeed = Scale(speeds[i], scale);
                ParticleSystem.EmissionModule emission = system.emission;
                emission.rateOverTime = Scale(rates[i], scale);
                system.Clear(true);
                system.Play(true);
            }
        }

        private static ParticleSystem.MinMaxCurve Scale(ParticleSystem.MinMaxCurve curve, float scale)
        {
            switch (curve.mode)
            {
                case ParticleSystemCurveMode.TwoConstants:
                    return new ParticleSystem.MinMaxCurve(curve.constantMin * scale, curve.constantMax * scale);
                case ParticleSystemCurveMode.Curve:
                case ParticleSystemCurveMode.TwoCurves:
                    curve.curveMultiplier *= scale;
                    return curve;
                default:
                    return new ParticleSystem.MinMaxCurve(curve.constant * scale);
            }
        }
    }
}
