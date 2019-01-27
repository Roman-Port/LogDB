using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace LogDB.Entities
{
    public class MinorPage
    {
        //Handles
        public MajorPage parentPage;
        public MajorPageObject parentPageEntry;
        public Stream s { get { return parentPage.s; } }

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

        //Sizes
        /// <summary>
        /// Size of the header
        /// </summary>
        public long headerSizeBytes = 34;
        /// <summary>
        /// Size of the table
        /// </summary>
        public long tableSizeBytes { get { return (long)parentPage.parentFile.minorPageSize * 18; } }
        /// <summary>
        /// Size of the page, with the header and table, EXCLUDING THE CONTENT.
        /// </summary>
        public long pageSizeBytes { get { return headerSizeBytes + tableSizeBytes; } }

        //File props
        public DateTime startTime;
        public DateTime endTime;
        public ushort fileVersion;
        public ulong contentSize;
        public ushort elementsUsed;
        public bool[] flags;

        //Misc
        /// <summary>
        /// Number of remaining elements in the table that we can fill.
        /// </summary>
        public ushort remainingElementSpace { get { return (ushort)(parentPage.parentFile.minorPageSize - elementsUsed); } }
        public ulong originalContentSize;
        public MinorPageObject[] entries;

        /// <summary>
        /// Reads a minor page at position
        /// </summary>
        /// <param name="o"></param>
        public MinorPage(MajorPageObject o)
        {
            //Set handles
            parentPageEntry = o;
            parentPage = o.parentPage;

            //Set offsets 
            offsetBeginning = s.Position;

            //Validate the page text
            if (!s.ReadConstantString(4, "page".ToCharArray()))
                throw new Exception($"Constant did not match at minor page {offsetBeginning}! Corrupted database?");

            //Read file props
            startTime = new DateTime((long)s.ReadULong());
            endTime = new DateTime((long)s.ReadULong());
            fileVersion = s.ReadUShort();
            contentSize = s.ReadULong();
            originalContentSize = contentSize;
            elementsUsed = s.ReadUShort();
            flags = s.ReadBitFlags(2);

            //Read entries
            entries = new MinorPageObject[parentPage.parentFile.minorPageSize];
            for (ushort i = 0; i < elementsUsed; i++)
                entries[i] = new MinorPageObject(this);

            
        }

        /// <summary>
        /// Gets the size of the entire page, including headers and content.
        /// </summary>
        /// <param name="f"></param>
        /// <returns></returns>
        public static ulong SafeGetAbsoluteSize(LogDBFile f, long pos)
        {
            ulong size;
            lock (f.s)
            {
                //First, get size of headers and table
                size = (18 * (ulong)f.minorPageSize) + 34;

                //Jump to the position of the content size and read it
                f.s.Position = pos + 4 + 8 + 8 + 2;
                size += f.s.ReadULong();
            }

            return size;
        }

        /// <summary>
        /// Safely updates the header in a thread-safe way.
        /// </summary>
        public void SafeUpdate()
        {
            lock(s)
            {
                //Jump to start, skipping sanity check
                s.Position = offsetBeginning + 4;

                //Write header
                s.WriteUInt64((ulong)startTime.Ticks);
                s.WriteUInt64((ulong)endTime.Ticks);
                s.WriteUInt16(fileVersion);
                s.WriteUInt64(contentSize);
                s.WriteUInt16(elementsUsed);
                s.WriteBitFlags(flags);

                //Update parent
                parentPage.contentSize += (contentSize - originalContentSize);
                originalContentSize = contentSize;
                parentPageEntry.end = endTime;
                parentPageEntry.start = startTime;
                parentPageEntry.SafeUpdate();
            }
        }

        /// <summary>
        /// Creates a new, writable, entry and adds it's data.
        /// </summary>
        /// <param name="time"></param>
        /// <returns></returns>
        public MinorPageObject SafeCreateNewEntry(DateTime time, Stream contentToWrite, bool[] flags = null)
        {
            MinorPageObject output;
            lock (s)
            {
                //Validate that we have space
                if (remainingElementSpace <= 0)
                    throw new Exception("Out of space on this major page!");

                //Validate flags
                if (flags == null)
                    flags = new bool[8];
                if (flags.Length != 8)
                    throw new Exception("Flags length MUST be 8.");

                //Validate input stream
                if (!contentToWrite.CanRead || !contentToWrite.CanSeek)
                    throw new Exception("Input stream to write MUST be readable and seekable.");
                if (contentToWrite.Length > uint.MaxValue - 1)
                    throw new Exception($"The input content stream is too large! The size limit is {(uint.MaxValue - 1).ToString()}. This stream is {contentToWrite.Length.ToString()} bytes long.");

                //Jump to beginning of table and skip to the first open slot
                s.Position = offsetTableBeginning + (18 * elementsUsed);
                long startPos = s.Position;

                //Write the entry. The offset is pulled from the current amount of space used by the content of this page.
                s.WriteUInt64((ulong)time.Ticks); //Time
                s.WriteUInt64(contentSize); //Offset
                s.WriteByte(0x00); //Flag part 1
                s.WriteByte(0x00); //Flag part 2

                //Create object
                s.Position = startPos;
                output = new MinorPageObject(this);
                entries[elementsUsed] = output;

                //Create the content of the Content Object
                s.Position = (long)output.offsetContentAbsolute;
                s.WriteUInt64((ulong)time.Ticks); //Time
                s.WriteUInt16(LogDBFile.CURRENT_FILE_VERSION); //Version
                s.WriteUInt32((uint)contentToWrite.Length); //Size
                s.WriteByte(0x00); //Flag
                contentToWrite.CopyTo(s);

                //Update header info
                elementsUsed++;
                endTime = new DateTime(Math.Min(endTime.Ticks, time.Ticks));
                //contentSize += 300;
                contentSize += 15 + (ulong)contentToWrite.Length; //Adding size of what we just wrote
                SafeUpdate();
            }
            return output;
        }
    }
}
