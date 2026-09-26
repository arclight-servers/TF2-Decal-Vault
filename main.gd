extends Control

var DecalSize = Vector2i(128, 128)

var Message : TextEdit
var MessageText = null
var MessageTimeLeft : float = 0.0
var MouseHoveringOver : bool = false

var ImageContainer : Control
var ImageInput : LineEdit

var LoadDecal : FileDialog

var Loading : Control
var LoadingBar : ProgressBar

const DecalButton = preload("decalbutton.gd")

func _enter_tree() -> void:
	pass
	var err = Steam.steamInitEx(440)
	if err.status != 0:
		Message = err.verbal

# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	Message = get_node("Message")
	ImageContainer = get_node("ScrollContainer/ImageContainer")
	ImageInput = ImageContainer.get_node("Download")
	LoadDecal = get_node("LoadDecal")
	Loading = get_node("Loading")
	LoadingBar = Loading.get_node("ProgressBar")

	if MessageText == null:
		ImageInput.text_submitted.connect(download_image_code)
		var result = Steam.download_ugc_result.connect(_on_download_ugc_result)
		if result != OK:
			MessageText = "Connecting callback download_ugc_result failed"

	Message.hide()
	if MessageText == null:
		Loading.hide()
	else:
		show_message(MessageText, -1)
		Loading.show()
		LoadingBar.hide()
	
	var dir = DirAccess.open("user://saveddecals")
	if dir == null:
		DirAccess.make_dir_absolute("user://saveddecals")
		DirAccess.make_dir_absolute("user://invaliddecals")
	else:
		var files = dir.get_files()
		for file in files:
			var id = file.to_int()
			if id != 0:
				add_image_file("user://saveddecals/" + file, id)
				print(id, " | ", int_to_code(id), " | ", String.num_uint64(id, 10))

# Called every frame. 'delta' is the elapsed time since the previous frame.
func _process(delta: float) -> void:
	if MessageTimeLeft > 0.0 && !MouseHoveringOver:
		MessageTimeLeft -= delta
		if MessageTimeLeft <= 0.0:
			Message.hide()
	
	if LoadingBar.value < 89.0:
		LoadingBar.value += delta * 5.0

func _exit_tree() -> void:
	Steam.steamShutdown()

func show_message(message: String, time: float) -> void:
	MessageText = message
	Message.text = " " + message
	Message.show()
	MessageTimeLeft = time
	print(message)

func code_to_int(code: String) -> int:
	var high = code.left(8).hex_to_int()
	var low = code.right(8).hex_to_int()

	if high >= (1 << 31):
		high = high - (1 << 32)	#uint to int

	return (high << 32) + low

func int_to_code(id: int) -> String:
	var high = id >> 32
	var low = id - (high << 32)
	if high < 0:
		high = (1 << 32) + high	#int to uint
	
	if low < 0:
		low = ~low
	
	return "%08X%08X" % [high, low]

func add_image_file(file: String, id: int) -> void:
	add_image(ImageTexture.create_from_image(Image.load_from_file(file)), id)
	
func add_image(file: ImageTexture, id: int, disabled: bool = false) -> void:
	var button = DecalButton.new()
	button.icon = file
	button.custom_minimum_size = DecalSize
	button.id = int_to_code(id)
	button.expand_icon = true
	button.disabled = disabled
	button.tooltip_text = int_to_code(id)
	ImageContainer.add_child(button)

func start_upload_image() -> void:
	LoadDecal.popup()

func upload_image(file: String) -> void:
	LoadingBar.value = 0.0
	Loading.show()

	var image = Image.load_from_file(file)
	if image == null:
		show_message("Failed Image.load_from_file", 8)
		Loading.hide()
		return

	LoadingBar.value = 1.0
	if image.get_size() != DecalSize:
		image.resize(128, 128)
	
	LoadingBar.value = 10.0
	var png = image.save_png_to_buffer()

func download_image_code(code: String) -> void:
	download_image(code_to_int(code))

func download_image(id: int) -> void:
	LoadingBar.value = 1.0
	Loading.show()
	Steam.ugcDownload(id, 0)

func _on_download_ugc_result(result: Steam.Result, download_data: Dictionary) -> void:
	if result != Steam.Result.RESULT_OK:
		show_message("Download of custom image file from UFS failed. (Error %s)" % result, 8)
		Loading.hide()
		return
	
	LoadingBar.value = 91.0
	var id = download_data.get("handle")
	var datasize = download_data.get("size")
	if id == 0 || datasize < 1:
		show_message("Download of custom image file from UFS (UGC=" + int_to_code(id) + ") failed.", 8)
		Loading.hide()
		return

	LoadingBar.value = 92.0
	var bytes = Steam.ugcRead(id, datasize, 0, Steam.UGCReadAction.UGC_READ_CLOSE)
	if bytes.size() < 1:
		show_message("Reading custom image file from UFS (UGC=" + int_to_code(id) + ") failed.", 8)
		Loading.hide()
		return
	
	LoadingBar.value = 93.0
	var image = Image.new()
	if image.load_png_from_buffer(bytes) != Error.OK:
		show_message("Loading PNG from custom image (UGC=" + int_to_code(id) + ") failed.", 8)
		Loading.hide()
		return
	
	LoadingBar.value = 94.0
	var valid = image.get_size() == DecalSize
	var filepath
	if valid:
		filepath = "user://saveddecals/" + String.num_int64(id) + ".png"
	else:
		show_message("Image downloaded not valid for decals " + image.GetSize(), 8)
		filepath = "user://invaliddecals/" + String.num_int64(id) + ".png"
	
	LoadingBar.value = 95.0
	if image.save_png(filepath) != Error.OK:
		show_message("Failed to save image to " + filepath, 8)

	Loading.hide()
	add_image(ImageTexture.create_from_image(image), id, !valid)

	
