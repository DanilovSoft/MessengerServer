using System;
using Microsoft.EntityFrameworkCore;

namespace EfProvider
{
    public static class PostgresEfExtensions
    {
        [DbFunction("crypt")]
        public static string Crypt(string password, string salt)
        {
            throw new NotImplementedException();
        }
    }
}