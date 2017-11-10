﻿using System;

namespace LiteDB
{
    /// <summary>
    /// Class to call method for convert BsonDocument to/from byte[] - based on http://bsonspec.org/spec.html
    /// </summary>
    public class BsonSerializer
    {
        private bool _utcDate;

        public BsonSerializer(bool utcDate)
        {
            _utcDate = utcDate;
        }

        public byte[] Serialize(BsonDocument doc)
        {
            if (doc == null) throw new ArgumentNullException("doc");

            var writer = new BsonWriter();

            return writer.Serialize(doc);
        }

        public BsonDocument Deserialize(byte[] bson)
        {
            if (bson == null || bson.Length == 0) throw new ArgumentNullException("bson");

            var reader = new BsonReader(_utcDate);

            return reader.Deserialize(bson);
        }
    }
}