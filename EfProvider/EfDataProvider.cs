using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System;
using DBCore;
using DBCore.Entities;
using EfProvider.Extensions;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace EfProvider
{
    public class EfDataProvider : IDataProvider
    {
        private readonly CustomEfDbContext _dbContext;

        public EfDataProvider([NotNull] CustomEfDbContext connection)
        {
            _dbContext = connection ?? throw new ArgumentNullException(nameof(connection));
        }

        public IDbContextTransaction Transaction()
        {
            return _dbContext.Database.BeginTransaction();
        }

        public IDbContextTransaction Transaction(IsolationLevel isolationLevel)
        {
            return _dbContext.Database.BeginTransaction(isolationLevel);
        }

        public IQueryable<T> Get<T>() where T : class, IEntity
        {
            return _dbContext.Set<T>().AsQueryable().AsNoTracking();
        }

        #region Modify

        public Task<T> InsertAsync<T>(T entity) where T : class, IEntity
        {
            return ExecuteCommand(state =>
            {
                Add(state);
                return _dbContext.SaveChangesAsync().ContinueWith(_ => state, TaskContinuationOptions.OnlyOnRanToCompletion);
            }, state: entity);
        }

        public Task<T> UpdateAsync<T>(T entity, bool ignoreSystemProps = true) where T : class, IEntity
        {
            return ExecuteCommand(state =>
            {
                UpdateEntity(state.entity, state.ignoreSystemProps);
                return _dbContext.SaveChangesAsync().ContinueWith(_ => state.entity, TaskContinuationOptions.OnlyOnRanToCompletion);
            }, state: (entity, ignoreSystemProps));
        }

        public Task DeleteAsync<T>(T entity) where T : class, IEntity
        {
            return ExecuteCommand(state =>
            {
                _dbContext.Set<T>().Remove(state);
                return _dbContext.SaveChangesAsync();
            }, state: entity);
        }

        public Task DeleteByIdAsync<T, TKey>(TKey id) where T : class, IEntity, IEntity<TKey> where TKey : IComparable
        {
            return ExecuteCommand(async state =>
            {
                var entity = await _dbContext.Set<T>().Where(t => state.Equals(t.Id)).SingleAsync()
                    .ConfigureAwait(false);
                _dbContext.Set<T>().Remove(entity);
                return await _dbContext.SaveChangesAsync().ConfigureAwait(false);
            }, state: id);
        }

        public Task SetDeleteAsync<T, TKey>(TKey id) where T : class, IEntity, IDeletedUtc, IEntity<TKey>
            where TKey : IComparable
        {
            return ExecuteCommand(async state =>
            {
                var entity = await _dbContext.Set<T>().Where(t => state.Equals(t.Id)).SingleAsync()
                    .ConfigureAwait(false);
                entity.DeletedUtc = DateTime.UtcNow;
                UpdateEntity(entity, ignoreSystemProps: false);
                return await _dbContext.SaveChangesAsync().ConfigureAwait(false);
            }, state: id);
        }

        #endregion

        #region BatchModify

        public Task BatchInsertAsync<T>(IEnumerable<T> entities) where T : class, IEntity
        {
            return ExecuteCommand(state =>
            {
                foreach (var entity in state)
                {
                    Add(entity);
                }

                return _dbContext.SaveChangesAsync();
            }, state: entities);
        }

        public Task BatchUpdateAsync<T>(IEnumerable<T> entities, bool ignoreSystemProps = true) where T : class, IEntity
        {
            return ExecuteCommand(state =>
            {
                foreach (var entity in state.entities)
                {
                    UpdateEntity(entity, state.ignoreSystemProps);
                }

                return _dbContext.SaveChangesAsync();
            }, state: (entities, ignoreSystemProps));
        }

        public Task BatchDeleteAsync<T>(IEnumerable<T> entities) where T : class, IEntity
        {
            return ExecuteCommand(state =>
            {
                _dbContext.Set<T>().RemoveRange(state);
                return _dbContext.SaveChangesAsync();
            }, state: entities);
        }

        public Task BatchDeleteByIdsAsync<T, TKey>(IEnumerable<TKey> ids) where T : class, IEntity, IEntity<TKey>
            where TKey : IComparable
        {
            return ExecuteCommand(async state =>
            {
                var entity = await _dbContext.Set<T>().Where(t => state.Contains(t.Id)).ToArrayAsync()
                    .ConfigureAwait(false);
                _dbContext.Set<T>().RemoveRange(entity);
                return await _dbContext.SaveChangesAsync().ConfigureAwait(false);
            }, state: ids);
        }

        public Task BatchSetDeleteAsync<T, TKey>(IEnumerable<TKey> ids)
            where T : class, IEntity, IDeletedUtc, IEntity<TKey>
            where TKey : IComparable
        {
            return ExecuteCommand(async state =>
            {
                var entities = await _dbContext.Set<T>().Where(t => state.Contains(t.Id)).ToArrayAsync()
                    .ConfigureAwait(false);

                foreach (var entity in entities)
                {
                    entity.DeletedUtc = DateTime.UtcNow;
                    UpdateEntity(entity, false);
                }

                return await _dbContext.SaveChangesAsync().ConfigureAwait(false);
            }, state: ids);
        }

        #endregion

        #region SafeExecute

        public Task<T> SafeExecuteAsync<T>([InstantHandle] Func<IDataProvider, Task<T>> action,
            IsolationLevel level = IsolationLevel.RepeatableRead, int retryCount = 3)
        {
            return InnerSafeExecuteAsync(action, level, retryCount);
        }

        public Task SafeExecuteAsync([InstantHandle] Func<IDataProvider, Task> action,
            IsolationLevel level = IsolationLevel.RepeatableRead, int retryCount = 3)
        {
            Task<object> EmptyResultWrapper(IDataProvider db)
            {
                return action(db).ContinueWith<object>(_ => null, TaskContinuationOptions.OnlyOnRanToCompletion);
            }

            return InnerSafeExecuteAsync(EmptyResultWrapper, level, retryCount);
        }

        private async Task<T> InnerSafeExecuteAsync<T>([InstantHandle] Func<IDataProvider, Task<T>> action,
            IsolationLevel level, int retryCount)
        {
            int count = 0;
            do
            {
                try
                {
                    using (IDbContextTransaction transaction = Transaction(level))
                    {
                        T t = await action(this).ConfigureAwait(false);
                        transaction.Commit();
                        return t;
                    }
                }
                catch (Exception exception)
                {
                    _dbContext.Reset();

                    if (exception.IsConcurrentModifyException() && ++count < retryCount)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
                        continue;
                    }

                    throw;
                }
            } while (true);
        }

        #endregion

        private void Add<T>(T entity) where T : class
        {
            if (entity is ICreatedUtc createdUtc)
            {
                createdUtc.CreatedUtc = DateTime.UtcNow;
            }

            if (entity is IUpdatedUtc updatedUtc)
            {
                updatedUtc.UpdatedUtc = DateTime.UtcNow;
            }

            _dbContext.Set<T>().Add(entity);
        }

        private void UpdateEntity<T>(T entity, bool ignoreSystemProps) where T : class
        {
            if (entity is IUpdatedUtc updatedUtc)
            {
                updatedUtc.UpdatedUtc = DateTime.UtcNow;
            }

            EntityEntry<T> entityEntry = _dbContext.Entry(entity);
            if (ignoreSystemProps)
            {
                if (entity is IDeletedUtc)
                {
                    entityEntry.Property(nameof(IDeletedUtc.DeletedUtc)).IsModified = false;
                }

                if (entity is ICreatedUtc)
                {
                    entityEntry.Property(nameof(ICreatedUtc.CreatedUtc)).IsModified = false;
                }
            }

            entityEntry.State = EntityState.Modified;
        }

        private async Task<T> ExecuteCommand<T, TState>(Func<TState, Task<T>> func, TState state)
        {
            try
            {
                return await func(state).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception.InnerException is PostgresException ex)
            {
                throw ex.NormalizePostgresException();
            }
        }
    }
}
