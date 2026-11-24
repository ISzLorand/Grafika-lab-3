using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Windowing;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using Szeminarium;

namespace GrafikaSzeminarium
{
    internal class Program
    {
        private static IWindow graphicWindow;

        private static GL Gl;

        private static ImGuiController imGuiController;

        private static ModelObjectDescriptor plankFlatNormals;
        private static ModelObjectDescriptor plankAngledNormals;

        private static CameraDescriptor camera = new CameraDescriptor();

        private static CubeArrangementModel cubeArrangementModel = new CubeArrangementModel();

        private const string ModelMatrixVariableName = "uModel";
        private const string NormalMatrixVariableName = "uNormal";
        private const string ViewMatrixVariableName = "uView";
        private const string ProjectionMatrixVariableName = "uProjection";

        private const string LightColorVariableName = "uLightColor";
        private const string LightPositionVariableName = "uLightPos";
        private const string ViewPositionVariableName = "uViewPos";

        private const string ShinenessVariableName = "uShininess";

        private static float shininess = 50;

        // 0 = Phong, 1 = Gouraud
        private static int shadingMode = 0;

        private static uint phongProgram;
        private static uint gouraudProgram;

        private static uint ActiveProgram => shadingMode == 0 ? phongProgram : gouraudProgram;

        static void Main(string[] args)
        {
            WindowOptions windowOptions = WindowOptions.Default;
            windowOptions.Title = "Grafika szeminárium";
            windowOptions.Size = new Silk.NET.Maths.Vector2D<int>(500, 500);

            graphicWindow = Window.Create(windowOptions);

            graphicWindow.Load += GraphicWindow_Load;
            graphicWindow.Update += GraphicWindow_Update;
            graphicWindow.Render += GraphicWindow_Render;
            graphicWindow.Closing += GraphicWindow_Closing;

            graphicWindow.Run();
        }

        private static void GraphicWindow_Closing()
        {
            plankAngledNormals.Dispose();
            plankFlatNormals.Dispose();
            Gl.DeleteProgram(phongProgram);
            Gl.DeleteProgram(gouraudProgram);
        }

        private static void GraphicWindow_Load()
        {
            Gl = graphicWindow.CreateOpenGL();

            var inputContext = graphicWindow.CreateInput();
            foreach (var keyboard in inputContext.Keyboards)
            {
                keyboard.KeyDown += Keyboard_KeyDown;
            }

            graphicWindow.FramebufferResize += s =>
            {
                Gl.Viewport(s);
            };

            imGuiController = new ImGuiController(Gl, graphicWindow, inputContext);

            plankFlatNormals = ModelObjectDescriptor.CreatePlank(Gl, smoothEdgeNormals: false);
            plankAngledNormals = ModelObjectDescriptor.CreatePlank(Gl, smoothEdgeNormals: true);

            Gl.ClearColor(System.Drawing.Color.White);

            Gl.Enable(EnableCap.CullFace);
            Gl.CullFace(TriangleFace.Back);

            Gl.Enable(EnableCap.DepthTest);
            Gl.DepthFunc(DepthFunction.Lequal);

            phongProgram = CreateShaderProgram(
                "Shaders.VertexShaderPhong.vert",
                "Shaders.FragmentShaderPhong.frag");

            gouraudProgram = CreateShaderProgram(
                "Shaders.VertexShaderGouraud.vert",
                "Shaders.FragmentShaderGouraud.frag");
        }

        private static uint CreateShaderProgram(string vertexResourcePath, string fragmentResourcePath)
        {
            uint vshader = Gl.CreateShader(ShaderType.VertexShader);
            uint fshader = Gl.CreateShader(ShaderType.FragmentShader);

            Gl.ShaderSource(vshader, GetEmbeddedResourceAsString(vertexResourcePath));
            Gl.CompileShader(vshader);
            Gl.GetShader(vshader, ShaderParameterName.CompileStatus, out int vStatus);
            if (vStatus != (int)GLEnum.True)
                throw new Exception($"Vertex shader ({vertexResourcePath}) failed to compile: " + Gl.GetShaderInfoLog(vshader));

            Gl.ShaderSource(fshader, GetEmbeddedResourceAsString(fragmentResourcePath));
            Gl.CompileShader(fshader);
            Gl.GetShader(fshader, ShaderParameterName.CompileStatus, out int fStatus);
            if (fStatus != (int)GLEnum.True)
                throw new Exception($"Fragment shader ({fragmentResourcePath}) failed to compile: " + Gl.GetShaderInfoLog(fshader));

            uint shaderProgram = Gl.CreateProgram();
            Gl.AttachShader(shaderProgram, vshader);
            Gl.AttachShader(shaderProgram, fshader);
            Gl.LinkProgram(shaderProgram);

            Gl.DetachShader(shaderProgram, vshader);
            Gl.DetachShader(shaderProgram, fshader);
            Gl.DeleteShader(vshader);
            Gl.DeleteShader(fshader);

            Gl.GetProgram(shaderProgram, GLEnum.LinkStatus, out var status);
            if (status == 0)
            {
                throw new Exception($"Error linking shader ({vertexResourcePath} + {fragmentResourcePath}): {Gl.GetProgramInfoLog(shaderProgram)}");
            }

            CheckError();
            return shaderProgram;
        }

