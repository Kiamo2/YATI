﻿// MIT License
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

using System.Linq;
using Godot;
using Godot.Collections;

namespace YATI;

public static class DictionaryBuilder
{
    private enum FileType
    {
        Xml,
        Json,
        Unknown
    }

    public static Dictionary GetDictionary(byte[] tiledFileContent, string sourceFile)
    {

        var type = FileType.Unknown;
        var extension = sourceFile.GetFile().GetExtension().ToLower();
        if (new[] { "tmx", "tsx", "xml", "tx" }.Contains(extension))
            type = FileType.Xml;
        else if (new[] { "tmj", "tsj", "json", "tj", "tiled-project" }.Contains(extension))
            type = FileType.Json;
        else
        {
            var chunk = System.Text.Encoding.UTF8.GetString(tiledFileContent, 0, 12);
            if (chunk.StartsWith("<?xml "))
                type = FileType.Xml;
            else if (chunk.StartsWith("{ \""))
                type = FileType.Json;
        }

        switch (type)
        {
            case FileType.Xml:
            {
                var dictBuilder = new DictionaryFromXml();
                return dictBuilder.Create(tiledFileContent, sourceFile);
            }
            case FileType.Json:
            {
                var json = new Json();
                if (json.Parse(System.Text.Encoding.UTF8.GetString(tiledFileContent)) == Error.Ok)
                    return (Dictionary)json.Data;
                break;
            }
            case FileType.Unknown:
                GD.PrintErr($"ERROR: File '{sourceFile}' has an unknown type. -> Continuing but result may be unusable");
                break;
        }

        return null;
    }
}
