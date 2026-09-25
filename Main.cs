namespace DecalVault;

using System;
using System.Threading.Tasks;
using Godot;
using Steamworks;

public partial class Main : Control
{
	private struct QueuedImageButton
	{
		public ImageTexture Texture;
		public ulong Id;
		public bool Disabled;
	}
	
	private Vector2I DecalSize = new() { X = 128, Y = 128 };

	private TextEdit Message;
	private string MessageText;
	private double MessageTimeLeft;
	private bool MouseHoveringOver;

	private HFlowContainer ImageContainer;
	private LineEdit ImageInput;
	private QueuedImageButton? QueuedImage;

	private FileDialog LoadDecal;

	private Control Loading;
	private ProgressBar LoadingBar;

	public override void _EnterTree()
	{
		try
		{
			SteamClient.Init(440);
		}
		catch (System.Exception e)
		{
			MessageText = e.Message;
		}
	}
	public override void _Ready()
	{
		Message = GetNode<TextEdit>("Message");

		try
		{
			Message.Hide();
			Node scroll = GetNode("ScrollContainer");
			ImageContainer = scroll.GetNode<HFlowContainer>("ImageContainer");
			ImageInput = ImageContainer.GetNode<LineEdit>("Download");
			LoadDecal = GetNode<FileDialog>("LoadDecal");
			Loading = GetNode<Control>("Loading");
			LoadingBar = Loading.GetNode<ProgressBar>("ProgressBar");

			Loading.Hide();

			LoadImagesAsync();
		}
		catch (Exception e)
		{
			MessageText = e.Message;
		}

		if (MessageText != null)
			ShowMessage(MessageText, -1);
	}
	public override void _Process(double delta)
	{
		if (MessageTimeLeft > 0.0 && !MouseHoveringOver)
		{
			MessageTimeLeft -= delta;
			if (MessageTimeLeft <= 0.0)
			{
				Message.Hide();
			}
		}

		if(QueuedImage != null)
		{
			var image = (QueuedImageButton)QueuedImage;
			AddImage(image.Texture, image.Id, image.Disabled);
		}

		SteamClient.RunCallbacks();
	}
	public override void _ExitTree()
	{
		SteamClient.Shutdown();
	}

