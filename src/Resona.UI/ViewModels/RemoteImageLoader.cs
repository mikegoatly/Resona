using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using Avalonia.Media.Imaging;

using Serilog;

namespace Resona.UI.ViewModels
{
    /// <summary>
    /// Downloads and caches (in memory, per URL and width) network cover images such as Spotify album art. Only the
    /// <see cref="Capacity"/> most recently used images are kept, so a long Spotify session on a device that never
    /// restarts doesn't keep every cover it has ever shown.
    /// </summary>
    public static class RemoteImageLoader
    {
        internal const int Capacity = 20;

        private static readonly ILogger logger = Log.ForContext(typeof(RemoteImageLoader));
        private static readonly HttpClient httpClient = new() { Timeout = TimeSpan.FromSeconds(15) };
        private static readonly Lock gate = new();
        private static readonly Dictionary<(string Url, int Width), LinkedListNode<Entry>> cache = [];
        private static readonly LinkedList<Entry> recentlyUsed = new();

        /// <summary>Returns the image, or the result of <paramref name="fallback"/> if it can't be downloaded or decoded.</summary>
        public static Task<Bitmap> LoadAsync(string url, int decodeWidth, Func<Bitmap> fallback)
        {
            var key = (url, decodeWidth);
            Entry? evicted = null;
            Task<Bitmap> image;

            lock (gate)
            {
                if (cache.TryGetValue(key, out var node))
                {
                    recentlyUsed.Remove(node);
                    recentlyUsed.AddFirst(node);
                    return node.Value.Image;
                }

                image = LoadCoreAsync(url, decodeWidth, fallback);
                cache[key] = recentlyUsed.AddFirst(new Entry(key, image));

                if (recentlyUsed.Count > Capacity)
                {
                    evicted = recentlyUsed.Last!.Value;
                    recentlyUsed.RemoveLast();
                    cache.Remove(evicted.Key);
                }
            }

            // The least recently used cover is long off screen by now, so its bitmap can go
            evicted?.Image.ContinueWith(t => t.Result.Dispose(), CancellationToken.None, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Default);
            return image;
        }

        private static async Task<Bitmap> LoadCoreAsync(string url, int decodeWidth, Func<Bitmap> fallback)
        {
            try
            {
                var bytes = await httpClient.GetByteArrayAsync(url).ConfigureAwait(false);
                using var stream = new MemoryStream(bytes);
                return Bitmap.DecodeToWidth(stream, decodeWidth);
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "Failed to load remote image {Url}", url);
                Forget((url, decodeWidth));
                return fallback();
            }
        }

        /// <summary>Drops a failed download so the next request retries it.</summary>
        private static void Forget((string Url, int Width) key)
        {
            lock (gate)
            {
                if (cache.Remove(key, out var node))
                {
                    recentlyUsed.Remove(node);
                }
            }
        }

        private sealed record Entry((string Url, int Width) Key, Task<Bitmap> Image);
    }
}
