## Offline authoring only. Explicit init refuses to overwrite saved Terrain3D regions.
extends SceneTree
const DEST = "res://assets/generated/village/terrain-sample-v1/"
const SOURCE = "res://art/terrain/village-sample-v1/"
func _initialize():
	call_deferred("run")
func run():
	if not "--terrain-init" in OS.get_cmdline_user_args():
		push_error("Requires --terrain-init; use the sample editor for subsequent manual changes."); quit(1); return
	if FileAccess.file_exists(DEST+"data/terrain3d_00_00.res"):
		push_error("Already initialized. Refusing to overwrite authored regions."); quit(1); return
	var terrain = ClassDB.instantiate("Terrain3D")
	terrain.name = "Terrain3D"
	terrain.free_editor_textures = false
	terrain.vertex_spacing = 0.5
	terrain.region_size = 256
	terrain.mesh_size = 32
	terrain.mesh_lods = 6
	terrain.cast_shadows = 0
	root.add_child(terrain)
	terrain.material.world_background = 0
	terrain.material.auto_shader = false
	terrain.material.set_shader_param("blend_sharpness", 0.65)
	terrain.assets = ClassDB.instantiate("Terrain3DAssets")
	var ids = ["Grass001", "Ground031", "Gravel014", "Ground037"]
	for i in range(4):
		var asset = ClassDB.instantiate("Terrain3DTextureAsset")
		asset.name = ids[i]
		asset.albedo_texture = load(DEST+ids[i]+"-albedo-height.png")
		asset.normal_texture = load(DEST+ids[i]+"-normal-roughness.png")
		asset.uv_scale = 0.5
		asset.detiling_rotation = 0.12
		terrain.assets.set_texture(i,asset)
	var height = Image.create_from_data(512,512,false,Image.FORMAT_RF,FileAccess.get_file_as_bytes(SOURCE+"height.bin"))
	var control = Image.create_from_data(512,512,false,Image.FORMAT_RF,FileAccess.get_file_as_bytes(SOURCE+"control.bin"))
	var colour = Image.create_from_data(512,512,false,Image.FORMAT_RGBA8,FileAccess.get_file_as_bytes(SOURCE+"color.bin"))
	terrain.data.import_images([height,control,colour],Vector3(-128,0,-128),0.0,1.0)
	DirAccess.make_dir_recursive_absolute(DEST+"data")
	terrain.data.save_directory(DEST+"data")
	terrain.data_directory = DEST+"data"
	terrain.collision.mode = 3
	assert(ResourceSaver.save(terrain.assets,DEST+"terrain-assets.tres")==OK)
	assert(ResourceSaver.save(terrain.material,DEST+"terrain-material.tres")==OK)
	# A headless importer has no GPU uniform defaults. Leave unspecified defaults absent.
	var material_text=FileAccess.get_file_as_string(DEST+"terrain-material.tres")
	var material_file=FileAccess.open(DEST+"terrain-material.tres",FileAccess.WRITE)
	for line in material_text.split("\n"):
		if not ": null" in line:material_file.store_line(line)
	material_file.close()
	# External resources are shared by the editor and runtime, never duplicate embedded copies.
	var scene_file=FileAccess.open(DEST+"terrain.tscn",FileAccess.WRITE)
	scene_file.store_string('[gd_scene load_steps=3 format=3]\n[ext_resource type="Terrain3DMaterial" path="'+DEST+'terrain-material.tres" id="1"]\n[ext_resource type="Terrain3DAssets" path="'+DEST+'terrain-assets.tres" id="2"]\n[node name="Terrain3D" type="Terrain3D"]\ndata_directory = "'+DEST+'data"\nmaterial = ExtResource("1")\nassets = ExtResource("2")\ncollision_mode = 3\nvertex_spacing = 0.5\nfree_editor_textures = false\nmesh_size = 32\nmesh_lods = 6\ncast_shadows = 0\n')
	scene_file.close()
	assert(height.save_exr(SOURCE+"height-initial.exr",true)==OK)
	print("VILLAGE_TERRAIN_IMPORT_PASS regions=",terrain.data.get_region_count())
	terrain.free()
	quit()
