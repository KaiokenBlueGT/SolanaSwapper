// Copyright (C) 2018-2021, The Replanetizer Contributors.
// Replanetizer is free software: you can redistribute it
// and/or modify it under the terms of the GNU General Public
// License as published by the Free Software Foundation,
// either version 3 of the License, or (at your option) any later version.
// Please see the LICENSE.md file for more details.

using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace LibReplanetizer
{
    public static class DataFunctions
    {
        [StructLayout(LayoutKind.Explicit)]
        struct FloatUnion
        {
            [FieldOffset(0)]
            public byte byte0;
            [FieldOffset(1)]
            public byte byte1;
            [FieldOffset(2)]
            public byte byte2;
            [FieldOffset(3)]
            public byte byte3;

            [FieldOffset(0)]
            public float value;
        }

        static FloatUnion FLOAT_BYTES;

        public static float ReadFloat(byte[] buf, int offset)
        {
            FLOAT_BYTES.byte0 = buf[offset + 3];
            FLOAT_BYTES.byte1 = buf[offset + 2];
            FLOAT_BYTES.byte2 = buf[offset + 1];
            FLOAT_BYTES.byte3 = buf[offset];
            return FLOAT_BYTES.value;
        }

        public static int ReadInt(byte[] buf, int offset)
        {
            return buf[offset + 0] << 24 | buf[offset + 1] << 16 | buf[offset + 2] << 8 | buf[offset + 3];
        }

        public static short ReadShort(byte[] buf, int offset)
        {
            return (short) (buf[offset + 0] << 8 | buf[offset + 1]);
        }

        public static uint ReadUint(byte[] buf, int offset)
        {
            return (uint) (buf[offset + 0] << 24 | buf[offset + 1] << 16 | buf[offset + 2] << 8 | buf[offset + 3]);
        }

        public static ushort ReadUshort(byte[] buf, int offset)
        {
            return (ushort) (buf[offset + 0] << 8 | buf[offset + 1]);
        }

        public static Matrix4 ReadMatrix4(byte[] buf, int offset)
        {
            return new Matrix4(
                ReadFloat(buf, offset + 0x00),
                ReadFloat(buf, offset + 0x04),
                ReadFloat(buf, offset + 0x08),
                ReadFloat(buf, offset + 0x0C),

                ReadFloat(buf, offset + 0x10),
                ReadFloat(buf, offset + 0x14),
                ReadFloat(buf, offset + 0x18),
                ReadFloat(buf, offset + 0x1C),

                ReadFloat(buf, offset + 0x20),
                ReadFloat(buf, offset + 0x24),
                ReadFloat(buf, offset + 0x28),
                ReadFloat(buf, offset + 0x2C),

                ReadFloat(buf, offset + 0x30),
                ReadFloat(buf, offset + 0x34),
                ReadFloat(buf, offset + 0x38),
                ReadFloat(buf, offset + 0x3C)
                );
        }

        public static Matrix3x4 ReadMatrix3x4(byte[] buf, int offset)
        {
            return new Matrix3x4(
                ReadFloat(buf, offset + 0x00),
                ReadFloat(buf, offset + 0x04),
                ReadFloat(buf, offset + 0x08),
                ReadFloat(buf, offset + 0x0C),

                ReadFloat(buf, offset + 0x10),
                ReadFloat(buf, offset + 0x14),
                ReadFloat(buf, offset + 0x18),
                ReadFloat(buf, offset + 0x1C),

                ReadFloat(buf, offset + 0x20),
                ReadFloat(buf, offset + 0x24),
                ReadFloat(buf, offset + 0x28),
                ReadFloat(buf, offset + 0x2C)
                );
        }

        public static byte[] ReadBlock(FileStream fs, long offset, int length)
        {
            if (length > 0)
            {
                fs.Seek(offset, SeekOrigin.Begin);
                byte[] returnBytes = new byte[length];
                fs.Read(returnBytes, 0, length);
                return returnBytes;
            }
            else
            {
                byte[] returnBytes = new byte[0x10];
                return returnBytes;
            }
        }

        public static byte[] ReadBlockNopad(FileStream fs, long offset, int length)
        {
            if (length > 0)
            {
                fs.Seek(offset, SeekOrigin.Begin);
                byte[] returnBytes = new byte[length];
                fs.Read(returnBytes, 0, length);
                return returnBytes;
            }
            return new byte[0];
        }

        public static byte[] ReadString(FileStream fs, int offset)
        {
            var output = new List<byte>();

            fs.Seek(offset, SeekOrigin.Begin);

            byte[] buffer = new byte[4];
            do
            {
                fs.Read(buffer, 0, 4);
                output.AddRange(buffer);
            }
            while (buffer[3] != '\0');

            output.RemoveAll(item => item == 0);

            return output.ToArray();
        }

        public static void WriteUint(byte[] byteArr, int offset, uint input)
        {
            byte[] byt = BitConverter.GetBytes(input);
            byteArr[offset + 0] = byt[3];
            byteArr[offset + 1] = byt[2];
            byteArr[offset + 2] = byt[1];
            byteArr[offset + 3] = byt[0];
        }

        public static void WriteInt(byte[] byteArr, int offset, int input)
        {
            byte[] byt = BitConverter.GetBytes(input);
            byteArr[offset + 0] = byt[3];
            byteArr[offset + 1] = byt[2];
            byteArr[offset + 2] = byt[1];
            byteArr[offset + 3] = byt[0];
        }

        public static void WriteFloat(byte[] byteArr, int offset, float input)
        {
            byte[] byt = BitConverter.GetBytes(input);
            byteArr[offset + 0] = byt[3];
            byteArr[offset + 1] = byt[2];
            byteArr[offset + 2] = byt[1];
            byteArr[offset + 3] = byt[0];
        }

        public static void WriteShort(byte[] byteArr, int offset, short input)
        {
            byte[] byt = BitConverter.GetBytes(input);
            byteArr[offset + 0] = byt[1];
            byteArr[offset + 1] = byt[0];
        }

        public static void WriteUshort(byte[] byteArr, int offset, ushort input)
        {
            byte[] byt = BitConverter.GetBytes(input);
            byteArr[offset + 0] = byt[1];
            byteArr[offset + 1] = byt[0];
        }

        public static void WriteMatrix4(byte[] byteArray, int offset, Matrix4 input)
        {
            WriteFloat(byteArray, offset + 0x00, input.M11);
            WriteFloat(byteArray, offset + 0x04, input.M12);
            WriteFloat(byteArray, offset + 0x08, input.M13);
            WriteFloat(byteArray, offset + 0x0C, input.M14);

            WriteFloat(byteArray, offset + 0x10, input.M21);
            WriteFloat(byteArray, offset + 0x14, input.M22);
            WriteFloat(byteArray, offset + 0x18, input.M23);
            WriteFloat(byteArray, offset + 0x1C, input.M24);

            WriteFloat(byteArray, offset + 0x20, input.M31);
            WriteFloat(byteArray, offset + 0x24, input.M32);
            WriteFloat(byteArray, offset + 0x28, input.M33);
            WriteFloat(byteArray, offset + 0x2C, input.M34);

            WriteFloat(byteArray, offset + 0x30, input.M41);
            WriteFloat(byteArray, offset + 0x34, input.M42);
            WriteFloat(byteArray, offset + 0x38, input.M43);
            WriteFloat(byteArray, offset + 0x3C, input.M44);
        }

        public static void WriteMatrix3x4(byte[] byteArray, int offset, Matrix3x4 input)
        {
            WriteFloat(byteArray, offset + 0x00, input.M11);
            WriteFloat(byteArray, offset + 0x04, input.M12);
            WriteFloat(byteArray, offset + 0x08, input.M13);
            WriteFloat(byteArray, offset + 0x0C, input.M14);

            WriteFloat(byteArray, offset + 0x10, input.M21);
            WriteFloat(byteArray, offset + 0x14, input.M22);
            WriteFloat(byteArray, offset + 0x18, input.M23);
            WriteFloat(byteArray, offset + 0x1C, input.M24);

            WriteFloat(byteArray, offset + 0x20, input.M31);
            WriteFloat(byteArray, offset + 0x24, input.M32);
            WriteFloat(byteArray, offset + 0x28, input.M33);
            WriteFloat(byteArray, offset + 0x2C, input.M34);
        }

        public static byte[] GetBytes(byte[] array, int ind, int length)
        {
            byte[] data = new byte[length];
            for (int i = 0; i < length; i++)
            {
                data[i] = array[ind + i];
            }
            return data;
        }

        public static int GetLength(int length, int alignment = 0)
        {
            while (length % 0x10 != alignment)
            {
                length++;
            }
            return length;
        }

        // vertexbuffers are often aligned to the nearest 0x80 in the file
        public static int DistToFile80(int length, int alignment = 0)
        {
            int added = 0;
            while (length % 0x80 != alignment)
            {
                length++;
                added++;
            }
            return added;
        }

        public static int GetLength20(int length, int alignment = 0)
        {
            while (length % 0x20 != alignment)
            {
                length++;
            }
            return length;
        }

        public static int GetLength100(int length)
        {
            while (length % 0x100 != 0)
            {
                length++;
            }
            return length;
        }

        public static void Pad(List<byte> arr)
        {
            while (arr.Count % 0x10 != 0)
            {
                arr.Add(0);
            }
        }

        /// <summary>
        /// Serializes a Collision object to the expected RCC file format (header + buffers)
        /// </summary>
        public static byte[] SerializeCollisionToRcc(LibReplanetizer.Models.Collision collision)
        {
            // This is a minimal implementation. You may need to adjust for your game version.
            // RCC files start with a header: [offset to collision data][length of collision data]
            // Then the collision data block (vertex/index buffers, etc)
            // We'll write a dummy header and then the buffers.
            using (var ms = new MemoryStream())
            {
                // Write header (8 bytes)
                int headerSize = 8;
                int collisionDataOffset = headerSize; // Data starts right after header
                // Calculate collision data length
                int vertexBytes = collision.vertexBuffer.Length * sizeof(float);
                int indexBytes = collision.indBuff.Length * sizeof(uint);
                int collisionDataLength = vertexBytes + indexBytes;
                // Write offset (relative to start)
                WriteInt(ms, collisionDataOffset);
                WriteInt(ms, collisionDataLength);
                // Write vertex buffer
                var vertexBuf = new byte[vertexBytes];
                Buffer.BlockCopy(collision.vertexBuffer, 0, vertexBuf, 0, vertexBytes);
                ms.Write(vertexBuf, 0, vertexBuf.Length);
                // Write index buffer
                var indexBuf = new byte[indexBytes];
                Buffer.BlockCopy(collision.indBuff, 0, indexBuf, 0, indexBytes);
                ms.Write(indexBuf, 0, indexBuf.Length);
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Serializes a Collision object to a valid hierarchical RCC chunk format (z/y/x/vertex blocks with counts and shifts)
        /// </summary>
        public static byte[] SerializeCollisionToRccChunked(LibReplanetizer.Models.Collision collision)
        {
            // --- PATCH: Write a full UYA-style chunk header (64 bytes, 16 pointers) ---
            using (var ms = new MemoryStream())
            {
                // Write 64-byte header (16 pointers, big-endian)
                int headerSize = 64;
                for (int i = 0; i < 16; i++)
                {
                    // Only pointer 0x10 (index 4) will point to collision block, rest are zero
                    int pointer = (i == 4) ? headerSize : 0;
                    ms.WriteByte((byte)((pointer >> 24) & 0xFF));
                    ms.WriteByte((byte)((pointer >> 16) & 0xFF));
                    ms.WriteByte((byte)((pointer >> 8) & 0xFF));
                    ms.WriteByte((byte)(pointer & 0xFF));
                }
                // --- Write collision block at offset 0x40 (64) ---
                ms.Position = headerSize;
                // --- zShift, zCount ---
                ushort zShift = 0;
                ushort zCount = 1;
                WriteUshort(ms, zShift);
                WriteUshort(ms, zCount);
                // --- yOffset ---
                int yOffsetPos = (int)ms.Position;
                WriteInt(ms, 0); // placeholder
                // --- yShift, yCount ---
                ushort yShift = 0;
                ushort yCount = 1;
                WriteUshort(ms, yShift);
                WriteUshort(ms, yCount);
                // --- xOffset ---
                int xOffsetPos = (int)ms.Position;
                WriteInt(ms, 0); // placeholder
                // --- xShift, xCount ---
                ushort xShift = 0;
                ushort xCount = 1;
                WriteUshort(ms, xShift);
                WriteUshort(ms, xCount);
                // --- vOffset ---
                int vOffsetPos = (int)ms.Position;
                WriteInt(ms, 0); // placeholder
                // --- Vertex Block ---
                int vBlockStart = (int)ms.Position;
                ushort faceCount = (ushort)(collision.indBuff.Length / 3);
                byte vertexCount = (byte)(collision.vertexBuffer.Length / 3);
                byte rCount = 0; // No r faces for now
                WriteUshort(ms, faceCount);
                ms.WriteByte(vertexCount);
                ms.WriteByte(rCount);
                // Vertex data (x, y, z floats) -- SCALE BY 1024
                for (int i = 0; i < vertexCount; i++)
                {
                    WriteFloat(ms, collision.vertexBuffer[i * 3 + 0] * 1024f);
                    WriteFloat(ms, collision.vertexBuffer[i * 3 + 1] * 1024f);
                    WriteFloat(ms, collision.vertexBuffer[i * 3 + 2] * 1024f);
                }
                // Face data (b0, b1, b2, b3)
                for (int i = 0; i < faceCount; i++)
                {
                    uint f0 = collision.indBuff[i * 3 + 0];
                    uint f1 = collision.indBuff[i * 3 + 1];
                    uint f2 = collision.indBuff[i * 3 + 2];
                    byte b0 = (byte)f0;
                    byte b1 = (byte)f1;
                    byte b2 = (byte)f2;
                    byte b3 = 0x1F; // Default type
                    ms.WriteByte(b0);
                    ms.WriteByte(b1);
                    ms.WriteByte(b2);
                    ms.WriteByte(b3);
                }
                // rCount extra indices (none for now)
                int vBlockEnd = (int)ms.Position;
                // --- Patch offsets ---
                PatchInt(ms, yOffsetPos, xOffsetPos - yOffsetPos);
                PatchInt(ms, xOffsetPos, vOffsetPos - xOffsetPos);
                PatchInt(ms, vOffsetPos, vBlockStart - vOffsetPos);
                ms.Position = vBlockEnd;
                return ms.ToArray();
            }
        }

        private static void WriteUshort(Stream stream, ushort value)
        {
            stream.WriteByte((byte)((value >> 8) & 0xFF));
            stream.WriteByte((byte)(value & 0xFF));
        }
        private static void WriteFloat(Stream stream, float value)
        {
            var bytes = BitConverter.GetBytes(value);
            stream.WriteByte(bytes[3]);
            stream.WriteByte(bytes[2]);
            stream.WriteByte(bytes[1]);
            stream.WriteByte(bytes[0]);
        }
        private static void WriteInt(Stream stream, int value)
        {
            // Write as big-endian (same as ReadInt)
            stream.WriteByte((byte)((value >> 24) & 0xFF));
            stream.WriteByte((byte)((value >> 16) & 0xFF));
            stream.WriteByte((byte)((value >> 8) & 0xFF));
            stream.WriteByte((byte)(value & 0xFF));
        }
        private static void PatchInt(Stream stream, int pos, int value)
        {
            long cur = stream.Position;
            stream.Position = pos;
            WriteInt(stream, value);
            stream.Position = cur;
        }
    }
}
