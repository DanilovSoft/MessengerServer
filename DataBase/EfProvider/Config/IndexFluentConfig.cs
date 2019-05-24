using DbModel;
using Microsoft.EntityFrameworkCore;

namespace EfProvider.Config
{
    internal static class IndexFluentConfig
    {
        public static void Config(ModelBuilder builder)
        {
            UserConfig(builder);
            UserGroupConfig(builder);
            MessageConfig(builder);
            GroupConfig(builder);
        }

        private static void GroupConfig(ModelBuilder builder)
        {
            builder.Entity<GroupDb>().HasIndex(x => x.Name).IsUnique(false);
            builder.Entity<GroupDb>().HasIndex(x => x.CreatedUtc).IsUnique(false);
            builder.Entity<GroupDb>().HasIndex(x => x.UpdatedUtc).IsUnique(false);
            builder.Entity<GroupDb>().HasIndex(x => x.DeletedUtc).IsUnique(false);
        }

        private static void MessageConfig(ModelBuilder builder)
        {
            builder.Entity<MessageDb>().HasIndex(x => x.CreatedUtc).IsUnique(false);
            builder.Entity<MessageDb>().HasIndex(x => x.UpdatedUtc).IsUnique(false);
        }

        private static void UserGroupConfig(ModelBuilder builder)
        {
            builder.Entity<UserGroupDb>().HasKey(x => new {x.UserId, x.GroupId});
            builder.Entity<UserGroupDb>().HasIndex(x => x.CreatedUtc).IsUnique(false);
            builder.Entity<UserGroupDb>().HasIndex(x => x.DeletedUtc).IsUnique(false);
        }

        private static void UserConfig(ModelBuilder builder)
        {
            builder.Entity<UserDb>().HasIndex(x => x.NormalLogin).IsUnique();
            builder.Entity<UserDb>().HasIndex(x => x.CreatedUtc).IsUnique(false);
            builder.Entity<UserDb>().HasIndex(x => x.UpdatedUtc).IsUnique(false);
        }
    }
}