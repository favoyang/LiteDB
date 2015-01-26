﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace LiteDB.Shell.Commands
{
    public class CollectionCount : BaseCollection, IShellCommand
    {
        public bool IsCommand(StringScanner s)
        {
            return this.IsCollectionCommand(s, "count");
        }

        public void Execute(LiteEngine db, StringScanner s, Display display)
        {
            var col = this.ReadCollection(db, s);
            var query = this.ReadQuery(s);

            display.WriteBson(col.Count(query));
        }
    }
}
