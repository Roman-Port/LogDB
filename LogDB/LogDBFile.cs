using LogDB.Entities;
using System;
using System.Collections.Generic;
using System.IO;

namespace LogDB
{
    public class LogDBFile
    {
        //Constatns
        public const ushort CURRENT_FILE_VERSION = 0;
        
        //File props
        public readonly ushort majorPageSize;
        public readonly ushort minorPageSize;

        public bool[] flags;

        //Runtime vars
        public Stream s;
        public List<MajorPage> pages;

        //Creation
        public LogDBFile(ushort majorPageSize, ushort minorPageSize, Stream s)
        {
            this.majorPageSize = majorPageSize;
            this.minorPageSize = minorPageSize;
            this.s = s;
        }

        public static LogDBFile CreateLogDBFile(ushort majorPageSize, ushort minorPageSize, Stream s)
        {
            //First, create the object.
            LogDBFile f = new LogDBFile(majorPageSize, minorPageSize, s);
            f.pages = new List<MajorPage>();

            //Now, begin writing the file header.
            f.s.Position = 0;
            f.s.WriteFixedCharArray("LogDB".ToCharArray());
            f.s.WriteUInt16(CURRENT_FILE_VERSION);
            f.s.WriteUInt16(majorPageSize);
            f.s.WriteUInt16(minorPageSize);
            f.s.WriteUInt32(0); 
            f.s.WriteByte(0x00);
            f.s.WriteByte(0x00);

            //Write reserved space
            for (int i = 0; i < 49; i++)
                f.s.WriteByte(0x00);

            //Next, we are going to add a major page.
            f.CreateNewMajorPageAtCursorPos();

            return f;
        }

        //Pages
        /// <summary>
        /// Creates a new page, but does it in a safe manner.
        /// </summary>
        /// <returns></returns>
        public MajorPage SafeCreatePage()
        {
            MajorPage p;
            lock (s)
            {
                //Obtain the latest page. We're going to seek to the end of it and write the page.
                MajorPage latestPage = pages[pages.Count - 1];

                //Jump to the absolute page end
                s.Position = latestPage.offsetContentEnd;

                //Write here
                p = CreateNewMajorPageAtCursorPos();
            }
            return p;
        }
        /// <summary>
        /// Creates a new major page at cursor position.
        /// </summary>
        /// <returns></returns>
        public MajorPage CreateNewMajorPageAtCursorPos()
        {
            MajorPage p;
            //Create a new page at the current position and return the object for it.
            //Write header. Use zero as the content size for now.
            long start = s.Position;
            s.WriteFixedCharArray("PAGE".ToCharArray()); //Sanity check
            s.WriteUInt16(CURRENT_FILE_VERSION);
            s.WriteUInt64(0);
            s.WriteUInt16(0);
            s.WriteByte(0x00);
            s.WriteByte(0x00);

            //Now, create all of the reserved space for the elements
            for (int i = 0; i < (26 * majorPageSize); i++)
                s.WriteByte(0x00);

            //Now, create the object for this major page.
            s.Position = start;
            p = new MajorPage(this);
            pages.Add(p);
            return p;
        }

        /// <summary>
        /// Returns the latest page with space, or creates a new one.
        /// </summary>
        /// <returns></returns>
        public MajorPage SafeGetLatestWritableMajorPage()
        {
            MajorPage p;
            lock (s)
            {
                //Check the latest page and see if it has space
                p = pages[pages.Count - 1];
                if(p.remainingElementSpace == 0)
                {
                    //A new page must be created
                    p = SafeCreatePage();
                }
            }
            return p;
        }

        public MinorPage SafeGetLatestWritableMinorPage(DateTime defaultTime)
        {
            MinorPage p;
            lock (s)
            {
                //First, grab the latest major page
                MajorPage mp = SafeGetLatestWritableMajorPage();

                //Get the latest minor page from inside of this.
                MajorPageObject lastMajorPageObject = null;
                for (int i = 0; i<mp.entries.Length; i++)
                {
                    MajorPageObject o = mp.entries[i];
                    if (o != null)
                        lastMajorPageObject = o;
                }

                //Create a new object if we need to do so
                if(lastMajorPageObject == null || lastMajorPageObject.SafeReadPage().elementsUsed >= minorPageSize)
                {
                    //Create a new page.
                    lastMajorPageObject = mp.SafeCreateNewEntry(defaultTime);
                }

                //Set minor page
                p = lastMajorPageObject.SafeReadPage();
            }
            return p;
        }

        public void SafeWriteNewEntryBytes(DateTime entryTime, Stream entryContent, bool[] entryFlags = null)
        {
            lock(s)
            {
                //Get latest writable page
                MinorPage p = SafeGetLatestWritableMinorPage(entryTime);

                //Write
                p.SafeCreateNewEntry(entryTime, entryContent, flags);
            }
        }
    }
}
