using DbModel;
using Microsoft.EntityFrameworkCore;

namespace EfProvider.Config
{
    internal static class ForeignKeysFluentConfig
    {
        public static void Config(ModelBuilder builder)
        {
            builder.Entity<UserDb>()
                .HasOne(d => d.Profile)
                .WithOne(u => u.User)
                .HasForeignKey<UserProfileDb>(d => d.Id)
                .OnDelete(DeleteBehavior.Restrict);

        }
    }
}