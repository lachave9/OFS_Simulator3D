using Godot;
using System;
using System.Text;
using System.Linq;

public partial class Simulator3D : Node3D
{
	[Export]
	private string WsSocketUrl = "ws://127.0.0.1:8080/ofs";

	private WebSocketPeer webSocketClient;
	public bool ClientConnected { get; private set; } = false;

	public float CurrentTime { get; private set; } = 0.0f;
	public bool IsPlaying { get; private set; } = false;
	public float PlaybackSpeed { get; private set; } = 1.0f;

	private Label label;
	private MeshInstance3D strokerMesh;
	private Funscript[] scripts = new Funscript[(int)ScriptType.TypeCount];

	public override void _Ready()
	{
		var args = OS.GetCmdlineArgs();
		if(args.Length > 0)
		{
			WsSocketUrl = args[0];
		}

		label = GetNode<Label>("UI/Label");
		strokerMesh = GetNode<MeshInstance3D>("Stroker");

		webSocketClient = new WebSocketPeer();
		connectToServer();
	}
	
	private void connectToServer()
	{
		var error = webSocketClient.ConnectToUrl(WsSocketUrl);
		GD.Print("Connecting to ", WsSocketUrl, " Error: ", error);
		label.Text = $"Trying to connect to {WsSocketUrl}";
	}
	
	private void connectionError()
	{
		ClientConnected = false;
		label.Text = "Connection error";
		
		scripts = new Funscript[(int)ScriptType.TypeCount];
		webSocketClient = new WebSocketPeer();
		connectToServer();
	}

	private void connectionClosed()
	{
		ClientConnected = false;
		label.Text = "Connection closed";

		scripts = new Funscript[(int)ScriptType.TypeCount];
		webSocketClient = new WebSocketPeer();
		connectToServer();
	}

	private void connectionEstablished()
	{
		ClientConnected = true;
		GD.Print("Connection established.");
		label.Text = "";
	}

	// public override void _Input(InputEvent ev)
	// {
	//     if(ev is InputEventKey key)
	//     {
	//         if(key.Pressed && !key.Echo && key.Keycode == Key.P)
	//         {
	//             var playCommand = new Godot.Collections.Dictionary();
	//             playCommand["type"] = "command";
	//             playCommand["name"] = "change_play";
	//             playCommand["data"] = new Godot.Collections.Dictionary()
	//             {
	//                 { "playing", !IsPlaying }
	//             };
	//             var jsonMsg = Json.Stringify(playCommand);
	//             GD.Print(jsonMsg);
	//             webSocketClient.GetPeer(1).SetWriteMode(WebSocketPeer.WriteMode.Text);
	//             webSocketClient.GetPeer(1).PutPacket(Encoding.UTF8.GetBytes(jsonMsg));
	//         }
	//     }
	// }

	private static ScriptType? getScriptType(string name)
	{
		var elements = name.Split('.');
		if (elements.Length == 1 || elements.Last().ToLower().Equals("l0"))
			return ScriptType.MainStroke;
		if (elements.Last().ToLower().Contains("raw"))
			return ScriptType.MainStroke;

		var last = elements.Last().ToLower();
		if (last.Contains("roll") || last.Contains("r1"))
			return ScriptType.Roll;
		else if (last.Contains("pitch") || last.Contains("r2"))
			return ScriptType.Pitch;
		else if (last.Contains("twist") || last.Contains("r0"))
			return ScriptType.Twist;
		else if (last.Contains("sway") || last.Contains("l2"))
			return ScriptType.Sway;
		else if (last.Contains("surge") || last.Contains("l1"))
			return ScriptType.Surge;

		return null;
	}

	private void addOrUpdate(Godot.Collections.Dictionary changeEvent)
	{
		var name = changeEvent["name"].AsString();
		var type = getScriptType(name);
		if(type.HasValue)
		{
			var script = scripts[(int)type.Value];
			if(script == null)
				scripts[(int)type.Value] = new Funscript(changeEvent);
			else 
				script.UpdateFromEvent(changeEvent);
		}
		else 
		{
			GD.PrintErr("Failed to determine script type for ", name);
		}
	}

