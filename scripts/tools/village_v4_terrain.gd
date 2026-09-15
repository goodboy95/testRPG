extends SceneTree
const OLD="res://assets/generated/village/terrain-sample-v1/"
const DEST="res://assets/generated/village/terrain-v4/"
const WORK="res://art/terrain/village-v4/"
func _initialize(): call_deferred("run")
func run():
	var args=OS.get_cmdline_user_args()
	var terrain=load((OLD if "--dump-v4" in args else DEST)+"terrain.tscn").instantiate()
	root.add_child(terrain)
	if "--dump-v4" in args:
		DirAccess.make_dir_recursive_absolute(WORK+"baseline")
		for i in range(3):
			var name=["height","control","color"][i]
			assert(terrain.data.export_image(WORK+"baseline/"+name+".exr",i)==OK)
			var img=Image.load_from_file(WORK+"baseline/"+name+".exr")
			img.convert(Image.FORMAT_RGBA8 if i==2 else Image.FORMAT_RF)
			var f=FileAccess.open(WORK+"baseline/"+name+".bin",FileAccess.WRITE)
			f.store_buffer(img.get_data());f.close()
		print("V4_TERRAIN_BASELINE_EXPORTED")
	elif "--import-v4" in args or "--apply-v4-edit" in args:
		if FileAccess.file_exists(DEST+"authored.json") and not "--apply-v4-edit" in args:
			push_error("Authored v4 exists; edit native regions, never reinitialize.");terrain.free();quit(1);return
		var h=Image.create_from_data(512,512,false,Image.FORMAT_RF,FileAccess.get_file_as_bytes(WORK+"height.bin"))
		var c=Image.create_from_data(512,512,false,Image.FORMAT_RF,FileAccess.get_file_as_bytes(WORK+"control.bin"))
		var col=Image.create_from_data(512,512,false,Image.FORMAT_RGBA8,FileAccess.get_file_as_bytes(WORK+"color.bin"))
		terrain.data.import_images([h,c,col],Vector3(-128,0,-128),0.0,1.0)
		terrain.data.save_directory(DEST+"data")
		assert(terrain.data.export_image(WORK+"height-authored.exr",0)==OK)
		var f=FileAccess.open(DEST+"authored.json",FileAccess.WRITE);f.store_string('{"version":4,"nativeRegionsAreEditable":true}');f.close()
		print("V4_TERRAIN_IMPORT_PASS")
	else:
		var heights=FileAccess.get_file_as_bytes(WORK+"height.bin")
		var original=FileAccess.get_file_as_bytes(WORK+"baseline/height.bin")
		var mask=FileAccess.get_file_as_bytes(WORK+"protected.bin")
		var error=0.0;var protected_error=0.0;var count=0
		for z in range(512):
			for x in range(512):
				var idx=z*512+x;var actual=terrain.data.get_height(Vector3(x*.5-128,0,z*.5-128))
				error=max(error,abs(actual-heights.decode_float(idx*4)))
				if mask[idx]:protected_error=max(protected_error,abs(actual-original.decode_float(idx*4)));count+=1
		assert(error<.001 and protected_error<=.01)
		terrain.free();await process_frame
		terrain=load(DEST+"terrain.tscn").instantiate();root.add_child(terrain)
		assert(terrain.data.get_region_count()==4)
		var f=FileAccess.open("res://outputs/home-access/terrain-check.json" if "--home-access" in args else "res://outputs/village-v4/terrain-check.json",FileAccess.WRITE)
		f.store_string(JSON.stringify({"samples":262144,"error":error,"protectedError":protected_error,"protectedSamples":count,"reopen":true},"  "));f.close()
		print("V4_TERRAIN_CHECK_PASS error=",error)
	terrain.free();quit()
