using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace LogDB
{
    public static class IOStream
    {
        public static bool is_little_endian = true;

        //API deserialization
        public static ushort ReadUShort(this Stream stream)
        {
            return BitConverter.ToUInt16(PrivateReadBytes(stream, 2), 0);
        }

        public static short ReadShort(this Stream stream)
        {
            return BitConverter.ToInt16(PrivateReadBytes(stream, 2), 0);
        }

        public static uint ReadUInt(this Stream stream)
        {
            return BitConverter.ToUInt32(PrivateReadBytes(stream, 4), 0);
        }

        public static int ReadInt(this Stream stream)
        {
            return BitConverter.ToInt32(PrivateReadBytes(stream, 4), 0);
        }

        public static ulong ReadULong(this Stream stream)
        {
            byte[] buf = PrivateReadBytes(stream, 8);
            return BitConverter.ToUInt64(buf, 0);
        }

        public static long ReadLong(this Stream stream)
        {
            return BitConverter.ToInt64(PrivateReadBytes(stream, 4), 0);
        }

        public static float ReadFloat(this Stream stream)
        {
            return BitConverter.ToSingle(PrivateReadBytes(stream, 4), 0);
        }

        public static double ReadDouble(this Stream stream)
        {
            return BitConverter.ToDouble(PrivateReadBytes(stream, 8), 0);
        }

        public static byte[] ReadBytes(this Stream stream, int length)
        {
            byte[] buf = new byte[length];
            stream.Read(buf, 0, length);
            return buf;
        }

        /// <summary>
        /// Reads a constant string and returns if it matches.
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="length"></param>
        /// <param name="comparer"></param>
        /// <returns></returns>
        public static bool ReadConstantString(this Stream stream, int length, char[] comparer)
        {
            //Read 
            char[] c = ReadFixedLengthString(stream, length);

            //Compare
            for(int i = 0; i<length; i++)
            {
                if (comparer[i] != c[i])
                    return false;
            }
            return true;
        }

        public static char[] ReadFixedLengthString(this Stream s, int length)
        {
            //Read buffer
            byte[] buf = new byte[length];
            s.Read(buf, 0, buf.Length);

            //Convert
            char[] c = new char[length];
            for (int i = 0; i < length; i++)
                c[i] = (char)buf[i];

            return c;
        }

        public static bool[] ReadBitFlags(this Stream stream, int bytes)
        {
            //Read buffer
            byte[] buf = new byte[bytes];
            stream.Read(buf, 0, bytes);

            //Convert out
            BitArray ba = new BitArray(buf);
            bool[] flags = new bool[bytes * 8];
            for (int i = 0; i < flags.Length; i++)
                flags[i] = ba.Get(i);

            return flags;
        }

        //Private deserialization API
        public static byte[] PrivateReadBytes(this Stream stream, int size)
        {
            //Read in from the buffer and respect the little endian setting.
            byte[] buf = new byte[size];
            //Read
            stream.Read(buf, 0, size);
            //Respect endians
            if (is_little_endian != BitConverter.IsLittleEndian)
                Array.Reverse(buf);
            return buf;
        }

        //Writing
        public static void PrivateWriteBytesRespectEndians(this Stream stream, byte[] data)
        {
            //Respect endians
            if (is_little_endian != BitConverter.IsLittleEndian)
                Array.Reverse(data);

            //Write
            stream.Write(data, 0, data.Length);
        }

        public static void WriteUInt32(this Stream stream, UInt32 i)
        {
            PrivateWriteBytesRespectEndians(stream, BitConverter.GetBytes(i));
        }

        public static void WriteUInt64(this Stream stream, UInt64 i)
        {
            PrivateWriteBytesRespectEndians(stream, BitConverter.GetBytes(i));
        }

        public static void WriteUInt16(this Stream stream, UInt16 i)
        {
            PrivateWriteBytesRespectEndians(stream, BitConverter.GetBytes(i));
        }

        /// <summary>
        /// Writes a FIXED LENGTH string.
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="c"></param>
        public static void WriteFixedCharArray(this Stream stream, char[] c)
        {
            //Write each byte
            foreach(char cc in c)
            {
                stream.WriteByte(((byte)cc));
            }
        }

        public static void WriteBitFlags(this Stream stream, bool[] b)
        {
            //Get the number of bytes
            if (b.Length % 8 != 0)
                throw new Exception($"Number of bits does not line up on byte boundry. Cannot continue.");
            int byteCount = b.Length / 8;

            //Create BitArray of this
            BitArray ba = new BitArray(b);

            //Copy out
            byte[] output = new byte[byteCount];
            ba.CopyTo(output, 0);

            //Write, ignoring endians
            stream.Write(output, 0, output.Length);
        }
    }
}
