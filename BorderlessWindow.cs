using Godot;
using System;

public partial class BorderlessWindow : Control
{
	private ResizeHandle topBar;
	private ResizeHandle bottomBar;
	private ResizeHandle leftBar;
	private ResizeHandle rightBar;

	private Vector2 windowTranslationOffset = Vector2.Zero;
	private bool isMovingWindow = false;


	public override void _Ready()
	{
		GetTree().Root.TransparentBg = true;
		DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.Borderless, true);
		DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.AlwaysOnTop, true);

		topBar = GetNode<ResizeHandle>("TopHandle");
		bottomBar = GetNode<ResizeHandle>("BottomHandle");
		leftBar = GetNode<ResizeHandle>("LeftHandle");
		rightBar = GetNode<ResizeHandle>("RightHandle");
		
		MouseDefaultCursorShape = CursorShape.Move;
	}

	public override void _GuiInput(InputEvent ev)
	{
		if(ev is InputEventMouseButton button)
		{
			if(button.ButtonIndex == MouseButton.Left)
			{
				if(button.Pressed && !isMovingWindow)
				{
					isMovingWindow = true;
					windowTranslationOffset = GetLocalMousePosition();
				}
				else
				{
					isMovingWindow = false;
				}
			}
		}
	}

	public override void _Process(double delta)
	{
		if(isMovingWindow)
		{
			DisplayServer.WindowSetPosition(DisplayServer.WindowGetPosition() + (Vector2I)(GetGlobalMousePosition() - windowTranslationOffset));
		}
	}
}
