using System;
using Microsoft.EntityFrameworkCore;

namespace EfProvider
{
    public static class PostgresEfExtensions
    {
        internal static void Config(ModelBuilder builder)
        {
            builder.HasDbFunction(() => Crypt(default, default));
        }

        [DbFunction("crypt")]
        public static string Crypt(string password, string salt)
        {
            throw new NotImplementedException();
        }
    }
}