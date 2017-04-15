﻿using SPICA.Formats.CtrH3D;
using SPICA.Formats.CtrH3D.LUT;
using SPICA.Formats.CtrH3D.Model;
using SPICA.Formats.CtrH3D.Model.Material;
using SPICA.Formats.CtrH3D.Model.Mesh;
using SPICA.Formats.CtrH3D.Texture;
using SPICA.PICA.Commands;
using SPICA.PICA.Converters;

using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Text;

using System.Numerics;

namespace SPICA.Formats.Generic.StudioMdl
{
    class SMD
    {
        private List<SMDNode> Nodes    = new List<SMDNode>();
        private List<SMDBone> Skeleton = new List<SMDBone>();
        private List<SMDMesh> Meshes   = new List<SMDMesh>();

        private enum SMDSection
        {
            None,
            Nodes,
            Skeleton,
            Triangles
        }

        public SMD(H3D Scene, int MdlIndex, int AnimIndex = -1)
        {
            int Index = 0;

            if (Scene == null || Scene.Models.Count == 0) return;

            if (MdlIndex != -1 && AnimIndex == -1)
            {
                H3DModel Mdl = Scene.Models[MdlIndex];

                foreach (H3DBone Bone in Mdl.Skeleton)
                {
                    SMDNode Node = new SMDNode
                    {
                        Index       = Index,
                        Name        = Bone.Name,
                        ParentIndex = Bone.ParentIndex
                    };

                    SMDBone B = new SMDBone
                    {
                        NodeIndex   = Index++,
                        Translation = Bone.Translation,
                        Rotation    = Bone.Rotation
                    };

                    Nodes.Add(Node);
                    Skeleton.Add(B);
                }

                foreach (H3DMesh Mesh in Mdl.Meshes)
                {
                    PICAVertex[] Vertices = Mesh.ToVertices();

                    SMDMesh M = new SMDMesh();

                    M.MaterialName = Mdl.Materials[Mesh.MaterialIndex].Texture0Name;

                    foreach (H3DSubMesh SM in Mesh.SubMeshes)
                    {
                        foreach (ushort i in SM.Indices)
                        {
                            PICAVertex v = Vertices[i].Clone();

                            if (Mdl.Skeleton != null &&
                                Mdl.Skeleton.Count > 0 &&
                                SM.Skinning != H3DSubMeshSkinning.Smooth)
                            {
                                int b = SM.BoneIndices[v.Indices[0]];

                                Matrix4x4 Transform = Mdl.Skeleton[b].GetWorldTransform(Mdl.Skeleton);

                                v.Position = Vector4.Transform(new Vector3(
                                    v.Position.X,
                                    v.Position.Y,
                                    v.Position.Z),
                                    Transform);

                                v.Normal.W = 0;

                                v.Normal = Vector4.Transform(v.Normal, Transform);
                                v.Normal = Vector4.Normalize(v.Normal);
                            }

                            M.Vertices.Add(v);
                        }
                    }

                    Meshes.Add(M);
                }
            }
        }

        public SMD(string FileName)
        {
            using (FileStream FS = new FileStream(FileName, FileMode.Open))
            {
                SMDModelImpl(FS);
            }
        }

        public SMD(Stream Stream)
        {
            SMDModelImpl(Stream);
        }

