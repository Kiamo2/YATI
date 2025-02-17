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

class_name DataLoader

static var zip_file: String

static func get_tiled_file_content(file_name: String, base_path: String):
    if zip_file == null:
        return get_tiled_file_content_from_file(file_name, base_path)
    else:
        return get_tiled_file_content_from_zip(file_name, base_path)

static func get_tiled_file_content_from_file(file_name: String, base_path: String):
    var checked_file = file_name
    if not FileAccess.file_exists(checked_file):
        checked_file = base_path.path_join(file_name)
    if not FileAccess.file_exists(checked_file): return null

    var file = FileAccess.open(checked_file, FileAccess.ModeFlags.READ)
    var ret = file.get_buffer(file.get_length())
    return ret

static func get_tiled_file_content_from_zip(file_name: String, base_path: String):
    var reader = ZIPReader.new()
    if reader.open(zip_file) == OK:
        var file_in_zip = file_name
        if not reader.file_exists(file_in_zip):
            file_in_zip = CommonUtils.cleanup_path(base_path.path_join(file_in_zip))
        if not reader.file_exists(file_in_zip): return null

        var ret = reader.read_file(file_in_zip)
        reader.close()
        return ret
    
    printerr("ERROR: Unable to open zip file '" + zip_file + "'.")
    CommonUtils.error_count += 1
    return null

static func load_image(file_name: String, base_path: String):
    if zip_file == null:
        return load_image_from_file(file_name, base_path)
    else:
        return load_image_from_zip(file_name, base_path)

static func load_image_from_file(file_name: String, base_path: String):
    var checked_file = file_name
    if not FileAccess.file_exists(checked_file):
        checked_file = base_path.path_join(file_name)
    if not FileAccess.file_exists(checked_file):
        printerr("ERROR: Image file '" + file_name + "' not found.")
        CommonUtils.error_count += 1
        return null

    var exists = ResourceLoader.exists(checked_file, "Image")
    if exists:
        return ResourceLoader.load(checked_file)

    var image = Image.load_from_file(checked_file)
    return ImageTexture.create_from_image(image)

static func load_image_from_zip(file_name: String, base_path: String):
    var reader = ZIPReader.new()
    if reader.open(zip_file) == OK:
        var file_in_zip = file_name
        if not reader.file_exists(file_in_zip):
            file_in_zip = CommonUtils.cleanup_path(base_path.path_join(file_in_zip))
        if not reader.file_exists(file_in_zip): return null

        var image_bytes = reader.read_file(file_in_zip)
        var image = Image.new()
        var extension = file_name.get_extension().to_lower()
        match extension:
            "png":
                image.load_png_from_buffer(image_bytes)
            "jpg", "jpeg":
                image.load_jpg_from_buffer(image_bytes)
            "bmp":
                image.load_bmp_from_buffer(image_bytes)
        reader.close()
        return ImageTexture.create_from_image(image)
    
    printerr("ERROR: Unable to open zip file '" + zip_file + "'.")
    CommonUtils.error_count += 1
    return null

static func load_resource_from_file(resource_file: String, base_path: String):
    var checked_file = resource_file
    if not FileAccess.file_exists(checked_file):
        checked_file = base_path.path_join(resource_file)
    if FileAccess.file_exists(checked_file):
        return ResourceLoader.load(checked_file)

    printerr("ERROR: Resource file '" + resource_file + "' not found.")
    CommonUtils.error_count += 1
    return null
