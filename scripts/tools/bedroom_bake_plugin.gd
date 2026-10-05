@tool
extends EditorPlugin

# Installed only in the disposable baking project, never enabled in the game.
# Godot 4.6 does not bind LightmapGI.bake; use its built-in editor action.
func _enter_tree() -> void:
	if "--bedroom-bake" in OS.get_cmdline_user_args():
		call_deferred("run_bake")

func nodes(root: Node) -> Array[Node]:
	var result: Array[Node] = [root]
	for child in root.get_children():
		result.append_array(nodes(child))
	return result

func run_bake() -> void:
	await get_tree().create_timer(2.0).timeout
	EditorInterface.open_scene_from_path("res://scenes/testing/BedroomTargetVisual.tscn")
	await get_tree().create_timer(2.0).timeout
	var root := EditorInterface.get_edited_scene_root()
	if root == null:
		push_error("BEDROOM_BAKE_FAILED no edited scene")
		get_tree().quit(1)
		return
	var gi: LightmapGI = root.get_node("LightmapGI")
	gi.light_data = null
	EditorInterface.edit_node(gi)
	EditorInterface.get_selection().clear()
	EditorInterface.get_selection().add_node(gi)
	await get_tree().create_timer(1.0).timeout
	var button: Button
	for node in nodes(EditorInterface.get_base_control()):
		if node is Button and node.text == "Bake Lightmaps":
			button = node
			break
	if button == null or button.disabled:
		push_error("BEDROOM_BAKE_FAILED editor bake action unavailable")
		get_tree().quit(1)
		return
	print("BEDROOM_BAKE_BEGIN")
	# Same built-in action as selecting a destination in the editor save dialog.
	# Supplying the file directly avoids modal dialogs in offline automation.
	var dialog: EditorFileDialog = button.get_child(0)
	dialog.emit_signal("file_selected", "res://assets/generated/bedroom-target-v2/bedroom.lmbake")
	await get_tree().create_timer(1.0).timeout
	if gi.light_data == null or gi.light_data.get_user_count() == 0:
		push_error("BEDROOM_BAKE_FAILED no baked users")
		get_tree().quit(1)
		return
	EditorInterface.save_scene()
	print("BEDROOM_BAKE_PASS users=", gi.light_data.get_user_count())
	get_tree().quit()
