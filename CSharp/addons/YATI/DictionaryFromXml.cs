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
using System;
using System.Globalization;
using System.Linq;
using Godot;
using Godot.Collections;
using Array = Godot.Collections.Array;

namespace YATI;

[Tool]
public class DictionaryFromXml
{
    private XmlParserCtrl _xml;
    private string _currentElement;
    private readonly Dictionary _result = new Dictionary();
    private Dictionary _currentDictionary;
    private Array _currentArray;
    private readonly CultureInfo _ci = (CultureInfo)CultureInfo.CurrentCulture.Clone();
    private bool _csvEncoded = true;
    private bool _isMap;
    private bool _inTileset;
    
    public Dictionary Create(byte[] tiledFileContent, string sourceFileName)
    {
        _ci.NumberFormat.NumberDecimalSeparator = ".";

        _xml = new XmlParserCtrl();

        var err = _xml.Open(tiledFileContent, sourceFileName);
        if (err != Error.Ok) return null;

        _currentElement = _xml.NextElement();
        _currentDictionary = _result;
        var baseAttributes = _xml.GetAttributes();

        _currentDictionary.Add("type", _currentElement);
        InsertAttributes(_currentDictionary, baseAttributes);
        _isMap = _currentElement == "map";

        var baseElement = _currentElement;
        while ((err == Error.Ok) && ((!_xml.IsEnd() || (_currentElement != baseElement))))
        {
            _currentElement = _xml.NextElement();
            if (_currentElement == null) { err = Error.ParseError; break; }
            if (_xml.IsEnd()) continue;
            var cAttributes = _xml.GetAttributes();
            var dictionaryBookmark = _currentDictionary;
            err = _xml.IsEmpty() ? SimpleElement(_currentElement, cAttributes) : NestedElement(_currentElement, cAttributes);
            _currentDictionary = dictionaryBookmark;
        }

        if (err == Error.Ok) return _result;
        GD.PrintErr($"Import aborted with {err} error.");
        return null;
    }

    private Error SimpleElement(string elementName, Dictionary<string, string> attributes)
    {
        switch (elementName)
        {
            case "image":
                _currentDictionary.Add("image", attributes["source"]);
                if (attributes.TryGetValue("width", out var value))
                    _currentDictionary.Add("imagewidth", int.Parse(value));
                if (attributes.TryGetValue("height", out value))
                    _currentDictionary.Add("imageheight", int.Parse(value));
                if (attributes.TryGetValue("trans", out value))
                    _currentDictionary.Add("transparentcolor", value);
                return Error.Ok;
            case "wangcolor":
                elementName = "color";
                break;
            case "point":
                _currentDictionary.Add("point", true);
                return Error.Ok;
            case "ellipse":
                _currentDictionary.Add("ellipse", true);
                return Error.Ok;
        }

        var dictKey = elementName;
        if ((elementName == "objectgroup" && (!_isMap || _inTileset)) || elementName is "text" or "tileoffset" or "grid")
        {
            // Create a single dictionary, not an array.
            _currentDictionary[dictKey] = new Dictionary();
            _currentDictionary = (Dictionary)_currentDictionary[dictKey];
            if (attributes.Count > 0)
                InsertAttributes(_currentDictionary, attributes);
        }
        else switch (dictKey)
        {
            case "polygon" or "polyline":
            {
                var arr = new Array();
                foreach (var pt in attributes["points"].Split(' '))
                {
                    var dict = new Dictionary();
                    var x = float.Parse(pt.Split(',')[0], NumberStyles.Any, _ci);
                    var y = float.Parse(pt.Split(',')[1], NumberStyles.Any, _ci);
                    dict.Add("x", x);
                    dict.Add("y", y);
                    arr.Add(dict);
                }
                _currentDictionary.Add(dictKey,arr);
                break;
            }
            case "frame" or "property":
            {
                // i.e. will be part of the superior array (animation or properties)
                var dict = new Dictionary();
                InsertAttributes(dict, attributes);
                _currentArray.Add(dict);
                break;
            }
            default:
            {
                if (new [] {"objectgroup", "imagelayer"}.Contains(dictKey))
                {
                    // to be later added to the layer attributes (by InsertAttributes)
                    attributes.Add("type", dictKey);
                    dictKey = "layer";
                }
                
                if (dictKey == "group")
                {
                    // Add nested "layers" array
                    attributes.Add("type", "group");
                    if (_currentDictionary.TryGetValue("layers", out var layersVal))
                        _currentArray = (Array)layersVal;
                    else
                    {
                        _currentArray = new Array();
                        _currentDictionary["layers"] = _currentArray;
                    }
                    dictKey = "layer";
                }

                if ((dictKey != "animation") && (dictKey != "properties"))
                    dictKey += "s";
                if (_currentDictionary.TryGetValue(dictKey, out var dictVal))
                {
                    _currentArray = (Array)dictVal;
                }
                else
                {
                    _currentArray = new Array();
                    _currentDictionary[dictKey] = _currentArray;
                }

                if ((dictKey != "animation") && (dictKey != "properties"))
                {
                    _currentDictionary = new Dictionary();
                    _currentArray.Add(_currentDictionary);
                }

                if (dictKey == "wangtiles")
                {
                    _currentDictionary.Add("tileid", int.Parse(attributes["tileid"]));
                    var arr = new Array();
                    foreach (var s in attributes["wangid"].Split(','))
                        arr.Add(int.Parse(s));
                    _currentDictionary.Add("wangid", arr);
                }
                else if (attributes.Count > 0)
                    InsertAttributes(_currentDictionary, attributes);

                break;
            }
        }
        
        return Error.Ok;
    }

