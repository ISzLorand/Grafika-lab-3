using Silk.NET.OpenGL;
using Silk.NET.Vulkan;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace GrafikaSzeminarium
{
    internal class ModelObjectDescriptor:IDisposable
    {
        private bool disposedValue;

        public uint Vao { get; private set; }
        public uint Vertices { get; private set; }
        public uint Colors { get; private set; }
        public uint Indices { get; private set; }
        public uint IndexArrayLength { get; private set; }

        private GL Gl;

        public unsafe static ModelObjectDescriptor CreateCube
            (GL Gl, Color front, Color back, Color top, Color bottom, Color left, Color right)
        {
            uint vao = Gl.GenVertexArray();
            Gl.BindVertexArray(vao);

            // counter clockwise is front facing (X, Y, Z, Nx, Ny, Nz)
            var vertexArray = new float[] {
                // top face (0, 1, 0)
                -0.5f,  0.5f,  0.5f,  0f,  1f,  0f,
                 0.5f,  0.5f,  0.5f,  0f,  1f,  0f,
                 0.5f,  0.5f, -0.5f,  0f,  1f,  0f,
                -0.5f,  0.5f, -0.5f,  0f,  1f,  0f,

                // front face (0, 0, 1)
                -0.5f,  0.5f,  0.5f,  0f,  0f,  1f,
                -0.5f, -0.5f,  0.5f,  0f,  0f,  1f,
                 0.5f, -0.5f,  0.5f,  0f,  0f,  1f,
                 0.5f,  0.5f,  0.5f,  0f,  0f,  1f,

                // left face (-1, 0, 0)
                -0.5f,  0.5f,  0.5f, -1f,  0f,  0f,
                -0.5f,  0.5f, -0.5f, -1f,  0f,  0f,
                -0.5f, -0.5f, -0.5f, -1f,  0f,  0f,
                -0.5f, -0.5f,  0.5f, -1f,  0f,  0f,

                // bottom face (0, -1, 0)
                -0.5f, -0.5f,  0.5f,  0f, -1f,  0f,
                 0.5f, -0.5f,  0.5f,  0f, -1f,  0f,
                 0.5f, -0.5f, -0.5f,  0f, -1f,  0f,
                -0.5f, -0.5f, -0.5f,  0f, -1f,  0f,

                // back face (0, 0, -1)
                 0.5f,  0.5f, -0.5f,  0f,  0f, -1f,
                -0.5f,  0.5f, -0.5f,  0f,  0f, -1f,
                -0.5f, -0.5f, -0.5f,  0f,  0f, -1f,
                 0.5f, -0.5f, -0.5f,  0f,  0f, -1f,

                // right face (1, 0, 0)
                 0.5f,  0.5f,  0.5f,  1f,  0f,  0f,
                 0.5f,  0.5f, -0.5f,  1f,  0f,  0f,
                 0.5f, -0.5f, -0.5f,  1f,  0f,  0f,
                 0.5f, -0.5f,  0.5f,  1f,  0f,  0f,
            };

            float[] colorArray = new float[] {
                top.R / 255f, top.G / 255f, top.B / 255f, top.A / 255f,
                top.R / 255f, top.G / 255f, top.B / 255f, top.A / 255f,
                top.R / 255f, top.G / 255f, top.B / 255f, top.A / 255f,
                top.R / 255f, top.G / 255f, top.B / 255f, top.A / 255f,

                front.R / 255f, front.G / 255f, front.B / 255f, front.A / 255f,
                front.R / 255f, front.G / 255f, front.B / 255f, front.A / 255f,
                front.R / 255f, front.G / 255f, front.B / 255f, front.A / 255f,
                front.R / 255f, front.G / 255f, front.B / 255f, front.A / 255f,

                left.R / 255f, left.G / 255f, left.B / 255f, left.A / 255f,
                left.R / 255f, left.G / 255f, left.B / 255f, left.A / 255f,
                left.R / 255f, left.G / 255f, left.B / 255f, left.A / 255f,
                left.R / 255f, left.G / 255f, left.B / 255f, left.A / 255f,

                bottom.R / 255f, bottom.G / 255f, bottom.B / 255f, bottom.A / 255f,
                bottom.R / 255f, bottom.G / 255f, bottom.B / 255f, bottom.A / 255f,
                bottom.R / 255f, bottom.G / 255f, bottom.B / 255f, bottom.A / 255f,
                bottom.R / 255f, bottom.G / 255f, bottom.B / 255f, bottom.A / 255f,

                back.R / 255f, back.G / 255f, back.B / 255f, back.A / 255f,
                back.R / 255f, back.G / 255f, back.B / 255f, back.A / 255f,
                back.R / 255f, back.G / 255f, back.B / 255f, back.A / 255f,
                back.R / 255f, back.G / 255f, back.B / 255f, back.A / 255f,

                right.R / 255f, right.G / 255f, right.B / 255f, right.A / 255f,
                right.R / 255f, right.G / 255f, right.B / 255f, right.A / 255f,
                right.R / 255f, right.G / 255f, right.B / 255f, right.A / 255f,
                right.R / 255f, right.G / 255f, right.B / 255f, right.A / 255f,
            };

            uint[] indexArray = new uint[] {
                0, 1, 2,
                0, 2, 3,

                4, 5, 6,
                4, 6, 7,

                8, 9, 10,
                10, 11, 8,

                12, 14, 13,
                12, 15, 14,

                17, 16, 19,
                17, 19, 18,

                20, 22, 21,
                20, 23, 22
            };

            uint vertices = Gl.GenBuffer();
            Gl.BindBuffer(GLEnum.ArrayBuffer, vertices);
            Gl.BufferData(GLEnum.ArrayBuffer, (ReadOnlySpan<float>)vertexArray.AsSpan(), GLEnum.StaticDraw);

            uint offsetPos = 0;
            uint offsetNormals = offsetPos + 3 * sizeof(float);
            uint vertexSize = offsetNormals + 3 * sizeof(float);

            // 0: pozicio
            Gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, vertexSize, (void*)offsetPos);
            Gl.EnableVertexAttribArray(0);

            // 2: normalvektor
            Gl.VertexAttribPointer(2, 3, VertexAttribPointerType.Float, true, vertexSize, (void*)offsetNormals);
            Gl.EnableVertexAttribArray(2);

            Gl.BindBuffer(GLEnum.ArrayBuffer, 0);

            uint colors = Gl.GenBuffer();
            Gl.BindBuffer(GLEnum.ArrayBuffer, colors);
            Gl.BufferData(GLEnum.ArrayBuffer, (ReadOnlySpan<float>)colorArray.AsSpan(), GLEnum.StaticDraw);
            Gl.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, 0, null);
            Gl.EnableVertexAttribArray(1);
            Gl.BindBuffer(GLEnum.ArrayBuffer, 0);

            uint indices = Gl.GenBuffer();
            Gl.BindBuffer(GLEnum.ElementArrayBuffer, indices);
            Gl.BufferData(GLEnum.ElementArrayBuffer, (ReadOnlySpan<uint>)indexArray.AsSpan(), GLEnum.StaticDraw);
            Gl.BindBuffer(GLEnum.ElementArrayBuffer, 0);

            return new ModelObjectDescriptor() {Vao= vao, Vertices = vertices, Colors = colors, Indices = indices, IndexArrayLength = (uint)indexArray.Length, Gl = Gl};

        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null


                // always unbound the vertex buffer first, so no halfway results are displayed by accident
                Gl.DeleteBuffer(Vertices);
                Gl.DeleteBuffer(Colors);
                Gl.DeleteBuffer(Indices);
                Gl.DeleteVertexArray(Vao);

                disposedValue = true;
            }
        }

        ~ModelObjectDescriptor()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: false);
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
