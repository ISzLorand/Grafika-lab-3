using Silk.NET.OpenGL;
using Silk.NET.Vulkan;
using System;
using System.Collections.Generic;
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

        public unsafe static ModelObjectDescriptor CreatePlank(GL gl, bool smoothEdgeNormals)
        {
            uint vao = gl.GenVertexArray();
            gl.BindVertexArray(vao);

            float halfWidth = 0.5f;   // teljes szelesseg: 1 egyseg
            float halfHeight = 1.0f;  // teljes magassag: 2 egyseg
            float thickness = 0.0f;  // vekony deszka vastagsag a Z tengelyen

            float angle10 = MathF.PI / 18f; // 10 fok radianban
            float sin10 = MathF.Sin(angle10);
            float cos10 = MathF.Cos(angle10);

            // Kulso lap (front face, CCW) normalisai:
            // Lapra meroleges: (0, 0, 1)
            // 10 fokkal kifele dontott X es Z komponens a bal (-X) es jobb (+X) oldalon:
            float leftNx = smoothEdgeNormals ? -sin10 : 0f;
            float rightNx = smoothEdgeNormals ? sin10 : 0f;
            float frontNz = smoothEdgeNormals ? cos10 : 1f;

            float[] vertexArray = new float[]
            {
                // Kulso (front) lap (z = +halfDepth), CCW nezve +Z felol
                -halfWidth,  halfHeight, thickness,   leftNx,  0f, frontNz, // 0: bal felso
                -halfWidth, -halfHeight, thickness,   leftNx,  0f, frontNz, // 1: bal also
                 halfWidth, -halfHeight, thickness,   rightNx, 0f, frontNz, // 2: jobb also
                 halfWidth,  halfHeight, thickness,   rightNx, 0f, frontNz, // 3: jobb felso

                // Belso (back) lap (z = -halfDepth), CCW nezve -Z felol
                 halfWidth,  halfHeight, -thickness,  0f, 0f, -1f,          // 4
                 halfWidth, -halfHeight, -thickness,  0f, 0f, -1f,          // 5
                -halfWidth, -halfHeight, -thickness,  0f, 0f, -1f,          // 6
                -halfWidth,  halfHeight, -thickness,  0f, 0f, -1f,          // 7
            };

            float[] colorArray = new float[]
            {
                0.75f, 0.45f, 0.20f, 1.0f,
                0.75f, 0.45f, 0.20f, 1.0f,
                0.75f, 0.45f, 0.20f, 1.0f,
                0.75f, 0.45f, 0.20f, 1.0f,

                0.55f, 0.30f, 0.12f, 1.0f,
                0.55f, 0.30f, 0.12f, 1.0f,
                0.55f, 0.30f, 0.12f, 1.0f,
                0.55f, 0.30f, 0.12f, 1.0f,
            };

            uint[] indexArray = new uint[]
            {
                0, 1, 2,
                0, 2, 3,

                4, 5, 6,
                4, 6, 7
            };

            uint vertices = gl.GenBuffer();
            gl.BindBuffer(GLEnum.ArrayBuffer, vertices);
            gl.BufferData(GLEnum.ArrayBuffer, (ReadOnlySpan<float>)vertexArray.AsSpan(), GLEnum.StaticDraw);

            uint offsetPos = 0;
            uint offsetNormals = offsetPos + 3 * sizeof(float);
            uint vertexSize = offsetNormals + 3 * sizeof(float);

            gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, vertexSize, (void*)offsetPos);
            gl.EnableVertexAttribArray(0);

            gl.VertexAttribPointer(2, 3, VertexAttribPointerType.Float, true, vertexSize, (void*)offsetNormals);
            gl.EnableVertexAttribArray(2);
            gl.BindBuffer(GLEnum.ArrayBuffer, 0);

            uint colors = gl.GenBuffer();
            gl.BindBuffer(GLEnum.ArrayBuffer, colors);
            gl.BufferData(GLEnum.ArrayBuffer, (ReadOnlySpan<float>)colorArray.AsSpan(), GLEnum.StaticDraw);
            gl.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, 0, null);
            gl.EnableVertexAttribArray(1);
            gl.BindBuffer(GLEnum.ArrayBuffer, 0);

            uint indices = gl.GenBuffer();
            gl.BindBuffer(GLEnum.ElementArrayBuffer, indices);
            gl.BufferData(GLEnum.ElementArrayBuffer, (ReadOnlySpan<uint>)indexArray.AsSpan(), GLEnum.StaticDraw);
            gl.BindBuffer(GLEnum.ElementArrayBuffer, 0);

            return new ModelObjectDescriptor
            {
                Vao = vao,
                Vertices = vertices,
                Colors = colors,
                Indices = indices,
                IndexArrayLength = (uint)indexArray.Length,
                Gl = gl
            };
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
