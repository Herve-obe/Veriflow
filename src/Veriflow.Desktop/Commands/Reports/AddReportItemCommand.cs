using Veriflow.Desktop.Commands;
using Veriflow.Desktop.Models;
using Veriflow.Desktop.ViewModels;

namespace Veriflow.Desktop.Commands.Reports
{
    /// <summary>
    /// Command to add a report item
    /// </summary>
    public class AddReportItemCommand : IUndoableCommand
    {
        private readonly ReportsViewModel _viewModel;
        private readonly ReportItem _item;
        private readonly bool _isVideo;

        public string Description => $"Add {(_isVideo ? "Camera" : "Sound")} Report: {_item.Filename}";

        public AddReportItemCommand(ReportsViewModel viewModel, ReportItem item, bool isVideo)
        {
            _viewModel = viewModel;
            _item = item;
            _isVideo = isVideo;
        }

        public void Execute()
        {
            if (_isVideo)
            {
                _viewModel.VideoReportItems.Add(_item);
            }
            else
            {
                _viewModel.AudioReportItems.Add(_item);
            }
        }

        public void Undo()
        {
            if (_isVideo)
            {
                _viewModel.VideoReportItems.Remove(_item);
            }
            else
            {
                _viewModel.AudioReportItems.Remove(_item);
            }
        }
    }
}
