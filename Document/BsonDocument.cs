﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace LiteDB
{
    public class BsonDocument : BsonValue, IDictionary<string, BsonValue>
    {
        public BsonDocument()
            : base(new Dictionary<string, BsonValue>())
        {
        }

        public BsonDocument(Dictionary<string, BsonValue> dict)
            : base(dict)
        {
            if (dict == null) throw new ArgumentNullException("dict");
        }

        public new Dictionary<string, BsonValue> RawValue
        {
            get
            {
                return (Dictionary<string, BsonValue>)base.RawValue;
            }
        }

        /// <summary>
        /// Get/Set a field for document. Fields are case sensitive
        /// </summary>
        public BsonValue this[string name]
        {
            get
            {
                return this.RawValue.GetOrDefault(name, BsonValue.Null);
            }
            set
            {
                if (!IsValidFieldName(name)) throw new ArgumentException(string.Format("Field '{0}' has an invalid name.", name));

                this.RawValue[name] = value ?? BsonValue.Null;
            }
        }

        /// <summary>
        /// Test if field name is a valid string: only [\w$]+[\w-]*
        /// </summary>
        internal static bool IsValidFieldName(string field)
        {
            if (string.IsNullOrEmpty(field)) return false;

            // do not use regex because is too slow
            for (var i = 0; i < field.Length; i++)
            {
                var c = field[i];

                if (char.IsLetterOrDigit(c) || c == '_' || (c == '$' && i == 0))
                {
                    continue;
                }
                else if (c == '-' && i > 0)
                {
                    continue;
                }
                else
                {
                    return false;
                }
            }

            return true;
        }

        #region Get/GetValues methods

        /// <summary>
        /// Get BsonValues from path or expression. Support JSONPath like
        /// </summary>
        public IEnumerable<BsonValue> GetValues(string expr, bool addNullIfEmpty = false)
        {
            if (string.IsNullOrEmpty(expr)) throw new ArgumentNullException("expr");

            return new LiteExpression(expr)
                .Execute(this, addNullIfEmpty);
        }

        /// <summary>
        /// Get BsonValue from string field
        /// </summary>
        public BsonValue Get(string field)
        {
            if (string.IsNullOrEmpty(field)) throw new ArgumentNullException("field");

            return new LiteExpression("$." + field)
                .Execute(this, true)
                .First();
        }

        #endregion

        #region CompareTo / ToString

        public override int CompareTo(BsonValue other)
        {
            // if types are different, returns sort type order
            if (other.Type != BsonType.Document) return this.Type.CompareTo(other.Type);

            var thisKeys = this.Keys.ToArray();
            var thisLength = thisKeys.Length;

            var otherDoc = other.AsDocument;
            var otherKeys = otherDoc.Keys.ToArray();
            var otherLength = otherKeys.Length;

            var result = 0;
            var i = 0;
            var stop = Math.Min(thisLength, otherLength);

            for (; 0 == result && i < stop; i++)
                result = this[thisKeys[i]].CompareTo(otherDoc[thisKeys[i]]);

            // are different
            if (result != 0) return result;

            // test keys length to check which is bigger
            if (i == thisLength) return i == otherLength ? 0 : -1;
            return 1;
        }

        public override string ToString()
        {
            return JsonSerializer.Serialize(this, false, true);
        }

        #endregion

        #region Update

        /// <summary>
        /// Update document fields using update rules
        /// </summary>
        public void Update(params Update[] updates)
        {
            if (updates == null) throw new ArgumentNullException(nameof(updates));

            foreach(var update in updates)
            {
                var path = update.Path.StartsWith("$") ? update.Path : "$." + update.Path;
                var parent = path.Substring(0, path.LastIndexOf('.'));
                var name = path.Substring(path.LastIndexOf('.') + 1);

                foreach(var item in this.GetValues(parent, false).Where(x => x.IsDocument))
                {
                    var value = update.FieldValue ?? update.Expression.Execute(this, true).First();

                    item.AsDocument[name] = value;
                }
            }
        }

        #endregion

        #region IDictionary

        public ICollection<string> Keys
        {
            get
            {
                return this.RawValue.Keys
                    .OrderBy(x => x == "_id" ? 1 : 2)
                    .ToList();
            }
        }

        public ICollection<BsonValue> Values
        {
            get
            {
                return this.RawValue.Values;
            }
        }

        public int Count
        {
            get
            {
                return this.RawValue.Count;
            }
        }

        public bool IsReadOnly
        {
            get
            {
                return false;
            }
        }

        public bool ContainsKey(string key)
        {
            return this.RawValue.ContainsKey(key);
        }

        public void Add(string key, BsonValue value)
        {
            this[key] = value;
        }

        public bool Remove(string key)
        {
            return this.RawValue.Remove(key);
        }

        public bool TryGetValue(string key, out BsonValue value)
        {
            return this.RawValue.TryGetValue(key, out value);
        }

        public void Add(KeyValuePair<string, BsonValue> item)
        {
            this[item.Key] = item.Value;
        }

        public void Clear()
        {
            this.RawValue.Clear();
        }

        public bool Contains(KeyValuePair<string, BsonValue> item)
        {
            return this.RawValue.Contains(item);
        }

        public void CopyTo(KeyValuePair<string, BsonValue>[] array, int arrayIndex)
        {
            ((ICollection<KeyValuePair<string, BsonValue>>)this.RawValue).CopyTo(array, arrayIndex);
        }

        public void CopyTo(BsonDocument doc)
        {
            var myDict = this.RawValue;
            var otherDict = doc.RawValue;

            foreach(var key in myDict.Keys)
            {
                otherDict[key] = myDict[key];
            }
        }

        public bool Remove(KeyValuePair<string, BsonValue> item)
        {
            return this.RawValue.Remove(item.Key);
        }

        public IEnumerator<KeyValuePair<string, BsonValue>> GetEnumerator()
        {
            return this.RawValue.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.RawValue.GetEnumerator();
        }

        #endregion
    }
}
