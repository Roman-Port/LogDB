using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace LogDB.Entities
{
    public class MinorPageObject
    {
        //Handles
        public MinorPage parentPage;
        public Stream s { get { return parentPage.s; } }

        //Offsets
        /// <summary>
        /// The position in the file of the beginning of this item.
        /// </summary>
        public long offsetBeginning;
        /// <summary>
        /// The position of the file of the end of this item.
        /// </summary>
        public long offsetEnd { get { return offsetBeginning + 18; } }
        /// <summary>
        /// The absolute position in the file where the contentStarts
        /// </summary>
        public ulong offsetContentAbsolute { get { return (ulong)parentPage.offsetContentStart + contentOffset; } }

        //File props
        public DateTime date;
        public ulong contentOffset;
        public bool[] flags;

        //Reading
        /// <summary>
        /// Reads the page at the current cursor position.
        /// </summary>
        /// <param name="parent"></param>
        public MinorPageObject(MinorPage parent)
        {
            //Set parent
            parentPage = parent;

            //Read props
            date = new DateTime((long)s.ReadULong());
            contentOffset = s.ReadULong();
            flags = s.ReadBitFlags(2);
        }
    }
}
