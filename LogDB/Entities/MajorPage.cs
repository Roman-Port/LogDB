using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace LogDB.Entities
{
    public class MajorPage
    {
        //Handles
        public LogDBFile parentFile;
        public Stream s { get { return parentFile.s; } }

        //Offsets
        /// <summary>
        /// The position in the file of the beginning of the page and it's header.
        /// </summary>
        public long offsetBeginning;
        /// <summary>
        /// The position of the file of the beginning of the table, right after the header.
        /// </summary>
        public long offsetTableBeginning { get { return offsetBeginning + headerSizeBytes; } }
        /// <summary>
        /// The offset to the beginning of the content, after the table.
        /// </summary>
        public long offsetContentStart { get { return offsetBeginning + pageSizeBytes; } }
        /// <summary>
        /// The offset to the absolute end of this page, including the content.
        /// </summary>
        public long offsetContentEnd { get { return offsetContentStart + absoluteSize; } }

        //Sizes
        /// <summary>
        /// Size of the header
        /// </summary>
        public long headerSizeBytes = 18;
        /// <summary>
        /// Size of the table
        /// </summary>
        public long tableSizeBytes { get { return (long)parentFile.majorPageSize * 26; } }
        /// <summary>
        /// Size of the page, with the header and table, EXCLUDING THE CONTENT.
        /// </summary>
        public long pageSizeBytes { get { return headerSizeBytes + tableSizeBytes; } }
        /// <summary>
        /// Size of the page, with the header, table, and content.
        /// </summary>
        public long absoluteSize { get { return pageSizeBytes+(long)contentSize; } }

        //File props
        public ushort fileVersion;
        public ulong contentSize;
        public ushort elementsUsed;
        public bool[] flags;

        //Misc
        /// <summary>
        /// Number of remaining elements in the table that we can fill.
        /// </summary>
        public ushort remainingElementSpace { get { return (ushort)(parentFile.majorPageSize - elementsUsed); } }

        //Table
        public MajorPageObject[] entries;

        //Creation
        /// <summary>
        /// Creates the MajorPage object from the file. Will read from the current cursor position.
        /// </summary>
        /// <param name="f"></param>
        public MajorPage(LogDBFile f)
        {
            //Set parent to begin
            parentFile = f;

            //Set offsets
            offsetBeginning = s.Position;

            //Read file props
            if (!s.ReadConstantString(4, "PAGE".ToCharArray()))
                throw new Exception($"Constant did not match at major page {offsetBeginning}! Corrupted database?");
            fileVersion = s.ReadUShort();
            contentSize = s.ReadULong();
            elementsUsed = s.ReadUShort();
            flags = s.ReadBitFlags(2);

            //Now, read all of the entries.
            entries = new MajorPageObject[parentFile.majorPageSize];
            for (ushort i = 0; i < elementsUsed; i++)
                entries[i] = new MajorPageObject(this);
        }

        /// <summary>
        /// Grabs the size of the content, such as all of the pages. 
        /// </summary>
        /// <returns></returns>
        public ulong SafeGetContentSize()
        {
            ulong size = 0;
            lock(s)
            {
                long pos = s.Position;
                foreach (var e in entries)
                {
                    if (e == null)
                        continue;

                    size += e.GetAbsoluteSizeOfPage();
                }
                //Console.WriteLine(size);
                s.Position = pos;
            }
            return size;
        }

        /// <summary>
        /// Updates the header of this page in a thread-safe manner.
        /// </summary>
        public void SafeUpdate()
        {
            lock(s)
            {
                //Jump to the start, skipping static sanity check.
                s.Position = offsetBeginning + 4;

                //Start writing entries
                s.WriteUInt16(LogDBFile.CURRENT_FILE_VERSION);
                s.WriteUInt64(contentSize);
                s.WriteUInt16(elementsUsed);
                s.WriteBitFlags(flags);
            }
        }

        /// <summary>
        /// Creates a new entry and fills in it's data.
        /// </summary>
        /// <param name="start"></param>
        public MajorPageObject SafeCreateNewEntry(DateTime start)
        {
            MajorPageObject output;
            lock (s)
            {
                //Validate that we have space
                if (remainingElementSpace <= 0)
                    throw new Exception("Out of space on this major page!");

                //Jump to beginning of table and skip to the first open slot
                s.Position = offsetTableBeginning + (26 * elementsUsed);
                long startPos = s.Position;

                //Write the start date and also write it as the end date for now
                s.WriteUInt64((ulong)start.Ticks);
                s.WriteUInt64((ulong)start.Ticks);

                //Write offset. The offset is pulled from the current amount of space used by the content of this page.
                s.WriteUInt64(contentSize);

                //Write blank flags
                s.WriteByte(0x00);
                s.WriteByte(0x00);

                //Add the header and table size to the parent content size.
                contentSize += (18 * (ulong)parentFile.minorPageSize) + 34;

                //Update header info
                elementsUsed++;
                SafeUpdate();

                //Create object
                s.Position = startPos;
                output = new MajorPageObject(this);
                entries[elementsUsed - 1] = output;

                //Create the minor page. Jump to it
                s.Position = (long)output.offsetContentAbsolute;
                s.WriteFixedCharArray("page".ToCharArray()); //Sanity check
                s.WriteUInt64((ulong)start.Ticks); //Start
                s.WriteUInt64((ulong)start.Ticks); //End
                s.WriteUInt16(LogDBFile.CURRENT_FILE_VERSION); //Version
                s.WriteUInt64(0); //Content size. 
                s.WriteUInt16(0); //Number of elements
                s.WriteByte(0x00); //Flag 1
                s.WriteByte(0x00); //Flag 2

                //Allocate the placeholder for the page content.
                for (int i = 0; i < (18 * parentFile.minorPageSize); i++)
                    s.WriteByte(0x00);
            }
            return output;
        }
    }
}
