using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Linq;
using System.Text.RegularExpressions;
using System;


namespace EfProvider.Config
{
    internal static class EnumFluentProvider
    {
        public static void MapEnum()
        {
        }

        public static void Config(ModelBuilder builder)
        {
        }

        private static string[] GetNames(Type enym)
        {
            var regex = new Regex(@"([A-Z])");
            var result = Enum.GetNames(enym)
                .Select(e => regex.Replace(e, match => $"_{match.Value.ToLower()}").Remove(0, 1));
            return result.ToArray();
        }
    }
}
