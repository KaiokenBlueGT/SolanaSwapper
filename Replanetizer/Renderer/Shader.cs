﻿// Copyright (C) 2018-2021, The Replanetizer Contributors.
// Replanetizer is free software: you can redistribute it
// and/or modify it under the terms of the GNU General Public
// License as published by the Free Software Foundation,
// either version 3 of the License, or (at your option) any later version.
// Please see the LICENSE.md file for more details.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using SixLabors.ImageSharp.PixelFormats;

namespace Replanetizer.Renderer
{
    public struct UniformFieldInfo
    {
        public int location;
        public string name;
        public int size;
        public ActiveUniformType type;
    }

    public class Shader
    {
        private static readonly NLog.Logger LOGGER = NLog.LogManager.GetCurrentClassLogger();

        private static readonly int TOTAL_NUM_UNIFORMS = Enum.GetNames(typeof(UniformName)).Length;

        public readonly string NAME;
        public int program { get; private set; }
        private readonly GLUniform[] uniforms = new GLUniform[TOTAL_NUM_UNIFORMS];
        private bool initialized = false;

        private readonly (ShaderType Type, string Path)[] FILES;

        public Shader(string name, string vertexShader, string fragmentShader, string geometryShader = "")
        {
            NAME = name;

            if (geometryShader != "")
            {
                FILES = new[]{
                (ShaderType.VertexShader, vertexShader),
                (ShaderType.FragmentShader, fragmentShader),
                (ShaderType.GeometryShader, geometryShader)
            };
            }
            else
            {
                FILES = new[]{
                (ShaderType.VertexShader, vertexShader),
                (ShaderType.FragmentShader, fragmentShader),
            };
            }

            program = CreateProgram(name, FILES);
        }

        public static Shader GetShaderFromFiles(string name, string pathVS, string pathFS)
        {
            Shader shader;
            using (StreamReader vs = new StreamReader(pathVS))
            {
                using (StreamReader fs = new StreamReader(pathFS))
                {
                    shader = new Shader(name, vs.ReadToEnd(), fs.ReadToEnd());
                }
            }

            return shader;
        }


        public static Shader GetShaderFromFiles(string name, string pathVS, string pathFS, string pathGS)
        {
            Shader shader;
            using (StreamReader vs = new StreamReader(pathVS))
            {
                using (StreamReader fs = new StreamReader(pathFS))
                {
                    using (StreamReader gs = new StreamReader(pathGS))
                    {
                        shader = new Shader(name, vs.ReadToEnd(), fs.ReadToEnd(), gs.ReadToEnd());
                    }

                }
            }

            return shader;
        }

        public void UseShader()
        {
            GLState.UseProgram(program);
        }

