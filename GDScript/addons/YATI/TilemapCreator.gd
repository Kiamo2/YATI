# MIT License
#
# Copyright (c) 2023 Roland Helmerichs
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

const FLIPPED_HORIZONTALLY_FLAG = 0x80000000
const FLIPPED_VERTICALLY_FLAG = 0x40000000
const FLIPPED_DIAGONALLY_FLAG = 0x20000000

const BACKGROUND_COLOR_RECT_NAME = "Background Color"
const WARNING_COLOR = "Yellow"

var _map_orientation: String
var _map_width: int = 0
var _map_height: int = 0
var _map_tile_width: int = 0
var _map_tile_height: int = 0
var _infinite = false
var _parallax_origin_x: int = 0
var _parallax_origin_y: int = 0
var _background_color = ""

var _tilemap = null
var _tilemap_offset_x: float = 0.0
var _tilemap_offset_y: float = 0.0
var _tileset = null
var _base_node = null
var _parallax_background = null
var _background = null
var _parallax_layer_existing = false

var _base_path = ""
var _base_name = ""
var _encoding = ""
var _compression = ""
var _map_layers_to_tilemaps = false
var _tm_layer_counter: int = 0
var _first_gids = []
var _atlas_sources = null
var _use_default_filter = false

var _iso_rot: float = 0.0
var _iso_skew: float = 0.0
var _iso_scale: Vector2

var _error_count = 0
var _warning_count = 0

enum object_class {
	BODY,
	AREA,
	NAVIGATION,
	OCCLUDER,
	LINE,
	PATH,
	POLYGON,
	UNKNOWN
}


func get_error_count():
	return _error_count


func get_warning_count():
	return _warning_count
	

func set_map_layers_to_tilemaps(value: bool):
	_map_layers_to_tilemaps = value


func set_use_default_filter(value: bool):
	_use_default_filter = value


func get_tileset():
	return _tileset


func create(source_file: String):
	_base_path = source_file.get_base_dir()
	var base_dictionary = preload("DictionaryBuilder.gd").new().get_dictionary(source_file)
	_map_orientation = base_dictionary.get("orientation", "othogonal")
	_map_width = base_dictionary.get("width", 0)
	_map_height = base_dictionary.get("height", 0)
	_map_tile_width = base_dictionary.get("tilewidth", 0)
	_map_tile_height = base_dictionary.get("tileheight", 0)
	_infinite = base_dictionary.get("infinite", false)
	_parallax_origin_x = base_dictionary.get("parallaxoriginx", 0)
	_parallax_origin_y = base_dictionary.get("parallaxoriginy", 0)
	_background_color = base_dictionary.get("backgroundcolor", "")

	if base_dictionary.has("tilesets"):
		var tilesets = base_dictionary["tilesets"]
		for tileSet in tilesets:
			_first_gids.append(int(tileSet["firstgid"]))
		var tileset_creator = preload("TilesetCreator.gd").new()
		tileset_creator.set_base_path(source_file)
		_tileset = tileset_creator.create_from_dictionary_array(tilesets)
		_error_count = tileset_creator.get_error_count()
		_warning_count = tileset_creator.get_warning_count()
		_atlas_sources = tileset_creator.get_registered_atlas_sources()
	if _tileset == null:
		# If tileset still null create an empty one
		_tileset = TileSet.new()
	_tileset.tile_size = Vector2i(_map_tile_width, _map_tile_height)
	match _map_orientation:
		"isometric":
			_tileset.tile_shape = TileSet.TILE_SHAPE_ISOMETRIC
			_tileset.tile_layout = TileSet.TILE_LAYOUT_DIAMOND_DOWN
		"staggered":
			var stagger_axis = base_dictionary.get("staggeraxis", "y")
			var stagger_index = base_dictionary.get("staggerindex", "odd")
			_tileset.tile_shape = TileSet.TILE_SHAPE_ISOMETRIC
			_tileset.tile_layout = TileSet.TILE_LAYOUT_STACKED if stagger_index == "odd" else TileSet.TILE_LAYOUT_STACKED_OFFSET
			_tileset.tile_offset_axis = TileSet.TILE_OFFSET_AXIS_VERTICAL if stagger_axis == "x" else TileSet.TILE_OFFSET_AXIS_HORIZONTAL
		"hexagonal":
			var stagger_axis = base_dictionary.get("staggeraxis", "y")
			var stagger_index = base_dictionary.get("staggerindex", "odd")
			_tileset.tile_shape = TileSet.TILE_SHAPE_HEXAGON
			_tileset.tile_layout = TileSet.TILE_LAYOUT_STACKED if stagger_index == "odd" else TileSet.TILE_LAYOUT_STACKED_OFFSET
			_tileset.tile_offset_axis = TileSet.TILE_OFFSET_AXIS_VERTICAL if stagger_axis == "x" else TileSet.TILE_OFFSET_AXIS_HORIZONTAL
	
	_tm_layer_counter = 0

	_base_node = Node2D.new()
	_base_name = source_file.get_file().get_basename()
	_base_node.name = _base_name
	_parallax_background = ParallaxBackground.new()
	_base_node.add_child(_parallax_background)
	_parallax_background.name = _base_name + " (PBG)"
	_parallax_background.owner = _base_node
	if _background_color != "":
		_background = ColorRect.new()
		_background.color = Color(_background_color)
		_background.size = Vector2(_map_width * _map_tile_width, _map_height * _map_tile_height)
		_base_node.add_child(_background)
		_background.name = BACKGROUND_COLOR_RECT_NAME
		_background.owner = _base_node
	
	if base_dictionary.has("layers"):
		for layer in base_dictionary["layers"]:
			handle_layer(layer, _base_node)

	if _parallax_background.get_child_count() == 0:
		_base_node.remove_child(_parallax_background)

	if _base_node.get_child_count() > 1: return _base_node

	var ret = _base_node.get_child(0)
	ret.name = _base_name
	return ret


