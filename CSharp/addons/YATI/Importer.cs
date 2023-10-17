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

#if TOOLS
using Godot;
using Godot.Collections;

[Tool]
public partial class Importer: EditorImportPlugin
{
    public override string _GetImporterName() => "YATI";

    public override string _GetVisibleName() => "Import from Tiled";

    public override string[] _GetRecognizedExtensions() => new [] { "tmx", "tmj" };

    public override string _GetResourceType() => "PackedScene";

    public override string _GetSaveExtension() => "tscn";

    public override float _GetPriority() => 0.1f;
    
    public override int _GetPresetCount() => 0;

    public override string _GetPresetName(int presetIndex) => "";

    public override Array<Dictionary> _GetImportOptions(string path, int presetIndex)
    {
        return new Array<Dictionary>()
        {
            new() { { "name", "use_tilemap_layers" }, { "default_value", false } },
            new() { { "name", "use_default_filter" }, { "default_value", false } },
            new() { { "name", "add_class_as_metadata" }, { "default_value", false } },
            new() { { "name", "map_wangset_to_terrain" }, { "default_value", false } },
            new() { { "name", "tiled_project_file" }, { "default_value", "" },
                    { "property_hint", (int)PropertyHint.File }, { "hint_string", "*.tiled-project;Project File" } },
            new() { { "name", "post_processor" }, { "default_value", "" },
                    { "property_hint", (int)PropertyHint.File }, { "hint_string", "*.cs;C# Script" } },
            new() { { "name", "save_tileset_to" }, { "default_value", "" },
                    { "property_hint", (int)PropertyHint.SaveFile }, { "hint_string", "*.tres;Resource File" } }
        };
    }
    
    public override int _GetImportOrder() => 99;

    public override bool _GetOptionVisibility(string path, StringName optionName, Dictionary options) => true;

    public override Error _Import(
        string sourceFile,
        string savePath,
        Dictionary options,
        Array<string> platformVariants,
        Array<string> genFiles
    )
    {
        GD.Print($"Import file '{sourceFile}'");
        if (!FileAccess.FileExists(sourceFile))
        {
            GD.PrintErr($"Import file '{sourceFile}' not found!");
            return Error.FileNotFound;
        }

        CustomTypes ct = null;
        var tilemapCreator = new TilemapCreator();
        if ((string)options["use_tilemap_layers"] == "false")
            tilemapCreator.SetMapLayersToTilemaps(true);
        if ((string)options["use_default_filter"] == "true")
            tilemapCreator.SetUseDefaultFilter(true);
        if ((string)options["add_class_as_metadata"] == "true")
            tilemapCreator.SetAddClassAsMetadata(true);
        if ((string)options["map_wangset_to_terrain"] == "true")
            tilemapCreator.SetMapWangsetToTerrain(true);
        if (options.ContainsKey("tiled_project_file") && (string)options["tiled_project_file"] != "")
        {
            ct = new CustomTypes();
            ct.LoadCustomTypes((string)options["tiled_project_file"]);
            tilemapCreator.SetCustomTypes(ct);
        }

        var node2D = tilemapCreator.Create(sourceFile);
        if (node2D == null)
            return Error.Failed;

        var errors = tilemapCreator.GetErrorCount();
        var warnings = tilemapCreator.GetWarningCount();
        if (options.ContainsKey("save_tileset_to") && (string)options["save_tileset_to"] != "")
        {
            var tileset = tilemapCreator.GetTileset();
            var saveRet = ResourceSaver.Save(tileset, (string)options["save_tileset_to"]);
            if (saveRet == Error.Ok)
                GD.Print($"Successfully saved tileset to '{(string)options["save_tileset_to"]}'");
            else
            {
                GD.PrintErr($"Saving tileset returned error {saveRet}");
                errors++;
            }
        }

        var postProcError = false;
        if (options.ContainsKey("post_processor") && (string)options["post_processor"] != "")
        {
            var postProc = new PostProcessing();
            node2D = postProc.CallPostProcess(node2D, (string)options["post_processor"]);
            postProcError = postProc.GetError() != Error.Ok;
        }
        
        var packedScene = new PackedScene();
        packedScene.Pack(node2D);
        //return ResourceSaver.Save(packedScene, $"{sourceFile.GetBaseName()}.{_GetSaveExtension()}");
        var ret = ResourceSaver.Save(packedScene, $"{savePath}.{_GetSaveExtension()}");
        if (ret != Error.Ok) return ret;
        // v1.5.3: Copying no longer necessary, leave that to Godot's "Please confirm..." dialog box
        //var dir = DirAccess.Open($"{sourceFile.GetBaseName().GetBaseDir()}");
        //ret = dir.Copy($"{savePath}.{_GetSaveExtension()}", $"{sourceFile.GetBaseName()}.{_GetSaveExtension()}");
        //if (ret != Error.Ok) return ret;
        var finalMessageString = "Import succeeded.";
        if (postProcError)
            finalMessageString = "Import finished.";
        if (errors > 0 || warnings > 0)
        {
            finalMessageString = "Import finished with ";
            if (errors > 0)
                finalMessageString += $"{errors} error";
            if (errors > 1)
                finalMessageString += "s";
            if (warnings > 0)
            {
                if (errors > 0)
                    finalMessageString += " and ";
                finalMessageString += $"{warnings} warning";
                if (warnings > 1)
                    finalMessageString += "s";
            }
            finalMessageString += ".";
        }
        GD.Print(finalMessageString);
        if (postProcError)
            GD.Print("Postprocessing was skipped due to some error.");
        ct?.UnloadCustomTypes();
        return ret;
    }
}
#endif
