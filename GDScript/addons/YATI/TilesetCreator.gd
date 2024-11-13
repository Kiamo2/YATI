# MIT License
#
# Copyright (c) 2024 Roland Helmerichs
#
# Permission is hereby granted, free of charge, to any person obtaining a copy
# of this software and associated documentation files (the "Software"), to deal
# in the Software without restriction, including without limitation the rights
# to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
# copies of the Software, and to permit persons to whom the Software is
# furnished to do so, subject to the following conditions:
#
# The above copyright notice and this permission notice shall be included in all
# copies or substantial portions of the Software.
#
# THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
# IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
# FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
# AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
# LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
# OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
# SOFTWARE.

@tool
extends RefCounted

const WARNING_COLOR = "yellow"
const CUSTOM_DATA_INTERNAL = "__internal__"
const GODOT_ATLAS_ID_PROPERTY = "godot_atlas_id"
const CLASS_INTERNAL = "class"

var _tileset = null
var _current_atlas_source = null
var _current_max_x = 0
var _current_max_y = 0
var _base_path_map = ""
var _base_path_tileset = ""
var _terrain_sets_counter: int = -1
var _terrain_counter: int = 0
var _tile_count: int = 0
var _columns: int = 0
var _tile_size: Vector2i
var _physics_layer_counter: int = -1
var _navigation_layer_counter: int = -1
var _occlusion_layer_counter: int = -1
var _append = false
var _atlas_sources = null
var _error_count = 0
var _warning_count = 0
var _map_tile_size: Vector2i
var _grid_size: Vector2i
var _tile_offset: Vector2i
var _object_alignment
var _object_groups = null
var _object_groups_counter: int = 0
var _tileset_orientation
var _map_wangset_to_terrain: bool = false
var _custom_data_prefix: String
var _ct: CustomTypes = null
var _current_first_gid = -1


enum layer_type {
	PHYSICS,
	NAVIGATION,
	OCCLUSION
}


func get_error_count():
	return _error_count


func get_warning_count():
	return _warning_count
	

func set_base_path(source_file: String):
	_base_path_map = source_file.get_base_dir()
	_base_path_tileset = _base_path_map


func set_map_parameters(map_tile_size: Vector2i):
	_map_tile_size = map_tile_size


func set_custom_types(ct: CustomTypes):
	_ct = ct


func map_wangset_to_terrain():
	_map_wangset_to_terrain = true


func set_custom_data_prefix(value: String):
	_custom_data_prefix = value


func create_from_dictionary_array(tileSets: Array):
	for tile_set in tileSets:
		var tile_set_dict = tile_set
	
		if tile_set.has("source"):
			var checked_file: String = tile_set["source"]
 
			# Catch the AutoMap Rules tileset (is Tiled internal)
			if checked_file.begins_with(":/automap"):
				continue # This is no error skip it
 
			if not FileAccess.file_exists(checked_file):
				checked_file = _base_path_map.path_join(checked_file)
			_base_path_tileset = checked_file.get_base_dir()
 
			tile_set_dict = preload("DictionaryBuilder.gd").new().get_dictionary(checked_file)
			if tile_set_dict != null and tile_set.has("firstgid"):
				tile_set_dict["firstgid"] = tile_set["firstgid"]
	
		# Possible error condition
		if tile_set_dict == null:
			_error_count += 1
			continue
	
		create_or_append(tile_set_dict)
		_append = true
   
	return _tileset


func create_from_file(source_file: String):
	var tile_set = preload("DictionaryBuilder.gd").new().get_dictionary(source_file)
	create_or_append(tile_set)
	return _tileset


func get_registered_atlas_sources():
	return _atlas_sources


func get_registered_object_groups():
	return _object_groups
	

