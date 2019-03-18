using System.Collections.Generic;
using System.IO;
using System.Text;
using OpenTK.Graphics.OpenGL4;
using SS14.Client.Graphics.Shaders;
using SS14.Shared.Utility;

namespace SS14.Client.Graphics.Clyde
{
    internal partial class Clyde
    {
        private int _defaultShader;

        private ShaderProgram Vertex2DProgram;

        private string _shaderWrapCodeSpriteFrag;
        private string _shaderWrapCodeSpriteVert;

        private readonly List<LoadedShader> _loadedShaders = new List<LoadedShader>();

        private class LoadedShader
        {
            public ShaderProgram Program;
        }

        public int LoadShader(ParsedShader shader, string name = null)
        {
            // TODO: vertex.
            var vertexSource = _shaderWrapCodeSpriteVert;
            var fragmentSource = _shaderWrapCodeSpriteFrag;

            var (header, body) = _getShaderCode(shader);

            fragmentSource = fragmentSource.Replace("[SHADER_HEADER_CODE]", header);
            fragmentSource = fragmentSource.Replace("[SHADER_CODE]", body);

            var program = _compileProgram(vertexSource, fragmentSource, name);

            program.BindBlock("projectionViewMatrices", ProjViewBindingIndex);
            program.BindBlock("uniformConstants", UniformConstantsBindingIndex);

            var loaded = new LoadedShader {Program = program};
            var ret = _loadedShaders.Count;
            _loadedShaders.Add(loaded);
            return ret;
        }

        private void _loadStockShaders()
        {
            string ReadFile(string path)
            {
                using (var reader = new StreamReader(_resourceCache.ContentFileRead(path), Encoding.UTF8))
                {
                    return reader.ReadToEnd();
                }
            }

            _shaderWrapCodeSpriteFrag = ReadFile("/Shaders/Internal/sprite.frag");
            _shaderWrapCodeSpriteVert = ReadFile("/Shaders/Internal/sprite.vert");

            var parsed = ShaderParser.Parse(ReadFile("/Shaders/Internal/default-sprite.swsl"));

            _defaultShader = LoadShader(parsed, "Vertex2DProgram");
            Vertex2DProgram = _loadedShaders[_defaultShader].Program;

            Vertex2DProgram.Use();
            _currentShader = _defaultShader;
        }

        private ShaderProgram _compileProgram(string vertexSource, string fragmentSource, string name = null)
        {
            Shader vertexShader = null;
            Shader fragmentShader = null;

            try
            {
                try
                {
                    vertexShader = new Shader(this, ShaderType.VertexShader, vertexSource);
                }
                catch (ShaderCompilationException e)
                {
                    throw new ShaderCompilationException(
                        "Failed to compile vertex shader, see inner for details.", e);
                }

                try
                {
                    fragmentShader = new Shader(this, ShaderType.FragmentShader, fragmentSource);
                }
                catch (ShaderCompilationException e)
                {
                    throw new ShaderCompilationException(
                        "Failed to compile fragment shader, see inner for details.", e);
                }

                var program = new ShaderProgram(this, name);
                program.Add(vertexShader);
                program.Add(fragmentShader);

                try
                {
                    program.Link();
                }
                catch (ShaderCompilationException e)
                {
                    program.Delete();

                    throw new ShaderCompilationException("Failed to link shaders. See inner for details.", e);
                }

                return program;
            }
            finally
            {
                vertexShader?.Delete();
                fragmentShader?.Delete();
            }
        }

        private static (string header, string body) _getShaderCode(ParsedShader shader)
        {
            var header = new StringBuilder();

            foreach (var uniform in shader.Uniforms.Values)
            {
                if (uniform.DefaultValue != null)
                {
                    header.AppendFormat("uniform {0} {1} = {2};", uniform.Type.GetNativeType(), uniform.Name,
                        uniform.DefaultValue);
                }
                else
                {
                    header.AppendFormat("uniform {0} {1};", uniform.Type.GetNativeType(), uniform.Name);
                }
            }

            // TODO: Varyings.

            ShaderFunctionDefinition fragmentMain = null;

            foreach (var function in shader.Functions)
            {
                if (function.Name == "fragment")
                {
                    fragmentMain = function;
                    continue;
                }
                header.AppendFormat("{0} {1}(", function.ReturnType.GetNativeType(), function.Name);
                var first = true;
                foreach (var parameter in function.Parameters)
                {
                    if (!first)
                    {
                        header.Append(", ");
                    }

                    first = false;

                    header.AppendFormat("{0} {1} {2}", parameter.Qualifiers.GetString(), parameter.Type.GetNativeType(),
                        parameter.Name);
                }

                header.AppendFormat(") {{\n{0}\n}}\n", function.Body);
            }

            return (header.ToString(), fragmentMain?.Body ?? "");
        }
    }
}