        public void Dispose()
        {
            if (initialized)
            {
                GL.DeleteProgram(program);
                initialized = false;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public GLUniform? GetUniformLocation(UniformName name)
        {
            GLUniform? uniform = uniforms[(int) name];

            if (uniform == null)
            {
                int location = GL.GetUniformLocation(program, name.ToString());

                if (location == -1)
                {
                    Debug.Print($"The uniform '{name.ToString()}' does not exist in the shader '{NAME}'!");
                }
                else
                {
                    uniform = new GLUniform(location);
                    uniforms[(int) name] = uniform;
                }
            }

            return uniform;
        }

        private int CreateProgram(string name, params (ShaderType Type, string source)[] shaderPaths)
        {
            GLUtil.CreateProgram(name, out int program);

            int[] shaders = new int[shaderPaths.Length];
            for (int i = 0; i < shaderPaths.Length; i++)
            {
                shaders[i] = CompileShader(name, shaderPaths[i].Type, shaderPaths[i].source);

                if (shaderPaths[i].Type == ShaderType.GeometryShader)
                {

                }
            }

            foreach (var shader in shaders)
                GL.AttachShader(program, shader);

            GL.LinkProgram(program);

            GL.GetProgram(program, GetProgramParameterName.LinkStatus, out int success);
            if (success == 0)
            {
                string info = GL.GetProgramInfoLog(program);
                LOGGER.Debug("GL.LinkProgram had info log [{0}]:\n{1}", name, info);
            }

            foreach (var shader in shaders)
            {
                GL.DetachShader(program, shader);
                GL.DeleteShader(shader);
            }

            initialized = true;

            return program;
        }

        private int CompileShader(string name, ShaderType type, string source)
        {
            GLUtil.CreateShader(type, name, out int shader);
            GL.ShaderSource(shader, source);
            GL.CompileShader(shader);

            GL.GetShader(shader, ShaderParameter.CompileStatus, out int success);
            if (success == 0)
            {
                string info = GL.GetShaderInfoLog(shader);
                LOGGER.Debug("GL.CompileShader for shader '{0}' [{1}] had info log:\n{2}", name, type, info);
            }

            return shader;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetUniformMatrix4(UniformName uniformName, ref Matrix4 mat)
        {
            GLUniform? uniform = GetUniformLocation(uniformName);

            if (uniform != null)
            {
                UseShader();
                uniform.SetMatrix4(ref mat);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetUniformMatrix4(UniformName uniformName, int count, float[] mat)
        {
            GLUniform? uniform = GetUniformLocation(uniformName);

            if (uniform != null)
            {
                UseShader();
                uniform.SetMatrix4(count, mat);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetUniformMatrix4(UniformName uniformName, int count, ref float mat)
        {
            GLUniform? uniform = GetUniformLocation(uniformName);

            if (uniform != null)
            {
                UseShader();
                uniform.SetMatrix4(count, ref mat);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetUniform1(UniformName uniformName, float v)
        {
            GLUniform? uniform = GetUniformLocation(uniformName);

            if (uniform != null)
            {
                UseShader();
                uniform.Set1(v);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetUniform1(UniformName uniformName, int v)
        {
            GLUniform? uniform = GetUniformLocation(uniformName);

            if (uniform != null)
            {
                UseShader();
                uniform.Set1(v);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetUniform2(UniformName uniformName, float v0, float v1)
        {
            GLUniform? uniform = GetUniformLocation(uniformName);

            if (uniform != null)
            {
                UseShader();
                uniform.Set2(v0, v1);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetUniform2(UniformName uniformName, int v0, int v1)
        {
            GLUniform? uniform = GetUniformLocation(uniformName);

            if (uniform != null)
            {
                UseShader();
                uniform.Set2((float) v0, (float) v1);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetUniform3(UniformName uniformName, Vector3 v)
        {
            GLUniform? uniform = GetUniformLocation(uniformName);

            if (uniform != null)
            {
                UseShader();
                uniform.Set3(v);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetUniform3(UniformName uniformName, float v0, float v1, float v2)
        {
            GLUniform? uniform = GetUniformLocation(uniformName);

            if (uniform != null)
            {
                UseShader();
                uniform.Set3(v0, v1, v2);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetUniform4(UniformName uniformName, Rgb24 color)
        {
            GLUniform? uniform = GetUniformLocation(uniformName);

            if (uniform != null)
            {
                UseShader();
                uniform.Set4(color);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetUniform4(UniformName uniformName, Rgba32 color)
        {
            GLUniform? uniform = GetUniformLocation(uniformName);

            if (uniform != null)
            {
                UseShader();
                uniform.Set4(color);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetUniform4(UniformName uniformName, float v0, float v1, float v2, float v3)
        {
            GLUniform? uniform = GetUniformLocation(uniformName);

            if (uniform != null)
            {
                UseShader();
                uniform.Set4(v0, v1, v2, v3);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetUniform4(UniformName uniformName, Vector4 v)
        {
            GLUniform? uniform = GetUniformLocation(uniformName);

            if (uniform != null)
            {
                UseShader();
                uniform.Set4(v);
            }
        }
    }
}
