using DbModel;
using Microsoft.EntityFrameworkCore;

namespace EfProvider.Config
{
    internal static class IndexFluentConfig
    {
        public static void Config(ModelBuilder builder)
        {
            builder.Entity<UserDb>().HasIndex(x => new {x.Login}).IsUnique();
            builder.Entity<UserDb>().HasIndex(x => new {x.CreatedUtc}).IsUnique(false);
            builder.Entity<UserDb>().HasIndex(x => new {x.UpdatedUtc}).IsUnique(false);
        }
    }
}