        private void SMDModelImpl(Stream Stream)
        {
            TextReader Reader = new StreamReader(Stream);

            SMDMesh CurrMesh = new SMDMesh();

            SMDSection CurrSection = SMDSection.None;

            int SkeletalFrame = 0;
            int VerticesLine = 0;

            string Line;
            while ((Line = Reader.ReadLine()) != null)
            {
                string[] Params = Line.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

                if (Params.Length > 0)
                {
                    switch (Params[0])
                    {
                        case "version": break;
                        case "nodes": CurrSection = SMDSection.Nodes; break;
                        case "skeleton": CurrSection = SMDSection.Skeleton; break;
                        case "time": SkeletalFrame = int.Parse(Params[1]); break;
                        case "triangles": CurrSection = SMDSection.Triangles; break;
                        case "end": CurrSection = SMDSection.None; break;

                        default:
                            switch (CurrSection)
                            {
                                case SMDSection.Nodes:
                                    int NameStart = Line.IndexOf('"') + 1;
                                    int NameLength = Line.LastIndexOf('"') - NameStart;

                                    Nodes.Add(new SMDNode
                                    {
                                        Index = int.Parse(Params[0]),
                                        Name = Line.Substring(NameStart, NameLength),
                                        ParentIndex = int.Parse(Params[2])
                                    });
                                    break;

                                case SMDSection.Skeleton:
                                    Skeleton.Add(new SMDBone
                                    {
                                        NodeIndex = int.Parse(Params[0]),
                                        Translation = new Vector3
                                        {
                                            X = ParseFloat(Params[1]),
                                            Y = ParseFloat(Params[2]),
                                            Z = ParseFloat(Params[3])
                                        },
                                        Rotation = new Vector3
                                        {
                                            X = ParseFloat(Params[4]),
                                            Y = ParseFloat(Params[5]),
                                            Z = ParseFloat(Params[6])
                                        }
                                    });
                                    break;

                                case SMDSection.Triangles:
                                    if ((VerticesLine++ & 3) == 0)
                                    {
                                        if (CurrMesh.MaterialName != Line)
                                        {
                                            Meshes.Add(CurrMesh = new SMDMesh { MaterialName = Line });
                                        }
                                    }
                                    else
                                    {
                                        PICAVertex Vertex = new PICAVertex();

                                        Vertex.Position.X = ParseFloat(Params[1]);
                                        Vertex.Position.Y = ParseFloat(Params[2]);
                                        Vertex.Position.Z = ParseFloat(Params[3]);

                                        Vertex.Normal.X = ParseFloat(Params[4]);
                                        Vertex.Normal.Y = ParseFloat(Params[5]);
                                        Vertex.Normal.Z = ParseFloat(Params[6]);

                                        Vertex.TexCoord0.X = ParseFloat(Params[7]);
                                        Vertex.TexCoord0.Y = ParseFloat(Params[8]);

                                        if (Params.Length > 9)
                                        {
                                            //NOTE: 3DS formats only supports 4 bones per vertex max
                                            //Warn user when more nodes are used?
                                            int NodesCount = int.Parse(Params[9]);

                                            for (int Node = 0; Node < Math.Min(NodesCount, 4); Node++)
                                            {
                                                Vertex.Indices[Node] = int.Parse(Params[10 + Node * 2]);
                                                Vertex.Weights[Node] = ParseFloat(Params[11 + Node * 2]);
                                            }
                                        }

                                        CurrMesh.Vertices.Add(Vertex);
                                    }
                                    break;
                            }
                            break;
                    }
                }
            }
        }

        private float ParseFloat(string Value)
        {
            return float.Parse(Value, CultureInfo.InvariantCulture);
        }

