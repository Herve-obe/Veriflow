using Veriflow.Desktop.Commands;
using Veriflow.Desktop.Models;
using Veriflow.Desktop.ViewModels;

namespace Veriflow.Desktop.Commands.Reports
{
    /// <summary>
    /// Command to clear report header information
    /// </summary>
    public class ClearInfosCommand : IUndoableCommand
    {
        private readonly ReportsViewModel _viewModel;
        private readonly ReportHeader _savedHeader;

        public string Description => "Clear Report Header Information";

        public ClearInfosCommand(ReportsViewModel viewModel)
        {
            _viewModel = viewModel;
            _savedHeader = _viewModel.Header.Clone();
        }

        public void Execute()
        {
            _viewModel.Header = new ReportHeader();
            
            // Apply default values based on report type
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
            _viewModel.Header = _savedHeader;
        }
    }
}
