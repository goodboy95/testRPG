extends SceneTree

func tally(node: Node, values: Dictionary) -> void:
	if node is MeshInstance3D:
		values.meshes += 1
		for i in node.mesh.get_surface_count():
			var arrays = node.mesh.surface_get_arrays(i)
			values.vertices += arrays[Mesh.ARRAY_VERTEX].size()
			values.triangles += arrays[Mesh.ARRAY_INDEX].size() / 3
	for child in node.get_children():
		tally(child, values)

func _initialize() -> void:
	var result = {}
	for name in ["BedroomTargetVisual", "BedroomMaterialVisual"]:
		var scene = load("res://scenes/testing/" + name + ".tscn").instantiate()
		var values = {"meshes": 0, "vertices": 0, "triangles": 0}
		tally(scene.get_node("BedroomModel"), values)
		result[name] = values
		scene.free()
	print("BEDROOM_RUNTIME_GEOMETRY ", JSON.stringify(result))
	var file = FileAccess.open("res://outputs/bedroom-material-runtime-geometry.json", FileAccess.WRITE)
	file.store_string(JSON.stringify(result, "  "))
	quit()
