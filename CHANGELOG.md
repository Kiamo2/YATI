# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [2.1.6] - 2024-12-23

### Fixed
- Fixed issue with nested groups (.tmx only)

## [2.1.5] - 2024-12-20

### Refactoring notes (no change in functionalitiy)

- All data loading from Tiled files is now amalgamated into a new module 'DataLoader'  
This is especially beneficial for runtime version users to allow for easier customizing of data loading.  
E.g. loading from Zip file basically only needs one additional line somewhere ahead from the TilsetCreator.create() call:  
```py
DataLoader.zip_file = "<path-to-zip>/xyz.zip"
```
## [2.1.4] - 2024-12-16

### Fixed

- Fixed issue if a tile's image source is a Tiled map (.tmx or .tmj)  
**Please note:** To make full use of this i.e. that it's looking like in Tiled, instanting the .tmx / .tmj (or it's saved .tscn) is additionally needed. \+ The referenced .tmx / .tmj should already have been imported.

### Refactoring notes (no change in functionalitiy)

- Moved some common code into a new module 'CommonUtils'

## [2.1.3] - 2024-12-08

### Added
- If option 'SaveTilesetTo' is used the saved tileset will automatcally be referenced in all TileMapLayer's tilesets 
- Also evaluate a Tiled custom property 'tile_set' (type 'file') on a (Tiled) tile layer - this was missing.  
This allows to force the use of a specific Godot tileset resource on the corresponding TileMapLayer.

## [2.1.2] - 2024-12-03

### Added
- Ability for the Godot 4.3 runtime version to load a Tiled project from a Zip file.  
*Note: This affects the runtime only, the addon version remains at 2.1.1*

## [2.1.1 / 1.7.1] - 2024-11-13

### Added
- Like 'instance' now also propagate 'XXXbody' tile class types to tilemap objects

### Fixed
- Fixed issue #63

## [2.1.0 / 1.7.0] - 2024-10-21

### Added
- Added new string option 'Custom Data Prefix' (Default value: 'data_') to make it possible to explicitly assign  
the custom property of a tile to Godot's Tile Custom Data.  
Properties with a name not starting with that prefix are now assigned only to the tile's meta data.  
(If neccessary, set the option to an empty string to generate the previous behavior)

### Fixed
- Fixed problem with tile objects being slightly off with tileset images having separation and/or margins.
- Fixed problems resulting from unreliable tileset entries (tilecount / columns).  
  (Both are now calculated from the width and height of the tileset image)
