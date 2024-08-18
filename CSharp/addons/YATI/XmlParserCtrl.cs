// MIT License
//
// Copyright (c) 2024 Roland Helmerichs
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

#if TOOLS
using Godot;
using Godot.Collections;

namespace YATI;

[Tool]
public class XmlParserCtrl
{
    private readonly XmlParser _parser = new();
    private string _parsedFileName;

    public Error Open(string sourceFile)
    {
        _parsedFileName = sourceFile;
        return _parser.Open(_parsedFileName);
    }

    public string NextElement()
    {
        var err = ParseOn();
        if (err != Error.Ok)
            return null;
        if (_parser.GetNodeType() == XmlParser.NodeType.Text)
        {
            var text = _parser.GetNodeData().Trim();
            if (text.Length > 0)
                return "<data>";
        }
        while ((_parser.GetNodeType() != XmlParser.NodeType.Element) &&
               (_parser.GetNodeType() != XmlParser.NodeType.ElementEnd))
        {
            err = ParseOn();
            if (err != Error.Ok)
                return null;
        }

        return _parser.GetNodeName();
    }
    
    public bool IsEnd()
    {
        return (_parser.GetNodeType() == XmlParser.NodeType.ElementEnd);
    }
    
    public bool IsEmpty()
    {
        return _parser.IsEmpty();
    }

    public string GetData()
    {
        return _parser.GetNodeData();
    }

    public Dictionary<string, string> GetAttributes()
    {
        var attributes = new Dictionary<string, string>();
        for (var i = 0; i < _parser.GetAttributeCount(); i++)
            attributes[_parser.GetAttributeName(i)] = _parser.GetAttributeValue(i);
        return attributes;
    }

    private Error ParseOn()
    {
        var err = _parser.Read();
        if (err != Error.Ok)
            GD.PrintErr($"Error parsing file '{_parsedFileName}' (around line {_parser.GetCurrentLine()}).");
        return err;
    }
}
#endif