	public void ShowMessage(string message, double time)
	{
		MessageText = message;
		Message.Text = " " + MessageText;
		Message.Show();
		MessageTimeLeft = time;
		GD.Print(message);
	}
	private async Task LoadImagesAsync()
	{
		try
		{
			using var dir = DirAccess.Open("user://saveddecals");
			if (dir != null)
			{
				string[] files = dir.GetFiles();

				foreach (var file in files)
				{
					if (ulong.TryParse(file.GetBaseName(), out ulong id))
					{
						AddImage("user://saveddecals/" + file, id);
					}
					else
					{
						ShowMessage("Unknown ID \"" + file.GetBaseName() + "\" in saved decals", 8);
						await Task.Delay(3000);
					}
				}
			}
			else
			{
				DirAccess.MakeDirAbsolute("user://saveddecals");
				DirAccess.MakeDirAbsolute("user://invaliddecals");
			}
		}
		catch (Exception e)
		{
			ShowMessage(e.Message, 8);
		}
	}
	private void AddImage(string filepath, ulong id, bool disabled = false)
	{
		try
		{
			AddImage(Image.LoadFromFile(filepath), id, disabled);
		}
		catch (Exception e)
		{
			ShowMessage(e.Message, 8);
		}
	}
	private void AddImage(Image image, ulong id, bool disabled = false)
	{
		try
		{
			AddImage(ImageTexture.CreateFromImage(image), id, disabled);
		}
		catch (Exception e)
		{
			ShowMessage(e.Message, 8);
		}
	}
	private void AddImage(ImageTexture texture, ulong id, bool disabled = false)
	{
		try
		{
			DecalButton button = new()
			{
				Icon = texture,
				CustomMinimumSize = new() { X = 128.0f, Y = 128.0f },
				ID = id,
				ExpandIcon = true,
				Disabled = disabled,
				TooltipText = GameFormatedId(id)
			};

			ImageContainer.AddChild(button);
		}
		catch (Exception e)
		{
			ShowMessage(e.Message, 8);
		}
	}
	public void MouseEnterMessageBox()
	{
		MouseHoveringOver = true;
	}
	public void MouseExitMessageBox()
	{
		MouseHoveringOver = false;
	}
	public void StartUploadImage()
	{
		try
		{
			LoadDecal.Popup();
		}
		catch (Exception e)
		{
			ShowMessage(e.Message, 8);
		}
	}
	public void UploadImage(string file)
	{
		try
		{
			LoadingBar.Value = 0.0f;
			Loading.Show();
			UploadImageAsync(file);
		}
		catch (Exception e)
		{
			ShowMessage(e.Message, 8);
		}
	}
	private async Task UploadImageAsync(string file)
	{
		try
		{
			LoadingBar.Value = 1.0f;
			var image = Image.LoadFromFile(file);

			LoadingBar.Value = 2.0f;
			if (image.GetSize() != DecalSize)
				image.Resize(128, 128);

			LoadingBar.Value = 10.0f;
			var png = image.SavePngToBuffer();

			LoadingBar.Value = 11.0f;
			var storage = "my_custom_images/0.png";

			LoadingBar.Value = 12.0f;
			if (!SteamRemoteStorage.FileWrite(storage, png))
				throw new Exception("Failed to save local copy of custom image \"" + storage + "\"");

			LoadingBar.Value = 13.0f;

			var task = SteamRemoteStorage.FileShareAsync(storage);
			while (!task.IsCompleted && LoadingBar.Value < 89.0f)
			{
				LoadingBar.Value += 0.1f;
				await Task.Delay(1);
			}

			var result = await task ?? throw new Exception("Custom texture uploaded to cloud FAILED");

			LoadingBar.Value = 91.0f;
			ulong id = result.Value;
			ShowMessage("Custom texture uploaded to cloud completed OK, assigned UGC ID " + GameFormatedId(id), 3);

			LoadingBar.Value = 92.0f;
			SteamRemoteStorage.FileDelete(storage);

			LoadingBar.Value = 93.0f;
			await DownloadImageAsync(id);
		}
		catch (Exception e)
		{
			ShowMessage(e.Message, 8);
		}

		Loading.Hide();
	}
	public void DownloadImage(string input)
	{
		try
		{
			ImageInput.Clear();

			ulong id;

			if (input.Length == 16)
			{
				string high = input.Left(8);
				string low = input.Right(8);

				id = (((ulong)high.HexToInt()) << 32) + (ulong)low.HexToInt();

				if(GameFormatedId(id) != input)
					throw new Exception("Could not convert code to uint64 \"" + input + "\" vs \"" + GameFormatedId(id) + "\"");
			}
			else
			{
				id = ulong.Parse(input);
			}
			
			LoadingBar.Value = 0.0f;
			Loading.Show();
			DownloadImageAsync(id);
		}
		catch (Exception e)
		{
			ShowMessage(e.Message, 8);
		}
	}
	private async Task DownloadImageAsync(ulong id)
	{
		try
		{
			LoadingBar.Value = 1.0f;

			var task = SteamRemoteStorage.UgcDownloadAsync(id);
			while (!task.IsCompleted && LoadingBar.Value < 89.0f)
			{
				LoadingBar.Value += 0.1f;
				await Task.Delay(1);
			}

			var result = await task ?? throw new Exception("Download of custom image file from UFS (UGC=" + GameFormatedId(id) + ") failed.");

			LoadingBar.Value = 91.0f;
			byte[] buffer;
			if ((result.SizeInBytes < 1) || (buffer = SteamRemoteStorage.UgcRead(result.Handle)) == null)
				throw new Exception("GetUGCDetails failed? (UGC=" + GameFormatedId(id) + " nFileSizeInBytes=" + result.SizeInBytes + ")");

			LoadingBar.Value = 92.0f;
			Image image = new();
			if (image.LoadPngFromBuffer(buffer) != Error.Ok)
				throw new Exception("Corrupt PNG file, UGC=" + GameFormatedId(id) + ".");

			LoadingBar.Value = 93.0f;
			bool valid = image.GetSize() == DecalSize;
			string filepath;
			if (valid)
			{
				filepath = "user://saveddecals/" + id + ".png";
			}
			else
			{
				ShowMessage("Image downloaded not valid for decals " + image.GetSize(), 8);
				filepath = "user://invaliddecals/" + id + ".png";
			}

			LoadingBar.Value = 94.0f;
			if (image.SavePng(filepath) != Error.Ok)
				ShowMessage("Failed to save image to " + filepath, 8);

			LoadingBar.Value = 95.0f;
			AddImage(image, id, !valid);
		}
		catch (Exception e)
		{
			ShowMessage(e.Message, 8);
		}

		Loading.Hide();
	}
	
	public static string GameFormatedId(ulong id)
	{
		return ((int)(id >> 32)).ToString("X8") + ((int)id).ToString("X8");
	}
}

public partial class DecalButton : Button
{
	public ulong ID;

	public override void _Pressed()
	{
		string code = Main.GameFormatedId(ID);
		DisplayServer.ClipboardSet(code);
		GetParent().GetParent().GetParent<Main>().ShowMessage("Copied \"" + code + "\" to clipboard", 3);
	}
}
