# LogDB
LogDB is an ultra fast write once, read many database. It can quickly write entries to the database by date, however they cannot be removed or edited.

## Features
* Ultra fast, indexed, realtime writes.
* Read/Search database by date very quickly

## Limitations
* LogDB is strictly write once. Entries cannot be deleted or edited, and must be placed in order for indexing.

Right now, only writing is supported. Read support coming soon.

## Usage (Writing)
The following code writes the contents of variable ``testFile`` (not created in the example, it's just a Stream) and writes it to the database at the current time 9 times.

```csharp
using (FileStream fs = new FileStream(@"E:\test_logdb.db", FileMode.Create))
{
    LogDBFile f = LogDBFile.CreateLogDBFile(10, 10, fs);
    for (int i = 0; i < 9; i++)
    {
        f.SafeWriteNewEntryBytes(DateTime.UtcNow, testFile);
        testFile.Position = 0;
    }
}
```

You can use any seekable and read/writable Stream you'd like, but a FileStream is recommended.
