﻿// <auto-generated />
using System;
using DbMigrator;
using DbModel.Base;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace DbMigrator.Migrations
{
    [DbContext(typeof(DbContextFactory.MigratorEfDbContext))]
    partial class MigratorEfDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("Npgsql:Enum:gender", "undefined,male,female")
                .HasAnnotation("Npgsql:PostgresExtension:pgcrypto", ",,")
                .HasAnnotation("Npgsql:PostgresExtension:uuid-ossp", ",,")
                .HasAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn)
                .HasAnnotation("ProductVersion", "2.2.4-servicing-10062")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            modelBuilder.Entity("DbModel.UserDb", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<DateTime>("CreatedUtc");

                    b.Property<string>("Login")
                        .IsRequired();

                    b.Property<string>("Pasword")
                        .IsRequired();

                    b.Property<string>("Salt")
                        .IsRequired();

                    b.Property<DateTime>("UpdatedUtc");

                    b.HasKey("Id");

                    b.HasIndex("CreatedUtc");

                    b.HasIndex("Login")
                        .IsUnique();

                    b.HasIndex("UpdatedUtc");

                    b.ToTable("User");
                });

            modelBuilder.Entity("DbModel.UserProfileDb", b =>
                {
                    b.Property<Guid>("Id");

                    b.Property<Gender>("Gender");

                    b.HasKey("Id");

                    b.ToTable("UserProfile");
                });

            modelBuilder.Entity("DbModel.UserProfileDb", b =>
                {
                    b.HasOne("DbModel.UserDb", "User")
                        .WithOne("Profile")
                        .HasForeignKey("DbModel.UserProfileDb", "Id")
                        .OnDelete(DeleteBehavior.Restrict);
                });
#pragma warning restore 612, 618
        }
    }
}
