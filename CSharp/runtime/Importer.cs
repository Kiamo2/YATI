// MIT License
//
// Copyright (c) 2023-2026 Roland Helmerichs
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

public static class Importer
{
	public static Node2D Import(string sourceFile, string projectFile = "")
	{
		var tilemapCreator = new TilemapCreator();
		
		// Options you might want to set (just uncomment)
		//
		//tilemapCreator.SetUseDefaultFilter(true);
		tilemapCreator.SetAddClassAsMetadata(true);
		//tilemapCreator.SetAddIdAsMetadata(true);
		//tilemapCreator.SetNoAlternativeTiles(true);
		//tilemapCreator.SetMapWangsetToTerrain(true);
		tilemapCreator.SetCustomDataPrefix("data_");
		//tilemapCreator.SetSaveTilesetTo("");

		if (projectFile != "" && FileAccess.FileExists(projectFile))
		{
			var ct = new CustomTypes();
			ct.LoadCustomTypes(projectFile);
			tilemapCreator.SetCustomTypes(ct);
		}
		return tilemapCreator.Create(sourceFile);
	}

	public static Node2D ImportFromZip(string zipFile, string sourceFileInZip, string projectFileInZip = "")
	{
		DataLoader.ZipFile = zipFile;
		return Import(sourceFileInZip, projectFileInZip);
	}

	// If you don't need post-processing better comment out or remove the methods below and also remove PostProcessing.cs to minimize useless code in your build
	public static Node2D ImportWithPostProcessing(string sourceFile, string postProcFile, string projectFile = "")
	{
		var node2D = Import(sourceFile, projectFile);
		var postProc = new PostProcessing();
		return postProc.CallPostProcess(node2D, postProcFile);
	}
	
	public static Node2D ImportFromZipWithPostProcessing(string zipFile, string sourceFileInZip, string postProcFileInZip, string projectFileInZip = "")
	{
		DataLoader.ZipFile = zipFile;
		var node2D = Import(sourceFileInZip, projectFileInZip);
		var postProc = new PostProcessing();
		return postProc.CallPostProcess(node2D, postProcFileInZip);
	}
}
