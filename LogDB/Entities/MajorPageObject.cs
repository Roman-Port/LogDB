using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace LogDB.Entities
{
    /// <summary>
    /// Entry in the major page.
    /// </summary>
    public class MajorPageObject
    {
        //Handles
        public MajorPage parentPage;
        public Stream s { get { return parentPage.s; } }

        //Offsets
        /// <summary>
        /// The position in the file of the beginning of this item.
        /// </summary>
        public long offsetBeginning;
        /// <summary>
        /// The position of the file of the end of this item.
        /// </summary>
        public long offsetEnd { get { return offsetBeginning + 26; } }
        /// <summary>
        /// The absolute position in the file where the contentStarts
        /// </summary>
        public ulong offsetContentAbsolute { get { return (ulong)parentPage.offsetContentStart + contentOffset; } }

        //File props
        public DateTime start;
        public DateTime end;
        public ulong contentOffset;
        public bool[] flags;

        //Reading
        /// <summary>
        /// Reads the page at the current cursor position.
        /// </summary>
        /// <param name="parent"></param>
        public MajorPageObject(MajorPage parent)
        {
            //Set parent
            parentPage = parent;

            //Read props
            start = new DateTime((long)s.ReadULong());
            end = new DateTime((long)s.ReadULong());
            contentOffset = s.ReadULong();
            flags = s.ReadBitFlags(2);
        }

        /// <summary>
        /// Jump to the page and read it in a thread-safe manner.
        /// </summary>
        /// <returns></returns>
        public MinorPage SafeReadPage()
        {
            MinorPage output;
            lock(s)
            {
                //Jump to
                s.Position = (long)offsetContentAbsolute;

                //Read
                output = new MinorPage(this);
            }
            return output;
        }

        /// <summary>
        /// Updates header and parents in a thread safe manner.
        /// </summary>
        public void SafeUpdate()
        {
            lock(s)
            {
                //Jump to me
                s.Position = offsetBeginning;

                //Write props
                s.WriteUInt64((ulong)start.Ticks);
                s.WriteUInt64((ulong)end.Ticks);
                s.WriteUInt64(contentOffset);
                s.WriteBitFlags(flags);

                //Update parent
                parentPage.SafeUpdate();
            }
        }

        /// <summary>
        /// Gets the absolute size of the entire referenced page.
        /// </summary>
        /// <returns></returns>
        public ulong GetAbsoluteSizeOfPage()
        {
            ulong s = MinorPage.SafeGetAbsoluteSize(parentPage.parentFile, (long)offsetContentAbsolute);
            Console.WriteLine(s);
            return s;
        }
    }
}
