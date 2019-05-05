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

        public async Task<T> Insert<T>(T entity) where T : class, IEntity
        {
            await ExecuteCommand(() =>
            {
                Add(entity);
                return _dbContext.SaveChangesAsync();
            });

            return entity;
        }

        public async Task<T> Update<T>(T entity, bool ignoreSystemProps = true) where T : class, IEntity
        {
            await ExecuteCommand(() =>
            {
                UpdateEntity(entity, ignoreSystemProps);
                return _dbContext.SaveChangesAsync();
            });

            return entity;
        }

        public async Task Delete<T>(T entity) where T : class, IEntity
        {
            await ExecuteCommand(() =>
            {
                _dbContext.Set<T>().Remove(entity);
                return _dbContext.SaveChangesAsync();
            });
        }

        public async Task DeleteById<T, TKey>(TKey id) where T : class, IEntity, IEntity<TKey>
            where TKey : IComparable
        {
            await ExecuteCommand(async () =>
            {
                var entity = await _dbContext.Set<T>().Where(t => id.Equals(t.Id)).SingleAsync();

                _dbContext.Set<T>().Remove(entity);
                return await _dbContext.SaveChangesAsync();
            });
        }

        public async Task SetDelete<T, TKey>(TKey id) where T : class, IEntity, IDeletedUtc, IEntity<TKey>
            where TKey : IComparable
        {
            await ExecuteCommand(async () =>
            {
                var entity = await _dbContext.Set<T>().Where(t => id.Equals(t.Id)).SingleAsync();
                entity.DeletedUtc = DateTime.UtcNow;

                UpdateEntity(entity, false);
                return await _dbContext.SaveChangesAsync();
            });
        }

        #endregion

        #region BatchModify

        public async Task BatchInsert<T>(IEnumerable<T> entities) where T : class, IEntity
        {
            await ExecuteCommand(() =>
            {
                foreach (var entity in entities)
                {
                    Add(entity);
                }

                return _dbContext.SaveChangesAsync();
            });
        }

        public async Task BatchUpdate<T>(IEnumerable<T> entities, bool ignoreSystemProps = true)
            where T : class, IEntity
        {
            await ExecuteCommand(() =>
            {
                foreach (var entity in entities)
                {
                    UpdateEntity(entity, ignoreSystemProps);
                }

                return _dbContext.SaveChangesAsync();
            });
        }

        public async Task BatchDelete<T>(IEnumerable<T> entities) where T : class, IEntity
        {
            await ExecuteCommand(() =>
            {
                _dbContext.Set<T>().RemoveRange(entities);
                return _dbContext.SaveChangesAsync();
            });
        }

        public async Task BatchDeleteByIds<T, TKey>(IEnumerable<TKey> ids) where T : class, IEntity, IEntity<TKey>
            where TKey : IComparable
        {
            await ExecuteCommand(async () =>
            {
                var entity = await _dbContext.Set<T>().Where(t => ids.Contains(t.Id)).ToArrayAsync();

                _dbContext.Set<T>().RemoveRange(entity);
                return await _dbContext.SaveChangesAsync();
            });
        }

        public async Task BatchSetDelete<T, TKey>(IEnumerable<TKey> ids)
            where T : class, IEntity, IDeletedUtc, IEntity<TKey>
            where TKey : IComparable
        {
            await ExecuteCommand(async () =>
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

        public async Task<T> SafeExecute<T>([InstantHandle] Func<IDataProvider, Task<T>> action,
            IsolationLevel level = IsolationLevel.RepeatableRead, int retryCount = 3)
        {
            var result = default(T);
            async Task Wrapper(IDataProvider db) => result = await action(db);

            await SafeExecute(Wrapper, level, retryCount);

            return result;
        }

        public async Task SafeExecute([InstantHandle] Func<IDataProvider, Task> action,
            IsolationLevel level = IsolationLevel.RepeatableRead, int retryCount = 3)
        {
            var count = 0;
            while (true)
            {
                try
                {
                    using (var transaction = Transaction(level))
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
                if ((object) entity is IDeletedUtc)
                {
                    entityEntry.Property(nameof(IDeletedUtc.DeletedUtc)).IsModified = false;
                }

                if ((object) entity is ICreatedUtc)
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
                return await func();
            }
            catch (Exception exception) when (exception.InnerException is PostgresException ex)
            {
                throw ex.NormalizePostgresException();
            }
        }
    }
}