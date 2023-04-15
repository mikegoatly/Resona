using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Resona.Persistence
{
    [Table("Track")]
    public class TrackRaw
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int TrackId { get; set; }

        [MaxLength(100)]
        public required string Name { get; set; }

        [MaxLength(100)]
        public required string FileName { get; set; }

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
