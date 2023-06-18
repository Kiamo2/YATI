# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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