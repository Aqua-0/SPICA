﻿using SPICA.PICA;
using SPICA.PICA.Commands;
using SPICA.PICA.Converters;
using SPICA.Serialization;
using SPICA.Serialization.Serializer;

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace SPICA.Formats.CtrH3D.Texture
{
    public class H3DTexture : ICustomSerialization, ICustomSerializeCmd, INamed
    {
        private uint[] Texture0Commands;
        private uint[] Texture1Commands;
        private uint[] Texture2Commands;

        public PICATextureFormat Format;

        public byte MipmapSize;
        private ushort Padding;

        public string Name;

        public string ObjectName { get { return Name; } }

        public bool IsCubeTexture { get { return RawBufferZNeg != null; } }

        [NonSerialized] public byte[] RawBufferXPos;
        [NonSerialized] public byte[] RawBufferXNeg;
        [NonSerialized] public byte[] RawBufferYPos;
        [NonSerialized] public byte[] RawBufferYNeg;
        [NonSerialized] public byte[] RawBufferZPos;
        [NonSerialized] public byte[] RawBufferZNeg;

        [NonSerialized] public uint Width;
        [NonSerialized] public uint Height;

        public H3DTexture() { }

        public H3DTexture(string FileName)
        {
            Bitmap Img = new Bitmap(FileName);

            if (Img.PixelFormat != PixelFormat.Format32bppArgb) Img = new Bitmap(Img);

            using (Img)
            {
                Name = Path.GetFileNameWithoutExtension(FileName);
                Format = PICATextureFormat.RGBA8;

                H3DTextureImpl(Img);
            }
        }

        public H3DTexture(string Name, Bitmap Img, PICATextureFormat Format = 0)
        {
            this.Name = Name;
            this.Format = Format;

            H3DTextureImpl(Img);
        }

        private void H3DTextureImpl(Bitmap Img)
        {
            MipmapSize = 1;

            Width = (uint)Img.Width;
            Height = (uint)Img.Height;

            RawBufferXPos = TextureConverter.Encode(Img, Format);
        }

        public Bitmap ToBitmap(int Face = 0)
        {
            return TextureConverter.Decode(BufferFromFace(Face), (int)Width, (int)Height, Format);
        }

        public byte[] ToRGBA(int Face = 0)
        {
            return TextureConverter.Decode(BufferFromFace(Face), (int)Width, (int)Height, Format, true);
        }

        private byte[] BufferFromFace(int Face)
        {
            switch (Face)
            {
                case 0: return RawBufferXPos;
                case 1: return RawBufferXNeg;
                case 2: return RawBufferYPos;
                case 3: return RawBufferYNeg;
                case 4: return RawBufferZPos;
                case 5: return RawBufferZNeg;

                default: throw new IndexOutOfRangeException("Expected a value in 0-6 range!");
            }
        }

        public void ReplaceData(H3DTexture Texture)
        {
            Format = Texture.Format;

            MipmapSize = Texture.MipmapSize;

            RawBufferXPos = Texture.RawBufferXPos;

            Width = Texture.Width;
            Height = Texture.Height;
        }

        void ICustomSerialization.Deserialize(BinaryDeserializer Deserializer)
        {
            PICACommandReader Reader = new PICACommandReader(Texture0Commands);

            uint[] Address = new uint[6];

            while (Reader.HasCommand)
            {
                PICACommand Cmd = Reader.GetCommand();

                uint Param = Cmd.Parameters[0];

                switch (Cmd.Register)
                {
                    case PICARegister.GPUREG_TEXUNIT0_DIM:
                        Height = Param & 0x7ff;
                        Width = (Param >> 16) & 0x7ff;
                        break;
                    case PICARegister.GPUREG_TEXUNIT0_ADDR1: Address[0] = Param; break;
                    case PICARegister.GPUREG_TEXUNIT0_ADDR2: Address[1] = Param; break;
                    case PICARegister.GPUREG_TEXUNIT0_ADDR3: Address[2] = Param; break;
                    case PICARegister.GPUREG_TEXUNIT0_ADDR4: Address[3] = Param; break;
                    case PICARegister.GPUREG_TEXUNIT0_ADDR5: Address[4] = Param; break;
                    case PICARegister.GPUREG_TEXUNIT0_ADDR6: Address[5] = Param; break;
                }
            }

            int Length = TextureConverter.CalculateLength((int)Width, (int)Height, Format);

            long Position = Deserializer.BaseStream.Position;

            for (int Face = 0; Face < 6; Face++)
            {
                if (Address[Face] == 0) break;

                Deserializer.BaseStream.Seek(Address[Face], SeekOrigin.Begin);

                switch (Face)
                {
                    case 0: RawBufferXPos = Deserializer.Reader.ReadBytes(Length); break;
                    case 1: RawBufferXNeg = Deserializer.Reader.ReadBytes(Length); break;
                    case 2: RawBufferYPos = Deserializer.Reader.ReadBytes(Length); break;
                    case 3: RawBufferYNeg = Deserializer.Reader.ReadBytes(Length); break;
                    case 4: RawBufferZPos = Deserializer.Reader.ReadBytes(Length); break;
                    case 5: RawBufferZNeg = Deserializer.Reader.ReadBytes(Length); break;
                }
            }

            Deserializer.BaseStream.Seek(Position, SeekOrigin.Begin);
        }

        bool ICustomSerialization.Serialize(BinarySerializer Serializer)
        {
            for (int Unit = 0; Unit < 3; Unit++)
            {
                PICACommandWriter Writer = new PICACommandWriter();

                switch (Unit)
                {
                    case 0:
                        Writer.SetCommand(PICARegister.GPUREG_TEXUNIT0_DIM, Height | (Width << 16));
                        Writer.SetCommand(PICARegister.GPUREG_TEXUNIT0_LOD, MipmapSize);
                        Writer.SetCommand(PICARegister.GPUREG_TEXUNIT0_ADDR1, 0);
                        Writer.SetCommand(PICARegister.GPUREG_TEXUNIT0_TYPE, (uint)Format);
                        break;

                    case 1:
                        Writer.SetCommand(PICARegister.GPUREG_TEXUNIT1_DIM, Height | (Width << 16));
                        Writer.SetCommand(PICARegister.GPUREG_TEXUNIT1_LOD, MipmapSize);
                        Writer.SetCommand(PICARegister.GPUREG_TEXUNIT1_ADDR, 0);
                        Writer.SetCommand(PICARegister.GPUREG_TEXUNIT1_TYPE, (uint)Format);
                        break;

                    case 2:
                        Writer.SetCommand(PICARegister.GPUREG_TEXUNIT2_DIM, Height | (Width << 16));
                        Writer.SetCommand(PICARegister.GPUREG_TEXUNIT2_LOD, MipmapSize);
                        Writer.SetCommand(PICARegister.GPUREG_TEXUNIT2_ADDR, 0);
                        Writer.SetCommand(PICARegister.GPUREG_TEXUNIT2_TYPE, (uint)Format);
                        break;
                }

                Writer.WriteEnd();

                switch (Unit)
                {
                    case 0: Texture0Commands = Writer.GetBuffer(); break;
                    case 1: Texture1Commands = Writer.GetBuffer(); break;
                    case 2: Texture2Commands = Writer.GetBuffer(); break;
                }
            }

            return false;
        }

        void ICustomSerializeCmd.SerializeCmd(BinarySerializer Serializer, object Value)
        {
            //TODO: Write all 6 faces of a Cube Map
            long Position = Serializer.BaseStream.Position + 0x10;

            Serializer.RawDataTex.Values.Add(new RefValue
            {
                Value = RawBufferXPos,
                Position = Position
            });

            Serializer.Relocator.RelocTypes.Add(Position, H3DRelocationType.RawDataTexture);
        }
    }
}