using LiteDB;
using System;

namespace eCommerce.Server.Helpers
{
    public static class DbHelper
    {
        public static void InsertOrUpdate<T>(LiteDatabase db, string collectionName, T entity)
        {
            var col = db.GetCollection<T>(collectionName);
            col.Upsert(entity);
        }

        public static void DeleteAll<T>(LiteDatabase db, string collectionName)
        {
            var col = db.GetCollection<T>(collectionName);
            col.DeleteAll();
        }
    }
} 