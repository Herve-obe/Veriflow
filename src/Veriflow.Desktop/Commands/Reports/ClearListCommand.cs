using Veriflow.Desktop.Commands;
using Veriflow.Desktop.Models;
using Veriflow.Desktop.ViewModels;
using System.Collections.Generic;
using System.Linq;

namespace Veriflow.Desktop.Commands.Reports
{
    /// <summary>
    /// Command to clear all items from a report list
    /// </summary>
    public class ClearListCommand : IUndoableCommand
    {
        private readonly ReportsViewModel _viewModel;
        private readonly bool _isVideo;
        private readonly List<ReportItem> _savedItems;
        private readonly ReportHeader _savedHeader;

        public string Description => $"Clear {(_isVideo ? "Camera" : "Sound")} Report List";

        public ClearListCommand(ReportsViewModel viewModel, bool isVideo)
        {
            _viewModel = viewModel;
            _isVideo = isVideo;
            
            // Save current state
            var collection = _isVideo ? _viewModel.VideoReportItems : _viewModel.AudioReportItems;
            _savedItems = new List<ReportItem>(collection);
            _savedHeader = _viewModel.Header.Clone();
        }

        public void Execute()
        {
            var collection = _isVideo ? _viewModel.VideoReportItems : _viewModel.AudioReportItems;
            collection.Clear();
            
            // Also clear header (matching current behavior)
            _viewModel.Header = new ReportHeader();
            if (_viewModel.CurrentReportType == ReportType.Audio)
            {
                _viewModel.Header.ProductionCompany = "SoundLog Pro Production";
            }
            else
            {
                _viewModel.Header.ProductionCompany = "Veriflow Video";
            }
        }

        public void Undo()
        {
            var collection = _isVideo ? _viewModel.VideoReportItems : _viewModel.AudioReportItems;
            foreach (var item in _savedItems)
            {
                collection.Add(item);
            }
            _viewModel.Header = _savedHeader;
        }
    }
}
