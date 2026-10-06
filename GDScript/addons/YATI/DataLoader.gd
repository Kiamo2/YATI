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

@tool
class_name DataLoader

const CommonUtils = preload("CommonUtils.gd")

static func get_tiled_file_content(file_name: String, base_path: String):
    var checked_file = file_name
    if not FileAccess.file_exists(checked_file):
        checked_file = base_path.path_join(file_name)
    if not FileAccess.file_exists(checked_file): return null

    var file = FileAccess.open(checked_file, FileAccess.ModeFlags.READ)
    var ret = file.get_buffer(file.get_length())
    return ret

static func load_image(file_name: String, base_path: String, transparent_color_hex: String = ""):
    var checked_file = file_name
    if not FileAccess.file_exists(checked_file):
        checked_file = base_path.path_join(file_name)

    var has_source_file: bool = FileAccess.file_exists(checked_file)
    var use_transparent_key: bool = not transparent_color_hex.strip_edges().is_empty()
    if ResourceLoader.exists(checked_file) and not use_transparent_key:
        return ResourceLoader.load(checked_file)

    if not has_source_file:
        printerr("ERROR: Image file '" + file_name + "' not found.")
        CommonUtils.error_count += 1
        return null

    var image = Image.load_from_file(checked_file)
    if image == null:
        return null
    if use_transparent_key:
        _apply_transparent_color_key(image, transparent_color_hex)
    return ImageTexture.create_from_image(image)

static func _apply_transparent_color_key(image: Image, transparent_color_hex: String) -> void:
    var clean_hex: String = transparent_color_hex.strip_edges()
    if clean_hex.is_empty():
        return
    var key_color: Color = Color(clean_hex)
    var key_r: int = int(round(key_color.r * 255.0))
    var key_g: int = int(round(key_color.g * 255.0))
    var key_b: int = int(round(key_color.b * 255.0))
    var width: int = image.get_width()
    var height: int = image.get_height()
    for y in range(height):
        for x in range(width):
            var pixel: Color = image.get_pixel(x, y)
            var p_r: int = int(round(pixel.r * 255.0))
            var p_g: int = int(round(pixel.g * 255.0))
            var p_b: int = int(round(pixel.b * 255.0))
            if p_r == key_r and p_g == key_g and p_b == key_b:
                image.set_pixel(x, y, Color(pixel.r, pixel.g, pixel.b, 0.0))

static func load_resource_from_file(resource_file: String, base_path: String):
    var checked_file = resource_file
    if not FileAccess.file_exists(checked_file):
        checked_file = base_path.path_join(resource_file)
    if FileAccess.file_exists(checked_file):
        return ResourceLoader.load(checked_file)

    printerr("ERROR: Resource file '" + resource_file + "' not found.")
    CommonUtils.error_count += 1
    return null