        private static string GetEmbeddedResourceAsString(string resourceRelativePath)
        {
            var assembly = Assembly.GetExecutingAssembly();
            string normalizedSuffix = resourceRelativePath.Replace('/', '.').Replace('\\', '.');

            string resourceFullPath = assembly.GetManifestResourceNames()
                .FirstOrDefault(name => name.EndsWith(normalizedSuffix, StringComparison.OrdinalIgnoreCase))
                ?? throw new FileNotFoundException(
                    $"Embedded resource not found ending with '{normalizedSuffix}'. Available resources: " +
                    string.Join(", ", assembly.GetManifestResourceNames()));

            using var resStream = assembly.GetManifestResourceStream(resourceFullPath)!;
            using var resStreamReader = new StreamReader(resStream);
            return resStreamReader.ReadToEnd();
        }

        private static void Keyboard_KeyDown(IKeyboard keyboard, Key key, int arg3)
        {
            switch (key)
            {
                case Key.Left:
                    camera.DecreaseZYAngle();
                    break;
                case Key.Right:
                    camera.IncreaseZYAngle();
                    break;
                case Key.Down:
                    camera.IncreaseDistance();
                    break;
                case Key.Up:
                    camera.DecreaseDistance();
                    break;
                case Key.U:
                    camera.IncreaseZXAngle();
                    break;
                case Key.D:
                    camera.DecreaseZXAngle();
                    break;
                case Key.Space:
                    cubeArrangementModel.AnimationEnabled = !cubeArrangementModel.AnimationEnabled;
                    break;
            }
        }

        private static void GraphicWindow_Update(double deltaTime)
        {
            cubeArrangementModel.AdvanceTime(deltaTime);

            imGuiController.Update((float)deltaTime);
        }

        private static unsafe void GraphicWindow_Render(double deltaTime)
        {
            Gl.Clear(ClearBufferMask.ColorBufferBit);
            Gl.Clear(ClearBufferMask.DepthBufferBit);

            Gl.UseProgram(ActiveProgram);

            SetUniform3(LightColorVariableName, new Vector3(1f, 1f, 1f));
            SetUniform3(LightPositionVariableName, new Vector3(camera.Position.X, camera.Position.Y, camera.Position.Z));
            SetUniform3(ViewPositionVariableName, new Vector3(camera.Position.X, camera.Position.Y, camera.Position.Z));
            SetUniform1(ShinenessVariableName, shininess);

            var viewMatrix = Matrix4X4.CreateLookAt(camera.Position, camera.Target, camera.UpVector);
            SetMatrix(viewMatrix, ViewMatrixVariableName);

            var projectionMatrix = Matrix4X4.CreatePerspectiveFieldOfView<float>((float)(Math.PI / 2), 1024f / 768f, 0.1f, 100f);
            SetMatrix(projectionMatrix, ProjectionMatrixVariableName);

            // Felso dezsa: 10 fokkal kifele dontott normalisokkal
            DrawTub(plankAngledNormals, new Vector3D<float>(0f, 1.2f, 0f));

            // Also dezsa: lapra meroleges normalisokkal
            DrawTub(plankFlatNormals, new Vector3D<float>(0f, -1.2f, 0f));

            ImGuiNET.ImGui.Begin("Lighting", ImGuiNET.ImGuiWindowFlags.AlwaysAutoResize | ImGuiNET.ImGuiWindowFlags.NoCollapse);
            ImGuiNET.ImGui.Text("Shading Model");
            ImGuiNET.ImGui.RadioButton("Phong", ref shadingMode, 0);
            ImGuiNET.ImGui.SameLine();
            ImGuiNET.ImGui.RadioButton("Gouraud", ref shadingMode, 1);
            ImGuiNET.ImGui.Separator();
            ImGuiNET.ImGui.SliderFloat("Shininess", ref shininess, 5, 100);
            ImGuiNET.ImGui.End();

            imGuiController.Render();
        }