func handle_layer(layer: Dictionary, parent: Node2D):
	var layer_offset_x = layer.get("offsetx", 0)
	var layer_offset_y = layer.get("offsety", 0)
	var layer_opacity = layer.get("opacity", 1.0)
	var layer_visible = layer.get("visible", true)
	_encoding = layer.get("encoding", "csv")
	_compression = layer.get("compression", "")
	var layer_type = layer.get("type", "tilelayer")
	var tint_color = layer.get("tintcolor", "#ffffff")

	if layer_type != "tilelayer" and not _map_layers_to_tilemaps:
		_tilemap = null
		_tm_layer_counter = 0

	if layer_type == "tilelayer":
		if _map_orientation == "isometric":
			layer_offset_x += _map_tile_width * (_map_height / 2.0 - 0.5)
		if _map_layers_to_tilemaps:
			_tilemap = TileMap.new()
			_tilemap.name = str(layer["name"])
			_tilemap.visible = layer_visible
			if layer_offset_x > 0 or layer_offset_y > 0:
				_tilemap.position = Vector2(layer_offset_x, layer_offset_y)
			if layer_opacity < 1.0 or tint_color != "#ffffff":
				_tilemap.modulate = Color(tint_color, layer_opacity)
			_tilemap.tile_set = _tileset
			handle_parallaxes(parent, _tilemap, layer)
			if _map_orientation == "isometric" or _map_orientation == "staggered":
				_tilemap.y_sort_enabled = true
				_tilemap.set_layer_y_sort_enabled(0, true)
		else:
			if _tilemap == null:
				_tilemap = TileMap.new()
				_tilemap.name = str(layer["name"])
				_tilemap.remove_layer(0)
				handle_parallaxes(parent, _tilemap, layer)
				_tilemap_offset_x = layer_offset_x
				_tilemap_offset_y = layer_offset_y
				_tilemap.position = Vector2(layer_offset_x, layer_offset_y)
				if _map_orientation == "isometric" or _map_orientation == "staggered":
					_tilemap.y_sort_enabled = true
			else:
				_tilemap.name += "|" + str(layer["name"])
			if _tilemap.tile_set == null:
				_tilemap.tile_set = _tileset 
			_tilemap.add_layer(_tm_layer_counter)
			_tilemap.set_layer_name(_tm_layer_counter, str(layer["name"]))
			_tilemap.set_layer_enabled(_tm_layer_counter, layer_visible)
			if _map_orientation == "isometric" or _map_orientation == "staggered":
				_tilemap.set_layer_y_sort_enabled(_tm_layer_counter, true)
			if abs(layer_offset_x -_tilemap_offset_x) > 0.01 or abs(layer_offset_y - _tilemap_offset_y) > 0.01:
				print_rich("[color="+WARNING_COLOR+"]Godot 4 has no tilemap layer offsets -> switch off 'use_tilemap_layers'[/color]")
				_warning_count += 1
			if layer_opacity < 1.0 or tint_color != "#ffffff":
				_tilemap.set_layer_modulate(_tm_layer_counter, Color(tint_color, layer_opacity))

		if not _use_default_filter:
			_tilemap.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
		
		if _infinite and layer.has("chunks"):
			# Chunks
			for chunk in layer["chunks"]:
				var offset_x = int(chunk["x"])
				var offset_y = int(chunk["y"])
				var chunk_width = int(chunk["width"])
				var chunk_height = int(chunk["height"])
				var chunk_data = handle_data(chunk["data"], chunk_width * chunk_height)
				if chunk_data != null:
					create_map_from_data(chunk_data, offset_x, offset_y, chunk_width)

		elif layer.has("data"):
			# Data
			var data = handle_data(layer["data"], _map_width * _map_height)
			if data != null:
				create_map_from_data(data, 0, 0, _map_width)

		if layer.has("properties"):
			handle_properties(_tilemap, layer["properties"])

		if not _map_layers_to_tilemaps:
			_tm_layer_counter += 1

	elif layer_type == "objectgroup":
		var layer_node = Node2D.new()
		handle_parallaxes(parent, layer_node, layer)
		
		if "name" in layer:
			layer_node.name = layer["name"]
		if layer_opacity < 1.0 or tint_color != "#ffffff":
			layer_node.modulate = Color(tint_color, layer_opacity)
		layer_node.visible = layer.get("visible", true)
		var layer_pos_x = layer.get("x", 0.0)
		var layer_pos_y = layer.get("y", 0.0)
		layer_node.position = Vector2(layer_pos_x + layer_offset_x, layer_pos_y + layer_offset_y)
		if not _use_default_filter:
			layer_node.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
		if _map_orientation == "isometric":
			layer_node.y_sort_enabled = true

		if layer.has("objects"):
			for obj in layer["objects"]:
				handle_object(obj, layer_node, _tileset, Vector2.ZERO)

		if layer.has("properties"):
			handle_properties(layer_node, layer["properties"])

	elif layer_type == "group":
		var group_node = Node2D.new()
		handle_parallaxes(parent, group_node, layer)
		group_node.name = layer.get("name", "group")
		if layer_opacity < 1.0 or tint_color != "#ffffff":
			group_node.modulate = Color(tint_color, layer_opacity)
		group_node.visible = layer.get("visible", true)
		var layer_pos_x = layer.get("x", 0.0)
		var layer_pos_y = layer.get("y", 0.0)
		group_node.position = Vector2(layer_pos_x + layer_offset_x, layer_pos_y + layer_offset_y)
	
		for child_layer in layer["layers"]:
			handle_layer(child_layer, group_node)

		if layer.has("properties"):
			handle_properties(group_node, layer["properties"])

	elif layer_type == "imagelayer":
		var texture_rect = TextureRect.new()
		handle_parallaxes(parent, texture_rect, layer)

		texture_rect.name = layer.get("name", "image")
		texture_rect.position = Vector2(layer_offset_x, layer_offset_y)

		var imagewidth = layer.get("imagewidth", 0)
		var imageheight = layer.get("imageheight", 0)
		texture_rect.size = Vector2(imagewidth, imageheight)
		if layer_opacity < 1.0 or tint_color != "#ffffff":
			texture_rect.modulate = Color(tint_color, layer_opacity)
		texture_rect.visible = layer_visible

		# ToDo: Not sure if this first check makes any sense since an image can't be imported properly if not in project tree
		var texture_path = layer["image"]
		if not FileAccess.file_exists(texture_path):
			texture_path = _base_path.get_base_dir().path_join(layer["image"])
		if not FileAccess.file_exists(texture_path):
			texture_path = _base_path.path_join(layer["image"])
		if FileAccess.file_exists(texture_path):
			texture_rect.texture = load(texture_path)
			var exists = ResourceLoader.exists(texture_path, "Image")
			if exists:
				texture_rect.texture = load(texture_path)
			else:
				var image = Image.load_from_file(texture_path)
				texture_rect.texture = ImageTexture.create_from_image(image)
		else:
			printerr("ERROR: Image file '" + layer["image"] + "' not found.")
			_error_count += 1

		if layer.has("properties"):
			handle_properties(texture_rect, layer["properties"])

