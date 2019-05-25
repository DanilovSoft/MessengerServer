using DbModel;
using Microsoft.EntityFrameworkCore;

namespace EfProvider.Config
{
    public static class AutoIncrementConfig
    {
        public static void Config(ModelBuilder builder)
        {
            builder.Entity<UserDb>().Property(a => a.Id).ValueGeneratedNever();
            builder.Entity<GroupDb>().Property(a => a.Id).ValueGeneratedNever();
        }
    }
}