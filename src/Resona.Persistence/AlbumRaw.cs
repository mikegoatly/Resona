using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Resona.Persistence
{
    [Table("Album")]
    public class AlbumRaw
    {
        private ICollection<SongRaw>? songs;

        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int AlbumId { get; set; }

        [MaxLength(100)]
        public required string Name { get; set; }

        [MaxLength(250)]
        public required string Path { get; set; }

        [MaxLength(100)]
        public required string Artist { get; set; }

        public virtual ICollection<SongRaw> Songs
        {
            get => this.songs ??= new List<SongRaw>();
            set => this.songs = value;
        }
    }
}
