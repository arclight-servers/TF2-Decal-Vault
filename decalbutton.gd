extends Button

var id

func _pressed() -> void:
	DisplayServer.clipboard_set(id)
	get_parent().get_parent().get_parent().show_message("Copied \"" + id + "\" to clipboard", 3);