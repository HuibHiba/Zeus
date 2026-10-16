extends SceneTree

# Read notices from the exact Godot binary used for the export.
func _initialize() -> void:
	var args := OS.get_cmdline_user_args()
	if args.size() != 1:
		push_error("Expected the destination notice file")
		quit(1)
		return
	var output := FileAccess.open(args[0], FileAccess.WRITE)
	if output == null:
		push_error("Cannot write Godot notices")
		quit(1)
		return
	output.store_string("Godot Engine " + Engine.get_version_info().string + "\n")
	output.store_string("https://godotengine.org/license/\n\n")
	output.store_string(Engine.get_license_text() + "\n\n")
	output.store_string("COMPONENT COPYRIGHTS\n")
	output.store_string(JSON.stringify(Engine.get_copyright_info(), "  ") + "\n\n")
	output.store_string("COMPONENT LICENSE TEXTS\n")
	var licenses := Engine.get_license_info()
	var names := licenses.keys()
	names.sort()
	for name in names:
		output.store_string("\n--- " + str(name) + " ---\n" + str(licenses[name]) + "\n")
	output.close()
	print("Godot notices written")
	quit()