    private Error NestedElement(string elementName, Dictionary<string, string> attributes)
    {
        switch (elementName)
        {
            case "wangsets":
                return Error.Ok;
            case "data":
                _currentDictionary.Add("type", "tilelayer");
                if (attributes.TryGetValue("encoding", out var encoding))
                {
                    _currentDictionary.Add("encoding", encoding);
                    _csvEncoded = attributes["encoding"] == "csv";
                }
                if (attributes.TryGetValue("compression", out var attribute))
                    _currentDictionary.Add("compression", attribute);
                    
                return Error.Ok;
            case "tileset":
                _inTileset = true;
                break;
        }

        var dictionaryBookmark1 = _currentDictionary;
        var arrayBookmark1 = _currentArray;
        var err = SimpleElement(elementName, attributes);
        var baseElement = _currentElement;
        while ((err == Error.Ok) && ((!_xml.IsEnd() || (_currentElement != baseElement))))
        {
            _currentElement = _xml.NextElement();
            if (_currentElement == null) return Error.ParseError;
            if (_xml.IsEnd()) continue;
            if (_currentElement == "<data>")
            {
                Variant data = _xml.GetData();
                if (baseElement is "text" or "property")
                    _currentDictionary.Add(baseElement, (string)data);
                else
                {
                    data = ((string)data).Trim();
                    if (_csvEncoded)
                    {
                        var arr = new Array();
                        foreach (var s in ((string)data).Split(',',StringSplitOptions.TrimEntries))
                            arr.Add(uint.Parse(s));
                        data = arr;
                    }
                    ((Dictionary)_currentArray[^1]).Add("data", data);
                }
                continue;
            }
            var cAttributes = _xml.GetAttributes();
            var dictionaryBookmark2 = _currentDictionary;
            var arrayBookmark2 = _currentArray;
            err = _xml.IsEmpty() ? SimpleElement(_currentElement, cAttributes) : NestedElement(_currentElement, cAttributes);
            _currentDictionary = dictionaryBookmark2;
            _currentArray = arrayBookmark2;
        }

        _currentDictionary = dictionaryBookmark1;
        _currentArray = arrayBookmark1;
        if (baseElement == "tileset")
            _inTileset = false;
        return err;
    }

    private void InsertAttributes(Dictionary targetDictionary, Dictionary<string, string> attributes)
    {
        foreach (var (key, value) in attributes)
        {
            var val = key switch
            {
                "infinite" => value == "1",
                "visible" => value == "1",
                "wrap" => value == "1",
                _ => (Variant)value
            };

            if (!key.Contains("version"))
            {
                if (int.TryParse((string)val, out var iTmp))
                    val = iTmp;
                else if (uint.TryParse((string)val, out var uiTmp))
                    val = uiTmp;
                else if (float.TryParse((string)val, NumberStyles.Float, _ci, out var fTmp))
                    val = fTmp;
            }
            targetDictionary.Add(key, val);
        }
    }
}
#endif