func handle_parallaxes(parent: Node, layer_node: Node, layer_dict: Dictionary):
	if layer_dict.has("parallaxx") or layer_dict.has("parallaxy"):
		if not _parallax_layer_existing:
			if _background != null:
				_background.reparent(_parallax_background)
			_parallax_layer_existing = true
	
		var par_x = layer_dict.get("parallaxx", 0.0)
		var par_y = layer_dict.get("parallaxy", 0.0)
		var parallax_node = ParallaxLayer.new()
		_parallax_background.add_child(parallax_node)
		parallax_node.owner = _base_node
		var px_name = layer_dict.get("name", "")
		parallax_node.name = px_name + " (PL)" if px_name != "" else "ParallaxLayer"
		parallax_node.motion_scale = Vector2(par_x, par_y)
		parallax_node.add_child(layer_node)
		layer_node.owner = _base_node
	else:
		parent.add_child(layer_node)
		layer_node.owner = _base_node


func handle_data(data, map_size):
	var ret: Array = []
	match _encoding:
		"csv":
			for cell in data:
				ret.append(cell)
		"base64":
			var bytes = Marshalls.base64_to_raw(data)
			if _compression != "":
				match _compression:
					"gzip":
						ret = bytes.decompress(map_size * 4, FileAccess.COMPRESSION_GZIP)
					"zlib":
						ret = bytes.decompress(map_size * 4, FileAccess.COMPRESSION_DEFLATE)
					"zstd":
						ret = bytes.decompress(map_size * 4, FileAccess.COMPRESSION_ZSTD)
					_:
						printerr("Decompression for type '" + _compression + "' not yet implemented.")
						_error_count += 1
						return []
				bytes = PackedByteArray(ret)
			ret = bytes.to_int32_array()	
	return ret


func create_map_from_data(layer_data: Array, offset_x: int, offset_y: int, map_width: int):
	var cell_counter: int = -1
	for cell in layer_data:
		cell_counter += 1
		var int_id: int = int(cell) & 0xFFFFFFFF
		var flipped_h = (int_id & FLIPPED_HORIZONTALLY_FLAG) > 0
		var flipped_v = (int_id & FLIPPED_VERTICALLY_FLAG) > 0
		var flipped_d = (int_id & FLIPPED_DIAGONALLY_FLAG) > 0
		var gid: int = int_id & 0x0FFFFFFF
		if gid <= 0: continue
		var cell_coords = Vector2(cell_counter % map_width + offset_x, cell_counter / map_width + offset_y)

		var source_id = get_matching_source_id(gid)
		var first_gid_id = get_first_gid_index(gid)
		if first_gid_id > source_id:
			source_id = first_gid_id
		# Should not be the case, but who knows...
		if source_id < 0: continue

		var atlas_source
		if _tileset.has_source(source_id):
			atlas_source = _tileset.get_source(source_id)
		else: continue
		var atlas_width: int = atlas_source.get_atlas_grid_size().x
		if atlas_width <= 0: continue

		var effective_gid: int = gid - _first_gids[get_first_gid_index(gid)]
		var atlas_coords = Vector2(effective_gid % atlas_width, effective_gid / atlas_width)
		if not atlas_source.has_tile(atlas_coords):
			atlas_source.create_tile(atlas_coords)
		var alt_id = 0
		if flipped_h or flipped_v or flipped_d:
			alt_id = (1 if flipped_h else 0) + (2 if flipped_v else 0) + (4 if flipped_d else 0)
			if not atlas_source.has_alternative_tile(atlas_coords, alt_id):
				atlas_source.create_alternative_tile(atlas_coords, alt_id)
				var tile_data = atlas_source.get_tile_data(atlas_coords, alt_id)
				tile_data.flip_h = flipped_h
				tile_data.flip_v = flipped_v
				tile_data.transpose = flipped_d
		_tilemap.set_cell(_tm_layer_counter, cell_coords, source_id, atlas_coords, alt_id)


func get_object_class(classname: String):
	var cn = classname.to_lower()
	var obj_class = {
		"": object_class.BODY,
		"collision": object_class.BODY,
		"staticbody": object_class.BODY,
		"area": object_class.AREA,
		"navigation": object_class.NAVIGATION,
		"occluder": object_class.OCCLUDER,
		"line": object_class.LINE,
		"path": object_class.PATH,
		"polygon": object_class.POLYGON
	}.get(cn, object_class.UNKNOWN)
	return obj_class


