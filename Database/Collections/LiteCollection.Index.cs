﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace LiteDB
{
    public partial class LiteCollection<T>
    {
        /// <summary>
        /// Create a new permanent index in all documents inside this collections if index not exists already. Returns true if index was created or false if already exits
        /// </summary>
        /// <param name="field">Document field name (case sensitive)</param>
        /// <param name="unique">Create a unique values index?</param>
        public virtual bool EnsureIndex(string field, bool unique = false)
        {
            if (string.IsNullOrEmpty(field)) throw new ArgumentNullException("field");

            if (!Regex.IsMatch(field, CollectionIndex.FIELD_PATTERN) || field.Length > CollectionIndex.FIELD_MAX_LENGTH) throw new LiteException("Invalid field format.");

            if (typeof(T) != typeof(BsonDocument) && typeof(T).GetProperty(field) == null)
            {
                throw new LiteException(string.Format("Property name {0} not found in {1} class", field, typeof(T).Name));
            }

            // do not create collection at this point
            var col = this.GetCollectionPage(false);

            // check if index already exists (collection must exists)
            if (col != null && col.Indexes.FirstOrDefault(x => x.Field.Equals(field, StringComparison.InvariantCultureIgnoreCase)) != null)
            {
                return false;
            };

            // start transaction
            this.Database.Transaction.Begin();

            try
            {
                // if not collection yet, create a new now
                if (col == null)
                {
                    col = this.Database.Collections.Add(this.Name);
                    _pageID = col.PageID;
                }

                // get index slot
                var slot = col.GetFreeIndex();

                // create index head
                var index = this.Database.Indexer.CreateIndex(col.Indexes[slot]);

                index.Field = field;
                index.Unique = unique;

                // read all objects (read from PK index)
                foreach (var node in this.Database.Indexer.FindAll(col.PK))
                {
                    var dataBlock = this.Database.Data.Read(node.DataBlock, true);

                    // read object
                    var doc = BsonSerializer.Deserialize<T>(dataBlock.Key, dataBlock.Buffer);

                    // adding index
                    var key = BsonSerializer.GetFieldValue(doc, field);

                    var newNode = this.Database.Indexer.AddNode(index, key);

                    // adding this new index Node to indexRef
                    dataBlock.IndexRef[slot] = newNode.Position;

                    // link index node to datablock
                    newNode.DataBlock = dataBlock.Position;

                    // mark datablock page as dirty
                    dataBlock.Page.IsDirty = true;
                }

                this.Database.Transaction.Commit();
            }
            catch
            {
                this.Database.Transaction.Rollback();
                throw;
            }

            return true;
        }

        /// <summary>
        /// Create a new permanent index in all documents inside this collections if index not exists already.
        /// </summary>
        /// <param name="property">Property linq expression</param>
        /// <param name="unique">Create a unique values index?</param>
        public virtual bool EnsureIndex<K>(Expression<Func<T, K>> property, bool unique = false)
        {
            var p = QueryVisitor.GetProperty<T, K>(property);

            return this.EnsureIndex(p.Name, unique);
        }

        /// <summary>
        /// Returns all indexes in this collections
        /// </summary>
        public IEnumerable<BsonObject> GetIndexes()
        {
            this.Database.Transaction.AvoidDirtyRead();

            var col = this.GetCollectionPage(false);

            if (col == null) return new List<BsonObject>();

            return col.Indexes.Where(x => !x.IsEmpty).Select(x => new BsonObject().Add("field", x.Field).Add("unique", x.Unique));
        }

        /// <summary>
        /// Drop index and release slot for another index
        /// </summary>
        public bool DropIndex(string field)
        {
            // start transaction
            this.Database.Transaction.Begin();

            try
            {
                var col = this.GetCollectionPage(false);

                // if collection not exists, no drop
                if (col == null)
                {
                    this.Database.Transaction.Abort();
                    return false;
                }

                // search for index reference - do not delelte "_id" index
                var index = col.Indexes.FirstOrDefault(x => x.Field.Equals(field, StringComparison.InvariantCultureIgnoreCase));

                if (index == null || field.Equals("_id", StringComparison.InvariantCultureIgnoreCase))
                {
                    this.Database.Transaction.Abort();
                    return false;
                }

                // delete all data pages + indexes pages
                this.Database.Indexer.DropIndex(index);

                // clear index reference
                index.Clear();

                // save collection page
                col.IsDirty = true;

                this.Database.Transaction.Commit();

                return true;
            }
            catch
            {
                this.Database.Transaction.Rollback();
                throw;
            }
        }
    }
}
