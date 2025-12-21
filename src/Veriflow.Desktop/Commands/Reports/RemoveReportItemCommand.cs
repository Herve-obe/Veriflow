using Veriflow.Desktop.Commands;
using Veriflow.Desktop.Models;
using Veriflow.Desktop.ViewModels;

namespace Veriflow.Desktop.Commands.Reports
{
    /// <summary>
    /// Command to remove a report item
    /// </summary>
    public class RemoveReportItemCommand : IUndoableCommand
    {
        private readonly ReportsViewModel _viewModel;
        private readonly ReportItem _item;
        private readonly bool _isVideo;
        private int _index;

        public string Description => $"Remove {(_isVideo ? "Camera" : "Sound")} Report: {_item.Filename}";

        public RemoveReportItemCommand(ReportsViewModel viewModel, ReportItem item, bool isVideo)
        {
            _viewModel = viewModel;
            _item = item;
            _isVideo = isVideo;
        }

        public void Execute()
        {
            var collection = _isVideo ? _viewModel.VideoReportItems : _viewModel.AudioReportItems;
            _index = collection.IndexOf(_item);
            collection.Remove(_item);
        }

        public void Undo()
        {
            var collection = _isVideo ? _viewModel.VideoReportItems : _viewModel.AudioReportItems;
            if (_index >= 0 && _index <= collection.Count)
            {
                collection.Insert(_index, _item);
            }
            else
            {
                collection.Add(_item);
            }
        }
    }
}