func create_or_append(tile_set: Dictionary):
	# Catch the AutoMap Rules tileset (is Tiled internal)
	if tile_set.has("name") and str(tile_set["name"]) == "AutoMap Rules":
		return # This is no error just skip it

	if not _append:
		_tileset = TileSet.new()
		_tileset.add_custom_data_layer()
		_tileset.set_custom_data_layer_name(0, CUSTOM_DATA_INTERNAL)
		_tileset.set_custom_data_layer_type(0, TYPE_INT)

	_tile_size = Vector2i(tile_set["tilewidth"], tile_set["tileheight"])
	if not _append:
		_tileset.tile_size = _map_tile_size
	_tile_count = tile_set.get("tilecount", 0)
	_columns = tile_set.get("columns", 0)
	_tileset_orientation = "orthogonal"
	_grid_size = _tile_size
	if tile_set.has("tileoffset"):
		var to = tile_set["tileoffset"]
		_tile_offset = Vector2i(to["x"], to["y"])
	else:
		_tile_offset = Vector2i.ZERO

	_current_first_gid = tile_set.get("firstgid", -1)

	if tile_set.has("grid"):
		var grid = tile_set["grid"]
		if grid.has("orientation"):
			_tileset_orientation = grid["orientation"]
		_grid_size.x = grid.get("width", _tile_size.x)
		_grid_size.y = grid.get("height", _tile_size.y)

	if tile_set.has("objectalignment"):
		_object_alignment = tile_set["objectalignment"]
	else:
		_object_alignment = "unspecified"

	if _append:
		_terrain_counter = 0

	if "image" in tile_set:
		_current_atlas_source = TileSetAtlasSource.new()
		var added_source_id: int = _tileset.add_source(_current_atlas_source, get_special_property(tile_set, GODOT_ATLAS_ID_PROPERTY))
		_current_atlas_source.texture_region_size = _tile_size
		if tile_set.has("margin"):
			_current_atlas_source.margins = Vector2i(tile_set["margin"], tile_set["margin"])
		if tile_set.has("spacing"):
			_current_atlas_source.separation = Vector2i(tile_set["spacing"], tile_set["spacing"])

		var texture = load_image(tile_set["image"])
		if not texture:
			# Can't continue without texture
			return;

		_current_atlas_source.texture = texture
		_columns = _current_atlas_source.texture.get_width() / _tile_size.x
		_tile_count = _columns * _current_atlas_source.texture.get_height() / _tile_size.x
		
		register_atlas_source(added_source_id, _tile_count, -1, _tile_offset)
		var atlas_grid_size = _current_atlas_source.get_atlas_grid_size()
		_current_max_x = atlas_grid_size.x - 1
		_current_max_y = atlas_grid_size.y - 1

	if tile_set.has("tiles"):
		handle_tiles(tile_set["tiles"])
	if tile_set.has("wangsets"):
		if _map_wangset_to_terrain:
			handle_wangsets_old_mapping(tile_set["wangsets"])
		else:
			handle_wangsets(tile_set["wangsets"])

	if _ct != null:
		_ct.merge_custom_properties(tile_set, "tileset")
		
	if tile_set.has("properties"):
		handle_tileset_properties(tile_set["properties"])


func load_image(path: String):
	var orig_path = path
	var ret: Texture2D = null
	# ToDo: Not sure if this first check makes any sense since an image can't be properly imported if not in project tree
	if not FileAccess.file_exists(path):
		path = _base_path_map.get_base_dir().path_join(orig_path)
	if not FileAccess.file_exists(path):
		path = _base_path_tileset.path_join(orig_path)
	if FileAccess.file_exists(path):
		var exists = ResourceLoader.exists(path, "Image")
		if exists:
			ret = load(path)
		else:
			var image = Image.load_from_file(path)
			ret = ImageTexture.create_from_image(image)
	else:
		printerr("ERROR: Image file '" + orig_path + "' not found.")
		_error_count += 1
	return ret


func register_atlas_source(source_id: int, num_tiles: int, assigned_tile_id: int, tile_offset: Vector2i):
	if _atlas_sources == null:
		_atlas_sources = []
	var atlas_source_item = {}
	atlas_source_item["sourceId"] = source_id
	atlas_source_item["numTiles"] = num_tiles
	atlas_source_item["assignedId"] = assigned_tile_id
	atlas_source_item["tileOffset"] = tile_offset
	atlas_source_item["tilesetOrientation"] = _tileset_orientation
	atlas_source_item["objectAlignment"] = _object_alignment
	atlas_source_item["firstGid"] = _current_first_gid
	_atlas_sources.push_back(atlas_source_item)
	

func register_object_group(tile_id: int, object_group: Dictionary):
	if _object_groups == null:
		_object_groups = {}
	_object_groups[tile_id] = object_group


