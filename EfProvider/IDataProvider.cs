using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using DbModel.Base;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore.Storage;

namespace EfProvider
{
    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    public interface IDataProvider
    {
        IQueryable<T> Get<T>() where T : class, IEntity;

        Task<T> InsertAsync<T>(T entity) where T : class, IEntity;
        /// <summary>
        /// Пакетная вставка записей в БД.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="entities"></param>
        /// <returns></returns>
        Task BatchInsertAsync<T>(IEnumerable<T> entities) where T : class, IEntity;

        Task<T> UpdateAsync<T>(T entity, bool ignoreSystemProps = true) where T : class, IEntity;
        Task BatchUpdateAsync<T>(IEnumerable<T> entities, bool ignoreSystemProps = true) where T : class, IEntity;

        Task DeleteAsync<T>(T entity) where T : class, IEntity;
        Task BatchDeleteAsync<T>(IEnumerable<T> entities) where T : class, IEntity;

        Task DeleteByIdAsync<T, TKey>(TKey id) where T : class, IEntity, IEntity<TKey> where TKey : IComparable;

        Task BatchDeleteByIdsAsync<T, TKey>(IEnumerable<TKey> ids)
            where T : class, IEntity, IEntity<TKey> where TKey : IComparable;

        Task SetDeleteAsync<T, TKey>(TKey id) where T : class, IEntity, IDeletedUtc, IEntity<TKey> where TKey : IComparable;

        Task BatchSetDeleteAsync<T, TKey>(IEnumerable<TKey> ids) where T : class, IEntity, IDeletedUtc, IEntity<TKey>
            where TKey : IComparable;

        IDbContextTransaction Transaction();
        IDbContextTransaction Transaction(IsolationLevel isolationLevel);

        Task<T> SafeExecuteAsync<T>([InstantHandle] Func<IDataProvider, Task<T>> action,
            IsolationLevel level = IsolationLevel.RepeatableRead, int retryCount = 3);

        Task SafeExecuteAsync([InstantHandle] Func<IDataProvider, Task> action,
            IsolationLevel level = IsolationLevel.RepeatableRead, int retryCount = 3);
    }
}