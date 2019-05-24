using System;
using DBCore.Extensions;
using Npgsql;

namespace EfProvider.Extensions
{
    public static class ExceptionExtensions
    {
        private static string _concurrentState = "40001";

        internal static Exception NormalizePostgresException(this PostgresException ex)
        {
            var message = ex.Message + ex.Detail;

            if (ex.SqlState == "23505")
            {
                return new ObjectAlreadyExistsException(message, ex);
            }

            if (ex.SqlState == _concurrentState)
            {
                return new ConcurrentModifyException(message, ex);
            }

            return new PostgreSqlException(ex.SqlState, message, ex);
        }

        internal static bool IsConcurrentModifyException(this Exception ex)
        {
            return ex is ObjectAlreadyExistsException
                   || BaseIsConcurrentModifyException(ex)
                   || ex.InnerException != null && BaseIsConcurrentModifyException(ex.InnerException)
                   || ex.InnerException?.InnerException != null
                   && BaseIsConcurrentModifyException(ex.InnerException.InnerException);

            bool BaseIsConcurrentModifyException(Exception innerException)
            {
                if (innerException == null)
                {
                    return false;
                }

                if (innerException is ConcurrentModifyException)
                {
                    return true;
                }

                return innerException is PostgresException sqlException && sqlException.SqlState == _concurrentState;
            }
        }
    }
}