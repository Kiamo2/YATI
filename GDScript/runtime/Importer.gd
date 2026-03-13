# MIT License
#
# Copyright (c) 2023-2025 Roland Helmerichs
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

func import(source_file: String, options: Dictionary = {}):
	var tilemapCreator = preload("TilemapCreator.gd").new()
	if options.get("use_default_filter", false) == true:
		tilemapCreator.set_use_default_filter(true)
	if options.get("add_class_as_metadata", true) == true:
		tilemapCreator.set_add_class_as_metadata(true)
	if options.get("add_id_as_metadata", false) == true:
		tilemapCreator.set_add_id_as_metadata(true)
	if options.get("no_alternative_tiles", false) == true:
		tilemapCreator.set_no_alternative_tiles(true)
	if options.get("map_wangset_to_terrain", false) == true:
		tilemapCreator.set_map_wangset_to_terrain(true)
	if options.has("custom_data_prefix") and options["custom_data_prefix"] != "":
		tilemapCreator.set_custom_data_prefix(options["custom_data_prefix"])
	if options.has("tiled_project_file") and options["tiled_project_file"] != "":
		var ct = CustomTypes.new()
		ct.load_custom_types(options["tiled_project_file"])
		tilemapCreator.set_custom_types(ct)
	if options.has("save_tileset_to") and options["save_tileset_to"] != "":
		tilemapCreator.set_save_tileset_to(options["save_tileset_to"])
	var node2D = tilemapCreator.create(source_file)
	if node2D == null:
		return null
	if options.has("post_processor") and options["post_processor"] != "":
		var post_proc = preload("PostProcessing.gd").new()
		node2D = post_proc.call_post_process(node2D, options["post_processor"])
	return node2D

func import_from_zip(zip_file: String, source_file_in_zip: String, options: Dictionary = {}):
	DataLoader.zip_file = zip_file
	return import(source_file_in_zip, options)
