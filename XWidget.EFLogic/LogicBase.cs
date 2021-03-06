﻿using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using System.Reflection;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq.Dynamic.Core;
using XWidget.Web.Exceptions;

namespace XWidget.EFLogic {
    /// <summary>
    /// 基礎邏輯操作器基礎
    /// </summary>
    public abstract class LogicBase<TContext, TEntity, TId>
        where TEntity : class
        where TContext : DbContext {
        /// <summary>
        /// 資料庫操作邏輯管理器
        /// </summary>
        public LogicManagerBase<TContext> Manager { get; private set; }

        /// <summary>
        /// 資料庫內容實例
        /// </summary>
        public TContext Database => Manager.Database;

        /// <summary>
        /// 唯一識別號屬性名稱
        /// </summary>
        public string IdentityPropertyName { get; private set; }

        /// <summary>
        /// 建立實例時略過唯一識別號欄位
        /// </summary>
        public bool CreateIgnoreIdentity { get; set; } = true;


        /// <summary>
        /// 基礎邏輯操作器基礎建構子，預設的主鍵為Id
        /// </summary>
        /// <param name="logicManager">資料庫操作邏輯管理器實例</param>
        public LogicBase(LogicManagerBase<TContext> logicManager) : this(logicManager, "Id") {

        }

        /// <summary>
        /// 基礎邏輯操作器基礎建構子
        /// </summary>
        /// <param name="logicManager">資料庫操作邏輯管理器實例</param>
        /// <param name="identityPropertyName">唯一識別號屬性選擇器</param>
        public LogicBase(LogicManagerBase<TContext> logicManager, string identityPropertyName) {
            this.Manager = logicManager;


            var idProperties = typeof(TEntity).GetProperties().Where(x => x.PropertyType == typeof(TId));

            if (idProperties.Count() == 0) {
                throw new InvalidOperationException("在指定類型中找不到指定的主鍵類型");
            }

            var idProperty = idProperties.FirstOrDefault(x => x.Name.ToLower() == identityPropertyName);

            if (idProperty == null) {
                idProperty = idProperties.FirstOrDefault(x => x.Name.ToLower() == (typeof(TEntity).Name + identityPropertyName).ToLower());
            }

            if (idProperty == null && idProperties.Count() == 1) {
                idProperty = idProperties.First();
            }

            if (idProperties == null) {
                throw new InvalidOperationException("在指定類型中找不到名稱為Id的屬性作為預設主鍵");
            }

            this.IdentityPropertyName = identityPropertyName;
        }

        /// <summary>
        /// 基礎邏輯操作器基礎建構子
        /// </summary>
        /// <param name="logicManager">資料庫操作邏輯管理器實例</param>
        /// <param name="identitySelector">唯一識別號屬性選擇器</param>
        public LogicBase(LogicManagerBase<TContext> logicManager, Expression<Func<TEntity, TId>> identitySelector) {
            this.Manager = logicManager;
            this.IdentityPropertyName = (identitySelector.Body as MemberExpression).Member.Name;
        }

        /// <summary>
        /// 取得DbSet
        /// </summary>
        /// <param name="caller">執行方法</param>
        /// <param name="arguments">參數</param>
        /// <returns>DbSet</returns>
        protected virtual IQueryable<TEntity> GetDbSet(MethodInfo caller, Dictionary<string, object> arguments) {
            var prop = Database.GetType().GetProperties().SingleOrDefault(x => x.PropertyType == typeof(DbSet<TEntity>));
            var dbSet = prop.GetValue(Database) as IQueryable<TEntity>;
            return dbSet;
        }

        /// <summary>
        /// 列表
        /// </summary>
        /// <param name="cond">條件</param>
        /// <returns>列表</returns>
        public virtual async Task<IQueryable<TEntity>> ListAsync(Expression<Func<TEntity, bool>> cond = null) {
            if (cond == null) cond = x => true;
            var dbSet = GetDbSet(this.GetType().GetMethod(
                nameof(SearchAsync),
                new Type[] {
                    typeof(string),
                    typeof(Expression<Func<TEntity, object>>[])
                }), new Dictionary<string, object>() {
                    [nameof(cond)] = cond
                });
            return dbSet.Where(cond);
        }

        /// <summary>
        /// 全文搜尋
        /// </summary>
        /// <param name="likePatten">SQL Like模式</param>
        /// <returns>搜尋結果</returns>
        public virtual async Task<IEnumerable<TEntity>> SearchAsync(
            string likePatten) {
            return await SearchAsync(likePatten, typeof(TEntity).GetProperties()
                .Where(x => x.PropertyType == typeof(string) && x.GetCustomAttribute<NotMappedAttribute>() == null)
                .Select(x => x.Name)
                .Select(x => {
                    var p = Expression.Parameter(typeof(TEntity), "x");

                    return Expression.Lambda<Func<TEntity, object>>(
                        Expression.MakeMemberAccess(p, typeof(TEntity).GetMember(x).First()), p
                    );
                }).ToArray());
        }

        /// <summary>
        /// 搜尋
        /// </summary>
        /// <param name="likePatten">SQL Like模式</param>
        /// <param name="properties">搜尋屬性名稱</param>
        /// <returns>搜尋結果</returns>
        public virtual async Task<IEnumerable<TEntity>> SearchAsync(
            string likePatten,
            params string[] properties) {
            return await SearchAsync(likePatten, properties.Select(x => {
                var p = Expression.Parameter(typeof(TEntity), "x");

                return Expression.Lambda<Func<TEntity, object>>(
                    Expression.MakeMemberAccess(p, typeof(TEntity).GetMember(x).First()), p
                );
            }).ToArray());
        }

        /// <summary>
        /// 搜尋
        /// </summary>
        /// <param name="likePatten">SQL Like模式</param>
        /// <param name="propertySelectors">比對屬性選擇器</param>
        /// <returns>搜尋結果</returns>
        public virtual async Task<IEnumerable<TEntity>> SearchAsync(
            string likePatten,
            params Expression<Func<TEntity, object>>[] propertySelectors) {
            var dbSet = GetDbSet(this.GetType().GetMethod(
                nameof(SearchAsync),
                new Type[] {
                    typeof(string),
                    typeof(Expression<Func<TEntity, object>>[])
                }), new Dictionary<string, object>() {
                    [nameof(likePatten)] = likePatten,
                    [nameof(propertySelectors)] = propertySelectors
                });

            var p = Expression.Parameter(typeof(TEntity), "x");

            var likeMethod = typeof(DbFunctionsExtensions).GetMethod("Like", new Type[] { typeof(DbFunctions), typeof(string), typeof(string) });

            List<Expression> equalExpList = new List<Expression>();

            // OR串接
            Expression AllOr(IEnumerable<Expression> exps) {
                Expression result = exps.First();
                foreach (var exp in exps.Skip(1)) {
                    result = Expression.OrElse(result, exp);
                }
                return result;
            }

            propertySelectors //轉換MemberEXpression為Like呼叫
                .Select(x => {
                    equalExpList.Add(
                        Expression.Call(
                            likeMethod,
                            new Expression[]{
                                Expression.Constant(null, typeof(DbFunctions)),
                                Expression.Property(
                                    p,
                                    (x.Body as MemberExpression).Member.Name
                                ),
                                Expression.Constant(likePatten)
                            }
                        )
                    );
                    return x;
                }).ToArray();

            return (IEnumerable<TEntity>)Enumerable.Where(dbSet, Expression.Lambda<Func<TEntity, bool>>(
                AllOr(equalExpList), p
            ).Compile());
        }

        /// <summary>
        /// 透過唯一識別號取得指定物件實例
        /// </summary>
        /// <param name="id">唯一識別號</param>
        /// <param name="parameters">參數</param>
        /// <returns>物件實例</returns>
        public async Task<TEntity> GetAsync(object id, object[] parameters = null) {
            return await GetAsync((TId)id, parameters);
        }

        /// <summary>
        /// 透過唯一識別號取得指定物件實例
        /// </summary>
        /// <param name="id">唯一識別號</param>
        /// <param name="parameters">參數</param>
        /// <returns>物件實例</returns>
        public virtual async Task<TEntity> GetAsync(TId id, object[] parameters = null) {
            var dbSet = GetDbSet(this.GetType().GetMethod(
                nameof(GetAsync),
                new Type[] {
                    typeof(TId),
                    typeof(object[])
                }), new Dictionary<string, object>() {
                    [nameof(id)] = id
                });

            var instance = dbSet.SingleOrDefault($"{IdentityPropertyName} == @0", id);

            if (instance == null) {
                throw new NotFoundException();
            }

            await AfterGet(instance, parameters);
            return instance;
        }

        /// <summary>
        /// 加入新的物件實例
        /// </summary>
        /// <param name="entity">物件實例</param>
        /// <param name="parameters">參數</param>
        /// <returns>加入後的物件</returns>
        public async Task<TEntity> CreateAsync(object entity, params object[] parameters) {
            return await CreateAsync((TEntity)entity, parameters);
        }

        /// <summary>
        /// 加入新的物件實例
        /// </summary>
        /// <param name="entity">物件實例</param>
        /// <param name="parameters">參數</param>
        /// <returns>加入後的物件</returns>
        public virtual async Task<TEntity> CreateAsync(TEntity entity, params object[] parameters) {
            if (CreateIgnoreIdentity) {
                (await Manager.GetEntityIdentityProperty(entity)).SetValue(entity, default(TId));
            }

            Database.Add(entity);

            await BeforeCreate(entity, parameters);
            await Database.SaveChangesAsync();
            await AfterCreate(entity, parameters);

            var type = typeof(TEntity);
            TId id = (TId)type.GetProperty(IdentityPropertyName).GetValue(entity);
            return await GetAsync(id, parameters);
        }

        /// <summary>
        /// 更新指定的物件實例
        /// </summary>
        /// <param name="entity">物件實例</param>
        /// <param name="parameters">參數</param>
        /// <returns>更新後的物件實例</returns>
        public async Task<TEntity> UpdateAsync(object entity, params object[] parameters) {
            return await UpdateAsync((TEntity)entity, parameters);
        }

        /// <summary>
        /// 更新指定的物件實例
        /// </summary>
        /// <param name="entity">物件實例</param>
        /// <param name="parameters">參數</param>
        /// <returns>更新後的物件實例</returns>
        public virtual async Task<TEntity> UpdateAsync(TEntity entity, params object[] parameters) {
            var type = typeof(TEntity);
            TId id = (TId)type.GetProperty(IdentityPropertyName).GetValue(entity);
            var instance = await GetAsync(id, parameters);

            if (instance == null) {
                throw new NotFoundException();
            }

            var mappingProps = type.GetProperties().Where(x => {
                return
                    x.GetCustomAttribute<NotMappedAttribute>() == null &&
                    x.PropertyType.Namespace == "System";
            });

            foreach (var mappingProp in mappingProps) {
                mappingProp.SetValue(instance, mappingProp.GetValue(entity));
            }

            await BeforeUpdate(instance, parameters);
            await Database.SaveChangesAsync();
            await AfterUpdate(instance, parameters);
            return instance;
        }


        /// <summary>
        /// 刪除指定的物件
        /// </summary>
        /// <param name="id">唯一識別號</param>
        /// <param name="parameters">參數</param>
        public async Task DeleteAsync(object id, params object[] parameters) {
            await DeleteAsync((TId)id, parameters);
        }

        /// <summary>
        /// 刪除指定的物件
        /// </summary>
        /// <param name="id">物件唯一識別號</param>
        /// <param name="parameters">參數</param>
        public virtual async Task DeleteAsync(TId id, params object[] parameters) {
            var instance = await GetAsync(id, parameters);

            if (instance == null) {
                throw new NotFoundException();
            }

            Database.RemoveCascade(instance);

            await BeforeDelete(instance, parameters);
            await Database.SaveChangesAsync();
            await AfterDelete(instance, parameters);
        }

        #region Hook
        public virtual async Task BeforeCreate(TEntity entity, params object[] parameters) { }
        public virtual async Task BeforeUpdate(TEntity entity, params object[] parameters) { }
        public virtual async Task BeforeDelete(TEntity entity, params object[] parameters) { }


        public virtual async Task AfterGet(TEntity entity, params object[] parameters) { }
        public virtual async Task AfterCreate(TEntity entity, params object[] parameters) { }
        public virtual async Task AfterUpdate(TEntity entity, params object[] parameters) { }
        public virtual async Task AfterDelete(TEntity entity, params object[] parameters) { }
        #endregion
    }
}