	private void removeScript(string name)
	{
		var script = scripts
			.Select((x, idx) => new Tuple<Funscript, int>(x, idx))
			.FirstOrDefault(x => x.Item1 != null && x.Item1.Name == name);
		if(script != null)
			scripts[script.Item2] = null;
	}
	



	private void dataReceived()
	{
		var packet = webSocketClient.GetPacket();
		string response = Encoding.UTF8.GetString(packet);

		var json = Json.ParseString(response);
		if(json.VariantType != Variant.Type.Nil)
		{
			var obj = json.AsGodotDictionary();
			if(!obj.ContainsKey("type")) return;

			string type = obj["type"].AsString();
			if(type == "event")
			{
				var data = obj["data"].AsGodotDictionary();
				switch(obj["name"].AsString())
				{
					case "time_change":
						CurrentTime = data["time"].AsSingle();
						break;
					case "project_change":
						scripts = new Funscript[(int)ScriptType.TypeCount];
						break;
					case "play_change":
						IsPlaying = data["playing"].AsBool();
						break;
					case "playbackspeed_change":
						PlaybackSpeed = data["speed"].AsSingle();
						break;
					case "funscript_change":
						GD.Print("Funscript update: ", data["name"]);
						addOrUpdate(data);
						break;
					case "funscript_remove":
						removeScript(data["name"].AsString());
						break;
				}
			}

		}        
	}

	public override void _Process(double delta)
	{
		webSocketClient.Poll();
		
		// Check connection state - simple approach like Godot 3.x
		var state = webSocketClient.GetReadyState();
		
		switch(state)
		{
			case WebSocketPeer.State.Open:
				if (!ClientConnected)
				{
					connectionEstablished();
				}
				// Check for incoming data
				while (webSocketClient.GetAvailablePacketCount() > 0)
				{
					dataReceived();
				}
				break;
			case WebSocketPeer.State.Closed:
				if (ClientConnected)
				{
					connectionClosed();
				}
				break;
		}

		if(IsPlaying) {
			// This is supposed to smooth out the timer
			// in between time updates received via the websocket
			CurrentTime += (float)(delta * PlaybackSpeed);
		}

		float mainStroke = 0.5f;
		float sway = 0.5f;
		float surge = 0.5f;
		float roll = 0.5f;
		float pitch = 0.5f;
		float twist = 0.5f;

		if(scripts[(int)ScriptType.MainStroke] != null)
		{
			var script = scripts[(int)ScriptType.MainStroke];
			mainStroke = script.GetPositionAt(CurrentTime);
		}

		if(scripts[(int)ScriptType.Sway] != null)
		{
			var script = scripts[(int)ScriptType.Sway];
			sway = script.GetPositionAt(CurrentTime);
		}

		if(scripts[(int)ScriptType.Surge] != null)
		{
			var script = scripts[(int)ScriptType.Surge];
			surge = script.GetPositionAt(CurrentTime);
		}

		if(scripts[(int)ScriptType.Roll] != null)
		{
			var script = scripts[(int)ScriptType.Roll];
			roll = script.GetPositionAt(CurrentTime);
		}

		if(scripts[(int)ScriptType.Pitch] != null)
		{
			var script = scripts[(int)ScriptType.Pitch];
			pitch = script.GetPositionAt(CurrentTime);
		}
		
		if(scripts[(int)ScriptType.Twist] != null)
		{
			var script = scripts[(int)ScriptType.Twist];
			twist = script.GetPositionAt(CurrentTime);
		}

		strokerMesh.RotationDegrees = new Vector3(
			0.0f,
			0.0f,
			0.0f
		);

		strokerMesh.GlobalRotate(Vector3.Right,
			Mathf.DegToRad(
				Mathf.Lerp(30f, -30f, pitch)
			)
		);

		strokerMesh.GlobalRotate(Vector3.Forward,
			Mathf.DegToRad(
				Mathf.Lerp(-30.0f, 30.0f, roll)
			)
		);

		strokerMesh.RotateObjectLocal(Vector3.Up, 
			Mathf.DegToRad(
				Mathf.Lerp(-135.0f, 135.0f, twist)
			)
		);

		strokerMesh.Position = new Vector3(
			Mathf.Lerp(0.5f, -0.5f, sway),
			Mathf.Lerp(-1.0f, 1.0f, mainStroke),
			Mathf.Lerp(0.5f, -0.5f, surge)
		);
	}
}
