using DbModel;
using Microsoft.EntityFrameworkCore;

namespace EfProvider.Config
{
    internal static class ForeignKeysFluentConfig
    {
        public static void Config(ModelBuilder builder)
        {
            builder.Entity<UserDb>()
                .HasOne(n => n.Profile)
                .WithOne(n => n.User)
                .HasForeignKey<UserProfileDb>(d => d.Id)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<UserDb>()
                .HasMany(n => n.Groups)
                .WithOne(n => n.User)
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<UserDb>()
                .HasMany(n => n.Invitations)
                .WithOne(n => n.Inviter)
                .HasForeignKey(n => n.InviterId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<UserDb>()
                .HasMany(n => n.Messages)
                .WithOne(n => n.User)
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<GroupDb>()
                .HasMany(n => n.Messages)
                .WithOne(n => n.Group)
                .HasForeignKey(n => n.GroupId)
                .OnDelete(DeleteBehavior.Restrict);
            
            builder.Entity<GroupDb>()
                .HasMany(n => n.Users)
                .WithOne(n => n.Group)
                .HasForeignKey(n => n.GroupId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}