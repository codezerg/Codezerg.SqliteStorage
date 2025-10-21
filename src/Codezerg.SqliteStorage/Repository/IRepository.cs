using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Codezerg.SqliteStorage.Repository;

public interface IRepository<T> where T : class
{
    // Create
    int Insert(T entity);
    int InsertWithIdentity(T entity);
    long InsertWithInt64Identity(T entity);
    int InsertRange(IEnumerable<T> entities);

    // Read
    IEnumerable<T> GetAll();
    IEnumerable<T> Find(Expression<Func<T, bool>> predicate);
    T FirstOrDefault(Expression<Func<T, bool>> predicate);
    IEnumerable<TResult> Select<TResult>(Expression<Func<T, TResult>> selector);
    IEnumerable<TResult> Query<TResult>(Func<IQueryable<T>, IEnumerable<TResult>> query);

    // Update  
    int Update(T entity);
    int UpdateRange(IEnumerable<T> entities);

    // Delete
    int Delete(T entity);
    int DeleteRange(IEnumerable<T> entities);
    int DeleteMany(Expression<Func<T, bool>> predicate);

    // Count
    int Count();
    int Count(Expression<Func<T, bool>> predicate);

    // Exists
    bool Exists(Expression<Func<T, bool>> predicate);
}