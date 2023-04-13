using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Resona.Persistence
{
    [Table("Song")]
    public class SongRaw
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int SongId { get; set; }

        [MaxLength(100)]
        public required string Name { get; set; }

        [MaxLength(100)]
        public required string FileName { get; set; }

        public required DateTimeOffset LastModifiedLocal { get; set; }

        public virtual required AlbumRaw Album { get; set; }
    }
}
