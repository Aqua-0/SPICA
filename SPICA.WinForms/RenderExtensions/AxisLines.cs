﻿using OpenTK;
using OpenTK.Graphics.ES30;

using SPICA.Renderer;

using System;

namespace SPICA.WinForms.RenderExtensions
{
    class AxisLines : TransformableObject, IDisposable
    {
        const int LinesCount = 102;

        private int VBOHandle;
        private int VAOHandle;

        public bool Visible;

        public AxisLines()
        {
            Vector4[] Buffer = new Vector4[]
            {
                new Vector4(0), new Vector4(1, 0, 0, 1), new Vector4(20,  0,  0, 1), new Vector4(1, 0, 0, 1),
                new Vector4(0), new Vector4(0, 1, 0, 1), new Vector4( 0, 20,  0, 1), new Vector4(0, 1, 0, 1),
                new Vector4(0), new Vector4(0, 0, 1, 1), new Vector4( 0,  0, 20, 1), new Vector4(0, 0, 1, 1)
            };

            VBOHandle = GL.GenBuffer();
            VAOHandle = GL.GenVertexArray();

            GL.BindBuffer(BufferTarget.ArrayBuffer, VBOHandle);
            GL.BufferData(BufferTarget.ArrayBuffer, new IntPtr(Buffer.Length * 16), Buffer, BufferUsageHint.StaticDraw);
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);

            GL.BindVertexArray(VAOHandle);

            GL.EnableVertexAttribArray(0);
            GL.EnableVertexAttribArray(3);

            GL.BindBuffer(BufferTarget.ArrayBuffer, VBOHandle);

            GL.VertexAttribPointer(0, 4, VertexAttribPointerType.Float, false, 32, 0);
            GL.VertexAttribPointer(3, 4, VertexAttribPointerType.Float, false, 32, 16);

            GL.BindVertexArray(0);
        }

        public void Render(int ShaderHandle)
        {
            if (Visible)
            {
                GL.UseProgram(ShaderHandle);

                RenderUtils.SetupShaderForPosCol(ShaderHandle);

                GL.UniformMatrix4(GL.GetUniformLocation(ShaderHandle, "ModelMatrix"), false, ref Transform);

                GL.LineWidth(2);

                GL.Disable(EnableCap.CullFace);
                GL.Disable(EnableCap.StencilTest);
                GL.Disable(EnableCap.DepthTest);
                GL.Disable(EnableCap.Blend);

                GL.DepthFunc(DepthFunction.Always);

                GL.BindVertexArray(VAOHandle);
                GL.DrawArrays(PrimitiveType.Lines, 0, 6);
                GL.BindVertexArray(0);
            }
        }

        private bool Disposed;

        protected virtual void Dispose(bool Disposing)
        {
            if (!Disposed)
            {
                GL.DeleteBuffer(VBOHandle);
                GL.DeleteVertexArray(VAOHandle);

                Disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }
    }
}