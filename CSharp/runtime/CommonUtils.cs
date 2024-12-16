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

using System;
using System.Globalization;
using Godot;

namespace YATI;

public static class CommonUtils
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static uint GetBitmaskIntegerFromString(string maskString, int max)
    {
        uint ret = 0;
        var s1Arr = maskString.Split(',', StringSplitOptions.TrimEntries);
        foreach (var s1 in s1Arr)
        {
            if (s1.Contains('-'))
            {
                var s2Arr = s1.Split('-', 2, StringSplitOptions.TrimEntries);
                if (!int.TryParse(s2Arr[0], out var i1) || !int.TryParse(s2Arr[1], out var i2)) continue;
                if (i1 > i2) continue;
                for (var i = i1; i <= i2; i++)
                    if (i <= max)
                        ret += (uint)Math.Pow(2, i - 1);
            }
            else if (int.TryParse(s1, out var i)) 
                if (i <= max) 
                    ret += (uint)Math.Pow(2, i - 1);
        }

        return ret;
    }

    public static Variant GetRightTypedValue(string type, string val)
    {
        switch (type)
        {
            case "bool":
                return bool.Parse(val);
            case "float":
                return float.Parse(val, Inv);
            case "int":
                return int.Parse(val);
            case "color":
            {
                // If alpha is present it's oddly the first byte, so we have to shift it to the end
                if (val.Length == 9) val = val[0] + val[3..] + val.Substring(1, 2);
                return val;
            }
            default:
                return val;
        }
    }
    
    public static string CleanupPath(string path)
    {
        while (true)
        {
            var pathArr = path.Split('/');
            var isClean = true;
            for (var i = 1; i < pathArr.Length; i++)
            {
                if (pathArr[i] == "..")
                {
                    pathArr[i] = string.Empty;
                    pathArr[i - 1] = string.Empty;
                    isClean = false;
                    break;
                }

                if (pathArr[i] != ".") continue;
                pathArr[i] = string.Empty;
                isClean = false;
            }

            var newPath = string.Empty;
            foreach (var t in pathArr)
            {
                if (t == string.Empty) continue;
                if (newPath != string.Empty) newPath += '/';
                if (t != string.Empty) newPath += t;
            }

            if (isClean) return newPath;
            path = newPath;
        }
    }
}