func handle_object(obj: Dictionary, layer_node: Node, tileset: TileSet, offset: Vector2) -> void:
	var obj_x = obj.get("x", offset.x)
	var obj_y = obj.get("y", offset.y)
	var obj_rot = obj.get("rotation", 0.0)
	var obj_width = obj.get("width", 0.0)
	var obj_height = obj.get("height", 0.0)
	var obj_visible = obj.get("visible", true)
	var obj_name = obj.get("name", "")
	var class_string = obj.get("class", "")
	var obj_class = get_object_class(class_string)
	
	if obj.has("template"):
		var template_path = _base_path.path_join(obj["template"])
		var template_dict = preload("DictionaryBuilder.gd").new().get_dictionary(template_path)
		var template_tileset = null

		if template_dict.has("tilesets"):
			var tilesets = template_dict["tilesets"]
			var tileset_creator = preload("TilesetCreator.gd").new()
			tileset_creator.set_base_path(template_path)
			template_tileset = tileset_creator.create_from_dictionary_array(tilesets)

		if template_dict.has("objects"):
			for template_obj in template_dict["objects"]:
				handle_object(template_obj, layer_node, template_tileset, Vector2(obj_x, obj_y))

	if obj.has("gid"):
		# gid refers to a tile in a tile set and object is created as sprite
		var int_id: int = obj["gid"]
		var flippedH = (int_id & FLIPPED_HORIZONTALLY_FLAG) > 0
		var flippedV = (int_id & FLIPPED_VERTICALLY_FLAG) > 0
		var gid: int = int_id & 0x0FFFFFFF

		var source_id = get_matching_source_id(gid)
		var first_gid_id = get_first_gid_index(gid)
		if first_gid_id > source_id:
			source_id = first_gid_id
		# Should not be the case, but who knows...
		if source_id < 0: return

		var gid_source = tileset.get_source(source_id)
		var obj_sprite = Sprite2D.new()
		layer_node.add_child(obj_sprite)
		obj_sprite.owner = _base_node
		obj_sprite.name = obj_name if obj_name != "" \
							else gid_source.resource_name if gid_source.resource_name != "" \
							else gid_source.texture.resource_path.get_file().get_basename() + "_tile"
		obj_sprite.position = transpose_coords(obj_x, obj_y)
		obj_sprite.texture = gid_source.texture

		if get_num_tiles_for_source_id(source_id) > 1:
			# Object is tile from partitioned tileset 
			var atlas_width: int = gid_source.get_atlas_grid_size().x

			# Can be zero if tileset had an error
			if atlas_width <= 0: return

			var effective_gid: int = gid - _first_gids[get_first_gid_index(gid)]
			var atlas_coords = Vector2(effective_gid % atlas_width, effective_gid / atlas_width)
			if not gid_source.has_tile(atlas_coords):
				gid_source.create_tile(atlas_coords)
			obj_sprite.region_enabled = true
			var region_size = Vector2(gid_source.texture_region_size)
			var pos: Vector2 = atlas_coords * region_size
			if get_property(obj, "clip_artifacts", "bool") == "true":
				pos += Vector2(0.5, 0.5)
				region_size -= Vector2(1.0, 1.0)
				obj_width -= 1.0
				obj_height -= 1.0
			obj_sprite.region_rect = Rect2(pos, region_size)
			obj_sprite.offset = {
				"orthogonal": Vector2(region_size.x / 2.0, -region_size.y / 2.0),
				"isometric": Vector2(0.0, -region_size.y / 2.0),
			}.get(_map_orientation, Vector2(region_size.x / 2.0, -region_size.y / 2.0))
			if abs(region_size.x - obj_width) > 0.01 or abs(region_size.y - obj_height) > 0.01:
				var scale_x: float = float(obj_width) / float(region_size.x)
				var scale_y: float = float(obj_height) / float(region_size.y)
				obj_sprite.scale = Vector2(scale_x, scale_y)
		else:
			# Object is single image tile
			var gid_width: float = gid_source.texture.get_width()
			var gid_height: float = gid_source.texture.get_height()
			obj_sprite.offset = Vector2(gid_width / 2.0, -gid_height / 2.0)
			obj_sprite.offset = {
				"orthogonal": Vector2(gid_width / 2.0, -gid_height / 2.0),
				"isometric": Vector2(0.0, -gid_height / 2.0),
			}.get(_map_orientation,  Vector2(gid_width / 2.0, -gid_height / 2.0))
			if gid_width != obj_width or gid_height != obj_height:
				var scale_x: float = float(obj_width) / gid_width
				var scale_y: float = float(obj_height) / gid_height
				obj_sprite.scale = Vector2(scale_x, scale_y)
			
		obj_sprite.flip_h = flippedH
		obj_sprite.flip_v = flippedV
		obj_sprite.rotation_degrees = obj_rot
		obj_sprite.visible = obj_visible

		if obj.has("properties"):
			handle_properties(obj_sprite, obj["properties"])

	elif obj.has("text"):
		var obj_text = Label.new()
		layer_node.add_child(obj_text)
		obj_text.owner = _base_node
		obj_text.name = obj_name if obj_name != "" else "text"
		obj_text.position = transpose_coords(obj_x, obj_y)
		obj_text.size = Vector2(obj_width, obj_height)
		obj_text.clip_text = true
		obj_text.rotation_degrees = obj_rot
		obj_text.visible = obj_visible
		var txt = obj["text"]
		obj_text.text = txt.get("text", "Hello World")
		var wrap = txt.get("wrap", false)
		obj_text.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART if wrap else TextServer.AUTOWRAP_OFF
		var align_h = txt.get("halign", "left")
		match align_h:
			"left": obj_text.horizontal_alignment = HORIZONTAL_ALIGNMENT_LEFT
			"center": obj_text.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
			"right": obj_text.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
			"justify": obj_text.horizontal_alignment = HORIZONTAL_ALIGNMENT_FILL
		var align_v = txt.get("valign", "top")
		match align_v:
			"top": obj_text.vertical_alignment = VERTICAL_ALIGNMENT_TOP
			"center": obj_text.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
			"bottom": obj_text.vertical_alignment = VERTICAL_ALIGNMENT_BOTTOM
		var font_family = txt.get("fontfamily", "Sans-Serif")
		var font = SystemFont.new()
		font.font_names = [font_family]
		font.oversampling = 5.0
		obj_text.add_theme_font_override("font", font)
		obj_text.add_theme_font_size_override("font_size", txt.get("pixelsize", 16))
		obj_text.add_theme_color_override("font_color", Color(txt.get("color", "#000000")))
		if obj.has("properties"):
			handle_properties(obj_text, obj["properties"])

	elif not obj.has("template"):
		if obj_class == object_class.UNKNOWN:
			print_rich("[color=" + WARNING_COLOR +"] -- Unknown class '" + class_string + "'. -> Assuming Default[/color]")
			_warning_count += 1
			obj_class = object_class.BODY	
		var object_base_coords = transpose_coords(obj_x, obj_y)
		if obj.has("point"):
			var marker = Marker2D.new()
			layer_node.add_child(marker)
			marker.owner = _base_node
			marker.name = obj_name if obj_name != "" else "point"
			marker.position = object_base_coords
			marker.rotation_degrees = obj_rot
			marker.visible = obj_visible
			if obj.has("properties"):
				handle_properties(marker, obj["properties"])
		elif obj.has("polygon"):
			if obj_class == object_class.BODY or obj_class == object_class.AREA:
				var co: CollisionObject2D
				if obj_class == object_class.AREA:
					co = Area2D.new()
					layer_node.add_child(co)
					co.name = obj_name + " (Area)" if obj_name != "" else "Area"
				else:
					co = StaticBody2D.new()
					layer_node.add_child(co)
					co.name = obj_name + " (SB)" if obj_name != "" else "StaticBody"
				co.owner = _base_node
				co.position = object_base_coords
				co.visible = obj_visible
				var polygon_shape = CollisionPolygon2D.new()
				polygon_shape.polygon = polygon_from_array(obj["polygon"])
				co.add_child(polygon_shape)
				polygon_shape.owner = _base_node
				polygon_shape.name = obj_name if obj_name != "" else "Polygon Shape"
				polygon_shape.position = Vector2.ZERO
				polygon_shape.rotation_degrees = obj_rot
				if obj.has("properties"):
					handle_properties(co, obj["properties"])
			elif obj_class == object_class.NAVIGATION:
				var nav_region = NavigationRegion2D.new()
				layer_node.add_child(nav_region)
				nav_region.owner = _base_node
				nav_region.name = obj_name + " (NR)" if obj_name != "" else "Navigation"
				nav_region.position = object_base_coords
				nav_region.rotation_degrees = obj_rot
				nav_region.visible = obj_visible
				var nav_poly = NavigationPolygon.new()
				nav_region.navigation_polygon = nav_poly
				nav_poly.add_outline(polygon_from_array(obj["polygon"]))
				nav_poly.make_polygons_from_outlines()
				if obj.has("properties"):
					handle_properties(nav_region, obj["properties"])
			elif obj_class == object_class.OCCLUDER:
				var light_occ = LightOccluder2D.new()
				layer_node.add_child(light_occ)
				light_occ.owner = _base_node
				light_occ.name = obj_name + " (LO)" if obj_name != "" else "Occluder"
				light_occ.position = object_base_coords
				light_occ.rotation_degrees = obj_rot
				light_occ.visible = obj_visible
				var occ_poly = OccluderPolygon2D.new()
				light_occ.occluder = occ_poly
				occ_poly.polygon = polygon_from_array(obj["polygon"])
				if obj.has("properties"):
					handle_properties(light_occ, obj["properties"])
			elif obj_class == object_class.POLYGON:
				var polygon = Polygon2D.new()
				layer_node.add_child(polygon)
				polygon.owner = _base_node
				polygon.name = obj_name if obj_name != "" else "Polygon"
				polygon.position = object_base_coords
				polygon.rotation_degrees = obj_rot
				polygon.visible = obj_visible
				polygon.polygon = polygon_from_array(obj["polygon"])
				if obj.has("properties"):
					handle_properties(polygon, obj["properties"])	
		elif obj.has("polyline"):
			if obj_class == object_class.LINE:
				var line = Line2D.new()
				layer_node.add_child(line)
				line.owner = _base_node
				line.name = obj_name if obj_name != "" else "Line"
				line.position = object_base_coords
				line.visible = obj_visible
				line.rotation_degrees = obj_rot
				line.width = 1.0
				line.points = polygon_from_array(obj["polyline"])
				if obj.has("properties"):
					handle_properties(line, obj["properties"])
			elif obj_class == object_class.PATH:
				var path = Path2D.new()
				layer_node.add_child(path)
				path.owner = _base_node
				path.name = obj_name if obj_name != "" else "Path"
				path.position = object_base_coords
				path.visible = obj_visible
				path.rotation_degrees = obj_rot
				var curve = Curve2D.new()
				for point in obj["polyline"]:
					curve.add_point(Vector2(point.x, point.y))
				path.curve = curve
				if obj.has("properties"):
					handle_properties(path, obj["properties"])
			else:
				var co: CollisionObject2D
				if obj_class == object_class.AREA:
					co = Area2D.new()
					layer_node.add_child(co)
					co.name = obj_name + " (Area)" if obj_name != "" else "Area"
				else:
					co = StaticBody2D.new()
					layer_node.add_child(co)
					co.name = obj_name + " (SB)" if obj_name != "" else "StaticBody"
				co.owner = _base_node
				co.position = object_base_coords
				co.visible = obj_visible
				var polyline_points: Array = obj["polyline"]
				for i in range(0, polyline_points.size()-1):
					var collision_shape = CollisionShape2D.new()
					co.add_child(collision_shape)
					var segment_shape = SegmentShape2D.new()
					segment_shape.a = transpose_coords(polyline_points[i]["x"], polyline_points[i]["y"], true)
					segment_shape.b = transpose_coords(polyline_points[i+1]["x"], polyline_points[i+1]["y"], true)
					collision_shape.owner = _base_node
					collision_shape.shape = segment_shape
					collision_shape.position = Vector2(obj_width / 2.0, obj_height / 2.0)
					collision_shape.rotation_degrees = obj_rot
					collision_shape.name = "Segment Shape"
				if obj.has("properties"):
					handle_properties(co, obj["properties"])
		else:
			if obj_class == object_class.BODY or obj_class == object_class.AREA:
				var co: CollisionObject2D
				if obj_class == object_class.AREA:
					co = Area2D.new()
					layer_node.add_child(co)
					co.name = obj_name + " (Area)" if obj_name != "" else "Area"
				else:
					co = StaticBody2D.new()
					layer_node.add_child(co)
					co.name = obj_name + " (SB)" if obj_name != "" else "StaticBody"
				co.owner = _base_node
				co.position = object_base_coords
				co.visible = obj_visible

				var collision_shape = CollisionShape2D.new()
				co.add_child(collision_shape)
				collision_shape.owner = _base_node
				if obj.has("ellipse"):
					var capsule_shape = CapsuleShape2D.new()
					capsule_shape.height = obj_height
					capsule_shape.radius = obj_width / 2.0
					collision_shape.shape = capsule_shape
					collision_shape.name = obj_name if obj_name != "" else "Capsule Shape"
				else: #Rectangle
					var rectangle_shape = RectangleShape2D.new()
					rectangle_shape.size = Vector2(obj_width, obj_height)
					collision_shape.shape = rectangle_shape
					collision_shape.name = obj_name if obj_name != "" else "Rectangle Shape"

				if _map_orientation == "isometric":
					if _iso_rot == 0.0:
						var q = float(_map_tile_height) / float(_map_tile_width)
						q *= q
						var cos_a = sqrt(1 / (q + 1))
						_iso_rot = acos(cos_a) * 180 / PI
						_iso_skew = (90 - 2 * _iso_rot) * PI / 180
						var scale = float(_map_tile_width) / (float(_map_tile_height) * 2 * cos_a)
						_iso_scale = Vector2(scale, scale)

					collision_shape.skew = _iso_skew
					collision_shape.scale = _iso_scale
					obj_rot += _iso_rot

				collision_shape.position = transpose_coords(obj_width / 2.0, obj_height / 2.0, true)
				collision_shape.rotation_degrees = obj_rot
				collision_shape.visible = obj_visible
				if obj.has("properties"):
					handle_properties(co, obj["properties"])
			elif obj_class == object_class.NAVIGATION:
				if obj.has("ellipse"):
					print_rich("[color="+WARNING_COLOR+"] -- Ellipse is unusable for NavigationRegion2D. -> Skipped[/color]")
					_warning_count += 1
				else:
					var nav_region = NavigationRegion2D.new()
					layer_node.add_child(nav_region)
					nav_region.owner = _base_node
					nav_region.name = obj_name + " (NR)" if obj_name != "" else "Navigation"
					nav_region.position = object_base_coords
					nav_region.rotation_degrees = obj_rot
					nav_region.visible = obj_visible
					var nav_poly = NavigationPolygon.new()
					nav_region.navigation_polygon = nav_poly
					nav_poly.add_outline(polygon_from_rectangle(obj_width, obj_height))
					nav_poly.make_polygons_from_outlines()
					if obj.has("properties"):
						handle_properties(nav_region, obj["properties"])
			elif obj_class == object_class.OCCLUDER:
				if obj.has("ellipse"):
					print_rich("[color="+WARNING_COLOR+"] -- Ellipse is unusable for LightOccluder2D. -> Skipped[/color]")
					_warning_count += 1
				else:
					var light_occ = LightOccluder2D.new()
					layer_node.add_child(light_occ)
					light_occ.owner = _base_node
					light_occ.name = obj_name + " (LO)" if obj_name != "" else "Occluder"
					light_occ.position = object_base_coords
					light_occ.rotation_degrees = obj_rot
					light_occ.visible = obj_visible
					var occ_poly = OccluderPolygon2D.new()
					light_occ.occluder = occ_poly
					occ_poly.polygon = polygon_from_rectangle(obj_width, obj_height)
					if obj.has("properties"):
						handle_properties(light_occ, obj["properties"])
			elif obj_class == object_class.POLYGON:
				if obj.has("ellipse"):
					print_rich("[color="+WARNING_COLOR+"] -- Ellipse is unusable for Polygon2D. -> Skipped[/color]")
					_warning_count += 1
				else:
					var polygon = Polygon2D.new()
					layer_node.add_child(polygon)
					polygon.owner = _base_node
					polygon.name = obj_name if obj_name != "" else "Polygon"
					polygon.position = object_base_coords
					polygon.rotation_degrees = obj_rot
					polygon.visible = obj_visible
					polygon.polygon = polygon_from_rectangle(obj_width, obj_height)
					if obj.has("properties"):
						handle_properties(polygon, obj["properties"])	


