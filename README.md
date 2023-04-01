# YATI (Yet Another Tiled Importer) for Godot 4

This is an addon for the [Godot Engine](https://godotengine.org) for importing files (.tmx, .tmj)
created by the [Tiled Map Editor](http://www.mapeditor.org).

**Please note: This addon is for Godot 4.0 only and won't work with Godot 3.x**

Tested on Windows 10 with Godot 4.0.1 and Tiled 1.10 (Tiled maps from older Tiled versions may work too)

Latest version: 1.2.2

## Installation

The addon is available in GDScript as well as in C# for the Mono version of Godot 4.

- Download either the [GDScript version](../../releases/download/v1.2.2/v1.2.2-gdscript.zip) or the [CSharp version](../../releases/download/v1.2.2/v1.2.2-csharp.zip)
- Move the unzipped addon folder with its entire content to your Godot project folder
- After starting your project in Godot the plugin should appear at Project>>Project Settings...>>Plugins
- **C# version:** Run your project once for building the plugin, otherwise enabling will fail
- Enable the plugin by ticking the enable checkbox

## Usage

- Place your Tiled map(s) with all its parts (.PNGs/.tsx/.tsj/.tx/.tj/...) somewhere inside your Godot project
- Please check: Tiled editor should seamlessly work inside your project i.e. all its references must be ok
- Once these requirements are all met, (re-)starting the project lets the import run automatically
- **Important recommendation:** Untick "Use multiple threads" in Project Settings (Advanced) Editor>>Import  
Otherwise - if you have more than one Tiled map - Godot may freeze (+crash) during import.

## Features

(Hopefully) nearly all Tiled features are supported:
- all kinds of layers
- all kinds of objects
- all map orientations 
- visibility, opacity, tint, offsets, probability
- parallaxes
- tile collisions
- tile animations
- templates
- custom properties  
...

By assigning values to the class attributes and by setting custom properties the resulting scene can be largely customized.

For details please refer to the [reference document](Reference.md)

## Limitations & Particularities

1. Ellipses are not available in Godot. Where appropriate a capsule is used instead. (Where not appropriate it's skipped)
2. Tileset animation in Godot requires involved tiles being equidistant and either horizontally or vertically arranged.<p>
In Tiled you can randomly choose every frame tile, such animations won't map and are skipped.
3. Godot 4 seems to make a left-down render - at least the 'perspective walls' example from the Tiled github site suggests this.  
I've not yet found anything to change this.
4. The C# version does not support zstd compression.
5. I'm currently using Windows 10 therefore I can only ensure the functionality for this OS.

## Known issues

1. Godot may freeze during import, see my recommendation concerning "Use Multiple Threads" in the Usage section.  
Maybe this is only a problem on Windows though...
2. Currently an imported map cannot be instanciated by drag&drop ("No loader found for resource: res://.godot/imported/...")<p>
To get around this problem the resulting .tscn file is additionally copied to where the .tmx/.tmj is located.  
If it's not at once appearing in the editor browser, simply minimize and maximize the Godot window and it will show up.

## Support

Support is active i.e. if issues should arise I'll do my best to resolve them.

## License
[MIT License](LICENSE). Copyright (c) 2023 Roland Helmerichs.
