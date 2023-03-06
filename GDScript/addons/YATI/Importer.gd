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
extends EditorImportPlugin

func _get_importer_name() -> String:
	return "YATI"

func _get_visible_name() -> String:
	return "Import from Tiled"

func _get_recognized_extensions() -> PackedStringArray:
	return PackedStringArray(["tmx", "tmj"])

func _get_resource_type() -> String:
	return "Node2D"

func _get_save_extension() -> String:
	return "tscn"
	
func _get_priority() -> float:
	return 0.1
	
func _get_preset_count() -> int:
	return 0

func _get_preset_name(preset_index: int) -> String:
	return ""

func _get_import_options(path: String, preset_index: int) -> Array:
	return [
		{ "name": "use_tilemap_layers", "default_value": false },
		{ "name": "use_default_filter", "default_value": false },
		{ "name": "post_processor", "default_value": "", "property_hint": PROPERTY_HINT_FILE, "hint_string": "*.gd;GDScript" },
		{ "name": "save_tileset_to", "default_value": "", "property_hint": PROPERTY_HINT_SAVE_FILE, "hint_string": "*.tres;Resource File" }
	]

func _get_import_order() -> int:
	return 99

func _get_option_visibility(path: String, option_name: StringName, options: Dictionary) -> bool:
	return true

func _import(source_file: String, save_path: String, options: Dictionary, platform_variants: Array, gen_files: Array):
	print("Import file '" + source_file + "'")
	if !FileAccess.file_exists(source_file):
		printerr("Import file '" + source_file + "' not found!")
		return ERR_FILE_NOT_FOUND

	var tilemapCreator = preload("TilemapCreator.gd").new()
	if options["use_tilemap_layers"] == false:
		tilemapCreator.set_map_layers_to_tilemaps(true)
	if options["use_default_filter"] == true:
		tilemapCreator.set_use_default_filter(true)
	var node2D = tilemapCreator.create(source_file)
	if node2D == null:
		return FAILED

	var errors = tilemapCreator.get_error_count()
	var warnings = tilemapCreator.get_warning_count()
	if options.has("save_tileset_to") and options["save_tileset_to"] != "":
		var tile_set = tilemapCreator.get_tileset()
		var save_ret = ResourceSaver.save(tile_set, options["save_tileset_to"])
		if save_ret == OK:
			print("Successfully saved tileset to '" + options["save_tileset_to"] + "'")
		else:
			printerr("Saving tileset returned error " + str(save_ret))
			errors += 1

	var post_proc_error = false
	if options.has("post_processor") and options["post_processor"] != "":
		var post_proc = preload("PostProcessing.gd").new()
		node2D = post_proc.call_post_process(node2D, options["post_processor"])
		post_proc_error = post_proc.get_error() != OK

	var packed_scene = PackedScene.new()
	packed_scene.pack(node2D)
	# return ResourceSaver.save(packed_scene, source_file.get_basename() + "." + _get_save_extension())
	var ret = ResourceSaver.save(packed_scene, save_path + "." + _get_save_extension())
	if ret == OK:
		var dir = DirAccess.open(source_file.get_basename().get_base_dir())
		ret = dir.copy(save_path + "." + _get_save_extension(), source_file.get_basename() + "." + _get_save_extension())
	if ret == OK:
		var final_message_string = "Import succeeded."
		if post_proc_error:
			final_message_string = "Import finished."
		if errors > 0 or warnings > 0:
			final_message_string = "Import finished with "
			if errors > 0:
				final_message_string += str(errors) + " error"
			if errors > 1:
				final_message_string += "s"
			if warnings > 0:
				if errors > 0:
					final_message_string += " and "
				final_message_string += str(warnings) + " warning"
				if warnings > 1:
					final_message_string += "s"
			final_message_string += "."
		print(final_message_string)
		if post_proc_error:
			print("Postprocessing was skipped due to some error.")
	return ret
