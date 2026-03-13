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

using Godot;
using Godot.Collections;

namespace YATI;

public static class Importer
{
	public static Node2D Import(string sourceFile, Dictionary options = null)
	{
		options ??= new Dictionary();
		
		var tilemapCreator = new TilemapCreator();
		
		if (options.ContainsKey("use_default_filter") && (bool)options["use_default_filter"])
			tilemapCreator.SetUseDefaultFilter(true);
		if (!options.ContainsKey("add_class_as_metadata") || (bool)options["add_class_as_metadata"])
			tilemapCreator.SetAddClassAsMetadata(true);
		if (options.ContainsKey("add_id_as_metadata") && (bool)options["add_id_as_metadata"])
			tilemapCreator.SetAddIdAsMetadata(true);
		if (options.ContainsKey("no_alternative_tiles") && (bool)options["no_alternative_tiles"])
			tilemapCreator.SetNoAlternativeTiles(true);
		if (options.ContainsKey("map_wangset_to_terrain") && (bool)options["map_wangset_to_terrain"])
			tilemapCreator.SetMapWangsetToTerrain(true);
		if (options.ContainsKey("custom_data_prefix") && (string)options["custom_data_prefix"] != "")
			tilemapCreator.SetCustomDataPrefix((string)options["custom_data_prefix"]);
		if (options.ContainsKey("tiled_project_file") && (string)options["tiled_project_file"] != "")
		{
			var ct = new CustomTypes();
			ct.LoadCustomTypes((string)options["tiled_project_file"]);
			tilemapCreator.SetCustomTypes(ct);
		}
		if (options.ContainsKey("save_tileset_to") && (string)options["save_tileset_to"] != "")
			tilemapCreator.SetSaveTilesetTo((string)options["save_tileset_to"]);

		var node2D = tilemapCreator.Create(sourceFile);
		if (node2D == null)
			return null;

		if (options.ContainsKey("post_processor") && (string)options["post_processor"] != "")
		{
			var postProc = new PostProcessing();
			node2D = postProc.CallPostProcess(node2D, (string)options["post_processor"]);
		}

		return node2D;
	}

	public static Node2D Import(string sourceFile, string projectFile = "")
	{
		var options = new Dictionary();
		if (projectFile != "")
			options["tiled_project_file"] = projectFile;
		return Import(sourceFile, options);
	}

	public static Node2D ImportFromZip(string zipFile, string sourceFileInZip, Dictionary options = null)
	{
		DataLoader.ZipFile = zipFile;
		return Import(sourceFileInZip, options);
	}

	public static Node2D ImportFromZip(string zipFile, string sourceFileInZip, string projectFileInZip = "")
	{
		var options = new Dictionary();
		if (projectFileInZip != "")
			options["tiled_project_file"] = projectFileInZip;
		return ImportFromZip(zipFile, sourceFileInZip, options);
	}
}
