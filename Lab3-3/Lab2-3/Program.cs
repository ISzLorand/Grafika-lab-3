using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Windowing;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Numerics;
using Szeminarium;
using System.IO;
using System.Reflection;

namespace GrafikaSzeminarium
{
    internal class Program
    {
        private static IWindow graphicWindow;
        private static IInputContext inputContext;
        private static GL Gl;
        private static ImGuiController imGuiController;

        private static ModelObjectDescriptor[] cubes;
        private static Matrix4X4<float>[] cubieTransforms;

        private static CameraDescriptor camera;

        private const string ModelMatrixVariableName = "uModel";
        private const string SliceMatrixVariableName = "uSlice";
        private const string NormalMatrixVariableName = "uNormal";
        private const string ViewMatrixVariableName = "uView";
        private const string ProjectionMatrixVariableName = "uProjection";

        private const string LightColorVariableName = "uLightColor";
        private const string LightPositionVariableName = "uLightPos";
        private const string ViewPositionVariableName = "uViewPos";
        private const string ShininessVariableName = "uShininess";

        // Phong fenyforras es UI allapotok
        private static Vector3 lightColor = new Vector3(1.0f, 1.0f, 1.0f);
        private static string lightPosXText = "4.0";
        private static string lightPosYText = "5.0";
        private static string lightPosZText = "6.0";
        private static float shininess = 40.0f;

        private const float Spacing = 1.05f;
        private const float NormalRotationSpeed = 6.0f;
        private const float ShuffleRotationSpeed = 16.0f;

        // Input buffer a folyamatos billentyuzet-kezeleshez
        private static HashSet<Key> pressedKeys = new HashSet<Key>();

        // Eger kezeles
        private static Vector2 lastMousePos;
        private static bool firstMouseMove = true;

        // Mod kapcsolok
        private static bool isSolveMode = false;
        private static bool wasUnsolved = false;
        private static bool isCelebrating = false;
        private static double celebrationTime = 0.0;

        // Oldalak azonositoi
        public enum Face { Red, Orange, Green, Blue, Yellow, White }
        public enum RotationAxis { X, Y, Z }

        public class CubeMove
        {
            public Face TargetFace;
            public bool Clockwise;
            public float Speed;
        }

        private static Queue<CubeMove> moveQueue = new Queue<CubeMove>();
        private static bool isAnimating = false;
        private static RotationAxis currentAxis;
        private static float currentAngle = 0.0f;
        private static float targetAngle = 0.0f;
        private static float currentSpeed = NormalRotationSpeed;
        private static List<int> currentActiveIndices = new List<int>();

        private static uint program;

        private static string GetEmbeddedResourceAsString(string resourceRelativePath)
        {
            string resourceFullPath = Assembly.GetExecutingAssembly().GetName().Name + "." + resourceRelativePath;

            using var resStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceFullPath)
                ?? throw new FileNotFoundException($"Embedded resource not found: {resourceFullPath}");
            using var resStreamReader = new StreamReader(resStream);
            return resStreamReader.ReadToEnd();
        }

        static void Main(string[] args)
        {
            WindowOptions windowOptions = WindowOptions.Default;
            windowOptions.Title = "Grafika szeminarium - Rubik Kocka (Phong)";
            windowOptions.Size = new Vector2D<int>(900, 750);

            graphicWindow = Window.Create(windowOptions);

            graphicWindow.Load += GraphicWindow_Load;
            graphicWindow.Update += GraphicWindow_Update;
            graphicWindow.Render += GraphicWindow_Render;
            graphicWindow.Closing += GraphicWindow_Closing;

            graphicWindow.Run();
        }

        private static void GraphicWindow_Closing()
        {
            imGuiController?.Dispose();
            foreach (ModelObjectDescriptor cube in cubes)
            {
                cube.Dispose();
            }
            Gl.DeleteProgram(program);
        }

