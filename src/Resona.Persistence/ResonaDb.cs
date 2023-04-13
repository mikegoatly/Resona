﻿using System.Diagnostics;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

using Serilog;

namespace Resona.Persistence
{
    public class ResonaDb : DbContext
    {
        private static readonly ILogger _logger = Log.Logger.ForContext<ResonaDb>();
        private static bool _initialized;

        public DbSet<AlbumRaw> Albums { get; set; }
        public DbSet<SongRaw> Songs { get; set; }

        [Conditional("DEBUG")]
        public static void Reset()
        {
            _initialized = false;
            using var context = new ResonaDb();
            context.Database.EnsureDeleted();

            Initialize();
        }

        public static void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            using var context = new ResonaDb();

            try
            {
                context.Database.Migrate();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "An error occurred migrating the database");
            }

            _initialized = true;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
#if DEBUG
            optionsBuilder.EnableSensitiveDataLogging();
#endif

            var dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "resona.db");

            _logger.Information("Configuring database: {Path}", dbPath);

            var connectionString = new SqliteConnectionStringBuilder()
            {
                DataSource = dbPath,
                Mode = SqliteOpenMode.ReadWriteCreate,
            }.ToString();

            optionsBuilder.UseSqlite(connectionString);
        }
    }
}