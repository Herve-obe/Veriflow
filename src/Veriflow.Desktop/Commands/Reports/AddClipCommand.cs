using Veriflow.Desktop.Commands;
using Veriflow.Desktop.Models;

namespace Veriflow.Desktop.Commands.Reports
{
    /// <summary>
    /// Command to add a clip to a report item
    /// </summary>
    public class AddClipCommand : IUndoableCommand
    {
        private readonly ReportItem _reportItem;
        private readonly ClipLogItem _clip;

        public string Description => $"Add Clip: {_clip.InPoint} - {_clip.OutPoint}";

        public AddClipCommand(ReportItem reportItem, ClipLogItem clip)
        {
            _reportItem = reportItem;
            _clip = clip;
        }

        public void Execute()
        {
            _reportItem.Clips.Add(_clip);
        }

        public void Undo()
        {
            _reportItem.Clips.Remove(_clip);
        }
    }
}