        private static unsafe void SetModelMatrix(Matrix4X4<float> modelMatrix)
        {
            SetMatrix(modelMatrix, ModelMatrixVariableName);

            int location = Gl.GetUniformLocation(ActiveProgram, NormalMatrixVariableName);
            if (location == -1)
            {
                throw new Exception($"{NormalMatrixVariableName} uniform not found on shader.");
            }

            var modelMatrixWithoutTranslation = new Matrix4X4<float>(modelMatrix.Row1, modelMatrix.Row2, modelMatrix.Row3, modelMatrix.Row4);
            modelMatrixWithoutTranslation.M41 = 0;
            modelMatrixWithoutTranslation.M42 = 0;
            modelMatrixWithoutTranslation.M43 = 0;
            modelMatrixWithoutTranslation.M44 = 1;

            Matrix4X4<float> modelInvers;
            Matrix4X4.Invert<float>(modelMatrixWithoutTranslation, out modelInvers);
            Matrix3X3<float> normalMatrix = new Matrix3X3<float>(Matrix4X4.Transpose(modelInvers));

            Gl.UniformMatrix3(location, 1, false, (float*)&normalMatrix);
            CheckError();
        }

        private static unsafe void SetUniform1(string uniformName, float uniformValue)
        {
            int location = Gl.GetUniformLocation(ActiveProgram, uniformName);
            if (location == -1)
            {
                throw new Exception($"{uniformName} uniform not found on shader.");
            }

            Gl.Uniform1(location, uniformValue);
            CheckError();
        }

        private static unsafe void SetUniform3(string uniformName, Vector3 uniformValue)
        {
            int location = Gl.GetUniformLocation(ActiveProgram, uniformName);
            if (location == -1)
            {
                throw new Exception($"{uniformName} uniform not found on shader.");
            }

            Gl.Uniform3(location, uniformValue);
            CheckError();
        }

        private static unsafe void DrawModelObject(ModelObjectDescriptor modelObject)
        {
            Gl.BindVertexArray(modelObject.Vao);
            Gl.BindBuffer(GLEnum.ElementArrayBuffer, modelObject.Indices);
            Gl.DrawElements(PrimitiveType.Triangles, modelObject.IndexArrayLength, DrawElementsType.UnsignedInt, null);
            Gl.BindBuffer(GLEnum.ElementArrayBuffer, 0);
            Gl.BindVertexArray(0);
        }

        private static unsafe void SetMatrix(Matrix4X4<float> mx, string uniformName)
        {
            int location = Gl.GetUniformLocation(ActiveProgram, uniformName);
            if (location == -1)
            {
                throw new Exception($"{uniformName} uniform not found on shader.");
            }

            Gl.UniformMatrix4(location, 1, false, (float*)&mx);
            CheckError();
        }

        public static void CheckError()
        {
            var error = (ErrorCode)Gl.GetError();
            if (error != ErrorCode.NoError)
                throw new Exception("GL.GetError() returned " + error.ToString());
        }

        private static void DrawTub(ModelObjectDescriptor plankModel, Vector3D<float> tubCenter)
        {
            const int plankCount = 18;
            const float plankWidth = 1.0f;
            float stepAngleRad = MathF.Tau / plankCount;
            float halfAngleRad = stepAngleRad / 2.0f;

            float idealZDistance = (plankWidth * 0.5f) / MathF.Tan(halfAngleRad);

            var translateToIdealRadius = Matrix4X4.CreateTranslation(0f, 0f, idealZDistance);
            var translateTubToWorld = Matrix4X4.CreateTranslation(tubCenter);

            for (int i = 0; i < plankCount; i++)
            {
                var rotY = Matrix4X4.CreateRotationY(i * stepAngleRad);
                var modelMatrix = translateToIdealRadius * rotY * translateTubToWorld;

                SetModelMatrix(modelMatrix);
                DrawModelObject(plankModel);
            }
        }
    }
}