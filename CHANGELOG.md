# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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