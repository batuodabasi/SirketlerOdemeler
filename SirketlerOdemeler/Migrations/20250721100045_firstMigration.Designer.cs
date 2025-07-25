﻿// <auto-generated />
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SirketlerOdemeler.Data;

#nullable disable

namespace SirketlerOdemeler.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20250721100045_firstMigration")]
    partial class firstMigration
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "9.0.7")
                .HasAnnotation("Relational:MaxIdentifierLength", 128);

            SqlServerModelBuilderExtensions.UseIdentityColumns(modelBuilder);

            modelBuilder.Entity("SirketlerOdemeler.Models.Odemeler", b =>
                {
                    b.Property<int>("OdemeId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("OdemeId"));

                    b.Property<int>("OdenenTutar")
                        .HasColumnType("int");

                    b.Property<int>("SKod")
                        .HasColumnType("int");

                    b.HasKey("OdemeId");

                    b.ToTable("Odemeler");
                });

            modelBuilder.Entity("SirketlerOdemeler.Models.Sirketler", b =>
                {
                    b.Property<int>("SKod")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("SKod"));

                    b.Property<string>("SirketAd")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("SirketMail")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("SKod");

                    b.ToTable("Sirketler");
                });
#pragma warning restore 612, 618
        }
    }
}
