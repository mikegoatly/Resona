using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

using Microsoft.EntityFrameworkCore;

namespace Resona.Persistence
{
    [Table("Album")]
    [Index(nameof(Kind), nameof(Name))]
    public class AlbumRaw
    {
        private ICollection<TrackRaw>? tracks;

        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int AlbumId { get; set; }

        public required AlbumKind Kind { get; set; }

        [MaxLength(100)]
        public required string Name { get; set; }

        [MaxLength(250)]
        public required string Path { get; set; }

        [MaxLength(100)]
        public string? Artist { get; set; }

        public virtual ICollection<TrackRaw> Tracks
        {
            get => this.tracks ??= new List<TrackRaw>();
            set => this.tracks = value;
        }

        [MaxLength(350)]
        public string? ThumbnailFile { get; set; }
    }
}
