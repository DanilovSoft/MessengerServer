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

        Task<T> Insert<T>(T entity) where T : class, IEntity;
        Task BatchInsert<T>(IEnumerable<T> entities) where T : class, IEntity;

        Task<T> Update<T>(T entity, bool ignoreSystemProps = true) where T : class, IEntity;
        Task BatchUpdate<T>(IEnumerable<T> entities, bool ignoreSystemProps = true) where T : class, IEntity;

        Task Delete<T>(T entity) where T : class, IEntity;
        Task BatchDelete<T>(IEnumerable<T> entities) where T : class, IEntity;

        Task DeleteById<T, TKey>(TKey id) where T : class, IEntity, IEntity<TKey> where TKey : IComparable;

        Task BatchDeleteByIds<T, TKey>(IEnumerable<TKey> ids)
            where T : class, IEntity, IEntity<TKey> where TKey : IComparable;

        Task SetDelete<T, TKey>(TKey id) where T : class, IEntity, IDeletedUtc, IEntity<TKey> where TKey : IComparable;

        Task BatchSetDelete<T, TKey>(IEnumerable<TKey> ids) where T : class, IEntity, IDeletedUtc, IEntity<TKey>
            where TKey : IComparable;

        IDbContextTransaction Transaction();
        IDbContextTransaction Transaction(IsolationLevel isolationLevel);

        Task<T> SafeExecute<T>([InstantHandle] Func<IDataProvider, Task<T>> action,
            IsolationLevel level = IsolationLevel.RepeatableRead, int retryCount = 3);

        Task SafeExecute([InstantHandle] Func<IDataProvider, Task> action,
            IsolationLevel level = IsolationLevel.RepeatableRead, int retryCount = 3);
    }
}