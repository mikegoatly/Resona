using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

using Serilog;

namespace Resona.Persistence
{
    public class ResonaDb : DbContext
    {
        private static readonly ILogger logger = Log.Logger.ForContext<ResonaDb>();
        private static bool initialized;

        public DbSet<AlbumRaw> Albums { get; set; }
        public DbSet<TrackRaw> Tracks { get; set; }

        public static void Reset()
        {
            initialized = false;
            using var context = new ResonaDb();
            context.Database.EnsureDeleted();

            Initialize();
        }

        public static bool Initialize()
        {
            if (initialized)
            {
                return true;
            }

            initialized = true;
            using var context = new ResonaDb();

            try
            {
                context.Database.Migrate();
                return true;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "An error occurred migrating the database");
                return false;
            }
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
#if DEBUG
            optionsBuilder.EnableSensitiveDataLogging();
#endif

            var dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "resona.db");

            var connectionString = new SqliteConnectionStringBuilder()
            {
                DataSource = dbPath,
                Mode = SqliteOpenMode.ReadWriteCreate,
            }.ToString();

            optionsBuilder.UseSqlite(connectionString);
        }
    }
}