- Fixed need for exporting with option 'Resolve object types and properties'.  
Save / Save all should now being sufficient.
- Fixed problem with missing meta data on alternative tiles (merged PR #60, thank you zhtsu)
  
## [2.0.3 / 1.6.10] - 2024-09-30

### Fixed
- Fixed issue #56

## [2.0.2 / 1.6.9] - 2024-09-18

### Fixed
- Fixed issue #46
- Fixed issue #53

## [2.0.1] - 2024-09-09

### Fixed
- Fixed issue #52

## [2.0.0] - 2024-08-19

### Added

- Added support for the new TileMapLayer in Godot 4.3  
  This required incompatible changes i.e. version 2.0.0 will not work with Godot 4.2.x or older

## [1.6.8] - 2024-08-18

### Added

- Added support for 'godot_atlas_id' custom property which can be used to force specific atlas ids.
  This was implemented on behalf of issue #48.

### Fixed

- Merged Nebelun's pull request which fixes an (GDScript only) issue concerning custom types loading.  
  Thank you Nebelun!
- Fixed issue #49
- Fixed issue #50

## [1.6.7] - 2024-03-26

### Fixed

- Merged milana-94888u's pull request which fixes an (GDScript only) issue if a tileset name is a number.  
  Thank you Milana!
- Fixed a rare issue if tile collision rects had zero height or zero width

## [1.6.6] - 2024-01-31

### Added

- Added support for `godot_script` custom property to set a script on to a node  
  This was done by 'atbigelow', thank you August!

## [1.6.5] - 2024-01-27

### Added

- Added new option 'no_alternative_tiles'.  
  If enabled any flipped/rotated tiles are no longer created as alternative tiles.  
  An enhancement in Godot 4.2 makes this possible.  
  *Please note: Rotated non-sqare tiles may appear wrongly placed if this option is `On`.  
  This currently can't be avoided due to Godot specifics.*

### Fixed

- Fixed issue #37 and merged Lertsenem's pull request (issue #38, his GDScript fix for #37).

## [1.6.4] - 2023-12-27

### Added

- In Godot 4.2 TileMap property 'cell_quadrant_size' was renamed to 'rendering_quadrant_size'.  
The list of custom properies was updated accordingly and the new name can be used.

### Fixed

- Merged mark-rafter's pull request (issue #36).   
  This fixes an issue if collision polygons on tiles are created as polylines

## [1.6.3] - 2023-12-09

### Fixed

- Merged lmReef's pull request (issue #33) and added the C# version.   
This fixes an issue with using different tilesets that use the same tile IDs
- Navigation polygons still seem to need an outline (was revised in v1.6.2). This is now fixed.

## [1.6.2] - 2023-12-04

### Added

- Added another option 'add_id_as_metadata' working similar like 'add_class_as_metadata'.  
If enabled the Tiled object id is added to the corresponding Godot element as meta data.  
This was requested in issue #32.

### Fixed

- Revised the creation of navigation polygons to avoid 'Deprecated...' messages in Godot 4.2.  

## [1.6.1] - 2023-11-09

### Fixed

- Revised the gid/firstgid handling to work more properly.  
  (In rare cases unpleasant problems could occur.)

## [1.6.0] - 2023-10-17

### Added

- Added support for Tiled's Custom Types (by use of new option 'Tiled Project File')  
Thanks to a proposal of 'Sythelux', see pull request #25

### Fixed

- Fixed two minor issues that could have occurred in rare cases.

## [1.5.4] - 2023-10-08

### Added

- Added support for `godot_group` custom property to set Group on the Node  
This was done by 'JoelCreatesGames', thank you Joel!
- Some docu extensions to clarify recurring questions/problems

## [1.5.3] - 2023-08-23

### Fixed

- Fixed issue #18
- Fixed 'No loader found for resource...' error (was my bad since the very first version).  
  It's now possible to instantiate a .tmx/.tmj directly by double clicking on it.  
  Thus copying the resulting .tscn parallel to the .tmx/.tmj is no longer done.  
  That's now left to Godot's 'Please confirm...' dialog opening upon double clicking a .tmx/.tmj

## [1.5.2] - 2023-08-22

### Added

- Runtime packages for C# as well as GDScript

### Fixed

- Fixed issue #17

## [1.5.1] - 2023-08-18

### Added

- Object Alignment on Tilesets is now supported.

### Fixed

- Sub rects on Tile objects resulting in sprites do work now.
- Tilesets with unsorted tile ids needed one more fix.

## [1.5.0] - 2023-08-14

### Added

- For defining the mapping of Tiled objects to Godot elements Tiled's Class field was used so far.<br>
  However, this Class field is required by some users for Custom Types.<br>
  As a new feature now a property 'godot_node_type' can be used for this mapping.<br>
  It has priority i.e. if 'godot_node_type' is present the class field is no longer evaluated.
- Post processors sometimes must know the content of the (Tiled) Class field.<br>
  By enabling a new option 'add_class_as_metadata' the Class field is made available in Godot.

### Fixed

- In rare cases tile ids in tilesets turned out to be not sorted.<br>
  This could produce errors and is now fixed.

## [1.4.0] - 2023-08-08

### Fixed

- Tiled wangsets ('terrain sets') were previously mapped to Godot terrains to keep the terrain set names.<br>Strictly speaking, this was not quite correct (issue #14).<br>
  From this version on they are now mapped to Godot terrain sets and the colors ('terrains') are mapped to Godot terrains.<br>So the mapping is as expected.<br>
  Please note: Godot terrain sets are automatically named 'Terrain Set 0', 'Terrain Set 1' etc. and cannot be named differently (Godot 4 "feature").<br><br>
  The previous mapping can still be enforced using the new 'Map Wangset to Terrain' option.

## [1.3.1] - 2023-06-22

### Fixed

- Godot custom data or meta data did not always have the right type (issue #13). This is now fixed.
- For some properties concerning layers non-existing layers were not created. This is now fixed.

## [1.3.0] - 2023-06-18

### Added

- Sub rectangles in Tiled tilesets are now supported (issue #12).

### Fixed

- Fixed a (rare) null reference problem that came up recently.

## [1.2.5] - 2023-05-27

### Fixed

- Fixed issue #11: If 'Use Tilemap Layers' is enabled set 'Z Index' at the respective layer and not on the canvas item.

### Added

- You (optionally) can be explicit with 'Z Index' by naming the property 'layer_z_index' or 'canvas_z_index'.

## [1.2.4] - 2023-05-07

### Fixed

- Fixed issue #9
- Separation setting for animated tilesets had a limit, reworked that
- GDScript: Minor correction regarding wangset color

## [1.2.3] - 2023-04-02

### Fixed

- GDScript: Fixed typo (upcoming at the end of issue #6)
- GDScript: Fixed problem in issue #7
- Changed some property names to match the reference

## [1.2.2] - 2023-04-01

### Fixed

- Isometric: Problem resolved with offsets introduced in 1.2.0 (issue #6)
- Isometric: Tilesets used by isometric maps can be orthogonal or isometric, this now no longer matters
- Isometric: Enhanced position and rotation calculations for tile collision elements

## [1.2.1] - 2023-03-29

### Fixed

- GDScript-only hotfix - some missing string conversions could result in error messages  
  (Probably no one was affected by this yet)

## [1.2.0] - 2023-03-27

### Added

- New classes 'area', 'staticbody', 'characterbody', 'rigidbody' for tile objects  
  Assigning one of this classes to a tile object results in  
  - Parent CollisionObject2D (according to the class) +
  - Child Sprite2D (Tile object) +
  - Child CollisionShapes based on those assigned to the tile in Tiled
- New class 'instance' + file property 'res_path' for all objects  
  for instantiating pre-existing scene files (e.g. *.tscn files)  
  (Feature implemented on request, issue #3)
- New optional custom (bool) property 'no_import' on layers  
  If set to 'true' the layer is not imported  
  (Feature implemented on request, issue #4)

### Fixed

- Collision shapes on isometric tiles now properly handled
- In case of embedded tilesets some elements were skipped

## [1.1.2] - 2023-03-21

### Fixed

- GDScript-only hotfix - somehow crept-in wrongly placed tab removed   
  (Probably no one was affected by this yet)

## [1.1.1] - 2023-03-13

### Added

- Compatibility with Tiled 1.10: both 'class' and 'type' attributes are evaluated

## [1.1.0] - 2023-03-12

### Fixed

- Wrong placement of tiles if tilesets with different tile sizes are used
- Physics, navigation and occluder polygons were missing on alternative tiles  
  (alternative tiles are created if tiles are flipped or rotated)

## [1.0.0] - 2023-03-06

First published release