func create_tile_if_not_existing_and_get_tiledata(tile_id: int):
	if tile_id < _tile_count:
		var row = tile_id / _columns
		var col = tile_id % _columns
		var tile_coords = Vector2i(col, row)
		if col > _current_max_x or row > _current_max_y:
			print_rich("[color="+WARNING_COLOR+"] -- Tile " + str(tile_id) + " at " + str(col) + "," + str(row) + " outside texture range. -> Skipped[/color]")
			_warning_count += 1
			return null
		var tile_at_coords = _current_atlas_source.get_tile_at_coords(tile_coords)
		if tile_at_coords == Vector2i(-1, -1):
			_current_atlas_source.create_tile(tile_coords)
		elif tile_at_coords != tile_coords:
			print_rich("[color="+WARNING_COLOR+"]WARNING: tile_at_coords not equal tile_coords![/color]")
			print_rich("[color="+WARNING_COLOR+"]         tile_coords:   " + str(col) + "," + str(row) + "[/color]")
			print_rich("[color="+WARNING_COLOR+"]         tile_at_coords: " + str(tile_at_coords.x) + "," + str(tile_at_coords.x) + "[/color]")
			print_rich("[color="+WARNING_COLOR+"]-> Tile skipped[/color]")
			_warning_count += 1
			return null
		return _current_atlas_source.get_tile_data(tile_coords, 0)
	print_rich("[color="+WARNING_COLOR+"] -- Tile id " + str(tile_id) + " outside tile count range (0-" + str(_tile_count-1) + "). -> Skipped.[/color]")
	_warning_count += 1
	return null


func handle_tiles(tiles: Array):
	for tile in tiles:
		var tile_id = tile["id"]
		var tile_class = tile.get("class", "")
		if tile_class == "":
			tile_class = tile.get("type", "")

		var current_tile
		if tile.has("image"):
			# Tile with its own image -> separate atlas source
			_current_atlas_source = TileSetAtlasSource.new()
			var added_source_id = _tileset.add_source(_current_atlas_source, get_special_property(tile, GODOT_ATLAS_ID_PROPERTY))
			register_atlas_source(added_source_id, 1, tile_id, Vector2i.ZERO)

			var texture_path = tile["image"]
			_current_atlas_source.texture = load_image(texture_path)
			_current_atlas_source.resource_name = texture_path.get_file().get_basename()
			var texture_width = _current_atlas_source.texture.get_width()
			if tile.has("width"):
				texture_width = tile["width"]
			var texture_height = _current_atlas_source.texture.get_height()
			if tile.has("height"):
				texture_height = tile["height"]
			_current_atlas_source.texture_region_size = Vector2i(texture_width, texture_height)
			var tile_offset_x = 0
			if tile.has("x"):
				tile_offset_x = tile["x"]
			var tile_offset_y = 0
			if tile.has("y"):
				tile_offset_y = tile["y"]
			_current_atlas_source.margins = Vector2i(tile_offset_x, tile_offset_y)

			_current_atlas_source.create_tile(Vector2(0, 0))
			current_tile = _current_atlas_source.get_tile_data(Vector2(0, 0), 0)
			current_tile.probability = tile.get("probability", 1.0)
		else:
			current_tile = create_tile_if_not_existing_and_get_tiledata(tile_id)
			if current_tile == null:
				#Error occurred
				continue

		if _tile_size.x != _map_tile_size.x or _tile_size.y != _map_tile_size.y:
			var diff_x = _tile_size.x - _map_tile_size.x
			if diff_x % 2 != 0:
				diff_x -= 1
			var diff_y = _tile_size.y - _map_tile_size.y
			if diff_y % 2 != 0:
				diff_y += 1
			current_tile.texture_origin = Vector2i(-diff_x/2, diff_y/2) - _tile_offset
				
		if tile.has("probability"):
			current_tile.probability = tile["probability"]
		if tile.has("animation"):
			handle_animation(tile["animation"], tile_id)
		if tile.has("objectgroup"):
			handle_objectgroup(tile["objectgroup"], current_tile, tile_id)

		if tile_class != "":
			current_tile.set_meta(CLASS_INTERNAL, tile_class)

		if _ct != null:
			_ct.merge_custom_properties(tile, "tile")
		
		if tile.has("properties"):
			handle_tile_properties(tile["properties"], current_tile)
	

