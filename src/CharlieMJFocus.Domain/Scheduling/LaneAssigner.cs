namespace CharlieMJFocus.Domain.Scheduling;

/// <summary>An item together with the side-by-side column it should occupy on the timeline.</summary>
public sealed record LaneItem<T>(T Item, int Lane, int LaneCount);

/// <summary>
/// Lays overlapping time blocks out in columns, the same way a calendar app does:
/// blocks that overlap share the width equally; blocks that don't overlap use the full width.
/// </summary>
public static class LaneAssigner
{
    public static IReadOnlyList<LaneItem<T>> Assign<T>(
        IEnumerable<T> items,
        Func<T, DateTimeOffset> startOf,
        Func<T, DateTimeOffset> endOf)
    {
        var ordered = items
            .OrderBy(startOf)
            .ThenBy(endOf)
            .ToList();

        var output = new List<LaneItem<T>>(ordered.Count);

        // A "cluster" is a run of blocks that are chained together by overlaps.
        var cluster = new List<(T Item, int Lane)>();
        var laneEnds = new List<DateTimeOffset>();
        var clusterEnd = DateTimeOffset.MinValue;

        void FlushCluster()
        {
            foreach (var (item, lane) in cluster)
            {
                output.Add(new LaneItem<T>(item, lane, laneEnds.Count));
            }

            cluster.Clear();
            laneEnds.Clear();
            clusterEnd = DateTimeOffset.MinValue;
        }

        foreach (var item in ordered)
        {
            var start = startOf(item);
            var end = endOf(item);

            // This block starts after everything in the cluster finished: close the cluster.
            if (cluster.Count > 0 && start >= clusterEnd)
            {
                FlushCluster();
            }

            // Reuse the first column that is free by this block's start, or open a new one.
            var lane = laneEnds.FindIndex(laneEnd => laneEnd <= start);

            if (lane < 0)
            {
                laneEnds.Add(end);
                lane = laneEnds.Count - 1;
            }
            else
            {
                laneEnds[lane] = end;
            }

            cluster.Add((item, lane));
            clusterEnd = end > clusterEnd ? end : clusterEnd;
        }

        FlushCluster();
        return output;
    }
}
