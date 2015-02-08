﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace LiteDB
{
    internal class CollectionService
    {
        private PageService _pager;
        private IndexService _indexer;

        public CollectionService(PageService pager, IndexService indexer)
        {
            _pager = pager;
            _indexer = indexer;
        }

        /// <summary>
        /// Get a exist collection. Returns null if not exists
        /// </summary>
        public CollectionPage Get(string name)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException("name");

            var pages = _pager.GetSeqPages<CollectionPage>(1); // PageID 1 = Master Collection

            var col = pages.FirstOrDefault(x => x.CollectionName.Equals(name, StringComparison.InvariantCultureIgnoreCase));

            return col;
        }

        /// <summary>
        /// Add a new collection. Check if name the not exists
        /// </summary>
        public CollectionPage Add(string name)
        {
            if(string.IsNullOrEmpty(name)) throw new ArgumentNullException("name");
            if(!Regex.IsMatch(name, CollectionPage.NAME_PATTERN)) throw new LiteException("Invalid collection name. Use only letters, numbers and _");

            var pages = _pager.GetSeqPages<CollectionPage>(1); // PageID 1 = Master Collection

            if (pages.FirstOrDefault(x => x.CollectionName.Equals(name, StringComparison.InvariantCultureIgnoreCase)) != null)
            {
                throw new ArgumentException("Collection name already exists (names are case unsensitive)");
            }

            if (pages.Count() >= CollectionPage.MAX_COLLECTIONS)
                throw new LiteException("This database exceded max collections: " + CollectionPage.MAX_COLLECTIONS);

            var col = _pager.NewPage<CollectionPage>(pages.Last());

            col.CollectionName = name;
            col.IsDirty = true;

            // create PK index
            var pk = _indexer.CreateIndex(col.PK);

            pk.Field = "_id";
            pk.Unique = true;

            return col;
        }

        /// <summary>
        /// Get all collections
        /// </summary>
        public IEnumerable<CollectionPage> GetAll()
        {
            return _pager.GetSeqPages<CollectionPage>(1); // PageID 1 = Master Collection
        }

        /// <summary>
        /// Drop a collection - remove all data pages + indexes pages
        /// </summary>
        public void Drop(CollectionPage col)
        {
            // add all pages to delete
            var pages = new HashSet<uint>();

            // search for all data page and index page
            for (byte i = 0; i < col.Indexes.Length; i++)
            {
                var index = col.Indexes[i];

                if (!index.IsEmpty)
                {
                    // get all nodes from index
                    var nodes = _indexer.FindAll(index);

                    foreach (var node in nodes)
                    {
                        if(i == 0)
                        {
                            // if PK index, add data pages too
                            pages.Add(node.DataBlock.PageID);
                        }

                        // add index page to delete list page
                        pages.Add(node.Position.PageID);
                    }
                }
            }

            // and now, lets delete all this pages
            foreach (var pageID in pages)
            {
                _pager.DeletePage(pageID);
            }

            // ajust collection page pointers
            if (col.PrevPageID != uint.MaxValue)
            {
                var prev = _pager.GetPage<BasePage>(col.PrevPageID);
                prev.NextPageID = col.NextPageID;
                prev.IsDirty = true;
            }
            if (col.NextPageID != uint.MaxValue)
            {
                var next = _pager.GetPage<BasePage>(col.NextPageID);
                next.PrevPageID = col.PrevPageID;
                next.IsDirty = true;
            }

            _pager.DeletePage(col.PageID, false);
        }
    }
}
