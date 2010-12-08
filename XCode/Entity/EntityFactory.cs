﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using System.Web;
using System.IO;

namespace XCode
{
    /// <summary>
    /// 实体工厂
    /// </summary>
    public static class EntityFactory
    {
        #region 创建实体
        /// <summary>
        /// 创建指定类型的实例
        /// </summary>
        /// <param name="typeName"></param>
        /// <returns></returns>
        public static IEntity Create(String typeName)
        {
            Type type = GetType(typeName);
            if (type == null) return null;

            return Create(type);
        }

        /// <summary>
        /// 创建指定类型的实例
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static IEntity Create(Type type)
        {
            if (type == null) return null;

            return Activator.CreateInstance(type) as IEntity;
        }
        #endregion

        #region 创建实体操作接口
        /// <summary>
        /// 创建实体操作接口
        /// </summary>
        /// <remarks>因为只用来做实体操作，所以只需要一个实例即可</remarks>
        /// <param name="typeName"></param>
        /// <returns></returns>
        public static IEntityOperate CreateOperate(String typeName)
        {
            Type type = GetType(typeName);
            if (type == null)
            {
                WriteLog("创建实体操作接口时无法找到{0}类！", typeName);
                return null;
            }

            return CreateOperate(type);
        }

        private static Dictionary<Type, IEntityOperate> op_cache = new Dictionary<Type, IEntityOperate>();
        /// <summary>
        /// 创建实体操作接口
        /// </summary>
        /// <remarks>因为只用来做实体操作，所以只需要一个实例即可</remarks>
        /// <param name="type"></param>
        /// <returns></returns>
        public static IEntityOperate CreateOperate(Type type)
        {
            if (type == null) return null;

            if (op_cache.ContainsKey(type)) return op_cache[type];
            lock (op_cache)
            {
                if (op_cache.ContainsKey(type)) return op_cache[type];

                EntityBase entity = Create(type) as EntityBase;
                if (entity == null) throw new Exception(String.Format("无法创建{0}的实体操作接口！", type));

                if (op_cache.ContainsKey(type))
                    op_cache[type] = entity;
                else
                    op_cache.Add(type, entity);

                return entity;
            }
        }

        /// <summary>
        /// 使用指定的实体对象创建实体操作接口，主要用于Entity内部调用，避免反射带来的损耗
        /// </summary>
        /// <param name="type"></param>
        /// <param name="entity"></param>
        /// <returns></returns>
        internal static IEntityOperate CreateOperate(Type type, IEntityOperate entity)
        {
            if (entity == null) return CreateOperate(type);

            //if (op_cache.ContainsKey(type)) return op_cache[type];
            lock (op_cache)
            {
                //if (op_cache.ContainsKey(type)) return op_cache[type];

                if (op_cache.ContainsKey(type))
                    op_cache[type] = entity;
                else
                    op_cache.Add(type, entity);

                return entity;
            }
        }
        #endregion

        #region 加载插件
        private static List<Type> _AllEntities;
        /// <summary>所有实体</summary>
        public static List<Type> AllEntities
        {
            get
            {
                if (_AllEntities != null) return _AllEntities;
                lock (typeof(EntityFactory))
                {
                    if (_AllEntities != null) return _AllEntities;

                    _AllEntities = LoadEntities();

                    return _AllEntities;
                }
            }
        }

        /// <summary>
        /// 列出所有实体类
        /// </summary>
        /// <returns></returns>
        public static List<Type> LoadEntities()
        {
            Assembly curAsm = Assembly.GetExecutingAssembly();
            String path = AppDomain.CurrentDomain.BaseDirectory;
            path = Path.GetDirectoryName(path);

            if (!String.IsNullOrEmpty(HttpRuntime.AppDomainAppId))
            {
                path = HttpRuntime.BinDirectory;
            }

            WriteLog("程序集目录：" + path);

            List<Assembly> asms = new List<Assembly>(AppDomain.CurrentDomain.GetAssemblies());
            for (int i = asms.Count - 1; i >= 0; i--)
            {
                Assembly asm = asms[i];

                if (asm.GlobalAssemblyCache ||
                    asm.FullName.StartsWith("Interop.") || asm.FullName.StartsWith("System.") ||
                    asm.FullName.StartsWith("System,") || asm.FullName.StartsWith("Microsoft.") ||
                    asm.FullName.StartsWith("XCode,") || asm.FullName.StartsWith("XLog,"))
                    asms.RemoveAt(i);
            }

            List<Type> types = new List<Type>();
            if (asms != null) WriteLog("共找到程序集：" + asms.Count);
            foreach (Assembly item in asms)
            {
                WriteLog("加载程序集：" + item.FullName);

                try
                {
                    Type[] ts = item.GetTypes();
                    if (ts != null && ts.Length > 0) types.AddRange(ts);
                }
                catch { }
            }
            if (types.Count < 1) return null;

            Type t = typeof(EntityBase);
            //查找插件类型，所有继承自Entity的类
            for (int i = types.Count - 1; i >= 0; i--)
            {
                if (!t.IsAssignableFrom(types[i]) || !IsEntity(types[i])) types.RemoveAt(i);
            }

            if (types.Count < 1)
                return null;
            else
                return types;
        }

        /// <summary>
        /// 是否实体类
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        private static Boolean IsEntity(Type type)
        {
            //为空、不是类、抽象类、泛型类 都不是实体类
            if (type == null || !type.IsClass || type.IsAbstract || type.IsGenericType) return false;

            //没有基类不是实体类
            if (type.BaseType == null) return false;

            //递归判断
            Type t = type;
            while (t != null && t != typeof(Object))
            {
                //基类必须是泛型，递归基类必须是Entity
                if (t.BaseType.IsGenericType && t.BaseType.Name == "Entity`1")
                {
                    Type[] typeArguments = t.BaseType.GetGenericArguments();
                    if (typeArguments != null && typeArguments.Length > 0)
                    {
                        //有泛型参数，并且泛型参数就是自己
                        if (typeArguments[0] == type)
                        {
                            return true;
                        }
                    }
                }
                t = t.BaseType;
            }

            return false;
        }

        private static Type GetType(String typeName)
        {
            Type type = Type.GetType(typeName);
            if (type == null)
            {
                if (!typeName.Contains("."))
                    type = AllEntities.Find(delegate(Type item) { return item.Name == typeName; });
                else
                    type = AllEntities.Find(delegate(Type item) { return item.FullName == typeName; });
            }
            return type;
        }
        #endregion

        #region 调试输出
        private static void WriteLog(String msg)
        {
            if (XCode.DataAccessLayer.Database.Debug) XCode.DataAccessLayer.Database.WriteLog(msg);
        }

        private static void WriteLog(String format, params Object[] args)
        {
            if (XCode.DataAccessLayer.Database.Debug) XCode.DataAccessLayer.Database.WriteLog(format, args);
        }
        #endregion
    }
}
