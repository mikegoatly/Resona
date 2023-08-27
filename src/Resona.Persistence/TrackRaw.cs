using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace Resona.Persistence
{
    [Table("Track")]
    public class TrackRaw
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int TrackId { get; set; }

        [UnconditionalSuppressMessage("Trimming", "IL2026")]
        [MaxLength(100)]
        public required string Name { get; set; }

        [UnconditionalSuppressMessage("Trimming", "IL2026")]
        [MaxLength(100)]
        public required string FileName { get; set; }

        [UnconditionalSuppressMessage("Trimming", "IL2026")]
        [MaxLength(100)]
        public string? Artist { get; set; }

        public uint TrackNumber { get; set; }

        public required DateTime LastModifiedUtc { get; set; }

        [ForeignKey(nameof(AlbumId))]
        public virtual required AlbumRaw Album { get; set; }

        public int AlbumId { get; set; }
        public TimeSpan? Duration { get; set; }
    }
}