        private static void GraphicWindow_Load()
        {
            Gl = graphicWindow.CreateOpenGL();

            inputContext = graphicWindow.CreateInput();
            foreach (var keyboard in inputContext.Keyboards)
            {
                keyboard.KeyDown += Keyboard_KeyDown;
                keyboard.KeyUp += Keyboard_KeyUp;
            }

            foreach (var mouse in inputContext.Mice)
            {
                mouse.MouseMove += Mouse_MouseMove;
            }

            graphicWindow.FramebufferResize += s =>
            {
                Gl.Viewport(s);
            };

            imGuiController = new ImGuiController(Gl, graphicWindow, inputContext);

            // Indulasnal Solve Mode-ban indulunk, hogy a kurzor azonnal latszodjon a UI hasznalatahoz
            isSolveMode = true;
            SetCursorMode(CursorMode.Normal);

            camera = new CameraDescriptor(new Vector3D<float>(4.2f, 3.2f, 4.2f));

            cubes = new ModelObjectDescriptor[27];
            cubieTransforms = new Matrix4X4<float>[27];

            int ind = 0;
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    for (int k = 0; k < 3; k++)
                    {
                        Color front = Color.Black, back = Color.Black;
                        Color top = Color.Black, bottom = Color.Black;
                        Color left = Color.Black, right = Color.Black;

                        if (i == 0) back = Color.Orange;
                        if (i == 2) front = Color.Red;

                        if (j == 0) left = Color.Blue;
                        if (j == 2) right = Color.Green;

                        if (k == 0) bottom = Color.WhiteSmoke;
                        if (k == 2) top = Color.Yellow;

                        cubes[ind] = ModelObjectDescriptor.CreateCube(Gl,
                            front, back, top, bottom, left, right);

                        float posZ = (i - 1) * Spacing;
                        float posX = (j - 1) * Spacing;
                        float posY = (k - 1) * Spacing;

                        cubieTransforms[ind] = Matrix4X4.CreateTranslation(posX, posY, posZ);
                        ind++;
                    }
                }
            }

            Gl.ClearColor(Color.FromArgb(20, 20, 25));
            Gl.Enable(EnableCap.CullFace);
            Gl.CullFace(TriangleFace.Back);

            Gl.Enable(EnableCap.DepthTest);
            Gl.DepthFunc(DepthFunction.Lequal);

            uint vshader = Gl.CreateShader(ShaderType.VertexShader);
            uint fshader = Gl.CreateShader(ShaderType.FragmentShader);

            Gl.ShaderSource(vshader, GetEmbeddedResourceAsString("Shaders.VertexShader.vert"));
            Gl.CompileShader(vshader);
            Gl.GetShader(vshader, ShaderParameterName.CompileStatus, out int vStatus);
            if (vStatus != (int)GLEnum.True)
                throw new Exception("Vertex shader hiba: " + Gl.GetShaderInfoLog(vshader));

            Gl.ShaderSource(fshader, GetEmbeddedResourceAsString("Shaders.FragmentShader.frag"));
            Gl.CompileShader(fshader);
            Gl.GetShader(fshader, ShaderParameterName.CompileStatus, out int fStatus);
            if (fStatus != (int)GLEnum.True)
                throw new Exception("Fragment shader hiba: " + Gl.GetShaderInfoLog(fshader));

            program = Gl.CreateProgram();
            Gl.AttachShader(program, vshader);
            Gl.AttachShader(program, fshader);
            Gl.LinkProgram(program);

            Gl.DetachShader(program, vshader);
            Gl.DetachShader(program, fshader);
            Gl.DeleteShader(vshader);
            Gl.DeleteShader(fshader);

            Console.WriteLine("Iranyitas:");
            Console.WriteLine("- TAB: Valts szabad kamera (WASD + Eger) es UI / Forgato mod kozott");
            Console.WriteLine("- UI gombokkal vagy billentyukkel (R, O, G, B, Y, W) forgathato a kocka");
        }

        private static void SetCursorMode(CursorMode mode)
        {
            if (inputContext == null) return;
            foreach (var mouse in inputContext.Mice)
            {
                if (mouse.Cursor.IsSupported(mode))
                {
                    mouse.Cursor.CursorMode = mode;
                }
                else if (mode == CursorMode.Raw && mouse.Cursor.IsSupported(CursorMode.Hidden))
                {
                    mouse.Cursor.CursorMode = CursorMode.Hidden;
                }
            }
        }

        private static void Mouse_MouseMove(IMouse mouse, Vector2 position)
        {
            if (isSolveMode) return;

            if (firstMouseMove)
            {
                lastMousePos = position;
                firstMouseMove = false;
                return;
            }

            float deltaX = position.X - lastMousePos.X;
            float deltaY = position.Y - lastMousePos.Y;
            lastMousePos = position;

            float sensitivity = 0.0025f;
            camera.AddYaw(-deltaX * sensitivity);
            camera.AddPitch(-deltaY * sensitivity);
        }

        private static void Keyboard_KeyDown(IKeyboard keyboard, Key key, int arg3)
        {
            pressedKeys.Add(key);

            if (key == Key.Tab)
            {
                isSolveMode = !isSolveMode;
                SetCursorMode(isSolveMode ? CursorMode.Normal : CursorMode.Raw);
                firstMouseMove = true;
                return;
            }

            if (key == Key.Escape)
            {
                Environment.Exit(0);
                return;
            }

            // Ha szovegmezobe gepelunk az ImGui-ban, ne induljanak forgatasok
            if (ImGuiNET.ImGui.GetIO().WantTextInput)
                return;

            if (isSolveMode)
            {
                if (key == Key.Space)
                {
                    StartShuffle();
                    return;
                }

                bool shift = pressedKeys.Contains(Key.ShiftLeft) || pressedKeys.Contains(Key.ShiftRight);
                bool clockwise = !shift;

                switch (key)
                {
                    case Key.R: EnqueueMove(Face.Red, clockwise, NormalRotationSpeed); break;
                    case Key.O: EnqueueMove(Face.Orange, clockwise, NormalRotationSpeed); break;
                    case Key.G: EnqueueMove(Face.Green, clockwise, NormalRotationSpeed); break;
                    case Key.B: EnqueueMove(Face.Blue, clockwise, NormalRotationSpeed); break;
                    case Key.Y: EnqueueMove(Face.Yellow, clockwise, NormalRotationSpeed); break;
                    case Key.W: EnqueueMove(Face.White, clockwise, NormalRotationSpeed); break;
                }
            }
        }

        private static void Keyboard_KeyUp(IKeyboard keyboard, Key key, int arg3)
        {
            pressedKeys.Remove(key);
        }

        private static void EnqueueMove(Face face, bool clockwise, float speed)
        {
            isCelebrating = false;
            moveQueue.Enqueue(new CubeMove { TargetFace = face, Clockwise = clockwise, Speed = speed });
        }

        private static void StartShuffle()
        {
            isCelebrating = false;
            wasUnsolved = true;
            moveQueue.Clear();

            Random rand = new Random();
            Face[] faces = (Face[])Enum.GetValues(typeof(Face));

            for (int i = 0; i < 30; i++)
            {
                Face randomFace = faces[rand.Next(faces.Length)];
                bool randomCw = rand.Next(2) == 0;
                moveQueue.Enqueue(new CubeMove
                {
                    TargetFace = randomFace,
                    Clockwise = randomCw,
                    Speed = ShuffleRotationSpeed
                });
            }
        }

        private static void GraphicWindow_Update(double deltaTime)
        {
            if (!isSolveMode)
            {
                float moveSpeed = 4.0f * (float)deltaTime;

                if (pressedKeys.Contains(Key.W)) camera.MoveForward(moveSpeed);
                if (pressedKeys.Contains(Key.S)) camera.MoveBackward(moveSpeed);
                if (pressedKeys.Contains(Key.A)) camera.StrafeLeft(moveSpeed);
                if (pressedKeys.Contains(Key.D)) camera.StrafeRight(moveSpeed);
                if (pressedKeys.Contains(Key.Space)) camera.StrafeUp(moveSpeed);
                if (pressedKeys.Contains(Key.ShiftLeft) || pressedKeys.Contains(Key.ControlLeft)) camera.StrafeDown(moveSpeed);

                float turnSpeed = 2.0f * (float)deltaTime;
                if (pressedKeys.Contains(Key.Left)) camera.AddYaw(turnSpeed);
                if (pressedKeys.Contains(Key.Right)) camera.AddYaw(-turnSpeed);
            }

            if (isAnimating)
            {
                float step = currentSpeed * (float)deltaTime;

                if (targetAngle > 0)
                {
                    currentAngle += step;
                    if (currentAngle >= targetAngle) FinishCurrentMove();
                }
                else
                {
                    currentAngle -= step;
                    if (currentAngle <= targetAngle) FinishCurrentMove();
                }
            }
            else if (moveQueue.Count > 0)
            {
                StartNextMove();
            }
            else if (wasUnsolved && !isCelebrating && CheckSolved())
            {
                isCelebrating = true;
                wasUnsolved = false;
                celebrationTime = 0.0;
            }

            if (isCelebrating)
            {
                celebrationTime += deltaTime;
            }

            imGuiController.Update((float)deltaTime);
        }

        private static void StartNextMove()
        {
            var move = moveQueue.Dequeue();
            isAnimating = true;
            currentAngle = 0.0f;
            currentSpeed = move.Speed;

            float halfPi = (float)(Math.PI / 2.0);
            float sign = move.Clockwise ? -1.0f : 1.0f;

            switch (move.TargetFace)
            {
                case Face.Red: currentAxis = RotationAxis.Z; targetAngle = sign * halfPi; break;
                case Face.Orange: currentAxis = RotationAxis.Z; targetAngle = -sign * halfPi; break;
                case Face.Green: currentAxis = RotationAxis.X; targetAngle = sign * halfPi; break;
                case Face.Blue: currentAxis = RotationAxis.X; targetAngle = -sign * halfPi; break;
                case Face.Yellow: currentAxis = RotationAxis.Y; targetAngle = sign * halfPi; break;
                case Face.White: currentAxis = RotationAxis.Y; targetAngle = -sign * halfPi; break;
            }

            currentActiveIndices.Clear();
            float threshold = 0.5f * Spacing;

            for (int idx = 0; idx < 27; idx++)
            {
                float x = cubieTransforms[idx].M41;
                float y = cubieTransforms[idx].M42;
                float z = cubieTransforms[idx].M43;

                bool belongs = move.TargetFace switch
                {
                    Face.Red => z > threshold,
                    Face.Orange => z < -threshold,
                    Face.Green => x > threshold,
                    Face.Blue => x < -threshold,
                    Face.Yellow => y > threshold,
                    Face.White => y < -threshold,
                    _ => false
                };

                if (belongs) currentActiveIndices.Add(idx);
            }
        }

        private static void FinishCurrentMove()
        {
            isAnimating = false;
            currentAngle = 0.0f;

            Matrix4X4<float> finalRot = CreateAxisRotation(currentAxis, targetAngle);

            foreach (int idx in currentActiveIndices)
            {
                cubieTransforms[idx] = cubieTransforms[idx] * finalRot;
                SnapMatrix(ref cubieTransforms[idx], Spacing);
            }

            currentActiveIndices.Clear();

            if (!CheckSolved())
            {
                wasUnsolved = true;
            }
        }

        private static Matrix4X4<float> CreateAxisRotation(RotationAxis axis, float angle)
        {
            return axis switch
            {
                RotationAxis.X => Matrix4X4.CreateRotationX(angle),
                RotationAxis.Y => Matrix4X4.CreateRotationY(angle),
                RotationAxis.Z => Matrix4X4.CreateRotationZ(angle),
                _ => Matrix4X4<float>.Identity
            };
        }

        private static void SnapMatrix(ref Matrix4X4<float> mat, float spacing)
        {
            mat.M11 = MathF.Round(mat.M11); mat.M12 = MathF.Round(mat.M12); mat.M13 = MathF.Round(mat.M13);
            mat.M21 = MathF.Round(mat.M21); mat.M22 = MathF.Round(mat.M22); mat.M23 = MathF.Round(mat.M23);
            mat.M31 = MathF.Round(mat.M31); mat.M32 = MathF.Round(mat.M32); mat.M33 = MathF.Round(mat.M33);

            mat.M41 = MathF.Round(mat.M41 / spacing) * spacing;
            mat.M42 = MathF.Round(mat.M42 / spacing) * spacing;
            mat.M43 = MathF.Round(mat.M43 / spacing) * spacing;
        }

        private static bool CheckSolved()
        {
            float threshold = 0.5f * Spacing;
            int ind = 0;

            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    for (int k = 0; k < 3; k++)
                    {
                        var mat = cubieTransforms[ind++];
                        if (i == 2 && (mat.M43 < threshold || mat.M33 < 0.9f)) return false;
                        if (i == 0 && (mat.M43 > -threshold || mat.M33 < 0.9f)) return false;
                        if (j == 2 && (mat.M41 < threshold || mat.M11 < 0.9f)) return false;
                        if (j == 0 && (mat.M41 > -threshold || mat.M11 < 0.9f)) return false;
                        if (k == 2 && (mat.M42 < threshold || mat.M22 < 0.9f)) return false;
                        if (k == 0 && (mat.M42 > -threshold || mat.M22 < 0.9f)) return false;
                    }
                }
            }
            return true;
        }

        private static Vector3 ParseLightPosition()
        {
            float x = ParseCoordinate(lightPosXText, 4.0f);
            float y = ParseCoordinate(lightPosYText, 5.0f);
            float z = ParseCoordinate(lightPosZText, 6.0f);
            return new Vector3(x, y, z);
        }

        private static float ParseCoordinate(string text, float fallback)
        {
            if (float.TryParse(text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out float val))
                return val;
            return fallback;
        }

        private static unsafe void GraphicWindow_Render(double deltaTime)
        {
            Gl.Clear(ClearBufferMask.ColorBufferBit);
            Gl.Clear(ClearBufferMask.DepthBufferBit);

            Gl.UseProgram(program);

            // Phong uniformok beallitasa
            Vector3 lightPos = ParseLightPosition();
            SetUniform3(LightColorVariableName, lightColor);
            SetUniform3(LightPositionVariableName, lightPos);
            SetUniform3(ViewPositionVariableName, new Vector3(camera.Position.X, camera.Position.Y, camera.Position.Z));
            SetUniform1(ShininessVariableName, shininess);

            float aspectRatio = (float)graphicWindow.Size.X / Math.Max(1, graphicWindow.Size.Y);
            var viewMatrix = Matrix4X4.CreateLookAt(camera.Position, camera.Target, camera.UpVector);
            SetMatrix(viewMatrix, ViewMatrixVariableName);

            var projectionMatrix = Matrix4X4.CreatePerspectiveFieldOfView<float>((float)(Math.PI / 2), aspectRatio, 0.1f, 100f);
            SetMatrix(projectionMatrix, ProjectionMatrixVariableName);

            Matrix4X4<float> sliceMatrix = isAnimating
                ? CreateAxisRotation(currentAxis, currentAngle)
                : Matrix4X4<float>.Identity;

            float spacingFactor = 1.0f + 0.8f * MathF.Abs(MathF.Sin((float)celebrationTime * 3.0f));
            Matrix4X4<float> spin = Matrix4X4.CreateRotationY((float)celebrationTime * 2.0f);

            for (int idx = 0; idx < 27; idx++)
            {
                Matrix4X4<float> model = cubieTransforms[idx];

                if (isCelebrating)
                {
                    model.M41 *= spacingFactor;
                    model.M42 *= spacingFactor;
                    model.M43 *= spacingFactor;
                    model = model * spin;
                }

                Matrix4X4<float> activeSlice = (isAnimating && currentActiveIndices.Contains(idx))
                    ? sliceMatrix
                    : Matrix4X4<float>.Identity;

                SetMatrix(model, ModelMatrixVariableName);
                SetMatrix(activeSlice, SliceMatrixVariableName);

                // A normalmatrixot a teljes (model * slice) transzformaciobol szamoljuk
                SetNormalMatrix(model * activeSlice);

                DrawModelObject(cubes[idx]);
            }

            RenderImGui();
            imGuiController.Render();
        }

        private static void RenderImGui()
        {
            ImGuiNET.ImGui.Begin("Phong & Rubik Vezerlo", ImGuiNET.ImGuiWindowFlags.AlwaysAutoResize | ImGuiNET.ImGuiWindowFlags.NoCollapse);

            // 1. Megvilagitas szinosszetetele (3 csuszka)
            ImGuiNET.ImGui.Text("Megvilagitas szine (Light Color):");
            ImGuiNET.ImGui.SliderFloat("Piros (R)", ref lightColor.X, 0.0f, 1.0f);
            ImGuiNET.ImGui.SliderFloat("Zold (G)", ref lightColor.Y, 0.0f, 1.0f);
            ImGuiNET.ImGui.SliderFloat("Kek (B)", ref lightColor.Z, 0.0f, 1.0f);
            ImGuiNET.ImGui.SliderFloat("Shininess", ref shininess, 5.0f, 128.0f);

            ImGuiNET.ImGui.Separator();

            // 2. Fenyforras pozicioja (3 szoveges mezo)
            ImGuiNET.ImGui.Text("Fenyforras pozicioja (X, Y, Z):");
            ImGuiNET.ImGui.SetNextItemWidth(100);
            ImGuiNET.ImGui.InputText("Light X", ref lightPosXText, 32);
            ImGuiNET.ImGui.SetNextItemWidth(100);
            ImGuiNET.ImGui.InputText("Light Y", ref lightPosYText, 32);
            ImGuiNET.ImGui.SetNextItemWidth(100);
            ImGuiNET.ImGui.InputText("Light Z", ref lightPosZText, 32);

            ImGuiNET.ImGui.Separator();

            // 3. Forgatasi navigacio gombokkal
            ImGuiNET.ImGui.Text("Lapforgatasok (CW / CCW):");

            DrawFaceRotationButtons("Piros (+Z)", Face.Red);
            DrawFaceRotationButtons("Narancs (-Z)", Face.Orange);
            DrawFaceRotationButtons("Zold (+X)", Face.Green);
            DrawFaceRotationButtons("Kek (-X)", Face.Blue);
            DrawFaceRotationButtons("Sarga (+Y)", Face.Yellow);
            DrawFaceRotationButtons("Feher (-Y)", Face.White);

            ImGuiNET.ImGui.Spacing();
            if (ImGuiNET.ImGui.Button("Keveres (Shuffle)"))
            {
                StartShuffle();
            }

            ImGuiNET.ImGui.Spacing();
            ImGuiNET.ImGui.TextDisabled(isSolveMode
                ? "Mod: UI / Forgatas (TAB -> Kamera)"
                : "Mod: Szabad kamera (TAB -> UI)");

            ImGuiNET.ImGui.End();
        }

        private static void DrawFaceRotationButtons(string label, Face face)
        {
            if (ImGuiNET.ImGui.Button($"{label} +"))
            {
                EnqueueMove(face, true, NormalRotationSpeed);
            }
            ImGuiNET.ImGui.SameLine();
            if (ImGuiNET.ImGui.Button($"{label} -"))
            {
                EnqueueMove(face, false, NormalRotationSpeed);
            }
        }

        private static unsafe void SetNormalMatrix(Matrix4X4<float> transformMatrix)
        {
            int location = Gl.GetUniformLocation(program, NormalMatrixVariableName);
            if (location == -1)
            {
                throw new Exception($"{NormalMatrixVariableName} uniform nem talalhato a shaderben.");
            }

            var withoutTranslation = new Matrix4X4<float>(
                transformMatrix.Row1,
                transformMatrix.Row2,
                transformMatrix.Row3,
                transformMatrix.Row4);
            withoutTranslation.M41 = 0;
            withoutTranslation.M42 = 0;
            withoutTranslation.M43 = 0;
            withoutTranslation.M44 = 1;

            Matrix4X4.Invert(withoutTranslation, out Matrix4X4<float> inverse);
            Matrix3X3<float> normalMatrix = new Matrix3X3<float>(Matrix4X4.Transpose(inverse));

            Gl.UniformMatrix3(location, 1, false, (float*)&normalMatrix);
            CheckError();
        }

        private static unsafe void SetUniform1(string uniformName, float uniformValue)
        {
            int location = Gl.GetUniformLocation(program, uniformName);
            if (location == -1)
                throw new Exception($"{uniformName} uniform nem talalhato a shaderben.");

            Gl.Uniform1(location, uniformValue);
            CheckError();
        }

        private static unsafe void SetUniform3(string uniformName, Vector3 uniformValue)
        {
            int location = Gl.GetUniformLocation(program, uniformName);
            if (location == -1)
                throw new Exception($"{uniformName} uniform nem talalhato a shaderben.");

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
            int location = Gl.GetUniformLocation(program, uniformName);
            if (location == -1)
                throw new Exception($"{uniformName} uniform nem talalhato a shaderben.");

            Gl.UniformMatrix4(location, 1, false, (float*)&mx);
            CheckError();
        }

        public static void CheckError()
        {
            var error = (ErrorCode)Gl.GetError();
            if (error != ErrorCode.NoError)
                throw new Exception("GL.GetError() hiba: " + error.ToString());
        }
    }
}