func handle_animation(frames: Array, tile_id: int) -> void:
	var frame_count: int = 0
	var separation_x: int = 0
	var separation_y: int = 0
	var separation_vect = Vector2(separation_x, separation_y)
	var anim_columns: int = 0
	var tile_coords = Vector2(tile_id % _columns, tile_id / _columns)
	var max_diff_x = _columns - tile_coords.x
	var max_diff_y = _tile_count / _columns - tile_coords.y
	var diff_x = 0
	var diff_y = 0
	for frame in frames:
		frame_count += 1
		var frame_tile_id: int = frame["tileid"]
		if frame_count == 2:
			diff_x = (frame_tile_id - tile_id) % _columns
			diff_y = (frame_tile_id - tile_id) / _columns
			if diff_x == 0 and diff_y > 0 and diff_y < max_diff_y:
				separation_y = diff_y - 1
				anim_columns = 1
			elif diff_y == 0 and diff_x > 0 and diff_x < max_diff_x:
				separation_x = diff_x - 1
				anim_columns = 0
			else:
				print_rich("[color="+WARNING_COLOR+"] -- Animated tile " + str(tile_id) + ": Succession of tiles not supported in Godot 4. -> Skipped[/color]")
				_warning_count += 1
				return
			separation_vect = Vector2(separation_x, separation_y)

		if frame_count > 1 and frame_count < frames.size():
			var next_frame_tile_id: int = frames[frame_count]["tileid"]
			var compare_diff_x = (next_frame_tile_id - frame_tile_id) % _columns
			var compare_diff_y = (next_frame_tile_id - frame_tile_id) / _columns
			if compare_diff_x != diff_x or compare_diff_y != diff_y:
				print_rich("[color="+WARNING_COLOR+"] -- Animated tile " + str(tile_id) + ": Succession of tiles not supported in Godot 4. -> Skipped[/color]")
				_warning_count += 1
				return

		if _current_atlas_source.has_room_for_tile(tile_coords, Vector2.ONE, anim_columns, separation_vect, frame_count, tile_coords):
			_current_atlas_source.set_tile_animation_separation(tile_coords, separation_vect)
			_current_atlas_source.set_tile_animation_columns(tile_coords, anim_columns)
			_current_atlas_source.set_tile_animation_frames_count(tile_coords, frame_count)
			var duration_in_secs = 1.0
			if "duration" in frame:
				duration_in_secs = float(frame["duration"]) / 1000.0
			_current_atlas_source.set_tile_animation_frame_duration(tile_coords, frame_count-1, duration_in_secs)
		else:
			print_rich("[color="+WARNING_COLOR+"] -- TileId " + str(tile_id) +": Not enough room for all animation frames, could only set " + str(frame_count) + " frames.[/color]")
			_warning_count += 1
			break


func handle_objectgroup(object_group: Dictionary, current_tile: TileData, tile_id: int):

	# v1.2:
	_object_groups_counter += 1
	register_object_group(_object_groups_counter, object_group)
	current_tile.set_custom_data(CUSTOM_DATA_INTERNAL, _object_groups_counter)
	
	var polygon_indices = {}
	var objects = object_group["objects"] as Array
	for obj in objects:
		if obj.has("point") and obj["point"]:
			# print_rich("[color="+WARNING_COLOR+"] -- 'Point' has currently no corresponding tileset element in Godot 4. -> Skipped[/color]")
			# _warning_count += 1
			break
		if obj.has("ellipse") and obj["ellipse"]:
			# print_rich("[color="+WARNING_COLOR+"] -- 'Ellipse' has currently no corresponding tileset element in Godot 4. -> Skipped[/color]")
			# _warning_count += 1
			break

		if _ct != null:
			_ct.merge_custom_properties(obj, "object")
		
		var object_base_coords = Vector2(obj["x"], obj["y"])
		object_base_coords = transpose_coords(object_base_coords.x, object_base_coords.y)
		object_base_coords -= Vector2(current_tile.texture_origin)
		if _tileset_orientation == "isometric":
			object_base_coords.y -= _grid_size.y / 2.0
			if _grid_size.y != _tile_size.y:
				object_base_coords.y += (_tile_size.y - _grid_size.y) / 2.0
		else:
			object_base_coords -= Vector2(_tile_size / 2.0)

		var rot = obj.get("rotation", 0.0)
		var sin_a = sin(rot * PI / 180.0)
		var cos_a = cos(rot * PI / 180.0)

		var polygon
		if obj.has("polygon") or obj.has("polyline"):
			var polygon_points = (obj["polygon"] if obj.has("polygon") else obj["polyline"]) as Array
			if polygon_points.size() < 3:
				print_rich("[color="+WARNING_COLOR+"] -- Skipped invalid polygon on tile " + str(tile_id) + " (less than 3 points)[/color]")
				_warning_count += 1
				break
			polygon = []
			for pt in polygon_points:
				var p_coord = transpose_coords(pt["x"], pt["y"])
				var p_coord_rot = Vector2(p_coord.x * cos_a - p_coord.y * sin_a, p_coord.x * sin_a + p_coord.y * cos_a)
				polygon.append(object_base_coords + p_coord_rot)
		else:
			# Should be a simple rectangle
			polygon = [Vector2(), Vector2(), Vector2(), Vector2()]
			polygon[0] = Vector2.ZERO
			polygon[1].y = polygon[0].y + obj.get("height", 0.0)
			polygon[1].x = polygon[0].x
			polygon[2].y = polygon[1].y
			polygon[2].x = polygon[0].x + obj.get("width", 0.0)
			polygon[3].y = polygon[0].y
			polygon[3].x = polygon[2].x
			var i = 0
			for pt in polygon:
				var pt_trans = transpose_coords(pt.x, pt.y)
				var pt_rot = Vector2(pt_trans.x * cos_a - pt_trans.y * sin_a, pt_trans.x * sin_a + pt_trans.y * cos_a)
				polygon[i] = object_base_coords + pt_rot
				i += 1

		var nav = get_special_property(obj, "navigation_layer")
		if nav >= 0:
			var nav_p = NavigationPolygon.new()
			nav_p.add_outline(polygon)
			#nav_p.make_polygons_from_outlines()
			# Replaced in 4.2 deprecated function make_polygons_from_outlines
			nav_p.vertices = polygon
			var pg = PackedInt32Array()
			for idx in range(nav_p.vertices.size()):
				pg.push_back(idx)
			nav_p.add_polygon(pg)

			ensure_layer_existing(layer_type.NAVIGATION, nav)
			current_tile.set_navigation_polygon(nav, nav_p)

		var occ = get_special_property(obj, "occlusion_layer")
		if occ >= 0:
			var occ_p = OccluderPolygon2D.new()
			occ_p.polygon = polygon
			ensure_layer_existing(layer_type.OCCLUSION, occ)
			current_tile.set_occluder(occ, occ_p)

		var phys = get_special_property(obj, "physics_layer")
		# If no property is specified assume collision (i.e. default)
		if phys < 0 and nav < 0 and occ < 0:
			phys = 0
		if phys < 0: continue
		var polygon_index = polygon_indices.get(phys, 0)
		polygon_indices[phys] = polygon_index + 1
		ensure_layer_existing(layer_type.PHYSICS, phys)
		current_tile.add_collision_polygon(phys)
		current_tile.set_collision_polygon_points(phys, polygon_index, polygon)
		if not obj.has("properties"): continue
		for property in obj["properties"]:
			var name = property.get("name", "")
			var type = property.get("type", "string")
			var val = property.get("value", "")
			if name == "": continue
			if name.to_lower() == "one_way" and type == "bool":
				current_tile.set_collision_polygon_one_way(phys, polygon_index, val.to_lower() == "true")
			elif name.to_lower() == "one_way_margin" and type == "int":
				current_tile.set_collision_polygon_one_way_margin(phys, polygon_index, int(val))


