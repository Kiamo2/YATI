// MIT License
//
// Copyright (c) 2023-2025 Roland Helmerichs
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

namespace YATI;

[Tool]
public static class DataLoader
{
    public static byte[] GetTiledFileContent(string fileName, string basePath)
    {
        var checkedFile = fileName;
        if (!FileAccess.FileExists(checkedFile))
            checkedFile = basePath.PathJoin(fileName);
        if (!FileAccess.FileExists(checkedFile)) return null;
        
        using var file = FileAccess.Open(checkedFile, FileAccess.ModeFlags.Read);
        var ret = file.GetBuffer((long)file.GetLength());
        return ret;
    }

    public static Texture2D LoadImage(string fileName, string basePath)
    {
        var checkedFile = fileName;
        if (!FileAccess.FileExists(fileName))
            checkedFile = basePath.PathJoin(fileName);
        if (!FileAccess.FileExists(checkedFile))
        {
            GD.PrintErr($"ERROR: Image file '{fileName}' not found.");
            CommonUtils.ErrorCount++;
            return null;
        }
        var exists = ResourceLoader.Exists(checkedFile, "Image");
        if (exists)
            return (Texture2D)ResourceLoader.Load(checkedFile, "Image");

        var image = Image.LoadFromFile(checkedFile);
        return ImageTexture.CreateFromImage(image);
    }
    
    public static Resource LoadResourceFromFile(string resourceFile, string basePath)
    {
        var checkedFile = resourceFile;
        if (!FileAccess.FileExists(checkedFile))
            checkedFile = basePath.PathJoin(checkedFile);
        if (FileAccess.FileExists(checkedFile))
            return ResourceLoader.Load(checkedFile);

        GD.PrintErr($"ERROR: Resource file '{resourceFile}' not found.");
        CommonUtils.ErrorCount++;
        return null;
    }
}
#endif