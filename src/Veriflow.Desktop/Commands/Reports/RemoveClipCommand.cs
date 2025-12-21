using Veriflow.Desktop.Commands;
using Veriflow.Desktop.Models;

namespace Veriflow.Desktop.Commands.Reports
{
    /// <summary>
    /// Command to remove a clip from a report item
    /// </summary>
    public class RemoveClipCommand : IUndoableCommand
    {
        private readonly ReportItem _reportItem;
        private readonly ClipLogItem _clip;
        private int _index;

        public string Description => $"Remove Clip: {_clip.InPoint} - {_clip.OutPoint}";

        public RemoveClipCommand(ReportItem reportItem, ClipLogItem clip)
        {
            _reportItem = reportItem;
            _clip = clip;
        }

        public void Execute()
        {
            _index = _reportItem.Clips.IndexOf(_clip);
            _reportItem.Clips.Remove(_clip);
        }

        public void Undo()
        {
            if (_index >= 0 && _index <= _reportItem.Clips.Count)
            {
                _reportItem.Clips.Insert(_index, _clip);
            }
            else
            {
                _reportItem.Clips.Add(_clip);
            }
        }
    }
}
