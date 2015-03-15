﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace LiteDB
{
    internal class QueryEquals : Query
    {
        private BsonValue _value;

        public QueryEquals(string field, BsonValue value)
            : base(field)
        {
            _value = value;
        }

        internal override IEnumerable<IndexNode> Execute(IndexService indexer, CollectionIndex index)
        {
            var value = _value.Normalize(index.Options);
            var node = indexer.Find(index, value, false, Query.Ascending);

            if (node == null) yield break;

            yield return node;

            if (index.Options.Unique == false)
            {
                // navigate using next[0] do next node - if equals, returns
                while (!node.Next[0].IsEmpty && ((node = indexer.GetNode(node.Next[0])).Value.CompareTo(value) == 0))
                {
                    if (node.IsHeadTail) yield break;

                    yield return node;
                }
            }
        }
    }
}
