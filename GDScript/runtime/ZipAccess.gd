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

class_name ZipAccess

extends RefCounted

var _zip_file_path: String
var _reader: ZIPReader

func open(zip_file_path: String) -> Error:
    _zip_file_path = zip_file_path
    _reader = ZIPReader.new()
    return _reader.open(_zip_file_path)

func file_exists(file_in_zip: String) -> bool:
    return _reader.file_exists(file_in_zip)

func get_file(file_in_zip: String) -> PackedByteArray:
    return _reader.read_file(file_in_zip)

func get_zip_file_path() -> String:
    return _zip_file_path

func close():
    _reader.close()
