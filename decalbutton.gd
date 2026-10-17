extends Button

var id
var lastclick = 0

func _pressed() -> void:
	if lastclick > Time.get_ticks_msec():
		OS.shell_open(ProjectSettings.globalize_path("user://newdecals"))
	else:
		DisplayServer.clipboard_set(id)
		get_parent().get_parent().get_parent().show_message("Copied \"" + id + "\" to clipboard", 3)
		lastclick = Time.get_ticks_msec() + 1000