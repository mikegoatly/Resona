﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Resona.Persistence;

#nullable disable

namespace Resona.Persistence.Migrations
{
    [DbContext(typeof(ResonaDb))]
    partial class ResonaDbModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder.HasAnnotation("ProductVersion", "7.0.5");

            modelBuilder.Entity("Resona.Persistence.AlbumRaw", b =>
                {
                    b.Property<int>("AlbumId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<string>("Artist")
                        .HasMaxLength(100)
                        .HasColumnType("TEXT");

                    b.Property<int>("Kind")
                        .HasColumnType("INTEGER");

                    b.Property<DateTime?>("LastPlayedDateUtc")
                        .HasColumnType("TEXT");

                    b.Property<int?>("LastPlayedTrackId")
                        .HasColumnType("INTEGER");

                    b.Property<double?>("LastPlayedTrackPosition")
                        .HasColumnType("REAL");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("TEXT");

                    b.Property<string>("Path")
                        .IsRequired()
                        .HasMaxLength(250)
                        .HasColumnType("TEXT");

                    b.Property<string>("ThumbnailFile")
                        .HasMaxLength(350)
                        .HasColumnType("TEXT");

                    b.HasKey("AlbumId");

                    b.HasIndex("LastPlayedDateUtc");

                    b.HasIndex("LastPlayedTrackId");

                    b.HasIndex("Kind", "Name");

                    b.ToTable("Album");
                });

            modelBuilder.Entity("Resona.Persistence.TrackRaw", b =>
                {
                    b.Property<int>("TrackId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<int>("AlbumId")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Artist")
                        .HasMaxLength(100)
                        .HasColumnType("TEXT");

                    b.Property<TimeSpan?>("Duration")
                        .HasColumnType("TEXT");

                    b.Property<string>("FileName")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("TEXT");

                    b.Property<DateTime>("LastModifiedUtc")
                        .HasColumnType("TEXT");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("TEXT");

                    b.Property<uint>("TrackNumber")
                        .HasColumnType("INTEGER");

                    b.HasKey("TrackId");

                    b.HasIndex("AlbumId");

                    b.ToTable("Track");
                });

            modelBuilder.Entity("Resona.Persistence.AlbumRaw", b =>
                {
                    b.HasOne("Resona.Persistence.TrackRaw", "LastPlayedTrack")
                        .WithMany()
                        .HasForeignKey("LastPlayedTrackId")
                        .OnDelete(DeleteBehavior.SetNull);

                    b.Navigation("LastPlayedTrack");
                });

            modelBuilder.Entity("Resona.Persistence.TrackRaw", b =>
                {
                    b.HasOne("Resona.Persistence.AlbumRaw", "Album")
                        .WithMany("Tracks")
                        .HasForeignKey("AlbumId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Album");
                });

            modelBuilder.Entity("Resona.Persistence.AlbumRaw", b =>
                {
                    b.Navigation("Tracks");
                });
#pragma warning restore 612, 618
        }
    }
}
