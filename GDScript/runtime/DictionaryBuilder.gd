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

extends RefCounted

enum FileType {
	Xml,
	Json,
	Unknown
}

func get_dictionary(source_file: String, za: ZipAccess = null):
	var checked_file = source_file
	if za:
		if not za.file_exists(checked_file):
			printerr("ERROR: File '" + source_file + "' not found. -> Continuing but result may be unusable")
			return null
	elif !FileAccess.file_exists(checked_file):
		checked_file = source_file.get_base_dir().path_join(source_file)
		if !FileAccess.file_exists(checked_file):
			printerr("ERROR: File '" + source_file + "' not found. -> Continuing but result may be unusable")
			return null

	var type = FileType.Unknown
	var extension = source_file.get_file().get_extension()
	if ["tmx", "tsx", "xml", "tx"].find(extension) >= 0:
		type = FileType.Xml
	elif ["tmj", "tsj", "json", "tj", "tiled-project"].find(extension) >= 0:
		type = FileType.Json
	else:
		var chunk
		if za:
			var file_bytes = za.get_file(checked_file).slice(0, 11)
			chunk = file_bytes.get_string_from_utf8()
		else:
			var file = FileAccess.open(checked_file, FileAccess.READ)
			chunk = file.get_buffer(12)
			file.close()

		if chunk.starts_with("<?xml "):
			type = FileType.Xml
		elif chunk.starts_with("{ \""):
			type = FileType.Json

	match type:
		FileType.Xml:
			var dict_builder = preload("DictionaryFromXml.gd").new()
			return dict_builder.create(checked_file, za)
		FileType.Json:
			var json = JSON.new()
			if za:
				if json.parse(za.get_file(checked_file).get_string_from_utf8()) == OK:
					return json.data
			else:
				var file = FileAccess.open(checked_file, FileAccess.READ)
				if json.parse(file.get_as_text()) == OK:
					file.close()
					return json.data
		FileType.Unknown:
			printerr("ERROR: File '" + source_file + "' has an unknown type. -> Continuing but result may be unusable")

	return null
