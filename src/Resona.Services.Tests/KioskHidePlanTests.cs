using Resona.Services.OS;

namespace Resona.Services.Tests
{
    public class KioskHidePlanTests
    {
        private static readonly Func<string, bool> NothingHidden = _ => false;

        [Fact]
        public void HidesCandidatesThatAreNotKept()
        {
            var plan = KioskHidePlan.Compute(
                candidates: ["com.game", "com.settings"],
                keep: Set("com.settings"),
                hiddenByUs: Set(),
                isHidden: NothingHidden);

            Assert.Equal(["com.game"], plan.ToHide);
            Assert.Empty(plan.ToUnhide);
        }

        [Fact]
        public void SkipsCandidatesThatAreAlreadyHidden()
        {
            var plan = KioskHidePlan.Compute(
                candidates: ["com.game"],
                keep: Set(),
                hiddenByUs: Set("com.game"),
                isHidden: p => p == "com.game");

            Assert.Empty(plan.ToHide);
        }

        [Fact]
        public void UnhidesAppsWeHidEarlierThatAreNowKept()
        {
            // e.g. Spotify was hidden by an older build, before it was added to the never-hide list
            var plan = KioskHidePlan.Compute(
                candidates: ["com.spotify.music", "com.game"],
                keep: Set("com.spotify.music"),
                hiddenByUs: Set("com.spotify.music", "com.game"),
                isHidden: _ => true);

            Assert.Equal(["com.spotify.music"], plan.ToUnhide);
            Assert.Empty(plan.ToHide);
        }

        [Fact]
        public void DoesNotUnhideKeptAppsThatSomethingElseHid()
        {
            var plan = KioskHidePlan.Compute(
                candidates: ["com.spotify.music"],
                keep: Set("com.spotify.music"),
                hiddenByUs: Set(),
                isHidden: _ => true);

            Assert.Empty(plan.ToUnhide);
        }

        private static HashSet<string> Set(params string[] packages) => new(packages, StringComparer.Ordinal);
    }
}