        public void Save(string FileName)
        {
            StringBuilder SB = new StringBuilder();

            SB.AppendLine("version 1");
            SB.AppendLine("nodes");

            foreach (SMDNode Node in Nodes)
            {
                SB.AppendLine($"{Node.Index} \"{Node.Name}\" {Node.ParentIndex}");
            }

            SB.AppendLine("end");

            SB.AppendLine("skeleton");
            SB.AppendLine("time 0");

            foreach (SMDBone Bone in Skeleton)
            {
                SB.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0} {1} {2} {3} {4} {5} {6}",
                    Bone.NodeIndex,
                    Bone.Translation.X,
                    Bone.Translation.Y,
                    Bone.Translation.Z,
                    Bone.Rotation.X, 
                    Bone.Rotation.Y,
                    Bone.Rotation.Z));
            }

            SB.AppendLine("end");

            SB.AppendLine("triangles");

            foreach (SMDMesh Mesh in Meshes)
            {
                for (int i = 0; i < Mesh.Vertices.Count; i += 3)
                {
                    SB.AppendLine(Mesh.MaterialName);

                    SB.AppendLine(GetSMDVertex(Mesh.Vertices[i + 0]));
                    SB.AppendLine(GetSMDVertex(Mesh.Vertices[i + 1]));
                    SB.AppendLine(GetSMDVertex(Mesh.Vertices[i + 2]));
                }
            }

            SB.AppendLine("end");

            File.WriteAllText(FileName, SB.ToString());
        }

        private string GetSMDVertex(PICAVertex Vtx)
        {
            string Indices = string.Empty;

            int i = 0;

            for (i = 0; i < 4; i++)
            {
                if (Vtx.Weights[i] == 0) break;

                Indices += $" {Vtx.Indices[i]} {Vtx.Weights[i].ToString(CultureInfo.InvariantCulture)}";
            }

            return string.Format(CultureInfo.InvariantCulture, "{0} {1} {2} {3} {4} {5} {6} {7} {8} {9}{10}",
                0,
                Vtx.Position.X,
                Vtx.Position.Y,
                Vtx.Position.Z,
                Vtx.Normal.X,
                Vtx.Normal.Y,
                Vtx.Normal.Z,
                Vtx.TexCoord0.X,
                Vtx.TexCoord0.Y,
                i,
                Indices);
        }

        public H3D ToH3D(string TextureSearchPath = null)
        {
            H3D Output = new H3D();

            H3DModel Model = new H3DModel();

            Model.Name = "Model";

            ushort MaterialIndex = 0;

            if (Skeleton.Count > 0) Model.Flags = H3DModelFlags.HasSkeleton;

            Model.BoneScaling = H3DBoneScaling.Maya;
            Model.MeshNodesVisibility.Add(true);

            foreach (SMDMesh Mesh in Meshes)
            {
                Vector3 MinVector = new Vector3();
                Vector3 MaxVector = new Vector3();

                Dictionary<PICAVertex, int> Vertices = new Dictionary<PICAVertex, int>();

                List<H3DSubMesh> SubMeshes = new List<H3DSubMesh>();

                Queue<PICAVertex> VerticesQueue = new Queue<PICAVertex>();

                foreach (PICAVertex Vertex in Mesh.Vertices)
                {
                    VerticesQueue.Enqueue(Vertex.Clone());
                }

                while (VerticesQueue.Count > 2)
                {
                    List<ushort> Indices     = new List<ushort>();
                    List<ushort> BoneIndices = new List<ushort>();

                    int TriCount = VerticesQueue.Count / 3;

                    while (TriCount-- > 0)
                    {
                        PICAVertex[] Triangle = new PICAVertex[3];

                        Triangle[0] = VerticesQueue.Dequeue();
                        Triangle[1] = VerticesQueue.Dequeue();
                        Triangle[2] = VerticesQueue.Dequeue();

                        List<ushort> TempIndices = new List<ushort>();

                        for (int Tri = 0; Tri < 3; Tri++)
                        {
                            PICAVertex Vertex = Triangle[Tri];

                            for (int i = 0; i < Vertex.Indices.Length; i++)
                            {
                                ushort Index = (ushort)Vertex.Indices[i];

                                if (!(BoneIndices.Contains(Index) || TempIndices.Contains(Index)))
                                {
                                    TempIndices.Add(Index);
                                }
                            }
                        }

                        if (BoneIndices.Count + TempIndices.Count > 20)
                        {
                            VerticesQueue.Enqueue(Triangle[0]);
                            VerticesQueue.Enqueue(Triangle[1]);
                            VerticesQueue.Enqueue(Triangle[2]);

                            continue;
                        }

                        for (int Tri = 0; Tri < 3; Tri++)
                        {
                            PICAVertex Vertex = Triangle[Tri];

                            for (int Index = 0; Index < Vertex.Indices.Length; Index++)
                            {
                                int BoneIndex = BoneIndices.IndexOf((ushort)Vertex.Indices[Index]);

                                if (BoneIndex == -1)
                                {
                                    BoneIndex = BoneIndices.Count;
                                    BoneIndices.Add((ushort)Vertex.Indices[Index]);
                                }

                                Vertex.Indices[Index] = BoneIndex;
                            }

                            if (Vertices.ContainsKey(Vertex))
                            {
                                Indices.Add((ushort)Vertices[Vertex]);
                            }
                            else
                            {
                                Indices.Add((ushort)Vertices.Count);

                                if (Vertex.Position.X < MinVector.X) MinVector.X = Vertex.Position.X;
                                if (Vertex.Position.Y < MinVector.Y) MinVector.Y = Vertex.Position.Y;
                                if (Vertex.Position.Z < MinVector.Z) MinVector.Z = Vertex.Position.Z;

                                if (Vertex.Position.X > MaxVector.X) MaxVector.X = Vertex.Position.X;
                                if (Vertex.Position.Y > MaxVector.Y) MaxVector.Y = Vertex.Position.Y;
                                if (Vertex.Position.Z > MaxVector.Z) MaxVector.Z = Vertex.Position.Z;

                                Vertices.Add(Vertex, Vertices.Count);
                            }
                        }
                    }

                    ushort[] BI = new ushort[20];

                    for (int Index = 0; Index < BoneIndices.Count; Index++)
                    {
                        BI[Index] = BoneIndices[Index];
                    }

                    SubMeshes.Add(new H3DSubMesh
                    {
                        Skinning = H3DSubMeshSkinning.Smooth,
                        BoneIndicesCount = (ushort)BoneIndices.Count,
                        BoneIndices = BI,
                        Indices = Indices.ToArray()
                    });
                }

                //Mesh
                H3DMesh M = new H3DMesh(Vertices.Keys, GetAttributes(), SubMeshes);

                M.Skinning = H3DMeshSkinning.Smooth;
                M.MeshCenter = (MinVector + MaxVector) * 0.5f;
                M.MaterialIndex = MaterialIndex;

                M.UpdateBoolUniforms();

                Model.AddMesh(M);

                //Material
                string MatName = Path.GetFileNameWithoutExtension(Mesh.MaterialName);

                H3DMaterial Material = H3DMaterial.Default;

                Material.Name = $"Mat{MaterialIndex++.ToString("D5")}_{MatName}";
                Material.Texture0Name = MatName;
                Material.MaterialParams.ShaderReference = "0@DefaultShader";
                Material.MaterialParams.ModelReference = $"{Material.Name}@{Model.Name}";
                Material.MaterialParams.LUTDist0TableName = "SpecTable";
                Material.MaterialParams.LUTDist0SamplerName = "SpecSampler";

                Model.Materials.Add(Material);

                if (TextureSearchPath != null && !Output.Textures.Contains(MatName))
                {
                    string TextureFile = Path.Combine(TextureSearchPath, Mesh.MaterialName);

                    if (File.Exists(TextureFile))
                    {
                        Output.Textures.Add(new H3DTexture(TextureFile));
                    }
                }
            }

            Output.LUTs.Add(H3DLUT.CelShading);

            //Build Skeleton
            foreach (SMDBone Bone in Skeleton)
            {
                SMDNode Node = Nodes[Bone.NodeIndex];

                Model.Skeleton.Add(new H3DBone
                {
                    Name        = Node.Name,
                    ParentIndex = (short)Node.ParentIndex,
                    Translation = Bone.Translation,
                    Rotation    = Bone.Rotation,
                    Scale       = Vector3.One
                });
            }

            //Calculate Absolute Inverse Transforms for all bones
            foreach (H3DBone Bone in Model.Skeleton)
            {
                Bone.CalculateTransform(Model.Skeleton);
            }

            Output.Models.Add(Model);

            Output.CopyMaterials();

            return Output;
        }

        private PICAAttribute[] GetAttributes()
        {
            PICAAttribute[] Attributes = new PICAAttribute[5];

            for (int i = 0; i < 3; i++)
            {
                PICAAttributeName Name = default(PICAAttributeName);

                switch (i)
                {
                    case 0: Name = PICAAttributeName.Position; break;
                    case 1: Name = PICAAttributeName.Normal; break;
                    case 2: Name = PICAAttributeName.TexCoord0; break;
                }

                Attributes[i] = new PICAAttribute
                {
                    Name     = Name,
                    Format   = PICAAttributeFormat.Float,
                    Elements = Name == PICAAttributeName.TexCoord0 ? 2 : 3,
                    Scale    = 1
                };
            }

            Attributes[3] = new PICAAttribute
            {
                Name     = PICAAttributeName.BoneIndex,
                Format   = PICAAttributeFormat.Ubyte,
                Elements = 4,
                Scale    = 1
            };

            Attributes[4] = new PICAAttribute
            {
                Name     = PICAAttributeName.BoneWeight,
                Format   = PICAAttributeFormat.Ubyte,
                Elements = 4,
                Scale    = 0.01f
            };

            return Attributes;
        }
    }
}