func transpose_coords(x: float, y: float):
	if _tileset_orientation == "isometric":
		var trans_x = (x - y) * _grid_size.x / _grid_size.y / 2.0
		var trans_y = (x + y) * 0.5
		return Vector2(trans_x, trans_y)

	return Vector2(x, y)


func get_special_property(dict: Dictionary, property_name: String):
	if not dict.has("properties"): return -1
	for	property in dict["properties"]:
		var name = property.get("name", "")
		var type = property.get("type", "string")
		var val = property.get("value", "")
		if name == "": continue
		if name.to_lower() == property_name and type == "int":
			return int(val)
	return -1


func load_resource_from_file(path: String):
	var orig_path = path
	var ret: Texture2D = null
	# ToDo: Not sure if this first check makes any sense since an image can't be properly imported if not in project tree
	if not FileAccess.file_exists(path):
		path = _base_path_map.get_base_dir().path_join(orig_path)
	if not FileAccess.file_exists(path):
		path = _base_path_tileset.path_join(orig_path)
	if FileAccess.file_exists(path):
		ret = ResourceLoader.load(path)
	else:
		printerr("ERROR: Resource file '" + orig_path + "' not found.")
		_error_count += 1
		return ret


func get_bitmask_integer_from_string(mask_string: String, max: int):
	var ret: int = 0
	var s1_arr = mask_string.split(",", false)
	for s1 in s1_arr:
		if s1.contains("-"):
			var s2_arr = s1.split("-", false, 1)
			var i1 = int(s2_arr[0]) if s2_arr[0].is_valid_int() else 0
			var i2 = int(s2_arr[1]) if s2_arr[1].is_valid_int() else 0
			if i1 == 0 or i2 == 0 or i1 > i2: continue
			for i in range(i1, i2+1):
				if i <= max:
					ret += pow(2, i-1)
		elif s1.is_valid_int():
			var i = int(s1)
			if i <= max:
				ret += pow(2, i-1)
	return ret


func get_right_typed_value(type: String, val: String):
	if type == "bool":
		return val == "true"
	elif type == "float":
		return float(val)
	elif type == "int":
		return int(val)
	elif type == "color":
		# If alpha is present it's strangely the first byte, so we have to shift it to the end
		if val.length() == 9: val = val[0] + val.substr(3) + val.substr(1,2)
		return val
	else:
		return val


