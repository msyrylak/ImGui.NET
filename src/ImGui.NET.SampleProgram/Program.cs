using System;
using System.Linq;
using System.Numerics;
using Veldrid;
using Veldrid.Sdl2;
using Veldrid.StartupUtilities;

using static ImGuiNET.ImGuiNative;

namespace ImGuiNET
{
    class Program
    {
        private static Sdl2Window _window;
        private static GraphicsDevice _gd;
        private static CommandList _cl;
        private static ImGuiController _controller;
        private static MemoryEditor _memoryEditor;

        // UI state
        private static Vector3 _clearColor = new Vector3(0.45f, 0.55f, 0.6f);
        private static byte[] _memoryEditorData;
        static bool[] s_opened = { true, true, true, true }; // Persistent user state

        static void SetThing(out float i, float val) { i = val; }

        static void Main(string[] args)
        {
            // Create window, GraphicsDevice, and all resources necessary
            VeldridStartup.CreateWindowAndGraphicsDevice(
                new WindowCreateInfo(50, 50, 1280, 720, WindowState.Normal, "MISC Simulator"),
                new GraphicsDeviceOptions(true, null, true),
                out _window,
                out _gd);
            _window.Resized += () =>
            {
                _gd.MainSwapchain.Resize((uint)_window.Width, (uint)_window.Height);
                _controller.WindowResized(_window.Width, _window.Height);
            };
            _cl = _gd.ResourceFactory.CreateCommandList();
            _controller = new ImGuiController(_gd, _gd.MainSwapchain.Framebuffer.OutputDescription, _window.Width, _window.Height);
            _memoryEditor = new MemoryEditor();
            Random random = new Random();
            _memoryEditorData = Enumerable.Range(0, 1024).Select(i => (byte)random.Next(255)).ToArray();

            SOC soc = new SOC();
            bool highlight = false;
            // Main application loop
            while (_window.Exists)
            {
                InputSnapshot snapshot = _window.PumpEvents();
                if (!_window.Exists) { break; }
                _controller.Update(1f / 60f, snapshot); // Feed the input events to our ImGui controller, which passes them through to ImGui.
                
                // display menu
                ImGui.Begin("Menu");
                ImGui.Text("Welcome!");
                int currentItem = -1;
                if (ImGui.Combo("Programs",  ref currentItem, "Subtraction\0Addition\0Multiplication\0Swap Values\0"))
                {
                    switch (currentItem)
                    {
                        case 0:
                            highlight = true;
                            soc.LoadProgram(SOC.memory, 65536, "subtraction");
                            break;
                        case 1:
                            highlight = true;
                            soc.LoadProgram(SOC.memory, 65536, "addition");
                            break;
                        case 2:
                            highlight = true;
                            soc.LoadProgram(SOC.memory, 65536, "multiplication");
                            break;
                        case 3:
                            highlight = true;
                            soc.LoadProgram(SOC.memory, 65536, "swapValues");
                            break;
                    }
                }
                if (ImGui.Button("Save"))
                {
                    soc.SaveProgram(SOC.memory, 65536);
                }
                if (ImGui.Button("Reset"))
                {
                    soc.Reset();
                }
                if (ImGui.Button("Run"))
                {
                    soc.Run(10);
                }
                if (ImGui.Button("Step"))
                {
                    soc.Run(1);
                }
                if (ImGui.Button("Exit"))
                {
                    _window.Close();
                }
                ImGui.End();

                // display the memory editor
                _memoryEditor.Draw("Memory Editor", SOC.memory, 65536, SOC.changes,SOC.PC, soc.highlightbyte, highlight);

                // display registers and its statuses
                ImGui.Begin("Registers");
                ImGui.Columns(4, "my columns");
                ImGui.Separator();
                ImGui.Text("R0"); ImGui.NextColumn();
                ImGui.Text("R1"); ImGui.NextColumn();
                ImGui.Text("R2"); ImGui.NextColumn();
                ImGui.Text("PC"); ImGui.NextColumn();
                ImGui.Text(Convert.ToString(soc.registers[0])); ImGui.NextColumn();
                ImGui.Text(Convert.ToString(soc.registers[1])); ImGui.NextColumn();
                ImGui.Text(Convert.ToString(soc.registers[2])); ImGui.NextColumn();
                ImGui.Text(Convert.ToString(SOC.PC));
                ImGui.Separator();
                ImGui.End();

                _cl.Begin();
                _cl.SetFramebuffer(_gd.MainSwapchain.Framebuffer);
                _cl.ClearColorTarget(0, new RgbaFloat(_clearColor.X, _clearColor.Y, _clearColor.Z, 1f));
                _controller.Render(_gd, _cl);
                _cl.End();
                _gd.SubmitCommands(_cl);
                _gd.SwapBuffers(_gd.MainSwapchain);
            }

            // Clean up Veldrid resources
            _gd.WaitForIdle();
            _controller.Dispose();
            _cl.Dispose();
            _gd.Dispose();
        }
    }
}
