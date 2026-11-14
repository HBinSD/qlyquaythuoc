namespace QuanLyQuayThuoc.GUI.Controls;

internal static class ControlLayout
{
    public static void SetSplitRatio(SplitContainer split, double ratio)
    {
        ArgumentNullException.ThrowIfNull(split);
        if (ratio <= 0 || ratio >= 1)
            throw new ArgumentOutOfRangeException(nameof(ratio));

        void UpdateSplitterDistance(object? sender, EventArgs e)
        {
            int availableWidth = split.ClientSize.Width - split.SplitterWidth;
            int minimumDistance = split.Panel1MinSize;
            int maximumDistance = availableWidth - split.Panel2MinSize;
            if (maximumDistance < minimumDistance)
                return;

            int distance = Math.Clamp(
                (int)Math.Round(availableWidth * ratio),
                minimumDistance,
                maximumDistance);
            if (split.SplitterDistance != distance)
                split.SplitterDistance = distance;
        }

        split.SizeChanged += UpdateSplitterDistance;
        UpdateSplitterDistance(split, EventArgs.Empty);
    }
}