func handle_tile_properties(properties: Array, current_tile: TileData):
	for property in properties:
		var name = property.get("name", "")
		var type = property.get("type", "string")
		var val = str(property.get("value", ""))
		if name == "": continue
		if name.to_lower() == "texture_origin_x" and  type == "int":
			current_tile.texture_origin = Vector2i(int(val), current_tile.texture_origin.y)
		elif name.to_lower() == "texture_origin_y" and  type == "int":
			current_tile.texture_origin = Vector2i(current_tile.texture_origin.x, int(val))
		elif name.to_lower() == "modulate" and  type == "string":
			current_tile.modulate = Color(val)
		elif name.to_lower() == "material" and  type == "file":
			current_tile.material = load_resource_from_file(val)
		elif name.to_lower() == "z_index" and  type == "int":
			current_tile.z_index = int(val)
		elif name.to_lower() == "y_sort_origin" and  type == "int":
			current_tile.y_sort_origin = int(val)
		elif name.to_lower() == "linear_velocity_x" and (type == "int" or type == "float"):
			ensure_layer_existing(layer_type.PHYSICS, 0)
			var lin_velo = current_tile.get_constant_linear_velocity(0)
			lin_velo.x = float(val)
			current_tile.set_constant_linear_velocity(0, lin_velo)
		elif name.to_lower().begins_with("linear_velocity_x_") and (type == "int" or type == "float"):
			if not name.substr(18).is_valid_int(): continue
			var layer_index = int(name.substr(18))
			ensure_layer_existing(layer_type.PHYSICS, layer_index)
			var lin_velo = current_tile.get_constant_linear_velocity(layer_index)
			lin_velo.x = float(val)
			current_tile.set_constant_linear_velocity(layer_index, lin_velo)
		elif name.to_lower() == "linear_velocity_y" and (type == "int" or type == "float"):
			ensure_layer_existing(layer_type.PHYSICS, 0)
			var lin_velo = current_tile.get_constant_linear_velocity(0)
			lin_velo.y = float(val)
			current_tile.set_constant_linear_velocity(0, lin_velo)
		elif name.to_lower().begins_with("linear_velocity_y_") and (type == "int" or type == "float"):
			if not name.substr(18).is_valid_int(): continue
			var layer_index = int(name.substr(18))
			ensure_layer_existing(layer_type.PHYSICS, layer_index)
			var lin_velo = current_tile.get_constant_linear_velocity(layer_index)
			lin_velo.y = float(val)
			current_tile.set_constant_linear_velocity(layer_index, lin_velo)
		elif name.to_lower() == "angular_velocity" and (type == "int" or type == "float"):
			ensure_layer_existing(layer_type.PHYSICS, 0)
			current_tile.set_constant_angular_velocity(0, float(val))
		elif name.to_lower().begins_with("angular_velocity_") and (type == "int" or type == "float"):
			if not name.substr(17).is_valid_int(): continue
			var layer_index = int(name.substr(17))
			ensure_layer_existing(layer_type.PHYSICS, layer_index)
			current_tile.set_constant_angular_velocity(layer_index, float(val))
		elif name.to_lower() != GODOT_ATLAS_ID_PROPERTY:
			if _custom_data_prefix == "" or name.to_lower().begins_with(_custom_data_prefix):
				name = name.substr(len(_custom_data_prefix))

				var custom_layer = _tileset.get_custom_data_layer_by_name(name)
				if custom_layer < 0:
					_tileset.add_custom_data_layer()
					custom_layer = _tileset.get_custom_data_layers_count() - 1
					_tileset.set_custom_data_layer_name(custom_layer, name)
					var custom_type = {
						"bool": TYPE_BOOL,
						"int": TYPE_INT,
						"string": TYPE_STRING,
						"float": TYPE_FLOAT,
						"color": TYPE_COLOR
					}.get(type, TYPE_STRING)
					_tileset.set_custom_data_layer_type(custom_layer, custom_type)

				current_tile.set_custom_data(name, get_right_typed_value(type, val))

			if _custom_data_prefix == "" or not name.to_lower().begins_with(_custom_data_prefix):
				current_tile.set_meta(name, get_right_typed_value(type, val))


