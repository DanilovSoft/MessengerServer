using System;
using System.Linq;
using System.Reflection;
using Microsoft.EntityFrameworkCore;

namespace EfProvider
{
    public static class PostgresEfExtensions
    {
        private static readonly MethodInfo[] Methods;

        static PostgresEfExtensions()
        {
            Methods = typeof(PostgresEfExtensions).GetMethods()
                .Where(m => Attribute.IsDefined(m, typeof(DbFunctionAttribute)))
                .ToArray();
        }

        internal static void Config(ModelBuilder builder)
        {
            foreach (var method in Methods)
            {
                builder.HasDbFunction(method);
            }
        }

        [DbFunction("crypt")]
        public static string Crypt(string password, string salt) => throw new NotImplementedException();


        [DbFunction("gen_salt")]
        public static string GenSalt(string algorithm, int iterCount = 6) => throw new NotImplementedException();
    }
}