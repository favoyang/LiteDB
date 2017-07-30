﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace LiteDB
{
    public partial class LiteEngine
    {
        /// <summary>
        /// Implements insert documents in a collection - returns _id value
        /// </summary>
        public BsonValue Insert(string collection, BsonDocument doc, BsonType autoId = BsonType.ObjectId)
        {
            if (doc == null) throw new ArgumentNullException("doc");

            this.Insert(collection, new BsonDocument[] { doc }, autoId);

            return doc["_id"];
        }

        /// <summary>
        /// Implements insert documents in a collection - use a buffer to commit transaction in each buffer count
        /// </summary>
        public int Insert(string collection, IEnumerable<BsonDocument> docs, BsonType autoId = BsonType.ObjectId)
        {
            if (collection.IsNullOrWhiteSpace()) throw new ArgumentNullException("collection");
            if (docs == null) throw new ArgumentNullException("docs");

            return this.Transaction<int>(collection, true, (col) =>
            {
                var count = 0;

                foreach (var doc in docs)
                {
                    this.InsertDocument(col, doc, autoId);

                    _trans.CheckPoint();

                    count++;
                }

                return count;
            });
        }

        /// <summary>
        /// Bulk documents to a collection - use data chunks for most efficient insert
        /// </summary>
        public int InsertBulk(string collection, IEnumerable<BsonDocument> docs, int batchSize = 5000, BsonType autoId = BsonType.ObjectId)
        {
            if (collection.IsNullOrWhiteSpace()) throw new ArgumentNullException("collection");
            if (docs == null) throw new ArgumentNullException("docs");
            if (batchSize < 100 || batchSize > 100000) throw new ArgumentException("batchSize must be a value between 100 and 100000");

            var count = 0;

            foreach(var batch in docs.Batch(batchSize))
            {
                count += this.Insert(collection, batch, autoId);
            }

            return count;
        }

        /// <summary>
        /// Internal implementation of insert a document
        /// </summary>
        private void InsertDocument(CollectionPage col, BsonDocument doc, BsonType autoId)
        {
            BsonValue id;

            // increase collection sequence _id
            col.Sequence++;

            _pager.SetDirty(col);

            // if no _id, add one
            if (!doc.RawValue.TryGetValue("_id", out id))
            {
                doc["_id"] = id =
                    autoId == BsonType.ObjectId ? new BsonValue(ObjectId.NewObjectId()) :
                    autoId == BsonType.Guid ? new BsonValue(Guid.NewGuid()) :
                    autoId == BsonType.Int32 ? new BsonValue((Int32)col.Sequence) :
                    autoId == BsonType.Int64 ? new BsonValue((Int64)col.Sequence) : 
                    autoId == BsonType.String ? new BsonValue(col.Sequence.ToString()) : BsonValue.Null;
            }

            // test if _id is a valid type
            if (id.IsNull || id.IsMinValue || id.IsMaxValue)
            {
                throw LiteException.InvalidDataType("_id", id);
            }

            _log.Write(Logger.COMMAND, "insert document on '{0}' :: _id = {1}", col.CollectionName, id);

            // serialize object
            var bytes = BsonSerializer.Serialize(doc);

            // storage in data pages - returns dataBlock address
            var dataBlock = _data.Insert(col, bytes);

            // store id in a PK index [0 array]
            var pk = _indexer.AddNode(col.PK, id, null);

            // do link between index <-> data block
            pk.DataBlock = dataBlock.Position;

            // for each index, insert new IndexNode
            foreach (var index in col.GetIndexes(false))
            {
                // for each index, get all keys (support now multi-key) - gets distinct values only
                // if index are unique, get single key only
                var keys = doc.GetValues(index.Field, index.Unique);

                // do a loop with all keys (multi-key supported)
                foreach(var key in keys)
                {
                    // insert node
                    var node = _indexer.AddNode(index, key, pk);

                    // link my index node to data block address
                    node.DataBlock = dataBlock.Position;
                }
            }
        }
    }
}