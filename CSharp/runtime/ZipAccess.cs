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

using Godot;

namespace YATI;

public class ZipAccess
{
    private readonly string _zipFilePath;
    private ZipReader _reader;

    public ZipAccess(string zipFilePath)
    {
        _zipFilePath = zipFilePath;
    }

    public Error Open()
    {
        _reader = new ZipReader();
        return _reader.Open(_zipFilePath);
    }

    public bool FileExists(string fileInZip)
    {
        return _reader.FileExists(fileInZip);
    }

    public byte[] GetFíle(string fileInZip)
    {
        return _reader.ReadFile(fileInZip);
    }

    public string GetZipFilePath()
    {
        return _zipFilePath;
    }
    
    public void Close()
    {
        _reader.Close();
    }
}