func handle_tileset_properties(properties: Array):
	for property in properties:
		var name = property.get("name", "")
		var type = property.get("type", "string")
		var val = str(property.get("value", ""))
		if name == "": continue
		var layer_index
		if name.to_lower() == "collision_layer" and type == "string":
			ensure_layer_existing(layer_type.PHYSICS, 0)
			_tileset.set_physics_layer_collision_layer(0, get_bitmask_integer_from_string(val, 32))
		elif name.to_lower().begins_with("collision_layer_") and type == "string":
			if not name.substr(16).is_valid_int(): continue
			layer_index = int(name.substr(16))
			ensure_layer_existing(layer_type.PHYSICS, layer_index)
			_tileset.set_physics_layer_collision_layer(layer_index, get_bitmask_integer_from_string(val, 32))
		elif name.to_lower() == "collision_mask" and type == "string":
			ensure_layer_existing(layer_type.PHYSICS, 0)
			_tileset.set_physics_layer_collision_mask(0, get_bitmask_integer_from_string(val, 32))
		elif name.to_lower().begins_with("collision_mask_") and type == "string":
			if not name.substr(15).is_valid_int(): continue
			layer_index = int(name.substr(15))
			ensure_layer_existing(layer_type.PHYSICS, layer_index)
			_tileset.set_physics_layer_collision_mask(layer_index, get_bitmask_integer_from_string(val, 32))
		elif name.to_lower() == "layers" and type == "string":
			ensure_layer_existing(layer_type.NAVIGATION, 0)
			_tileset.set_navigation_layer_layers(0, get_bitmask_integer_from_string(val, 32))
		elif name.to_lower().begins_with("layers_") and type == "string":
			if not name.substr(7).is_valid_int(): continue
			layer_index = int(name.substr(7))
			ensure_layer_existing(layer_type.NAVIGATION, layer_index)
			_tileset.set_navigation_layer_layers(layer_index, get_bitmask_integer_from_string(val, 32))
		elif name.to_lower() == "light_mask" and type == "string":
			ensure_layer_existing(layer_type.OCCLUSION, 0)
			_tileset.set_occlusion_layer_light_mask(0, get_bitmask_integer_from_string(val, 20))
		elif name.to_lower().begins_with("light_mask_") and type == "string":
			if not name.substr(11).is_valid_int(): continue
			layer_index = int(name.substr(11))
			ensure_layer_existing(layer_type.OCCLUSION, layer_index)
			_tileset.set_occlusion_layer_light_mask(layer_index, get_bitmask_integer_from_string(val, 20))
		elif name.to_lower() == "sdf_collision_" and type == "bool":
			ensure_layer_existing(layer_type.OCCLUSION, 0)
			_tileset.set_occlusion_layer_sdf_collision(0, val.to_lower() == "true")
		elif name.to_lower().begins_with("sdf_collision_") and type == "bool":
			if not name.substr(14).is_valid_int(): continue
			layer_index = int(name.substr(14))
			ensure_layer_existing(layer_type.OCCLUSION, layer_index)
			_tileset.set_occlusion_layer_sdf_collision(layer_index, val.to_lower() == "true")
		elif name.to_lower() != GODOT_ATLAS_ID_PROPERTY:
			_tileset.set_meta(name, get_right_typed_value(type, val))


func ensure_layer_existing(tp: layer_type, layer: int):
	match tp:
		layer_type.PHYSICS:
			while _physics_layer_counter < layer:
				_tileset.add_physics_layer()
				_physics_layer_counter += 1
		layer_type.NAVIGATION:
			while _navigation_layer_counter < layer:
				_tileset.add_navigation_layer()
				_navigation_layer_counter += 1
		layer_type.OCCLUSION:
			while _occlusion_layer_counter < layer:
				_tileset.add_occlusion_layer()
				_occlusion_layer_counter += 1
	

