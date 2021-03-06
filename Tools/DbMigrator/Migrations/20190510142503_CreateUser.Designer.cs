﻿// <auto-generated />
using System;
using DbMigrator;
using DbModel.DbTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace DbMigrator.Migrations
{
    [DbContext(typeof(DbContextFactory.MigratorEfDbContext))]
    [Migration("20190510142503_Create_User")]
    partial class CreateUser
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
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
                    b.Property<int>("Id");

                    b.Property<DateTime>("CreatedUtc");

                    b.Property<string>("Login")
                        .IsRequired()
                        .HasMaxLength(32);

                    b.Property<string>("NormalLogin")
                        .IsRequired()
                        .HasMaxLength(32);

                    b.Property<string>("Password")
                        .IsRequired()
                        .HasMaxLength(60);

                    b.Property<DateTime>("UpdatedUtc");

                    b.HasKey("Id");

                    b.HasIndex("CreatedUtc");

                    b.HasIndex("NormalLogin")
                        .IsUnique();

                    b.HasIndex("UpdatedUtc");

                    b.ToTable("Users");
                });

            modelBuilder.Entity("DbModel.UserProfileDb", b =>
                {
                    b.Property<int>("Id");

                    b.Property<Gender>("Gender");

                    b.HasKey("Id");

                    b.ToTable("UserProfiles");
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
