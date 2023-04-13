using System.ComponentModel.DataAnnotations;

namespace Resona.Services.Configuration
{
    public class AudiobookConfiguration
    {
        [Required]
        public string AudiobookPath { get; set; } = null!;
        [Required]
        public string MusicPath { get; set; } = null!;
        [Required]
        public string SleepPath { get; set; } = null!;
    }
}
