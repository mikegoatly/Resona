namespace Resona.Services.OS
{
    /// <summary>
    /// Decides which apps the Android kiosk should hide or unhide. Kept free of Android types so it can be unit tested.
    /// </summary>
    public static class KioskHidePlan
    {
        public sealed record Result(IReadOnlyList<string> ToHide, IReadOnlyList<string> ToUnhide);

        /// <param name="candidates">Apps that could be hidden (launchable apps plus the always-hide list).</param>
        /// <param name="keep">Apps that must stay visible.</param>
        /// <param name="hiddenByUs">Apps the kiosk hid on earlier runs.</param>
        /// <param name="isHidden">Whether an app is currently hidden.</param>
        public static Result Compute(
            IEnumerable<string> candidates,
            IReadOnlySet<string> keep,
            IReadOnlySet<string> hiddenByUs,
            Func<string, bool> isHidden)
        {
            var toHide = candidates
                .Where(p => !keep.Contains(p) && !isHidden(p))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            // Apps we hid on an earlier run that are now meant to stay visible (e.g. added to the never-hide list
            // after they were hidden). Apps hidden by something else are left alone.
            var toUnhide = hiddenByUs
                .Where(keep.Contains)
                .Order(StringComparer.Ordinal)
                .ToList();

            return new Result(toHide, toUnhide);
        }
    }
}
