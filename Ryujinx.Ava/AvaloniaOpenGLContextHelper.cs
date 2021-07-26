﻿using OpenTK.Graphics.OpenGL;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Ryujinx.Ava.Ui.Controls;
using Ryujinx.Graphics.OpenGL;

namespace Ryujinx.Ava
{
    internal unsafe class AvaloniaOpenGLContextHelper
    {
        public static AvaloniaOpenGLContext CreateBackgroundContext(Window* sharedContext, bool debug)
        {
            GLFW.WindowHint(WindowHintOpenGlProfile.OpenGlProfile, OpenGlProfile.Core);
            GLFW.WindowHint(WindowHintContextApi.ContextCreationApi, ContextApi.NativeContextApi);
            GLFW.WindowHint(WindowHintInt.ContextVersionMajor, 3);
            GLFW.WindowHint(WindowHintInt.ContextVersionMinor, 3);
            GLFW.WindowHint(WindowHintBool.OpenGLDebugContext, debug);
            GLFW.WindowHint(WindowHintBool.Visible, false);

            Window* window = GLFW.CreateWindow(100, 100, "BackgroundContext", null, sharedContext);
            GLFW.MakeContextCurrent(window);

            GL.LoadBindings(new OpenToolkitBindingsContext());

            GLFW.MakeContextCurrent(null);

            return new AvaloniaOpenGLContext(window);
        }

        public class AvaloniaOpenGLContext : IOpenGLContext
        {
            internal AvaloniaOpenGLContext(Window* window)
            {
                Window = window;
            }

            public Window* Window { get; }

            public void Dispose()
            {
                GLFW.DestroyWindow(Window);
            }

            public void MakeCurrent()
            {
                GLFW.MakeContextCurrent(Window);
            }
        }
    }
}