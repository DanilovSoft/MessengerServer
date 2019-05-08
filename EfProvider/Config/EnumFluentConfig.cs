using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Text.RegularExpressions;
using System;
using DbModel.Base;
using Npgsql;


namespace EfProvider.Config
{
    internal static class EnumFluentConfig
    {
        public static void MapEnum()
        {
            AddTypeMapper<Gender>();
        }

        public static void Config(ModelBuilder builder)
        {
            AddEnum<Gender>(builder);
        }

        private static void AddTypeMapper<T>() where T : struct
        {
            NpgsqlConnection.GlobalTypeMapper.MapEnum<T>(GetName<T>());
        }

        private static void AddEnum<T>(ModelBuilder builder) where T : struct
        {
            builder.ForNpgsqlHasEnum(GetName<T>(), GetLabels(typeof(T)));
        }

        private static string GetName<T>() where T : struct
        {
            return typeof(T).Name.ToLower();
        }

        private static string[] GetLabels(Type enym)
        {
            var regex = new Regex(@"([A-Z])");
            var result = Enum.GetNames(enym)
                .Select(e => regex.Replace(e, match => $"_{match.Value.ToLower()}").Remove(0, 1));
            return result.ToArray();
        }
    }
}