// MIT License
//
// Copyright (c) 2023 Roland Helmerichs
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System;
using Godot;

public class PostProcessing
{
    private Error _error = Error.Ok;

    public Error GetError()
    {
        return _error;
    }
        
    public Node2D CallPostProcess(Node2D baseNode, string path)
    {
        var script = (CSharpScript)GD.Load(path);
        if (script == null || !script.IsClass("CSharpScript"))
        {
            GD.PrintErr("Script could not be properly recognized/loaded. -> Postprocessing skipped");
            _error = Error.FileUnrecognized;
            return baseNode;
        }
        var scriptObj = (GodotObject)script.New();
        try
        {
            if (!scriptObj.HasMethod("_PostProcess"))
            {
                GD.PrintErr("Script has no method '_PostProcess'. -> Postprocessing skipped");
                _error = Error.MethodNotFound;
                return baseNode;
            }

            try
            {
                var returnedNode = scriptObj.Call("_PostProcess", (Variant)baseNode);
                if (returnedNode.GetType().IsAssignableTo(typeof(Node2D)))
                    return (Node2D)returnedNode;
                throw new NullReferenceException();
            }
            catch (NullReferenceException)
            {
                GD.PrintErr("Script returned invalid data. -> Postprocessing skipped");
                _error = Error.InvalidData;
                return baseNode;
            }
        }
        catch (NullReferenceException)
        {
            GD.PrintErr("Script error (Class name not equal filename? Or not inherited from Node?). -> Postprocessing skipped");
            _error = Error.InvalidData;
            return baseNode;
        }
    }
}