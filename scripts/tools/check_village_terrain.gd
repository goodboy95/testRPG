## Read-only native-data roundtrip; exports only to the verification output folder.
extends SceneTree
const DEST="res://assets/generated/village/terrain-sample-v1/"
func _initialize():
	call_deferred("run")
func run():
	var scene=load(DEST+"terrain.tscn")
	var terrain=scene.instantiate()
	root.add_child(terrain)
	var samples=FileAccess.get_file_as_bytes("res://art/terrain/village-sample-v1/height.bin")
	var baseline=FileAccess.get_file_as_bytes("res://art/terrain/village-sample-v1/baseline.bin")
	var protected=FileAccess.get_file_as_bytes("res://art/terrain/village-sample-v1/protected.bin")
	var max_error=0.0
	var protected_error=0.0
	for z in range(512):
		for x in range(512):
			var index=z*512+x
			var actual=terrain.data.get_height(Vector3(x*.5-128,0,z*.5-128))
			max_error=max(max_error,abs(actual-samples.decode_float(index*4)))
			if protected[index]:protected_error=max(protected_error,abs(actual-baseline.decode_float(index*4)))
	assert(max_error<.001)
	assert(protected_error<=.01)
	var out="res://outputs/village-terrain/native-check/"
	DirAccess.make_dir_recursive_absolute(out)
	assert(terrain.data.export_image(out+"height.exr",0)==OK)
	var exported=Image.load_from_file(out+"height.exr")
	assert(exported.get_width()==512 and exported.get_height()==512)
	# EXR loaders may expand a single float channel to RGBF; preserve precision, not channel count.
	assert(exported.get_format() in [Image.FORMAT_RF,Image.FORMAT_RGBF,Image.FORMAT_RGBAF])
	for z in range(0,512,7):
		for x in range(0,512,7):assert(abs(exported.get_pixel(x,z).r-samples.decode_float((z*512+x)*4))<.001)
	terrain.free()
	await process_frame
	terrain=scene.instantiate();root.add_child(terrain)
	assert(terrain.data.get_region_count()==4)
	assert(abs(terrain.data.get_height(Vector3(-56,0,55))-samples.decode_float((366*512+144)*4))<.001)
	terrain.free()
	var f=FileAccess.open(out+"report.json",FileAccess.WRITE)
	f.store_string(JSON.stringify({"samples":262144,"maximumError":max_error,"protectedError":protected_error,"reopen":true,"floatExportRoundtrip":true},"  "))
	f.close()
	print("VILLAGE_TERRAIN_NATIVE_CHECK_PASS samples=262144 protectedError=",protected_error)
	quit()
