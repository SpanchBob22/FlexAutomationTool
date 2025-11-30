using System.Windows.Forms; 
using WindowsInput;
using FlexAutomator.Blocks.Base;
using ExecutionContext = FlexAutomator.Blocks.Base.ExecutionContext;

namespace FlexAutomator.Blocks.Actions;

public class MouseClickActionBlock : ActionBlock
{
    public override string Type => "MouseClick";

    public override async Task<BlockResult> ExecuteAsync(ExecutionContext context)
    {
        await Task.CompletedTask;

        try
        {
            if (!Parameters.TryGetValue("X", out var xStr) || !int.TryParse(xStr, out var targetX))
                return BlockResult.Failed("Некоректна координата X");

            if (!Parameters.TryGetValue("Y", out var yStr) || !int.TryParse(yStr, out var targetY))
                return BlockResult.Failed("Некоректна координата Y");

            var button = Parameters.TryGetValue("Button", out var buttonStr) ? buttonStr : "Left";

            var simulator = new InputSimulator();


            var virtualScreen = SystemInformation.VirtualScreen;


            double absoluteX = ((double)targetX - virtualScreen.Left) * 65535.0d / virtualScreen.Width;
            double absoluteY = ((double)targetY - virtualScreen.Top) * 65535.0d / virtualScreen.Height;

            if (absoluteX < 0) absoluteX = 0;
            if (absoluteX > 65535) absoluteX = 65535;
            if (absoluteY < 0) absoluteY = 0;
            if (absoluteY > 65535) absoluteY = 65535;

            simulator.Mouse.MoveMouseToPositionOnVirtualDesktop(absoluteX, absoluteY);

            switch (button.ToLower())
            {
                case "left":
                    simulator.Mouse.LeftButtonClick();
                    break;
                case "right":
                    simulator.Mouse.RightButtonClick();
                    break;
                default:
                    return BlockResult.Failed($"Підтримується лише Left та Right кнопки миші");
            }

            return BlockResult.Successful();
        }
        catch (Exception ex)
        {
            return BlockResult.Failed($"Помилка кліку миші: {ex.Message}");
        }
    }
}