func get_property(obj: Dictionary, property_name: String, property_type: String):
	var ret = ""
	if not obj.has("properties"): return ret
	for property in obj["properties"]:
		var name = property.get("name", "")
		var type = property.get("type", "string")
		var val = property.get("value", "")
		if name.to_lower() == property_name and type == property_type:
			return val
	return ret


func polygon_from_array(poly_array: Array):
	var polygon = []
	for pt in poly_array:
		var p_coord = transpose_coords(pt["x"], pt["y"], true)
		polygon.append(p_coord)
	return polygon


func polygon_from_rectangle(width: float, height: float):
	var polygon = [Vector2(), Vector2(), Vector2(), Vector2()]
	polygon[0] = Vector2.ZERO
	polygon[1].y = polygon[0].y + height
	polygon[1].x = polygon[0].x
	polygon[2].y = polygon[1].y
	polygon[2].x = polygon[0].x + width
	polygon[3].y = polygon[0].y
	polygon[3].x = polygon[2].x
	polygon[1] = transpose_coords(polygon[1].x, polygon[1].y, true)
	polygon[2] = transpose_coords(polygon[2].x, polygon[2].y, true)
	polygon[3] = transpose_coords(polygon[3].x, polygon[3].y, true)
	return polygon


func transpose_coords(x: float, y: float, no_offset_x: bool = false):
	if _map_orientation == "isometric":
		var trans_x = (x - y) * _map_tile_width / _map_tile_height / 2.0
		if not no_offset_x:
			trans_x += _map_height * _map_tile_width / 2.0
		var trans_y = (x + y) * 0.5
		return Vector2(trans_x, trans_y)

	return Vector2(x, y)


