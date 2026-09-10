using System;
using System.Collections.Generic;
using System.Linq;

namespace RimCBM.Core
{
    public sealed class PowerRequest
    {
        public string Id;
        public float StartupWd, RunningWd;
        public int Priority;
    }
    public static class PowerRules
    {
        public static float Clamp(float energy, float capacity) => Math.Max(0, Math.Min(energy, Math.Max(0, capacity)));
        // A consumer gets the whole interval or none. Failed requests never partially drain the pool.
        public static HashSet<string> Allocate(ref float energy, IEnumerable<PowerRequest> requests)
        {
            var supplied = new HashSet<string>();
            foreach (var request in requests.OrderByDescending(r => r.Priority).ThenBy(r => r.Id, StringComparer.Ordinal))
            {
                float cost = Math.Max(0, request.StartupWd) + Math.Max(0, request.RunningWd);
                if (float.IsNaN(cost) || float.IsInfinity(cost) || energy < cost) continue;
                energy -= cost; supplied.Add(request.Id);
            }
            return supplied;
        }
    }
}
