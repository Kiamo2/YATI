# YATI runtime package

The YATI runtime package allows for importing Tiled maps during (game) runtime.  
To offer such a package was proposed by Jeff Brooks (see issue #16) as being of value for several users.  

Like for the editor plugin there's a [GDScript version](../../releases/download/v2.1.5/runtime-v2.1.5-gdscript.zip) and a [CSharp version](../../releases/download/v2.1.5/runtime-v2.1.5-csharp.zip) available.  

(Runtime downloads for Godot 4.2.x: [GDScript version](../../releases/download/v1.7.1/runtime-v1.7.1-gdscript.zip) and a [CSharp version](../../releases/download/v1.7.1/runtime-v1.7.1-csharp.zip))

## Installation

- Download the package version you need and unzip it
- Move unzipped 'runtime' folder with its entire content somewhere to your Godot source files.  
  The folder name ('runtime') can be changed if you so wish.

## Usage

- Call the import method in module 'Importer' with the path to the Tiled .tmx/.tmj as parameter.
- **Since v1.6.0:** Optional second file parameter for the Tiled project file to make use of Custom Types 
- **Since v2.1.2:** Import from Zip file by alternative method 'import_from_zip' (GDScript) / 'ImportFromZip' (C#)  
Parameters are:
  - zipfile
  - path inside zipfile to .tmx/.tmj
  - path inside zipfile to Tiled project file (optional)    

  **Note:** It's not possible to load resources (.tres/.res/.tscn) from zip (no method available)
- The return value is a Godot 'Node2D' element.

Mini Example in GDScript:

```py
extends Node2D

# Called when the node enters the scene tree for the first time.
func _ready():
    var importer_module = preload("res://src/runtime/Importer.gd").new()
    var added_node = importer_module.import("res://test/desert.tmx")
    # Since v1.6.0 also possible:
    # var added_node = importer_module.import("res://test/desert.tmx", "res://test/desert.tiled-project")
    # Since v2.1.2 also possible:
    # var added_node = importer_module.import_from_zip("J://test/desert.zip", "tmx/desert.tmx", "desert.tiled-project")
    get_tree().current_scene.add_child(added_node.duplicate())
	
```

Mini Example in C#:

```py
using Godot;
using YATI;

public partial class BaseNode : Node2D
{
    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        var addedNode = Importer.Import("res://test/desert.tmx");
        // Since v1.6.0 also possible:
        // var addedNode = Importer.Import("res://test/desert.tmx", "res://test/desert.tiled-project")
        // Since v2.1.2 also possible:
        // var addedNode = Importer.ImportFromZip("J://test/desert.zip", "tmx/desert.tmx", "desert.tiled-project")
        GetTree().CurrentScene.AddChild(addedNode.Duplicate());
    }
}	
```
**Note:** the C# package has a namespace YATI thus the 'using YATI;' statement
