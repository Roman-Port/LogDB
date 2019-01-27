using System;
using System.IO;
using LogDB;

namespace TestApp
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Testing");

            if (File.Exists(@"E:\test_logdb.db"))
                File.Delete(@"E:\test_logdb.db");

            FileStream testFile = new FileStream(@"E:\test_text.txt", FileMode.Open);

            using (FileStream fs = new FileStream(@"E:\test_logdb.db", FileMode.Create))
            {
                LogDBFile f = LogDBFile.CreateLogDBFile(10, 10, fs);
                for (int i = 0; i < 9; i++)
                {
                    f.SafeWriteNewEntryBytes(DateTime.UtcNow, testFile);
                    testFile.Position = 0;
                }
            }

            Console.WriteLine("Done");
            Console.ReadLine();
        }
    }
}
