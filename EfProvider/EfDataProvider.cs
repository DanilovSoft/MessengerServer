using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System;
using DbModel.Base;
using EfProvider.Extensions;


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

        public async Task<T> InsertAsync<T>(T entity) where T : class, IEntity
        {
            await ExecuteCommand(() =>
            {
                Add(entity);
                return _dbContext.SaveChangesAsync();
            });

            return entity;
        }

        public async Task<T> UpdateAsync<T>(T entity, bool ignoreSystemProps = true) where T : class, IEntity
        {
            await ExecuteCommand(() =>
            {
                UpdateEntity(entity, ignoreSystemProps);
                return _dbContext.SaveChangesAsync();
            });

            return entity;
        }

        public Task DeleteAsync<T>(T entity) where T : class, IEntity
        {
            return ExecuteCommand(() =>
            {
                _dbContext.Set<T>().Remove(entity);
                return _dbContext.SaveChangesAsync();
            });
        }

        public Task DeleteByIdAsync<T, TKey>(TKey id) where T : class, IEntity, IEntity<TKey>
            where TKey : IComparable
        {
            return ExecuteCommand(async () =>
            {
                var entity = await _dbContext.Set<T>().Where(t => id.Equals(t.Id)).SingleAsync();

                _dbContext.Set<T>().Remove(entity);
                return await _dbContext.SaveChangesAsync();
            });
        }

        public Task SetDeleteAsync<T, TKey>(TKey id) where T : class, IEntity, IDeletedUtc, IEntity<TKey>
            where TKey : IComparable
        {
            return ExecuteCommand(async () =>
            {
                var entity = await _dbContext.Set<T>().Where(t => id.Equals(t.Id)).SingleAsync();
                entity.DeletedUtc = DateTime.UtcNow;

                UpdateEntity(entity, false);
                return await _dbContext.SaveChangesAsync();
            });
        }

        #endregion

        #region BatchModify

        public Task BatchInsertAsync<T>(IEnumerable<T> entities) where T : class, IEntity
        {
            return ExecuteCommand(() =>
            {
                foreach (var entity in entities)
                {
                    Add(entity);
                }
                return _dbContext.SaveChangesAsync();
            });
        }

        public Task BatchUpdateAsync<T>(IEnumerable<T> entities, bool ignoreSystemProps = true) where T : class, IEntity
        {
            return ExecuteCommand(() =>
            {
                foreach (var entity in entities)
                {
                    UpdateEntity(entity, ignoreSystemProps);
                }
                return _dbContext.SaveChangesAsync();
            });
        }

        public Task BatchDeleteAsync<T>(IEnumerable<T> entities) where T : class, IEntity
        {
            return ExecuteCommand(() =>
            {
                _dbContext.Set<T>().RemoveRange(entities);
                return _dbContext.SaveChangesAsync();
            });
        }

        public Task BatchDeleteByIdsAsync<T, TKey>(IEnumerable<TKey> ids) where T : class, IEntity, IEntity<TKey>
            where TKey : IComparable
        {
            return ExecuteCommand(async () =>
            {
                var entity = await _dbContext.Set<T>().Where(t => ids.Contains(t.Id)).ToArrayAsync();

                _dbContext.Set<T>().RemoveRange(entity);
                return await _dbContext.SaveChangesAsync();
            });
        }

        public Task BatchSetDeleteAsync<T, TKey>(IEnumerable<TKey> ids)
            where T : class, IEntity, IDeletedUtc, IEntity<TKey>
            where TKey : IComparable
        {
            return ExecuteCommand(async () =>
            {
                var entities = await _dbContext.Set<T>().Where(t => ids.Contains(t.Id)).ToArrayAsync();

                foreach (var entity in entities)
                {
                    entity.DeletedUtc = DateTime.UtcNow;
                    UpdateEntity(entity, false);
                }

                return await _dbContext.SaveChangesAsync();
            });
        }

        #endregion

        #region SafeExecute

        public async Task<T> SafeExecuteAsync<T>([InstantHandle] Func<IDataProvider, Task<T>> action,
            IsolationLevel level = IsolationLevel.RepeatableRead, int retryCount = 3)
        {
            var result = default(T);
            async Task Wrapper(IDataProvider db) => result = await action(db);

            await SafeExecuteAsync(Wrapper, level, retryCount);

            return result;
        }

        public async Task SafeExecuteAsync([InstantHandle] Func<IDataProvider, Task> action,
            IsolationLevel level = IsolationLevel.RepeatableRead, int retryCount = 3)
        {
            var count = 0;
            while (true)
            {
                try
                {
                    using (IDbContextTransaction transaction = Transaction(level))
                    {
                        await action(this);
                        transaction.Commit();
                        break;
                    }
                }
                catch (Exception exception)
                {
                    _dbContext.Reset();

                    if (exception.IsConcurrentModifyException() && ++count < retryCount)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(1));
                        continue;
                    }
                    throw;
                }
            }
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

            var entityEntry = _dbContext.Entry(entity);
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

        private async Task<int> ExecuteCommand(Func<Task<int>> func)
        {
            try
            {
                return await func().ConfigureAwait(false);
            }
            catch (Exception exception) when (exception.InnerException is PostgresException ex)
            {
                throw ex.NormalizePostgresException();
            }
        }
    }
}