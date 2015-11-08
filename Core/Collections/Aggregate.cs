﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace LiteDB
{
    public partial class LiteCollection<T>
    {
        #region Count/Exits

        /// <summary>
        /// Get document count using property on collection.
        /// </summary>
        public int Count()
        {
            this.Database.Transaction.AvoidDirtyRead();

            var col = this.GetCollectionPage(false);

            if (col == null) return 0;

            return Convert.ToInt32(col.DocumentCount);
        }

        /// <summary>
        /// Count documnets with a query. This method does not deserialize any document. Needs indexes on query expression
        /// </summary>
        public int Count(Query query)
        {
            if (query == null) throw new ArgumentNullException("query");

            this.Database.Transaction.AvoidDirtyRead();

            var nodes = query.Run<T>(this);

            return nodes.Count();
        }

        /// <summary>
        /// Count documnets with a query. This method does not deserialize any document. Needs indexes on query expression
        /// </summary>
        public int Count(Expression<Func<T, bool>> predicate)
        {
            return this.Count(_visitor.Visit(predicate));
        }

        /// <summary>
        /// Returns true if query returns any document. This method does not deserialize any document. Needs indexes on query expression
        /// </summary>
        public bool Exists(Query query)
        {
            if (query == null) throw new ArgumentNullException("query");

            this.Database.Transaction.AvoidDirtyRead();

            var nodes = query.Run<T>(this);

            return nodes.FirstOrDefault() != null;
        }

        /// <summary>
        /// Returns true if query returns any document. This method does not deserialize any document. Needs indexes on query expression
        /// </summary>
        public bool Exists(Expression<Func<T, bool>> predicate)
        {
            return this.Exists(_visitor.Visit(predicate));
        }

        #endregion

        #region Min/Max

        /// <summary>
        /// Returns the first/min value from a index field
        /// </summary>
        public BsonValue Min(string field)
        {
            if (string.IsNullOrEmpty(field)) throw new ArgumentNullException("field");

            this.Database.Transaction.AvoidDirtyRead();

            var index = this.GetOrCreateIndex(field);

            if (index == null) return BsonValue.MinValue;

            var head = this.Database.Indexer.GetNode(index.HeadNode);
            var next = this.Database.Indexer.GetNode(head.Next[0]);

            if (next.IsHeadTail(index)) return BsonValue.MinValue;

            return next.Key;
        }

        /// <summary>
        /// Returns the first/min _id field
        /// </summary>
        public BsonValue Min()
        {
            return this.Min("_id");
        }

        /// <summary>
        /// Returns the first/min field using a linq expression
        /// </summary>
        public BsonValue Min<K>(Expression<Func<T, K>> property)
        {
            var field = _visitor.GetBsonField(property);

            return this.Min(field);
        }

        /// <summary>
        /// Returns the last/max value from a index field
        /// </summary>
        public BsonValue Max(string field)
        {
            if (string.IsNullOrEmpty(field)) throw new ArgumentNullException("field");

            this.Database.Transaction.AvoidDirtyRead();

            var index = this.GetOrCreateIndex(field);

            if (index == null) return BsonValue.MaxValue;

            var tail = this.Database.Indexer.GetNode(index.TailNode);
            var prev = this.Database.Indexer.GetNode(tail.Prev[0]);

            if (prev.IsHeadTail(index)) return BsonValue.MaxValue;

            return prev.Key;
        }

        /// <summary>
        /// Returns the last/max _id field
        /// </summary>
        public BsonValue Max()
        {
            return this.Max("_id");
        }

        /// <summary>
        /// Returns the last/max field using a linq expression
        /// </summary>
        public BsonValue Max<K>(Expression<Func<T, K>> property)
        {
            var field = _visitor.GetBsonField(property);

            return this.Max(field);
        }

        #endregion
    }
}