func get_first_gid_index(gid: int):
	var index = 0
	var gid_index = 0
	for first_gid in _first_gids:
		if gid >= first_gid:
			gid_index = index
		index += 1
	return gid_index


func get_matching_source_id(gid: int):
	var limit: int = 0
	var prev_source_id: int = -1
	if _atlas_sources == null:
		return -1
	for src in _atlas_sources:
		var source_id: int = src["sourceId"]
		limit += src["numTiles"] + source_id - prev_source_id - 1
		if gid <= limit:
			return source_id
		prev_source_id = source_id
	return -1


func get_num_tiles_for_source_id(source_id: int):
	for src in _atlas_sources:
		if src["sourceId"] == source_id:
			return src["numTiles"]
	return -1


func load_resource_from_file(path: String):
	var orig_path = path
	var ret: Resource = null
	# ToDo: Not sure if this first check makes any sense since an image can't be properly imported if not in project tree
	if not FileAccess.file_exists(path):
		path = _base_path.get_base_dir().path_join(orig_path)
	if not FileAccess.file_exists(path):
		path = _base_path.path_join(orig_path)
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
	

func handle_properties(target_node: Node, properties: Array):
	var has_children = false
	if target_node is StaticBody2D or target_node is Area2D:
		has_children = target_node.get_child_count() > 0 
	for property in properties:
		var name: String = property.get("name", "")
		var type: String = property.get("type", "string")
		var val: String = str(property.get("value", ""))
		if name == "": continue
		if name.begins_with("__") and has_children:
			var child_prop_dict = {}
			child_prop_dict["name"] = name.substr(2)
			child_prop_dict["type"] = type
			child_prop_dict["value"] = val
			var child_props = []
			child_props.append(child_prop_dict)
			for child in target_node.get_children():
				handle_properties(child, child_props)
		
		# CanvasItem properties
		if name.to_lower() == "modulate" and type == "string":
			target_node.modulate = Color(val)
		elif name.to_lower() == "self_modulate" and type == "string":
			target_node.self_modulate = Color(val)
		elif name.to_lower() == "show_behind_parent" and type == "bool":
			target_node.show_behind_parent = val.to_lower() == "true"
		elif name.to_lower() == "top_level" and type == "bool":
			target_node.top_level = val.to_lower() == "true"
		elif name.to_lower() == "clip_children" and type == "int":
			if int(val) < CanvasItem.CLIP_CHILDREN_MAX:
				target_node.clip_children = int(val)
		elif name.to_lower() == "light_mask" and type == "string":
			target_node.light_mask = get_bitmask_integer_from_string(val, 20)
		elif name.to_lower() == "visibility_layer" and type == "string":
			target_node.visibility_layer = get_bitmask_integer_from_string(val, 20)	
		elif name.to_lower() == "z_index" and type == "int":
			target_node.z_index = int(val)
		elif name.to_lower() == "z_as_relative" and type == "bool":
			target_node.z_as_relative = val.to_lower() == "true"
		elif name.to_lower() == "y_sort_enabled" and type == "bool":
			target_node.y_sort_enabled = val.to_lower() == "true"
		elif name.to_lower() == "texture_filter" and type == "int":
			if int(val) < CanvasItem.TEXTURE_FILTER_MAX:
				target_node.texture_filter = int(val)
		elif name.to_lower() == "texture_repeat" and type == "int":
			if int(val) < CanvasItem.TEXTURE_REPEAT_MAX:
				target_node.texture_repeat = int(val)
		elif name.to_lower() == "material" and type == "file":
			target_node.material = load_resource_from_file(val)
		elif name.to_lower() == "use_parent_material" and type == "bool":
			target_node.use_parent_material = val.to_lower() == "true"
	
		# TileMap properties
		elif name.to_lower() == "cell_quadrant_size" and type == "int" and target_node is TileMap:
			target_node.cell_quadrant_size = int(val)
		elif name.to_lower() == "collision_animatable" and type == "bool" and target_node is TileMap:
			target_node.collision_animatable = val.to_lower() == "true"
		elif name.to_lower() == "collision_visibility_mode" and type == "int" and target_node is TileMap:
			if int(val) < 3:
				target_node.collision_visibility_mode = int(val)
		elif name.to_lower() == "navigation_visibility_mode" and type == "int" and target_node is TileMap:
			if int(val) < 3:
				target_node.navigation_visibility_mode = int(val)
			
		# CollisionObject2D properties
		elif name.to_lower() == "disable_mode" and type == "int" and target_node is CollisionObject2D:
			if int(val) < 3:
				target_node.disable_mode = int(val)
		elif name.to_lower() == "collision_layer" and type == "string" and target_node is CollisionObject2D:
			target_node.collision_layer = get_bitmask_integer_from_string(val, 32)
		elif name.to_lower() == "collision_mask" and type == "string" and target_node is CollisionObject2D:
			target_node.collision_mask = get_bitmask_integer_from_string(val, 32)
		elif name.to_lower() == "collision_priority" and  (type == "float" or type == "int") and target_node is CollisionObject2D:
			target_node.collision_priority = float(val)
		elif name.to_lower() == "input_pickable" and type == "bool" and target_node is CollisionObject2D:
			target_node.input_pickable = val.to_lower() == "true"

		# CollisionPolygon2D properties
		elif name.to_lower() == "build_mode" and type == "int" and has_children and int(val) < 2:
			for child in target_node.get_children():
				if child is CollisionPolygon2D:
					child.build_mode = int(val)

		# CollisionPolygon2D & CollisionShape2D properties
		elif name.to_lower() == "disabled" and type == "bool" and has_children:
			for child in target_node.get_children():
				child.disabled = val.to_lower() == "true"
		elif name.to_lower() == "one_way_collision" and type == "bool" and has_children:
			for child in target_node.get_children():
				child.one_way_collision = val.to_lower() == "true"
		elif name.to_lower() == "one_way_collision_margin" and (type == "float" or type == "int") and has_children:
			for child in target_node.get_children():
				child.one_way_collision_margin = float(val)

		# CollisionShape2D properties
		elif name.to_lower() == "debug_color" and type == "string" and has_children:
			for child in target_node.get_children():
				if child is CollisionShape2D:
					child.debug_color = Color(val)

		# Area2D properties
		elif name.to_lower() == "monitoring" and type == "bool" and target_node is Area2D:
			target_node.monitoring = val.to_lower() == "true"
		elif name.to_lower() == "monitorable" and type == "bool" and target_node is Area2D:
			target_node.monitorable = val.to_lower() == "true"
		elif name.to_lower() == "priority" and (type == "float" or type == "int") and target_node is Area2D:
			target_node.priority = float(val)
		elif name.to_lower() == "gravity_space_override" and type == "int" and target_node is Area2D:
			if int(val) < 5:
				target_node.gravity_space_override = int(val)
		elif name.to_lower() == "gravity_point" and type == "bool" and target_node is Area2D:
			target_node.gravity_point = val.to_lower() == "true"
		elif name.to_lower() == "gravitiy_point_center_x" and (type == "float" or type == "int") and target_node is Area2D:
			target_node.gravity_point_center = Vector2(float(val), target_node.gravity_point_center.y)
		elif name.to_lower() == "gravitiy_point_center_y" and (type == "float" or type == "int") and target_node is Area2D:
			target_node.gravity_point_center = Vector2(target_node.gravity_point_center.x, float(val))
		elif name.to_lower() == "gravity_point_unit_distance" and (type == "float" or type == "int") and target_node is Area2D:
			target_node.gravitiy_point_unit_distance = float(val)
		elif name.to_lower() == "gravitiy_direction_x" and (type == "float" or type == "int") and target_node is Area2D:
			target_node.gravity_direction = Vector2(float(val), target_node.gravity_direction.y)
		elif name.to_lower() == "gravitiy_direction_y" and (type == "float" or type == "int") and target_node is Area2D:
			target_node.gravity_direction = Vector2(target_node.gravity_direction.x, float(val))
		elif name.to_lower() == "gravity" and (type == "float" or type == "int") and target_node is Area2D:
			target_node.gravitiy = float(val)
		elif name.to_lower() == "linear_damp_space_override" and type == "int" and target_node is Area2D:
			if int(val) < 5:
				target_node.linear_damp_space_override = int(val)
		elif name.to_lower() == "linear_damp" and (type == "float" or type == "int") and target_node is Area2D:
			target_node.linear_damp = float(val)
		elif name.to_lower() == "angular_damp_space_override" and type == "int" and target_node is Area2D:
			if int(val) < 5:
				target_node.angular_damp_space_override = int(val)
		elif name.to_lower() == "angular_damp" and (type == "float" or type == "int") and target_node is Area2D:
			target_node.angular_damp = float(val)
			
		# StaticBody2D properties
		elif name.to_lower() == "physics_material" and type == "file" and target_node is StaticBody2D:
			target_node.pysics_material = load_resource_from_file(val)
		elif name.to_lower() == "constant_linear_velocity_x" and (type == "float" or type == "int") and target_node is StaticBody2D:
			target_node.constant_linear_velocity = Vector2(float(val), target_node.constant_linear_velocity.y)
		elif name.to_lower() == "constant_linear_velocity_y" and (type == "float" or type == "int") and target_node is StaticBody2D:
			target_node.constant_linear_velocity = Vector2(target_node.constant_linear_velocity.x, float(val))
		elif name.to_lower() == "constant_angular_velocity" and (type == "float" or type == "int") and target_node is StaticBody2D:
			target_node.constant_angular_velocity = float(val)

		# NavigationRegion2D properties
		elif name.to_lower() == "enabled" and type == "bool" and target_node is NavigationRegion2D:
			target_node.enabled = val.to_lower() == "true"
		elif name.to_lower() == "navigation_layers" and type == "string" and target_node is NavigationRegion2D:
			target_node.navigation_layers = get_bitmask_integer_from_string(val, 32)
		elif name.to_lower() == "enter_cost" and (type == "float" or type == "int") and target_node is NavigationRegion2D:
			target_node.enter_cost = float(val)
		elif name.to_lower() == "travel_cost" and (type == "float" or type == "int") and target_node is NavigationRegion2D:
			target_node.travel_cost = float(val)

		# LightOccluder2D properties
		elif name.to_lower() == "sdf_collision" and type == "bool" and target_node is LightOccluder2D:
			target_node.sdf_collision = val.to_lower() == "true"
		elif name.to_lower() == "occluder_light_mask" and type == "string" and target_node is LightOccluder2D:
			target_node.occluder_light_mask = get_bitmask_integer_from_string(val, 20)

		# Polygon2D properties
		elif name.to_lower() == "color" and type == "string" and target_node is Polygon2D:
			target_node.color = Color(val)

		# Line2D properties
		elif name.to_lower() == "width" and (type == "float" or type == "int") and target_node is Line2D:
			target_node.width = float(val)
		elif name.to_lower() == "default_color" and type == "string" and target_node is Line2D:
			target_node.default_color = Color(val)

		# Marker2D properties
		elif name.to_lower() == "gizmo_extents" and (type == "float" or type == "int") and target_node is Marker2D:
			target_node.gizmo_extents = float(val)

		# Other properties are added as Metadata
		else:
			target_node.set_meta(name, val)
