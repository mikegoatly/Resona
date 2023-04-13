using System.Collections.Generic;

namespace Resona.Services.Libraries
{
    public record AudioContent(AudioKind AudioKind, string Name, string? Artist, int Index, IReadOnlyList<AudioTrack> Tracks);
    public record AudioTrack(string FileName, string Title, string? Artist, uint TrackNumber, int TrackIndex);
}