func handle_wangsets_old_mapping(wangsets):
	_tileset.add_terrain_set()
	_terrain_sets_counter += 1
	for wangset in wangsets:
		var current_terrain_set = _terrain_sets_counter
		_tileset.add_terrain(current_terrain_set)
		var current_terrain = _terrain_counter
		if "name" in wangset:
			_tileset.set_terrain_name(current_terrain_set, _terrain_counter, wangset["name"])

		var terrain_mode = TileSet.TERRAIN_MODE_MATCH_CORNERS
		if wangset.has("type"):
			terrain_mode = {
				"corner": TileSet.TERRAIN_MODE_MATCH_CORNERS,
				"edge": TileSet.TERRAIN_MODE_MATCH_SIDES,
				"mixed": TileSet.TERRAIN_MODE_MATCH_CORNERS_AND_SIDES
			}.get(wangset["type"], terrain_mode)

		_tileset.set_terrain_set_mode(current_terrain_set, terrain_mode)

		if wangset.has("colors"):
			_tileset.set_terrain_color(current_terrain_set, _terrain_counter, Color(wangset["colors"][0]["color"]))

		if wangset.has("wangtiles"):
			for wangtile in wangset["wangtiles"]:
				var tile_id = wangtile["tileid"]
				var current_tile = create_tile_if_not_existing_and_get_tiledata(tile_id)
				if current_tile == null:
					break

				if _tile_size.x != _map_tile_size.x or _tile_size.y != _map_tile_size.y:
					var diff_x = _tile_size.x - _map_tile_size.x
					if diff_x % 2 != 0:
						diff_x -= 1
					var diff_y = _tile_size.y - _map_tile_size.y
					if diff_y % 2 != 0:
						diff_y += 1
					current_tile.texture_origin = Vector2i(-diff_x/2, diff_y/2) - _tile_offset

				current_tile.terrain_set = current_terrain_set
				current_tile.terrain = current_terrain
				var i = 0
				for wi in wangtile["wangid"]:
					var peering_bit = {
						1: TileSet.CELL_NEIGHBOR_TOP_RIGHT_CORNER,
						2: TileSet.CELL_NEIGHBOR_RIGHT_SIDE,
						3: TileSet.CELL_NEIGHBOR_BOTTOM_RIGHT_CORNER,
						4: TileSet.CELL_NEIGHBOR_BOTTOM_SIDE,
						5: TileSet.CELL_NEIGHBOR_BOTTOM_LEFT_CORNER,
						6: TileSet.CELL_NEIGHBOR_LEFT_SIDE,
						7: TileSet.CELL_NEIGHBOR_TOP_LEFT_CORNER
					}.get(i, TileSet.CELL_NEIGHBOR_TOP_SIDE)
					if wi > 0:
						current_tile.set_terrain_peering_bit(peering_bit, current_terrain)
					i += 1

		_terrain_counter += 1


func handle_wangsets(wangsets):
	for wangset in wangsets:
		_tileset.add_terrain_set()
		_terrain_sets_counter += 1
		_terrain_counter = -1
		var current_terrain_set = _terrain_sets_counter

		var current_terrain = _terrain_counter
		var terrain_set_name = ""
		if "name" in wangset:
			terrain_set_name = wangset["name"]

		var terrain_mode = TileSet.TERRAIN_MODE_MATCH_CORNERS
		if wangset.has("type"):
			terrain_mode = {
				"corner": TileSet.TERRAIN_MODE_MATCH_CORNERS,
				"edge": TileSet.TERRAIN_MODE_MATCH_SIDES,
				"mixed": TileSet.TERRAIN_MODE_MATCH_CORNERS_AND_SIDES
			}.get(wangset["type"], terrain_mode)

		_tileset.set_terrain_set_mode(current_terrain_set, terrain_mode)

		if wangset.has("colors"):
			for wangcolor in wangset["colors"]:
				_terrain_counter += 1
				_tileset.add_terrain(current_terrain_set)
				_tileset.set_terrain_color(current_terrain_set, _terrain_counter, Color(wangcolor["color"]))
				var col_name = terrain_set_name
				if wangcolor.has("name"):
					if wangcolor["name"] != "":
						col_name = wangcolor["name"]
				_tileset.set_terrain_name(current_terrain_set, _terrain_counter, col_name)

		if wangset.has("wangtiles"):
			for wangtile in wangset["wangtiles"]:
				var tile_id = wangtile["tileid"]
				var current_tile = create_tile_if_not_existing_and_get_tiledata(tile_id)
				if current_tile == null:
					break

				if _tile_size.x != _map_tile_size.x or _tile_size.y != _map_tile_size.y:
					var diff_x = _tile_size.x - _map_tile_size.x
					if diff_x % 2 != 0:
						diff_x -= 1
					var diff_y = _tile_size.y - _map_tile_size.y
					if diff_y % 2 != 0:
						diff_y += 1
					current_tile.texture_origin = Vector2i(-diff_x/2, diff_y/2) - _tile_offset

				current_tile.terrain_set = current_terrain_set
				var i = 0
				for wi in wangtile["wangid"]:
					var peering_bit = {
						1: TileSet.CELL_NEIGHBOR_TOP_RIGHT_CORNER,
						2: TileSet.CELL_NEIGHBOR_RIGHT_SIDE,
						3: TileSet.CELL_NEIGHBOR_BOTTOM_RIGHT_CORNER,
						4: TileSet.CELL_NEIGHBOR_BOTTOM_SIDE,
						5: TileSet.CELL_NEIGHBOR_BOTTOM_LEFT_CORNER,
						6: TileSet.CELL_NEIGHBOR_LEFT_SIDE,
						7: TileSet.CELL_NEIGHBOR_TOP_LEFT_CORNER
					}.get(i, TileSet.CELL_NEIGHBOR_TOP_SIDE)
					if wi > 0:
						current_tile.terrain = wi-1
						current_tile.set_terrain_peering_bit(peering_bit, wi-1)
					i += 1
