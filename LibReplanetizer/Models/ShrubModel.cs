﻿// Copyright (C) 2018-2021, The Replanetizer Contributors.
// Replanetizer is free software: you can redistribute it
// and/or modify it under the terms of the GNU General Public
// License as published by the Free Software Foundation,
// either version 3 of the License, or (at your option) any later version.
// Please see the LICENSE.md file for more details.

using System.ComponentModel;
using System.IO;
using System.Collections.Generic; // Add this namespace for List<>
using static LibReplanetizer.DataFunctions;

namespace LibReplanetizer.Models
{
    public class ShrubModel : Model
    {
        const int SHRUBTEXELEMSIZE = 0x10;
        const int SHRUBVERTELEMSIZE = 0x18;
        const int SHRUBUVELEMSIZE = 0x08;

        [Category("Culling Parameters"), DisplayName("Position X")]
        public float cullingX { get; set; }
        [Category("Culling Parameters"), DisplayName("Position Y")]
        public float cullingY { get; set; }
        [Category("Culling Parameters"), DisplayName("Position Z")]
        public float cullingZ { get; set; }
        [Category("Culling Parameters"), DisplayName("Radius")]
        public float cullingRadius { get; set; }

        public uint off20 { get; set; }
        public short off2A { get; set; }
        public uint off2C { get; set; }

        public uint off34 { get; set; }
        public uint off38 { get; set; }
        public uint off3C { get; set; }

        // Add a default constructor for creating empty shrub models (useful for cloning)
        public ShrubModel()
        {
            // Initialize with default values
            id = 0;
            cullingX = 0.0f;
            cullingY = 0.0f;
            cullingZ = 0.0f;
            cullingRadius = 1.0f;
            size = 1.0f;
            
            textureConfig = new List<TextureConfig>();
            vertexBuffer = new float[0];
            indexBuffer = new ushort[0];
        }

        public ShrubModel(FileStream fs, byte[] tieBlock, int num)
        {
            int offset = num * 0x40;
            cullingX = ReadFloat(tieBlock, offset + 0x00);
            cullingY = ReadFloat(tieBlock, offset + 0x04);
            cullingZ = ReadFloat(tieBlock, offset + 0x08);
            cullingRadius = ReadFloat(tieBlock, offset + 0x0C);

            int vertexPointer = ReadInt(tieBlock, offset + 0x10);
            int uvPointer = ReadInt(tieBlock, offset + 0x14);
            int indexPointer = ReadInt(tieBlock, offset + 0x18);
            int texturePointer = ReadInt(tieBlock, offset + 0x1C);

            off20 = ReadUint(tieBlock, offset + 0x20);
            int vertexCount = ReadInt(tieBlock, offset + 0x24);
            short textureCount = ReadShort(tieBlock, offset + 0x28);
            off2A = ReadShort(tieBlock, offset + 0x2A);
            off2C = ReadUint(tieBlock, offset + 0x2C);

            id = ReadShort(tieBlock, offset + 0x30);
            off34 = ReadUint(tieBlock, offset + 0x34);
            off38 = ReadUint(tieBlock, offset + 0x38);
            off3C = ReadUint(tieBlock, offset + 0x3C);

            size = 1.0f;

            textureConfig = GetTextureConfigs(fs, texturePointer, textureCount, SHRUBTEXELEMSIZE);
            int faceCount = GetFaceCount();

            //Get vertex buffer float[vertX, vertY, vertZ, normX, normY, normZ] and UV array float[U, V] * vertexCount
            vertexBuffer = GetVertices(fs, vertexPointer, uvPointer, vertexCount, SHRUBVERTELEMSIZE, SHRUBUVELEMSIZE);

            //Get index buffer ushort[i] * faceCount
            indexBuffer = GetIndices(fs, indexPointer, faceCount);
        }

        public byte[] SerializeHead(int offStart)
        {
            byte[] outBytes = new byte[0x40];

            WriteFloat(outBytes, 0x00, cullingX);
            WriteFloat(outBytes, 0x04, cullingY);
            WriteFloat(outBytes, 0x08, cullingZ);
            WriteFloat(outBytes, 0x0C, cullingRadius);

            int texturePointer = GetLength(offStart);
            int hack = DistToFile80(texturePointer + textureConfig.Count * SHRUBTEXELEMSIZE);
            int vertexPointer = GetLength(texturePointer + textureConfig.Count * SHRUBTEXELEMSIZE + hack); //+ 0x70
            int uvPointer = GetLength(vertexPointer + (vertexBuffer.Length / 8) * SHRUBVERTELEMSIZE);
            int indexPointer = GetLength(uvPointer + (vertexBuffer.Length / 8) * SHRUBUVELEMSIZE);

            WriteInt(outBytes, 0x10, vertexPointer);
            WriteInt(outBytes, 0x14, uvPointer);
            WriteInt(outBytes, 0x18, indexPointer);
            WriteInt(outBytes, 0x1C, texturePointer);

            WriteUint(outBytes, 0x20, off20);
            WriteInt(outBytes, 0x24, vertexBuffer.Length / 8);
            WriteShort(outBytes, 0x28, (short) textureConfig.Count);
            WriteShort(outBytes, 0x2A, off2A);
            WriteUint(outBytes, 0x2C, off2C);

            WriteShort(outBytes, 0x30, id);
            WriteUint(outBytes, 0x34, off34);
            WriteUint(outBytes, 0x38, off38);
            WriteUint(outBytes, 0x3C, off3C);

            return outBytes;
        }

        public byte[] SerializeBody(int offStart)
        {
            int texturePointer = 0;
            int hack = DistToFile80(GetLength(offStart) + texturePointer + textureConfig.Count * SHRUBTEXELEMSIZE);
            int vertexPointer = GetLength(texturePointer + textureConfig.Count * SHRUBTEXELEMSIZE + hack); //+ 0x70
            int uvPointer = GetLength(vertexPointer + (vertexBuffer.Length / 8) * SHRUBVERTELEMSIZE);
            int indexPointer = GetLength(uvPointer + (vertexBuffer.Length / 8) * SHRUBUVELEMSIZE);
            int length = GetLength(indexPointer + indexBuffer.Length * 2);

            byte[] outBytes = new byte[length];
            SerializeTieVertices().CopyTo(outBytes, vertexPointer);
            GetFaceBytes().CopyTo(outBytes, indexPointer);
            SerializeUVs().CopyTo(outBytes, uvPointer);

            for (int i = 0; i < textureConfig.Count; i++)
            {
                WriteInt(outBytes, texturePointer + i * 0x10 + 0x00, textureConfig[i].id);
                WriteInt(outBytes, texturePointer + i * 0x10 + 0x04, textureConfig[i].start);
                WriteInt(outBytes, texturePointer + i * 0x10 + 0x08, textureConfig[i].size);
                WriteInt(outBytes, texturePointer + i * 0x10 + 0x0C, textureConfig[i].mode);
            }

            return outBytes;
        }
    }
}
