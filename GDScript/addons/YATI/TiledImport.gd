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
extends EditorPlugin

const SAFENESS_NO_INSTANTIATION_SETTING = "YATI/safeness/no_instantiation"

var _xmlImport = null

func _get_plugin_name() -> String:
	return "Yet another Tiled importer"

func _enter_tree():
	_xmlImport = preload("Importer.gd").new()
	add_import_plugin(_xmlImport)

	if not ProjectSettings.has_setting(SAFENESS_NO_INSTANTIATION_SETTING):
		ProjectSettings.set_setting(SAFENESS_NO_INSTANTIATION_SETTING, false)
	ProjectSettings.set_initial_value(SAFENESS_NO_INSTANTIATION_SETTING, false)
	ProjectSettings.add_property_info({
		"name": SAFENESS_NO_INSTANTIATION_SETTING,
		"type": TYPE_BOOL,
	})

func _exit_tree():
	remove_import_plugin(_xmlImport)
	_xmlImport = null
