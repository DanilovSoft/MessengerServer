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
    [Migration("20190511162207_GroupAndMessage")]
    partial class GroupAndMessage
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

            modelBuilder.Entity("DbModel.GroupDb", b =>
                {
                    b.Property<long>("Id");

                    b.Property<string>("AvatarUrl");

                    b.Property<DateTime>("CreatedUtc");

                    b.Property<int>("CreatorId");

                    b.Property<DateTime?>("DeletedUtc");

                    b.Property<string>("Name")
                        .HasMaxLength(120);

                    b.Property<DateTime>("UpdatedUtc");

                    b.HasKey("Id");

                    b.HasIndex("CreatedUtc");

                    b.HasIndex("CreatorId");

                    b.HasIndex("DeletedUtc");

                    b.HasIndex("Name");

                    b.HasIndex("UpdatedUtc");

                    b.ToTable("Groups");
                });

            modelBuilder.Entity("DbModel.MessageDb", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<DateTime>("CreatedUtc");

                    b.Property<string>("FileUrl");

                    b.Property<long>("GroupId");

                    b.Property<string>("Text");

                    b.Property<DateTime>("UpdatedUtc");

                    b.Property<int>("UserId");

                    b.HasKey("Id");

                    b.HasIndex("CreatedUtc");

                    b.HasIndex("GroupId");

                    b.HasIndex("UpdatedUtc");

                    b.HasIndex("UserId");

                    b.ToTable("Messages");
                });

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

            modelBuilder.Entity("DbModel.UserGroupDb", b =>
                {
                    b.Property<int>("UserId");

                    b.Property<long>("GroupId");

                    b.Property<DateTime>("CreatedUtc");

                    b.Property<DateTime?>("DeletedUtc");

                    b.Property<int?>("InviterId");

                    b.HasKey("UserId", "GroupId");

                    b.HasIndex("CreatedUtc");

                    b.HasIndex("DeletedUtc");

                    b.HasIndex("GroupId");

                    b.HasIndex("InviterId");

                    b.ToTable("UserGroups");
                });

            modelBuilder.Entity("DbModel.UserProfileDb", b =>
                {
                    b.Property<int>("Id");

                    b.Property<string>("AvatarUrl");

                    b.Property<string>("DisplayName");

                    b.Property<Gender>("Gender");

                    b.HasKey("Id");

                    b.ToTable("UserProfiles");
                });

            modelBuilder.Entity("DbModel.GroupDb", b =>
                {
                    b.HasOne("DbModel.UserDb", "Creator")
                        .WithMany("Creations")
                        .HasForeignKey("CreatorId")
                        .OnDelete(DeleteBehavior.Restrict);
                });

            modelBuilder.Entity("DbModel.MessageDb", b =>
                {
                    b.HasOne("DbModel.GroupDb", "Group")
                        .WithMany("Messages")
                        .HasForeignKey("GroupId")
                        .OnDelete(DeleteBehavior.Restrict);

                    b.HasOne("DbModel.UserDb", "User")
                        .WithMany("Messages")
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Restrict);
                });

            modelBuilder.Entity("DbModel.UserGroupDb", b =>
                {
                    b.HasOne("DbModel.GroupDb", "Group")
                        .WithMany("Users")
                        .HasForeignKey("GroupId")
                        .OnDelete(DeleteBehavior.Restrict);

                    b.HasOne("DbModel.UserDb", "Inviter")
                        .WithMany("Invitations")
                        .HasForeignKey("InviterId")
                        .OnDelete(DeleteBehavior.Restrict);

                    b.HasOne("DbModel.UserDb", "User")
                        .WithMany("Groups")
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Restrict);
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
