# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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