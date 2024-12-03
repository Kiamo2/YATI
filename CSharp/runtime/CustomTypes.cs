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

using System.Collections.Generic;
using System.Linq;
using Godot;
using Godot.Collections;

namespace YATI;

public class CustomTypes
{
    private Array<Dictionary> _customTypes;
    
    public void LoadCustomTypes(string projectFile, ZipAccess za = null)
    {
        var projFileAsDictionary = DictionaryBuilder.GetDictionary(projectFile, za);
        if (projFileAsDictionary.TryGetValue("propertyTypes", out var propTypes))
            _customTypes = (Array<Dictionary>)propTypes;
    }

    public void UnloadCustomTypes()
    {
        _customTypes?.Clear();
    }
    
    public void MergeCustomProperties(Dictionary obj, string scope)
    {
        if (_customTypes == null) return;

        var classString = (string)obj.GetValueOrDefault("class", "");
        if (classString == "")
            classString = (string)obj.GetValueOrDefault("type", "");

        Array<Dictionary> properties;
        var newKey = false;
        if (obj.TryGetValue("properties", out var props))
            properties = (Array<Dictionary>)props;
        else
        {
            properties = new Array<Dictionary>();
            newKey = true;
        }
        
        foreach (var ctProp in _customTypes)
        {
            var ptName = (string)ctProp.GetValueOrDefault("name", "");
            var ptType = (string)ctProp.GetValueOrDefault("type", "");
            var ptScope = (Array<string>)ctProp.GetValueOrDefault("useAs", System.Array.Empty<string>());
            if (ptName != classString || ptType != "class" || !ptScope.Contains(scope)) continue;
            var ptMembers = (Array<Dictionary>)ctProp.GetValueOrDefault("members", System.Array.Empty<string>());
            foreach (var mem in ptMembers)
            {
                var name = (string)mem.GetValueOrDefault("name", "");
                var append = properties.Select(prop => (string)prop.GetValueOrDefault("name", "")).All(propName => propName != name);
                if (!append) continue;
                if (newKey)
                {
                    obj["properties"] = new Array<Dictionary>();
                    newKey = false;
                }
                ((Array)obj["properties"]).Add(mem);
            } 
        }
    }
}
